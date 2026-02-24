// Services/IInvoiceService.cs
using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.ViewModels;

namespace InventorySystem.Services
{
    public interface IInvoiceService
    {
        /// <summary>
        /// Creates a new Invoice record for the given sale/shipment, renders the PDF from
        /// InvoiceReportPrint, stores the bytes on the row, and saves to the database.
        /// Returns the newly created Invoice.
        /// </summary>
        Task<Invoice> GenerateAndStoreInvoiceAsync(int saleId, int shipmentId, string issuedBy);

        /// <summary>
        /// Returns the stored PDF bytes for an existing invoice.
        /// Returns null if the invoice has no PDF stored.
        /// </summary>
        Task<byte[]?> GetInvoicePdfAsync(int invoiceId);

        /// <summary>
        /// Returns all invoices for a sale, ordered by InvoiceDate descending.
        /// </summary>
        Task<List<Invoice>> GetInvoicesBySaleAsync(int saleId);

        /// <summary>
        /// Returns a single invoice by ID including its related Sale and Shipment.
        /// </summary>
        Task<Invoice?> GetInvoiceByIdAsync(int invoiceId);

        /// <summary>
        /// Generates the next sequential invoice number (e.g., INV-2025-00142).
        /// </summary>
        Task<string> GenerateInvoiceNumberAsync();

        /// <summary>
        /// Builds the <see cref="InvoiceReportViewModel"/> for an existing Invoice row.
        /// This is the canonical method used both for PDF generation and for the browser
        /// print preview, guaranteeing they are identical documents.
        /// </summary>
        Task<InvoiceReportViewModel?> BuildInvoiceViewModelAsync(int invoiceId);

        /// <summary>
        /// Creates a pre-shipment Invoice for sales that require payment before
        /// shipping (e.g. Immediate / PrePayment terms).  No Shipment row is
        /// required — <see cref="Invoice.ShipmentId"/> will be <c>null</c>.
        /// Returns the newly created Invoice.
        /// </summary>
        Task<Invoice> GeneratePreShipmentInvoiceAsync(int saleId, string issuedBy);
    }
}
