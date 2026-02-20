// Controllers/Purchases/PurchasesController.Receiving.cs
using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Controllers
{
	public partial class PurchasesController
	{
		// ?? Goods Receiving ???????????????????????????????????????????????????

		[HttpGet]
		public async Task<IActionResult> GetReceiveGoodsModal(int id)
		{
			try
			{
				var purchase = await _context.Purchases
					.Include(p => p.Item)
					.Include(p => p.Vendor)
					.FirstOrDefaultAsync(p => p.Id == id);

				if (purchase == null)
					return Json(new { success = false, message = "Purchase order not found" });

				if (purchase.Status == PurchaseStatus.Received)
					return Json(new { success = false, message = "Purchase order has already been received" });

				if (purchase.Status == PurchaseStatus.Cancelled)
					return Json(new { success = false, message = "Cannot receive a cancelled purchase order" });

				var viewModel = BuildReceiveViewModel(purchase);
				return PartialView("_ReceiveGoodsModal", viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading receive goods modal for purchase {PurchaseId}", id);
				return Json(new { success = false, message = "Error loading receive modal" });
			}
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<JsonResult> ReceiveGoods([FromBody] ReceiveGoodsRequest request)
		{
			try
			{
				var purchase = await _context.Purchases
					.Include(p => p.Item)
					.Include(p => p.Vendor)
					.FirstOrDefaultAsync(p => p.Id == request.PurchaseId);

				if (purchase == null)
					return Json(new { success = false, message = "Purchase order not found" });

				if (purchase.Status == PurchaseStatus.Received)
					return Json(new { success = false, message = "Purchase order has already been received" });

				if (request.QuantityReceived <= 0)
					return Json(new { success = false, message = "Received quantity must be greater than 0" });

				if (request.QuantityReceived > purchase.QuantityPurchased && request.ReceiptType != "overage")
					return Json(new { success = false, message = "Received quantity cannot exceed ordered quantity without selecting overage handling" });

				var receiptType = request.ReceiptType?.ToLower() ?? "complete";
				var result = receiptType switch
				{
					"partial" => await ReceivePartial(purchase, request),
					"short" => await ReceiveShortClose(purchase, request),
					"overage" => await ReceiveOverage(purchase, request),
					_ => await ReceiveComplete(purchase, request)
				};

				return result.Success
					? Json(new { success = true, message = result.Message, redirectToIndex = true })
					: Json(new { success = false, message = result.Message });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error receiving goods for purchase {PurchaseId}", request.PurchaseId);
				return Json(new { success = false, message = $"Error processing receipt: {ex.Message}" });
			}
		}

		// ?? Receive from OpenPurchaseOrders ???????????????????????????????????

		[HttpGet]
		public async Task<IActionResult> ReceivePurchaseOrder(int id)
		{
			try
			{
				var purchase = await _context.Purchases
					.Include(p => p.Item)
					.Include(p => p.Vendor)
					.FirstOrDefaultAsync(p => p.Id == id);

				if (purchase == null)
					return Json(new { success = false, message = "Purchase order not found" });

				if (purchase.Status == PurchaseStatus.Received)
					return Json(new { success = false, message = "Purchase order has already been received" });

				if (purchase.Status == PurchaseStatus.Cancelled)
					return Json(new { success = false, message = "Cannot receive a cancelled purchase order" });

				var viewModel = BuildReceiveViewModel(purchase);
				return PartialView("_ReceivePurchaseOrderModal", viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading receive PO modal for purchase {PurchaseId}", id);
				return Json(new { success = false, message = "Error loading receive modal" });
			}
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ReceivePurchaseOrder(ReceivePurchaseViewModel model)
		{
			try
			{
				ModelState.Remove("PurchaseOrderNumber");
				ModelState.Remove("VendorName");
				ModelState.Remove("ItemPartNumber");
				ModelState.Remove("ItemDescription");

				if (!ModelState.IsValid)
				{
					var errors = ModelState
						.Where(x => x.Value?.Errors.Count > 0)
						.Select(x => new { Field = x.Key, Errors = x.Value!.Errors.Select(e => e.ErrorMessage) });
					_logger.LogWarning("ReceivePurchaseOrder validation failed: {Errors}",
						System.Text.Json.JsonSerializer.Serialize(errors));
					return Json(new { success = false, message = "Invalid data provided. Please check all required fields." });
				}

				var purchase = await _context.Purchases
					.Include(p => p.Item)
					.Include(p => p.Vendor)
					.FirstOrDefaultAsync(p => p.Id == model.PurchaseId);

				if (purchase == null)
					return Json(new { success = false, message = "Purchase order not found" });

				if (purchase.Status == PurchaseStatus.Received)
					return Json(new { success = false, message = "Purchase order has already been received" });

				using var transaction = await _context.Database.BeginTransactionAsync();
				try
				{
					purchase.Status = PurchaseStatus.Received;
					purchase.ActualDeliveryDate = model.ReceivedDate;
					purchase.RemainingQuantity = 0;

					var timestamp = DateTime.Now.ToString("MM/dd/yyyy HH:mm");
					var receiveNote = $"[{timestamp}] Received {model.QuantityReceived} units by {model.ReceivedBy}";
					if (!string.IsNullOrEmpty(model.Notes)) receiveNote += $" - {model.Notes}";
					purchase.Notes = string.IsNullOrEmpty(purchase.Notes) ? receiveNote : $"{purchase.Notes}\n{receiveNote}";

					var item = await _context.Items.FindAsync(purchase.ItemId);
					if (item != null)
						item.CurrentStock += model.QuantityReceived;

					await CreateAccountsPayableIfNeeded(purchase);
					await _accountingService.GenerateJournalEntriesForPurchaseAsync(purchase);

					await _context.SaveChangesAsync();
					await transaction.CommitAsync();

					return Json(new
					{
						success = true,
						message = $"Purchase order {purchase.PurchaseOrderNumber} received successfully - {model.QuantityReceived} units"
					});
				}
				catch (Exception ex)
				{
					await transaction.RollbackAsync();
					_logger.LogError(ex, "Transaction rolled back for receive PO {PurchaseId}", model.PurchaseId);
					throw;
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error receiving purchase order {PurchaseId}", model.PurchaseId);
				return Json(new { success = false, message = $"Error receiving purchase order: {ex.Message}" });
			}
		}

		[HttpGet]
		public async Task<IActionResult> PendingReceipts()
		{
			try
			{
				var pendingPurchases = await _context.Purchases
					.Include(p => p.Item)
					.Include(p => p.Vendor)
					.Where(p => p.Status == PurchaseStatus.Ordered ||
								p.Status == PurchaseStatus.Shipped ||
								p.Status == PurchaseStatus.PartiallyReceived)
					.OrderBy(p => p.ExpectedDeliveryDate ?? p.PurchaseDate)
					.ToListAsync();

				var viewModel = new PendingReceiptsViewModel
				{
					PendingPurchases = pendingPurchases,
					TotalPendingValue = pendingPurchases.Sum(p => p.ExtendedTotal),
					OverduePurchases = pendingPurchases
						.Where(p => (p.ExpectedDeliveryDate ?? p.PurchaseDate.AddDays(7)) < DateTime.Today)
						.ToList()
				};

				return View(viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading pending receipts");
				SetErrorMessage("Error loading pending receipts");
				return View(new PendingReceiptsViewModel());
			}
		}

		// ?? Receipt strategy methods ??????????????????????????????????????????

		private async Task<(bool Success, string Message)> ReceiveComplete(Purchase purchase, ReceiveGoodsRequest request)
		{
			await _purchaseService.ReceivePurchaseAsync(
				request.PurchaseId,
				request.ReceivedDate,
				request.ReceivedBy,
				request.Notes);

			return (true, $"Purchase order {purchase.PurchaseOrderNumber} received successfully - {request.QuantityReceived} units");
		}

		private async Task<(bool Success, string Message)> ReceivePartial(Purchase purchase, ReceiveGoodsRequest request)
		{
			purchase.Item.CurrentStock += request.QuantityReceived;
			purchase.ActualDeliveryDate = request.ReceivedDate;
			purchase.RemainingQuantity = purchase.QuantityPurchased - request.QuantityReceived;
			purchase.Status = PurchaseStatus.PartiallyReceived;

			var note = BuildReceiveNote($"Partially received {request.QuantityReceived} of {purchase.QuantityPurchased} units", request);
			purchase.Notes = AppendNote(purchase.Notes, note);

			await _context.SaveChangesAsync();
			await CreateSimpleAccountsPayable(purchase, request.QuantityReceived);

			return (true, $"Partial receipt processed - {request.QuantityReceived} of {purchase.QuantityPurchased} units received. Remaining: {purchase.RemainingQuantity}");
		}

		private async Task<(bool Success, string Message)> ReceiveShortClose(Purchase purchase, ReceiveGoodsRequest request)
		{
			purchase.Item.CurrentStock += request.QuantityReceived;
			purchase.ActualDeliveryDate = request.ReceivedDate;
			purchase.Status = PurchaseStatus.Received;

			var shortageQty = purchase.QuantityPurchased - request.QuantityReceived;
			var note = BuildReceiveNote($"Short shipment: Received {request.QuantityReceived}, {shortageQty} units short. PO closed", request);
			purchase.Notes = AppendNote(purchase.Notes, note);

			await _context.SaveChangesAsync();

			if (shortageQty > 0)
			{
				var shortage = new VendorShortage
				{
					VendorId = purchase.VendorId,
					PurchaseId = purchase.Id,
					ItemId = purchase.ItemId,
					ShortageQuantity = shortageQty,
					UnitCost = purchase.CostPerUnit,
					TotalCostImpact = shortageQty * purchase.CostPerUnit,
					ShortageDate = request.ReceivedDate,
					Reason = "Short shipment - PO closed",
					Status = "Open",
					CreatedBy = request.ReceivedBy ?? "System"
				};

				_context.VendorShortages.Add(shortage);
				await _context.SaveChangesAsync();
			}

			await CreateSimpleAccountsPayable(purchase, request.QuantityReceived);

			return (true, $"Short shipment processed - {request.QuantityReceived} units received, {shortageQty} units written off. PO closed.");
		}

		private async Task<(bool Success, string Message)> ReceiveOverage(Purchase purchase, ReceiveGoodsRequest request)
		{
			purchase.Item.CurrentStock += request.QuantityReceived;
			purchase.ActualDeliveryDate = request.ReceivedDate;
			purchase.Status = PurchaseStatus.Received;

			var overageQty = request.QuantityReceived - purchase.QuantityPurchased;
			purchase.QuantityPurchased = request.QuantityReceived;
			purchase.RemainingQuantity = 0;

			var note = BuildReceiveNote($"Overage: {request.QuantityReceived} units (+{overageQty} over ordered)", request);
			purchase.Notes = AppendNote(purchase.Notes, note);

			await _context.SaveChangesAsync();
			await CreateSimpleAccountsPayable(purchase, request.QuantityReceived);

			return (true, $"Overage received - {request.QuantityReceived} units received (+{overageQty} overage). Purchase order updated.");
		}

		// ?? Private helpers ???????????????????????????????????????????????????

		private ReceivePurchaseViewModel BuildReceiveViewModel(Purchase purchase) =>
			new()
			{
				PurchaseId           = purchase.Id,
				PurchaseOrderNumber  = purchase.PurchaseOrderNumber ?? "N/A",
				VendorName           = purchase.Vendor.CompanyName,
				ItemPartNumber       = purchase.Item.PartNumber,
				ItemDescription      = purchase.Item.Description,
				QuantityOrdered      = purchase.QuantityPurchased,
				QuantityReceived     = purchase.QuantityPurchased,
				ReceivedDate         = DateTime.Today,
				ExpectedDeliveryDate = purchase.ExpectedDeliveryDate,
				ReceivedBy           = User.Identity?.Name ?? "User"
			};

		private string BuildReceiveNote(string summary, ReceiveGoodsRequest request)
		{
			var timestamp = DateTime.Now.ToString("MM/dd/yyyy HH:mm");
			var note = $"[{timestamp}] {summary} by {request.ReceivedBy}";
			if (!string.IsNullOrEmpty(request.Notes)) note += $" - {request.Notes}";
			return note;
		}

		private static string AppendNote(string? existing, string newNote) =>
			string.IsNullOrEmpty(existing) ? newNote : $"{existing}\n{newNote}";

		// ?? Request model ?????????????????????????????????????????????????????

		public class ReceiveGoodsRequest
		{
			public int PurchaseId { get; set; }
			public int QuantityReceived { get; set; }
			public DateTime ReceivedDate { get; set; } = DateTime.Today;
			public string ReceiptType { get; set; } = "complete";
			public string? VendorInvoiceNumber { get; set; }
			public string? ReceivedBy { get; set; }
			public string? Notes { get; set; }
		}
	}
}
