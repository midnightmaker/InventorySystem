// Services/InvoiceService.cs
using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.EntityFrameworkCore;
using Rotativa.AspNetCore;

namespace InventorySystem.Services
{
    public class InvoiceService : IInvoiceService
    {
        private readonly InventoryContext _context;
        private readonly ICompanyInfoService _companyInfoService;
        private readonly ICustomerPaymentService _paymentService;
        private readonly ILogger<InvoiceService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public InvoiceService(
            InventoryContext context,
            ICompanyInfoService companyInfoService,
            ICustomerPaymentService paymentService,
            ILogger<InvoiceService> logger,
            IServiceProvider serviceProvider)
        {
            _context        = context;
            _companyInfoService = companyInfoService;
            _paymentService = paymentService;
            _logger         = logger;
            _serviceProvider = serviceProvider;
        }

        /// <inheritdoc/>
        public async Task<Invoice> GenerateAndStoreInvoiceAsync(int saleId, int shipmentId, string issuedBy)
        {
            // ?? 1. Load sale ??????????????????????????????????????????????????
            var sale = await _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.SaleItems)
                    .ThenInclude(si => si.Item)
                .Include(s => s.SaleItems)
                    .ThenInclude(si => si.FinishedGood)
                .Include(s => s.SaleItems)
                    .ThenInclude(si => si.ServiceType)
                .Include(s => s.RelatedAdjustments)
                .Include(s => s.CustomerPayments)
                .FirstOrDefaultAsync(s => s.Id == saleId)
                ?? throw new InvalidOperationException($"Sale {saleId} not found.");

            // ?? 2. Amounts — mirroring InvoiceReportPrint (full-sale basis) ??
            //    The single source of truth is the InvoiceReportPrint view which
            //    uses the full sale totals, not a per-shipment pro-ration.
            var totalAdjustments = sale.RelatedAdjustments?.Sum(a => a.AdjustmentAmount) ?? 0m;

            // ?? 3. Build the Invoice row ??????????????????????????????????????
            //    InvoiceNumber = sale.SaleNumber so it matches what the Print
            //    button shows in the browser.
            var invoice = new Invoice
            {
                InvoiceNumber  = sale.SaleNumber,
                InvoiceType    = InvoiceType.Invoice,
                InvoiceDate    = DateTime.Today,
                DueDate        = sale.PaymentDueDate,
                SaleId         = saleId,
                ShipmentId     = shipmentId,
                // Freeze the full-sale amounts (consistent with the printed document)
                SubtotalAmount = sale.SubtotalAmount,
                DiscountAmount = sale.DiscountCalculated,
                TaxAmount      = sale.TaxAmount,
                ShippingAmount = sale.ShippingCost,
                TotalAmount    = sale.TotalAmount,
                IssuedBy       = issuedBy,
                CreatedAt      = DateTime.Now
            };

            // ?? 4. Persist first so BuildInvoiceViewModelAsync can load it ???
            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            // ?? 5. Generate PDF from the canonical viewmodel ?????????????????
            //    BuildInvoiceViewModelAsync mirrors InvoiceReportPrint exactly,
            //    so Rotativa renders the same document the Print button produces.
            try
            {
                var viewModel = await BuildInvoiceViewModelAsync(invoice.Id);
                if (viewModel != null)
                {
                    var pdfBytes = await RenderViewAsPdfBytesAsync("InvoiceReportPdf", viewModel);
                    if (pdfBytes != null && pdfBytes.Length > 0)
                    {
                        invoice.PdfData        = pdfBytes;
                        invoice.PdfGeneratedAt = DateTime.Now;
                        await _context.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "PDF generation failed for invoice {InvoiceNumber}. Invoice row saved without PDF.",
                    invoice.InvoiceNumber);
            }

            _logger.LogInformation(
                "Invoice {InvoiceNumber} created for Sale {SaleId} / Shipment {ShipmentId}. HasPdf={HasPdf}",
                invoice.InvoiceNumber, saleId, shipmentId, invoice.HasPdf);

            return invoice;
        }

        /// <inheritdoc/>
        public async Task<Invoice> GeneratePreShipmentInvoiceAsync(int saleId, string issuedBy)
        {
            // ?? 1. Load sale ??????????????????????????????????????????????????
            var sale = await _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.SaleItems)
                    .ThenInclude(si => si.Item)
                .Include(s => s.SaleItems)
                    .ThenInclude(si => si.FinishedGood)
                .Include(s => s.SaleItems)
                    .ThenInclude(si => si.ServiceType)
                .Include(s => s.RelatedAdjustments)
                .Include(s => s.CustomerPayments)
                .FirstOrDefaultAsync(s => s.Id == saleId)
                ?? throw new InvalidOperationException($"Sale {saleId} not found.");

            if (sale.SaleStatus == SaleStatus.Cancelled)
                throw new InvalidOperationException("Cannot generate an invoice for a cancelled sale.");

            // ?? 2. Derive a unique invoice number ?????????????????????????????
            // Subsequent versions get a -v2, -v3 … suffix so the unique index
            // on InvoiceNumber is never violated when regenerating.
            var invoiceNumber = await GenerateUniquePreShipmentInvoiceNumberAsync(sale.SaleNumber);

            // ?? 3. Build the Invoice row (no ShipmentId) ??????????????????????
            // InvoiceType.PreShipment marks this as a real binding invoice issued
            // before shipment so the customer can pay upfront — NOT proforma.
            var invoice = new Invoice
            {
                InvoiceNumber  = invoiceNumber,
                InvoiceType    = InvoiceType.PreShipment,
                InvoiceDate    = DateTime.Today,
                DueDate        = sale.PaymentDueDate,
                SaleId         = saleId,
                ShipmentId     = null,
                SubtotalAmount = sale.SubtotalAmount,
                DiscountAmount = sale.DiscountCalculated,
                TaxAmount      = sale.TaxAmount,
                ShippingAmount = sale.ShippingCost,
                TotalAmount    = sale.TotalAmount,
                IssuedBy       = issuedBy,
                CreatedAt      = DateTime.Now
            };

            // ?? 4. Persist first so BuildInvoiceViewModelAsync can load it ???
            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            // ?? 5. Generate PDF ???????????????????????????????????????????????
            try
            {
                var viewModel = await BuildInvoiceViewModelAsync(invoice.Id);
                if (viewModel != null)
                {
                    var pdfBytes = await RenderViewAsPdfBytesAsync("InvoiceReportPdf", viewModel);
                    if (pdfBytes != null && pdfBytes.Length > 0)
                    {
                        invoice.PdfData        = pdfBytes;
                        invoice.PdfGeneratedAt = DateTime.Now;
                        await _context.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "PDF generation failed for pre-shipment invoice {InvoiceNumber}. Invoice row saved without PDF.",
                    invoice.InvoiceNumber);
            }

            _logger.LogInformation(
                "Pre-shipment invoice {InvoiceNumber} created for Sale {SaleId}. HasPdf={HasPdf}",
                invoice.InvoiceNumber, saleId, invoice.HasPdf);

            return invoice;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Builds the viewmodel using exactly the same logic as the
        /// <c>InvoiceReportPrint</c> controller action so the browser preview
        /// and the stored PDF are always identical.
        /// </remarks>
        public async Task<InvoiceReportViewModel?> BuildInvoiceViewModelAsync(int invoiceId)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Sale)
                    .ThenInclude(s => s.Customer)
                .Include(i => i.Sale)
                    .ThenInclude(s => s.SaleItems)
                        .ThenInclude(si => si.Item)
                .Include(i => i.Sale)
                    .ThenInclude(s => s.SaleItems)
                        .ThenInclude(si => si.FinishedGood)
                .Include(i => i.Sale)
                    .ThenInclude(s => s.SaleItems)
                        .ThenInclude(si => si.ServiceType)
                .Include(i => i.Sale)
                    .ThenInclude(s => s.RelatedAdjustments)
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice == null) return null;

            var sale         = invoice.Sale;
            var amountPaid   = await _paymentService.GetTotalPaymentsBySaleAsync(sale.Id);
            var companyInfo  = await GetCompanyInfoSafe();

            return BuildViewModelMirroringPrintAction(sale, companyInfo, amountPaid, invoice.InvoiceType);
        }

        /// <inheritdoc/>
        public async Task<byte[]?> GetInvoicePdfAsync(int invoiceId)
        {
            var invoice = await _context.Invoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            return invoice?.PdfData;
        }

        /// <inheritdoc/>
        public async Task<List<Invoice>> GetInvoicesBySaleAsync(int saleId)
        {
            return await _context.Invoices
                .Where(i => i.SaleId == saleId)
                .Include(i => i.Shipment)
                .OrderByDescending(i => i.CreatedAt)
                .ThenByDescending(i => i.InvoiceDate)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<Invoice?> GetInvoiceByIdAsync(int invoiceId)
        {
            return await _context.Invoices
                .Include(i => i.Sale)
                    .ThenInclude(s => s.Customer)
                .Include(i => i.Shipment)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);
        }

        /// <inheritdoc/>
        public async Task<string> GenerateInvoiceNumberAsync()
        {
            var year   = DateTime.Today.Year;
            var prefix = $"INV-{year}-";

            var lastInvoice = await _context.Invoices
                .Where(i => i.InvoiceNumber.StartsWith(prefix))
                .OrderByDescending(i => i.InvoiceNumber)
                .Select(i => i.InvoiceNumber)
                .FirstOrDefaultAsync();

            int nextSeq = 1;
            if (lastInvoice != null)
            {
                var seqPart = lastInvoice.Replace(prefix, "");
                if (int.TryParse(seqPart, out int parsed))
                    nextSeq = parsed + 1;
            }

            return $"{prefix}{nextSeq:D5}";
        }

        // ?? Private helpers ???????????????????????????????????????????????????

        /// <summary>
        /// Builds an <see cref="InvoiceReportViewModel"/> using exactly the same
        /// logic as <c>SalesController.InvoiceReportPrint</c>.  This is the single
        /// source of truth — both the stored PDF and the browser preview use this
        /// method.
        /// </summary>
        private static InvoiceReportViewModel BuildViewModelMirroringPrintAction(
            Sale        sale,
            CompanyInfo companyInfo,
            decimal     amountPaid,
            InvoiceType invoiceType = InvoiceType.Invoice)
        {
            var isQuotation      = sale.IsQuotation;
            var totalAdjustments = sale.RelatedAdjustments?.Sum(a => a.AdjustmentAmount) ?? 0m;
            var isShipped        = sale.SaleStatus == SaleStatus.Shipped || sale.SaleStatus == SaleStatus.Delivered;

            // Pre-shipment banner is only relevant while the sale is still awaiting
            // payment AND has not yet shipped. Once the sale ships or is fully paid
            // the banner no longer applies — the goods are either on their way or
            // payment criteria have already been met.
            var effectiveTotal = sale.TotalAmount - totalAdjustments;
            var isFullyPaid    = amountPaid >= effectiveTotal && effectiveTotal > 0;
            var isPreShipment  = invoiceType == InvoiceType.PreShipment && !isShipped && !isFullyPaid;

            // A pre-shipment invoice is a REAL invoice — never proforma.
            // Only treat as proforma if the sale hasn't shipped AND it isn't a pre-shipment invoice.
            var isProforma = !isPreShipment &&
                             (sale.SaleStatus != SaleStatus.Shipped && sale.SaleStatus != SaleStatus.Delivered);

            var dueDate = isQuotation && isProforma
                ? (DateTime?)sale.SaleDate.AddDays(60)
                : sale.PaymentDueDate;

            // Build customer info using the same AP-routing logic as the controller
            var customer = BuildCustomerInfo(sale);

            var lineItems = sale.SaleItems.Select(si => new InvoiceLineItem
            {
                ItemId      = si.ItemId ?? si.FinishedGoodId ?? si.ServiceTypeId ?? 0,
                PartNumber  = si.ProductPartNumber  ?? "N/A",
                Description = si.ProductName        ?? "N/A",
                Quantity    = si.QuantitySold,
                UnitPrice   = si.UnitPrice,
                Notes       = si.Notes              ?? string.Empty,
                ProductType = si.ItemId.HasValue        ? "Item"
                            : si.ServiceTypeId.HasValue  ? "Service"
                            :                              "FinishedGood",
                QuantityBackordered = si.QuantityBackordered,
                SerialNumber        = si.SerialNumber,
                ModelNumber         = si.ModelNumber
            }).ToList();

            return new InvoiceReportViewModel
            {
                InvoiceNumber       = sale.SaleNumber,
                InvoiceDate         = sale.SaleDate,
                DueDate             = dueDate,
                SaleStatus          = sale.SaleStatus,
                PaymentStatus       = sale.PaymentStatus,
                PaymentTerms        = sale.Terms,
                Notes               = sale.Notes          ?? string.Empty,
                OrderNumber         = sale.OrderNumber    ?? string.Empty,
                ShippingAddress     = sale.ShippingAddress ?? string.Empty,
                TotalShipping       = sale.ShippingCost,
                TotalTax            = sale.TaxAmount,
                TotalDiscount       = sale.DiscountCalculated,
                DiscountReason      = sale.DiscountReason,
                HasDiscount         = sale.HasDiscount,
                TotalAdjustments    = totalAdjustments,
                OriginalAmount      = sale.TotalAmount,
                AmountPaid          = amountPaid,
                PaymentMethod       = sale.PaymentMethod  ?? string.Empty,
                IsOverdue           = sale.IsOverdue,
                DaysOverdue         = sale.DaysOverdue,
                IsProforma          = isProforma,
                IsPreShipmentInvoice = isPreShipment,
                IsQuotation         = isQuotation,
                InvoiceTitle        = isQuotation && isProforma ? "Quotation"
                                    : isProforma                ? "Proforma Invoice"
                                    :                             "Invoice",
                CompanyInfo         = companyInfo,
                Customer            = customer,
                IsDirectedToAP      = sale.Customer?.DirectInvoicesToAP      ?? false,
                APContactName       = sale.Customer?.AccountsPayableContactName,
                RequiresPO          = sale.Customer?.RequiresPurchaseOrder    ?? false,
                CustomerEmail       = customer.CustomerEmail,
                EmailSubject        = $"Invoice {sale.SaleNumber}",
                EmailMessage        = $"Please find attached Invoice {sale.SaleNumber} for your recent purchase.",
                LineItems           = lineItems
            };
        }

        private static CustomerInfo BuildCustomerInfo(Sale sale)
        {
            var customer = sale.Customer;
            if (customer == null) return new CustomerInfo();

            // Mirror the GetInvoiceRecipientInfo helper in SalesController
            var companyName  = !string.IsNullOrEmpty(customer.CompanyName)
                ? customer.CompanyName
                : customer.CustomerName;
            var contactName  = customer.CustomerName;
            string recipientEmail, billingAddress;

            if (customer.DirectInvoicesToAP && customer.HasAccountsPayableInfo)
            {
                contactName    = customer.AccountsPayableContactName ?? $"Accounts Payable - {companyName}";
                recipientEmail = customer.AccountsPayableEmail ?? customer.Email ?? string.Empty;
                billingAddress = customer.InvoiceBillingAddress ?? string.Empty;
            }
            else
            {
                recipientEmail = customer.ContactEmail ?? customer.Email ?? string.Empty;
                billingAddress = customer.FullBillingAddress ?? string.Empty;
            }

            return new CustomerInfo
            {
                CompanyName     = companyName,
                CustomerName    = contactName,
                CustomerEmail   = recipientEmail,
                CustomerPhone   = customer.Phone ?? string.Empty,
                BillingAddress  = billingAddress,
                ShippingAddress = sale.ShippingAddress ?? customer.FullShippingAddress ?? string.Empty
            };
        }

        /// <summary>
        /// Renders a named Razor view to a PDF byte array using Rotativa.
        /// </summary>
        private async Task<byte[]?> RenderViewAsPdfBytesAsync(string viewName, object model)
        {
            var httpContextAccessor = _serviceProvider.GetService<IHttpContextAccessor>();
            var httpContext         = httpContextAccessor?.HttpContext;

            if (httpContext == null)
            {
                _logger.LogWarning("No HttpContext available — skipping PDF generation.");
                return null;
            }

            var actionContext = new ActionContext(
                httpContext,
                httpContext.GetRouteData() ?? new Microsoft.AspNetCore.Routing.RouteData(),
                new ActionDescriptor());

            var viewAsPdf = new ViewAsPdf(viewName, model)
            {
                PageSize        = Rotativa.AspNetCore.Options.Size.Letter,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
                PageMargins     = new Rotativa.AspNetCore.Options.Margins(10, 10, 10, 10)
            };

            return await viewAsPdf.BuildFile(actionContext);
        }

        private async Task<CompanyInfo> GetCompanyInfoSafe()
        {
            try   { return await _companyInfoService.GetCompanyInfoAsync(); }
            catch { return new CompanyInfo { CompanyName = "Your Company" }; }
        }

        /// <summary>
        /// Returns a unique invoice number for a pre-shipment invoice.
        /// <list type="bullet">
        ///   <item>1st generation ? <c>SALE-2025-00042</c>       (plain sale number)</item>
        ///   <item>2nd generation ? <c>SALE-2025-00042-A</c></item>
        ///   <item>3rd generation ? <c>SALE-2025-00042-B</c></item>
        ///   <item>…and so on up to Z, then AA, AB…</item>
        /// </list>
        /// This keeps the number traceable to the originating sale while satisfying
        /// the unique index on <c>Invoices.InvoiceNumber</c>.
        /// </summary>
        private async Task<string> GenerateUniquePreShipmentInvoiceNumberAsync(string saleNumber)
        {
            // Load every invoice number that could collide: the bare sale number
            // or anything starting with "<saleNumber>-".
            var existing = await _context.Invoices
                .Where(i => i.InvoiceNumber == saleNumber
                         || i.InvoiceNumber.StartsWith(saleNumber + "-"))
                .Select(i => i.InvoiceNumber)
                .ToListAsync();

            // No collision — first-time generation keeps the plain sale number.
            if (!existing.Contains(saleNumber))
                return saleNumber;

            // Collect all letter suffixes already in use (e.g. "A", "B", "AA").
            var usedSuffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var prefix = saleNumber + "-";
            foreach (var num in existing)
            {
                if (num.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    usedSuffixes.Add(num[prefix.Length..]);
            }

            // Generate the next alphabetic suffix (A, B, … Z, AA, AB, …).
            var candidate = NextAlphaSuffix(usedSuffixes);
            return $"{saleNumber}-{candidate}";
        }

        /// <summary>
        /// Returns the next alphabetic suffix not already present in <paramref name="used"/>.
        /// Sequence: A, B, … Z, AA, AB, … AZ, BA, … ZZ, AAA, …
        /// </summary>
        private static string NextAlphaSuffix(IReadOnlySet<string> used)
        {
            // Iterate single letters first, then two-letter combinations, etc.
            for (int length = 1; ; length++)
            {
                foreach (var candidate in AlphaCombinations(length))
                {
                    if (!used.Contains(candidate))
                        return candidate;
                }
            }
        }

        /// <summary>
        /// Enumerates all upper-case alphabetic strings of exactly <paramref name="length"/>
        /// characters in lexicographic order (A…Z, AA…ZZ, …).
        /// </summary>
        private static IEnumerable<string> AlphaCombinations(int length)
        {
            if (length == 1)
            {
                for (char c = 'A'; c <= 'Z'; c++)
                    yield return c.ToString();
                yield break;
            }

            foreach (var prefix in AlphaCombinations(length - 1))
                for (char c = 'A'; c <= 'Z'; c++)
                    yield return prefix + c;
        }
    }
}
