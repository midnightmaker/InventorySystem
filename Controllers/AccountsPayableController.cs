using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.Models.Accounting;
using InventorySystem.Models.Enums;
using InventorySystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Controllers
{
	public class AccountsPayableController : Controller
	{
		private readonly InventoryContext _context;
		public AccountsPayableController(InventoryContext context)
		{
			_context = context;
		}

		[HttpGet]
		public async Task<IActionResult> CreatePrepayment(int purchaseId)
		{
			var purchase = await _context.Purchases
					.Include(p => p.Vendor)
					.Include(p => p.Item)
					.FirstOrDefaultAsync(p => p.Id == purchaseId);

			if (purchase == null)
				return NotFound();

			var viewModel = new CreatePrepaymentViewModel
			{
				PurchaseId = purchaseId,
				VendorId = purchase.VendorId,
				PurchaseOrderNumber = purchase.PurchaseOrderNumber,
				VendorName = purchase.Vendor.CompanyName,
				ItemDescription = $"{purchase.Item.PartNumber} - {purchase.Item.Description}",
				TotalPurchaseAmount = purchase.ExtendedTotal,
				PrepaymentDate = DateTime.Today
			};

			return View(viewModel);
		}


		[HttpPost]
		public async Task<IActionResult> CreatePrepayment(CreatePrepaymentViewModel model)
		{
			if (!ModelState.IsValid)
				return View(model);

			// Check if A/P already exists for this purchase
			var existingAP = await _context.AccountsPayable
					.FirstOrDefaultAsync(ap => ap.PurchaseId == model.PurchaseId);

			if (existingAP != null)
			{
				ModelState.AddModelError("", "An Accounts Payable record already exists for this purchase order.");
				return View(model);
			}

			var accountsPayable = new AccountsPayable
			{
				VendorId = model.VendorId,
				PurchaseId = model.PurchaseId,
				PurchaseOrderNumber = model.PurchaseOrderNumber,
				InvoiceDate = model.PrepaymentDate,
				DueDate = model.PrepaymentDate,

				InvoiceAmount = model.TotalPurchaseAmount,
				PrepaymentAmount = model.PrepaymentAmount,
				AmountPaid = model.PrepaymentAmount,

				PaymentStatus = model.PrepaymentAmount >= model.TotalPurchaseAmount
							? PaymentStatus.Paid
							: PaymentStatus.PartiallyPaid,

				ApprovalStatus = InvoiceApprovalStatus.Approved,
				InvoiceReceived = false,
				PaymentTerms = "Prepaid",

				Notes = $"Prepayment for PO {model.PurchaseOrderNumber} - {model.PaymentMethod}",
				CreatedBy = User.Identity?.Name ?? "System",
				CreatedDate = DateTime.Now
			};

			_context.AccountsPayable.Add(accountsPayable);

			var vendorPayment = new VendorPayment
			{
				AccountsPayableId = accountsPayable.Id,
				PaymentAmount = model.PrepaymentAmount,
				PaymentDate = model.PrepaymentDate,
				PaymentMethod = model.PaymentMethod, // Direct enum usage - no parsing!
				PaymentType = PaymentType.Prepayment,
				Notes = $"Prepayment for PO {model.PurchaseOrderNumber}",
				CreatedBy = User.Identity?.Name ?? "System",
				CreatedDate = DateTime.Now
			};

			vendorPayment.AccountsPayable = accountsPayable;
			_context.VendorPayments.Add(vendorPayment);

			await _context.SaveChangesAsync();

			TempData["SuccessMessage"] = model.PrepaymentAmount >= model.TotalPurchaseAmount
					? $"Purchase Order {model.PurchaseOrderNumber} is fully prepaid ({model.PrepaymentAmount:C})"
					: $"Prepayment of {model.PrepaymentAmount:C} recorded for PO {model.PurchaseOrderNumber}. Remaining balance: {(model.TotalPurchaseAmount - model.PrepaymentAmount):C}";

			return RedirectToAction("Details", "Purchases", new { id = model.PurchaseId });
		}
	}
}


