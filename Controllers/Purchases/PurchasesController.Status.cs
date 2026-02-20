// Controllers/Purchases/PurchasesController.Status.cs
using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Controllers
{
	public partial class PurchasesController
	{
		[HttpGet]
		public async Task<IActionResult> GetStatusUpdateModal(int id)
		{
			try
			{
				var purchase = await _context.Purchases
					.Include(p => p.Item)
					.Include(p => p.Vendor)
					.FirstOrDefaultAsync(p => p.Id == id);

				if (purchase == null)
					return Json(new { success = false, message = "Purchase not found" });

				var viewModel = new UpdatePurchaseStatusViewModel
				{
					PurchaseId = purchase.Id,
					CurrentStatus = purchase.Status,
					PurchaseOrderNumber = purchase.PurchaseOrderNumber,
					ItemName = $"{purchase.Item.PartNumber} - {purchase.Item.Description}",
					VendorName = purchase.Vendor.CompanyName,
					QuantityPurchased = purchase.QuantityPurchased,
					ExpectedDeliveryDate = purchase.ExpectedDeliveryDate,
					ActualDeliveryDate = purchase.ActualDeliveryDate,
					CanReceive = purchase.Status == PurchaseStatus.Ordered || purchase.Status == PurchaseStatus.Shipped,
					CanCancel = purchase.Status != PurchaseStatus.Received && purchase.Status != PurchaseStatus.Cancelled
				};

				viewModel.PopulateAvailableStatuses();

				return PartialView("_UpdatePurchaseStatusModal", viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading status update modal for purchase {PurchaseId}", id);
				return Json(new { success = false, message = "Error loading status update form" });
			}
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> UpdatePurchaseStatus(UpdatePurchaseStatusViewModel model)
		{
			if (!ModelState.IsValid)
			{
				var errors = ModelState
					.Where(x => x.Value!.Errors.Count > 0)
					.Select(x => new { Field = x.Key, Errors = x.Value!.Errors.Select(e => e.ErrorMessage) });
				_logger.LogWarning("UpdatePurchaseStatus validation failed: {Errors}",
					System.Text.Json.JsonSerializer.Serialize(errors));
				return Json(new { success = false, message = "Invalid data provided" });
			}

			try
			{
				_logger.LogInformation("Updating purchase status for PurchaseId: {PurchaseId} to {NewStatus}",
					model.PurchaseId, model.NewStatus);

				var purchase = await _context.Purchases
					.Include(p => p.Item)
					.Include(p => p.Vendor)
					.FirstOrDefaultAsync(p => p.Id == model.PurchaseId);

				if (purchase == null)
					return Json(new { success = false, message = "Purchase not found" });

				var oldStatus = purchase.Status;

				using var transaction = await _context.Database.BeginTransactionAsync();
				try
				{
					purchase.Status = model.NewStatus;

					await ApplyStatusSpecificUpdates(purchase, model, oldStatus);

					AppendStatusNote(purchase, oldStatus, model.NewStatus, model.Notes);

					if (model.NewStatus == PurchaseStatus.Received && oldStatus != PurchaseStatus.Received)
					{
						_logger.LogInformation("Generating journal entries for received purchase {PurchaseId}", purchase.Id);
						await _accountingService.GenerateJournalEntriesForPurchaseAsync(purchase);
					}

					var saveResult = await _context.SaveChangesAsync();
					_logger.LogInformation("SaveChanges returned {SaveResult} entities affected", saveResult);

					await transaction.CommitAsync();
					_logger.LogInformation("Transaction committed for purchase {PurchaseOrderNumber}", purchase.PurchaseOrderNumber);

					var successMessage = model.NewStatus switch
					{
						PurchaseStatus.Received => $"Purchase order {purchase.PurchaseOrderNumber} marked as received. Inventory updated.",
						PurchaseStatus.Shipped => $"Purchase order {purchase.PurchaseOrderNumber} marked as shipped.",
						PurchaseStatus.Cancelled => $"Purchase order {purchase.PurchaseOrderNumber} has been cancelled.",
						_ => $"Purchase order {purchase.PurchaseOrderNumber} status updated to {model.NewStatus}."
					};

					return Json(new
					{
						success = true,
						message = successMessage,
						newStatus = model.NewStatus.ToString(),
						redirectToDetails = true
					});
				}
				catch (Exception ex)
				{
					await transaction.RollbackAsync();
					_logger.LogError(ex, "Transaction rolled back for purchase status update {PurchaseId}", model.PurchaseId);
					throw;
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error updating purchase status for purchase {PurchaseId}", model.PurchaseId);
				return Json(new { success = false, message = $"Error updating status: {ex.Message}" });
			}
		}

		// ?? Private helpers ???????????????????????????????????????????????????

		private async Task ApplyStatusSpecificUpdates(Purchase purchase, UpdatePurchaseStatusViewModel model, PurchaseStatus oldStatus)
		{
			switch (model.NewStatus)
			{
				case PurchaseStatus.Shipped:
					if (model.ShippedDate.HasValue)
					{
						purchase.ExpectedDeliveryDate = model.ShippedDate.Value.AddDays(model.EstimatedDeliveryDays ?? 3);
						_logger.LogInformation("Updated expected delivery date to {ExpectedDelivery}", purchase.ExpectedDeliveryDate);
					}
					break;

				case PurchaseStatus.Received:
					purchase.ActualDeliveryDate = model.ReceivedDate ?? DateTime.Now;

					var itemToUpdate = await _context.Items.FindAsync(purchase.ItemId);
					if (itemToUpdate == null)
						throw new InvalidOperationException($"Item {purchase.ItemId} not found");

					var oldStock = itemToUpdate.CurrentStock;
					itemToUpdate.CurrentStock += purchase.QuantityPurchased;
					_logger.LogInformation("Updated inventory for item {ItemId}: {OldStock} + {Quantity} = {NewStock}",
						purchase.ItemId, oldStock, purchase.QuantityPurchased, itemToUpdate.CurrentStock);

					await CreateAccountsPayableIfNeeded(purchase);
					break;

				case PurchaseStatus.Cancelled:
					if (string.IsNullOrEmpty(model.Reason))
						throw new InvalidOperationException("Reason is required for cancellation");
					_logger.LogInformation("Cancelling purchase with reason: {Reason}", model.Reason);
					break;
			}
		}

		private static void AppendStatusNote(Purchase purchase, PurchaseStatus oldStatus, PurchaseStatus newStatus, string? notes)
		{
			if (string.IsNullOrEmpty(notes)) return;

			var timestamp = DateTime.Now.ToString("MM/dd/yyyy HH:mm");
			var statusNote = $"[{timestamp}] Status changed from {oldStatus} to {newStatus}: {notes}";

			purchase.Notes = string.IsNullOrEmpty(purchase.Notes)
				? statusNote
				: $"{purchase.Notes}\n{statusNote}";
		}
	}
}
