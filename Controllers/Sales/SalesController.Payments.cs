// Controllers/Sales/SalesController.Payments.cs
using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.Services;
using InventorySystem.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace InventorySystem.Controllers
{
	public partial class SalesController
	{
		// GET: Sales/RecordPayment
		[HttpGet]
		public async Task<IActionResult> RecordPayment(int? saleId)
		{
			if (saleId.HasValue)
				return RedirectToAction("Details", new { id = saleId.Value });

			return RedirectToAction("Index");
		}

		// POST: Sales/RecordPayment
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> RecordPayment(int saleId, decimal paymentAmount, string paymentMethod, DateTime paymentDate, string? paymentNotes)
		{
			try
			{
				_logger.LogInformation("Recording payment for Sale ID: {SaleId}, Amount: {PaymentAmount}, Method: {PaymentMethod}, Date: {PaymentDate}",
					saleId, paymentAmount, paymentMethod, paymentDate);

				var sale = await _salesService.GetSaleByIdAsync(saleId);
				if (sale == null)
				{
					SetErrorMessage("Sale not found.");
					return RedirectToAction("Index");
				}

				var paymentService = HttpContext.RequestServices.GetRequiredService<ICustomerPaymentService>();

				if (paymentAmount <= 0)
				{
					SetErrorMessage("Payment amount must be greater than zero.");
					return RedirectToAction("Details", new { id = saleId });
				}

				if (string.IsNullOrWhiteSpace(paymentMethod))
				{
					SetErrorMessage("Payment method is required.");
					return RedirectToAction("Details", new { id = saleId });
				}

				if (!await paymentService.ValidatePaymentAmountAsync(saleId, paymentAmount))
				{
					var remainingBalance = await paymentService.GetRemainingBalanceAsync(saleId);
					SetErrorMessage($"Payment amount ${paymentAmount:F2} exceeds remaining balance of ${remainingBalance:F2}.");
					return RedirectToAction("Details", new { id = saleId });
				}

				var payment = await paymentService.RecordPaymentAsync(
					saleId: saleId,
					amount: paymentAmount,
					paymentMethod: paymentMethod,
					paymentDate: paymentDate,
					paymentReference: null,
					notes: paymentNotes,
					createdBy: User.Identity?.Name ?? "System"
				);

				var totalPayments = await paymentService.GetTotalPaymentsBySaleAsync(saleId);
				var remainingBalanceAfter = await paymentService.GetRemainingBalanceAsync(saleId);
				var isFullyPaid = await paymentService.IsSaleFullyPaidAsync(saleId);
				var customerDisplayName = GetCustomerDisplayName(sale.Customer);

				var successMessage = isFullyPaid
					? $"Payment of ${paymentAmount:F2} recorded successfully for {customerDisplayName}! Sale is now fully paid (total payments: ${totalPayments:F2}). Journal entry {payment.JournalEntryNumber ?? "pending"} created."
					: $"Partial payment of ${paymentAmount:F2} recorded successfully for {customerDisplayName}. Total paid: ${totalPayments:F2}. Remaining balance: ${remainingBalanceAfter:F2}. Journal entry {payment.JournalEntryNumber ?? "pending"} created.";

				SetSuccessMessage(successMessage);
				return RedirectToAction("Details", new { id = saleId });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error recording payment for Sale ID: {SaleId}", saleId);
				SetErrorMessage($"Error recording payment: {ex.Message}");
				return RedirectToAction("Details", new { id = saleId });
			}
		}

		// GET: Sales/InvoiceReport
		[HttpGet]
		public async Task<IActionResult> InvoiceReport(int saleId)
		{
			try
			{
				var sale = await _salesService.GetSaleByIdAsync(saleId);
				if (sale == null)
				{
					SetErrorMessage("Sale not found.");
					return RedirectToAction("Index");
				}

				var paymentService = HttpContext.RequestServices.GetRequiredService<ICustomerPaymentService>();
				var (recipientName, recipientEmail, billingAddress, companyName, contactName) = GetInvoiceRecipientInfo(sale.Customer);

				var customer = new CustomerInfo
				{
					CompanyName = companyName,
					CustomerName = contactName,
					CustomerEmail = recipientEmail,
					CustomerPhone = sale.Customer?.Phone ?? string.Empty,
					BillingAddress = billingAddress,
					ShippingAddress = sale.ShippingAddress ?? sale.Customer?.FullShippingAddress ?? string.Empty
				};

				var totalAdjustments = sale.RelatedAdjustments?.Sum(a => a.AdjustmentAmount) ?? 0;
				var isProforma = sale.SaleStatus != SaleStatus.Shipped && sale.SaleStatus != SaleStatus.Delivered;

				var viewModel = new InvoiceReportViewModel
				{
					InvoiceNumber = sale.SaleNumber,
					InvoiceDate = sale.SaleDate,
					DueDate = sale.PaymentDueDate,
					SaleStatus = sale.SaleStatus,
					PaymentStatus = sale.PaymentStatus,
					PaymentTerms = sale.Terms,
					Notes = sale.Notes ?? string.Empty,
					Customer = customer,
					LineItems = sale.SaleItems.Select(si => new InvoiceLineItem
					{
						ItemId = si.ItemId ?? si.FinishedGoodId ?? si.ServiceTypeId ?? 0,
						PartNumber = si.ProductPartNumber,
						Description = si.ProductName,
						Quantity = si.QuantitySold,
						UnitPrice = si.UnitPrice,
						Notes = si.Notes ?? string.Empty,
						ProductType = si.ItemId.HasValue ? "Item" : si.ServiceTypeId.HasValue ? "Service" : "FinishedGood",
						QuantityBackordered = si.QuantityBackordered,
						SerialNumber = si.SerialNumber,
						ModelNumber = si.ModelNumber
					}).ToList(),
					CompanyInfo = await GetCompanyInfo(),
					CustomerEmail = recipientEmail,
					EmailSubject = $"{(isProforma ? "Proforma Invoice" : "Invoice")} {sale.SaleNumber}",
					EmailMessage = $"Please find attached {(isProforma ? "Proforma Invoice" : "Invoice")} {sale.SaleNumber} for your recent {(isProforma ? "quote" : "purchase")}.",
					PaymentMethod = sale.PaymentMethod ?? string.Empty,
					IsOverdue = sale.IsOverdue,
					DaysOverdue = sale.DaysOverdue,
					ShippingAddress = sale.ShippingAddress ?? string.Empty,
					OrderNumber = sale.OrderNumber ?? string.Empty,
					TotalShipping = sale.ShippingCost,
					TotalTax = sale.TaxAmount,
					TotalDiscount = sale.DiscountCalculated,
					DiscountReason = sale.DiscountReason,
					HasDiscount = sale.HasDiscount,
					TotalAdjustments = totalAdjustments,
					OriginalAmount = sale.TotalAmount,
					IsProforma = isProforma,
					InvoiceTitle = isProforma ? "Proforma Invoice" : "Invoice",
					IsDirectedToAP = sale.Customer?.DirectInvoicesToAP ?? false,
					APContactName = sale.Customer?.AccountsPayableContactName,
					RequiresPO = sale.Customer?.RequiresPurchaseOrder ?? false,
					AmountPaid = await paymentService.GetTotalPaymentsBySaleAsync(sale.Id)
				};

				ViewBag.SaleId = saleId;
				return View(viewModel);
			}
			catch (Exception ex)
			{
				SetErrorMessage($"Error generating invoice: {ex.Message}");
				return RedirectToAction("Index");
			}
		}

		// GET: Sales/InvoiceReportPrint
		[HttpGet]
		public async Task<IActionResult> InvoiceReportPrint(int saleId)
		{
			try
			{
				var sale = await _salesService.GetSaleByIdAsync(saleId);
				if (sale == null)
				{
					SetErrorMessage("Sale not found.");
					return RedirectToAction("Index");
				}

				var paymentService = HttpContext.RequestServices.GetRequiredService<ICustomerPaymentService>();
				var totalAdjustments = sale.RelatedAdjustments?.Sum(a => a.AdjustmentAmount) ?? 0;
				var (recipientName, recipientEmail, billingAddress, companyName, contactName) = GetInvoiceRecipientInfo(sale.Customer);

				var customer = new CustomerInfo
				{
					CompanyName = companyName,
					CustomerName = contactName,
					CustomerEmail = recipientEmail,
					CustomerPhone = sale.Customer?.Phone ?? string.Empty,
					BillingAddress = billingAddress,
					ShippingAddress = sale.ShippingAddress ?? sale.Customer?.FullShippingAddress ?? string.Empty
				};

				var viewModel = new InvoiceReportViewModel
				{
					InvoiceNumber = sale.SaleNumber,
					InvoiceDate = sale.SaleDate,
					DueDate = sale.PaymentDueDate,
					SaleStatus = sale.SaleStatus,
					PaymentStatus = sale.PaymentStatus,
					PaymentTerms = sale.Terms,
					Notes = sale.Notes ?? string.Empty,
					Customer = customer,
					TotalAdjustments = totalAdjustments,
					OriginalAmount = sale.TotalAmount,
					LineItems = sale.SaleItems.Select(si => new InvoiceLineItem
					{
						ItemId = si.ItemId ?? si.FinishedGoodId ?? si.ServiceTypeId ?? 0,
						PartNumber = si.ProductPartNumber,
						Description = si.ProductName,
						Quantity = si.QuantitySold,
						UnitPrice = si.UnitPrice,
						Notes = si.Notes ?? string.Empty,
						ProductType = si.ItemId.HasValue ? "Item" : si.ServiceTypeId.HasValue ? "Service" : "FinishedGood",
						QuantityBackordered = si.QuantityBackordered,
						SerialNumber = si.SerialNumber,
						ModelNumber = si.ModelNumber
					}).ToList(),
					CompanyInfo = await GetCompanyInfo(),
					CustomerEmail = recipientEmail,
					EmailSubject = $"Invoice {sale.SaleNumber}",
					EmailMessage = $"Please find attached Invoice {sale.SaleNumber} for your recent purchase.",
					PaymentMethod = sale.PaymentMethod ?? string.Empty,
					IsOverdue = sale.IsOverdue,
					DaysOverdue = sale.DaysOverdue,
					ShippingAddress = sale.ShippingAddress ?? string.Empty,
					OrderNumber = sale.OrderNumber ?? string.Empty,
					TotalShipping = sale.ShippingCost,
					TotalTax = sale.TaxAmount,
					TotalDiscount = sale.DiscountCalculated,
					DiscountReason = sale.DiscountReason,
					HasDiscount = sale.HasDiscount,
					IsDirectedToAP = sale.Customer?.DirectInvoicesToAP ?? false,
					APContactName = sale.Customer?.AccountsPayableContactName,
					RequiresPO = sale.Customer?.RequiresPurchaseOrder ?? false,
					AmountPaid = await paymentService.GetTotalPaymentsBySaleAsync(sale.Id)
				};

				return View(viewModel);
			}
			catch (Exception ex)
			{
				SetErrorMessage($"Error generating printable invoice: {ex.Message}");
				return RedirectToAction("Index");
			}
		}
	}
}