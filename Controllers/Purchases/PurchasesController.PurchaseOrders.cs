// Controllers/Purchases/PurchasesController.PurchaseOrders.cs
using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.Services;
using InventorySystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Controllers
{
	public partial class PurchasesController
	{
		[HttpGet]
		public async Task<IActionResult> PurchaseOrderReport(string poNumber)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(poNumber))
				{
					SetErrorMessage("Purchase Order Number is required.");
					return RedirectToAction(nameof(Index));
				}

				var viewModel = await BuildPurchaseOrderReportViewModelAsync(poNumber);
				if (viewModel == null)
				{
					SetErrorMessage($"No purchases found for PO Number: {poNumber}");
					return RedirectToAction(nameof(Index));
				}

				viewModel.VendorEmail = viewModel.Vendor.ContactEmail ?? string.Empty;
				viewModel.EmailSubject = $"Purchase Order {poNumber}";
				viewModel.EmailMessage = $"Please find attached Purchase Order {poNumber} for your review and processing.";

				return View(viewModel);
			}
			catch (Exception ex)
			{
				SetErrorMessage($"Error generating PO report: {ex.Message}");
				return RedirectToAction(nameof(Index));
			}
		}

		[HttpGet]
		public async Task<IActionResult> PurchaseOrderReportPrint(string poNumber)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(poNumber))
					return BadRequest("Purchase Order Number is required.");

				var viewModel = await BuildPurchaseOrderReportViewModelAsync(poNumber);
				if (viewModel == null)
					return NotFound($"No purchases found for PO Number: {poNumber}");

				return View("PurchaseOrderReportPrint", viewModel);
			}
			catch (Exception ex)
			{
				return BadRequest($"Error generating PO report: {ex.Message}");
			}
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> EmailPurchaseOrderReport(PurchaseOrderReportViewModel model)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(model.VendorEmail))
				{
					SetErrorMessage("Vendor email address is required.");
					return RedirectToAction(nameof(PurchaseOrderReport), new { poNumber = model.PurchaseOrderNumber });
				}

				// Email sending is stubbed — wire up IEmailService when available.
				await RenderViewToStringAsync("PurchaseOrderReportEmail", model);

				return RedirectToAction(nameof(PurchaseOrderReport), new { poNumber = model.PurchaseOrderNumber });
			}
			catch (Exception ex)
			{
				SetErrorMessage($"Error sending email: {ex.Message}");
				return RedirectToAction(nameof(PurchaseOrderReport), new { poNumber = model.PurchaseOrderNumber });
			}
		}

		[HttpGet]
		public async Task<IActionResult> CompanyLogo()
		{
			try
			{
				var companyInfoService = HttpContext.RequestServices.GetRequiredService<ICompanyInfoService>();
				var companyInfo = await companyInfoService.GetCompanyInfoAsync();

				if (companyInfo?.LogoData != null && companyInfo.LogoData.Length > 0)
					return File(companyInfo.LogoData, companyInfo.LogoContentType ?? "image/png", companyInfo.LogoFileName);

				return NotFound();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error retrieving company logo");
				return NotFound();
			}
		}

		// ?? Private helpers ???????????????????????????????????????????????????

		private async Task<PurchaseOrderReportViewModel?> BuildPurchaseOrderReportViewModelAsync(string poNumber)
		{
			var purchases = await _context.Purchases
				.Include(p => p.Item)
				.Include(p => p.Vendor)
				.Where(p => p.PurchaseOrderNumber == poNumber)
				.OrderBy(p => p.Item.PartNumber)
				.ToListAsync();

			if (!purchases.Any()) return null;

			var primaryVendor = purchases.First().Vendor;

			return new PurchaseOrderReportViewModel
			{
				PurchaseOrderNumber = poNumber,
				PurchaseDate = purchases.Min(p => p.PurchaseDate),
				ExpectedDeliveryDate = purchases.FirstOrDefault()?.ExpectedDeliveryDate,
				Status = purchases.First().Status,
				Notes = string.Join("; ", purchases
					.Where(p => !string.IsNullOrEmpty(p.Notes))
					.Select(p => p.Notes)
					.Distinct()),
				Vendor = primaryVendor,
				LineItems = purchases.Select(p => new PurchaseOrderLineItem
				{
					ItemId = p.ItemId,
					PartNumber = p.Item.PartNumber,
					Description = p.Item.Description,
					Quantity = p.QuantityPurchased,
					UnitCost = p.CostPerUnit,
					ShippingCost = p.ShippingCost,
					TaxAmount = p.TaxAmount,
					Notes = p.Notes ?? string.Empty,
					PurchaseDate = p.PurchaseDate,
					ExpectedDeliveryDate = p.ExpectedDeliveryDate,
					Status = p.Status
				}).ToList(),
				CompanyInfo = await GetCompanyInfo()
			};
		}

		private Task<string> RenderViewToStringAsync(string viewName, object model)
		{
			var viewModel = model as PurchaseOrderReportViewModel;
			if (viewModel == null) return Task.FromResult(string.Empty);

			var html = $@"
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; }}
        .header {{ text-align: center; border-bottom: 2px solid #333; padding-bottom: 10px; }}
        .company-info, .vendor-info {{ margin: 20px 0; }}
        table {{ width: 100%; border-collapse: collapse; margin: 20px 0; }}
        th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
        th {{ background-color: #f2f2f2; }}
        .total-row {{ font-weight: bold; background-color: #f9f9f9; }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>PURCHASE ORDER</h1>
        <h2>PO# {viewModel.PurchaseOrderNumber}</h2>
    </div>
    <div class='company-info'>
        <h3>From:</h3>
        <p><strong>{viewModel.CompanyInfo.CompanyName}</strong><br/>
        {viewModel.CompanyInfo.Address}<br/>
        {viewModel.CompanyInfo.City}, {viewModel.CompanyInfo.State} {viewModel.CompanyInfo.ZipCode}<br/>
        Phone: {viewModel.CompanyInfo.Phone}<br/>
        Email: {viewModel.CompanyInfo.Email}</p>
    </div>
    <div class='vendor-info'>
        <h3>To:</h3>
        <p><strong>{viewModel.Vendor.CompanyName}</strong><br/>
        {GetVendorAddressForEmail(viewModel.Vendor)}<br/>
        Phone: {viewModel.Vendor.ContactPhone}<br/>
        Email: {viewModel.Vendor.ContactEmail}</p>
    </div>
    <p><strong>PO Date:</strong> {viewModel.PurchaseDate:MM/dd/yyyy}<br/>
    <strong>Expected Delivery:</strong> {viewModel.ExpectedDeliveryDate?.ToString("MM/dd/yyyy") ?? "TBD"}<br/>
    <strong>Status:</strong> {viewModel.Status}</p>
    <table>
        <thead>
            <tr>
                <th>Item #</th><th>Description</th><th>Qty</th><th>Unit Price</th><th>Total</th>
            </tr>
        </thead>
        <tbody>";

			foreach (var item in viewModel.LineItems)
			{
				html += $@"
            <tr>
                <td>{item.PartNumber}</td>
                <td>{item.Description}</td>
                <td>{item.Quantity}</td>
                <td>${item.UnitCost:F2}</td>
                <td>${item.LineTotal:F2}</td>
            </tr>";
			}

			html += $@"
            <tr class='total-row'>
                <td colspan='4'><strong>TOTAL</strong></td>
                <td><strong>${viewModel.SubtotalAmount:F2}</strong></td>
            </tr>
        </tbody>
    </table>
    {(string.IsNullOrEmpty(viewModel.Notes) ? "" : $"<p><strong>Notes:</strong> {viewModel.Notes}</p>")}
    <p><em>Please include the purchase order number with all shipments</em></p>
</body>
</html>";

			return Task.FromResult(html);
		}
	}
}
