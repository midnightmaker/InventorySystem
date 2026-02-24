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

				// A sale is only proforma/quotation if it hasn't shipped AND there is no
				// pre-shipment (prepayment) invoice — pre-shipment invoices are real invoices.
				var hasPreShipmentInvoice = (await _invoiceService.GetInvoicesBySaleAsync(saleId))
					.Any(i => i.InvoiceType == InvoiceType.PreShipment);

				var isShipped    = sale.SaleStatus == SaleStatus.Shipped || sale.SaleStatus == SaleStatus.Delivered;
				var isProforma   = !isShipped && !hasPreShipmentInvoice;
				var isQuotation  = sale.IsQuotation;

				// For quotations, the valid period is 60 days from the quote date
				var dueDate = isQuotation && isProforma
					? (DateTime?)sale.SaleDate.AddDays(60)
					: sale.PaymentDueDate;

				// Determine the document label for email subject/message
				var docLabel = isQuotation && isProforma ? "Quotation" : isProforma ? "Proforma Invoice" : "Invoice";

				var viewModel = new InvoiceReportViewModel
				{
					InvoiceNumber = sale.SaleNumber,
					InvoiceDate = sale.SaleDate,
					DueDate = dueDate,
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
					EmailSubject = $"{docLabel} {sale.SaleNumber}",
					EmailMessage = $"Please find attached {docLabel} {sale.SaleNumber} for your recent {(isProforma ? "quote" : "purchase")}.",
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
					IsPreShipmentInvoice = hasPreShipmentInvoice,
					IsQuotation = isQuotation,
					InvoiceTitle = docLabel,
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

				// A sale is only proforma/quotation if it hasn't shipped AND there is no
				// pre-shipment (prepayment) invoice — pre-shipment invoices are real invoices.
				var hasPreShipmentInvoice = (await _invoiceService.GetInvoicesBySaleAsync(saleId))
					.Any(i => i.InvoiceType == InvoiceType.PreShipment);

				var isShipped   = sale.SaleStatus == SaleStatus.Shipped || sale.SaleStatus == SaleStatus.Delivered;
				var isProforma  = !isShipped && !hasPreShipmentInvoice;
				var isQuotation = sale.IsQuotation;

				// For quotations, the valid period is 60 days from the quote date
				var dueDate = isQuotation && isProforma
					? (DateTime?)sale.SaleDate.AddDays(60)
					: sale.PaymentDueDate;

				var viewModel = new InvoiceReportViewModel
				{
					InvoiceNumber = sale.SaleNumber,
					InvoiceDate = sale.SaleDate,
					DueDate = dueDate,
					SaleStatus = sale.SaleStatus,
					PaymentStatus = sale.PaymentStatus,
					PaymentTerms = sale.Terms,
					Notes = sale.Notes ?? string.Empty,
					Customer = customer,
					TotalAdjustments = totalAdjustments,
					OriginalAmount = sale.TotalAmount,
					IsQuotation = isQuotation,
					IsProforma = isProforma,
					IsPreShipmentInvoice = hasPreShipmentInvoice,
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

		// GET: Sales/ViewInvoice/{invoiceId}
		// Renders InvoiceReportPrint using the exact same viewmodel that Rotativa
		// used to generate the stored PDF — both flow through BuildInvoiceViewModelAsync
		// which mirrors InvoiceReportPrint (sale.SaleNumber, all items, full amounts).
		[HttpGet]
		public async Task<IActionResult> ViewInvoice(int invoiceId)
		{
			try
			{
				var viewModel = await _invoiceService.BuildInvoiceViewModelAsync(invoiceId);
				if (viewModel == null)
				{
					SetErrorMessage("Invoice not found.");
					return RedirectToAction("Index");
				}

				// Provide SaleId so the view's "Back to Sale" link works
				var invoice = await _invoiceService.GetInvoiceByIdAsync(invoiceId);
				ViewBag.SaleId = invoice?.SaleId;

				return View("InvoiceReportPrint", viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error building invoice view for invoice {InvoiceId}", invoiceId);
				SetErrorMessage($"Error loading invoice: {ex.Message}");
				return RedirectToAction("Index");
			}
		}

		// POST: Sales/GeneratePreShipmentInvoice
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> GeneratePreShipmentInvoice(int saleId)
		{
			try
			{
				var sale = await _salesService.GetSaleByIdAsync(saleId);
				if (sale == null)
				{
					SetErrorMessage("Sale not found.");
					return RedirectToAction("Index");
				}

				if (sale.SaleStatus == SaleStatus.Cancelled)
				{
					SetErrorMessage("Cannot generate an invoice for a cancelled sale.");
					return RedirectToAction("Details", new { id = saleId });
				}

				if (!sale.SaleItems.Any())
				{
					SetErrorMessage("Cannot generate an invoice for a sale with no line items.");
					return RedirectToAction("Details", new { id = saleId });
				}

				var invoice = await _invoiceService.GeneratePreShipmentInvoiceAsync(
					saleId,
					User.Identity?.Name ?? "System");

				// Signal the Details page to auto-open the invoice preview modal
				TempData["ShowInvoicePreview"] = true;
				TempData["PreviewInvoiceId"]   = invoice.Id;
				TempData["PreviewInvoiceNumber"] = invoice.InvoiceNumber;
				TempData["PreviewHasPdf"]      = invoice.HasPdf;

				return RedirectToAction("Details", new { id = saleId });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error generating pre-shipment invoice for Sale ID: {SaleId}", saleId);
				SetErrorMessage($"Error generating invoice: {ex.Message}");
				return RedirectToAction("Details", new { id = saleId });
			}
		}
	}
}