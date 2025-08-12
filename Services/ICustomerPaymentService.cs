using InventorySystem.Models;
using InventorySystem.Models.Enums;

namespace InventorySystem.Services
{
    public interface ICustomerPaymentService
    {
        // Basic CRUD operations
        Task<CustomerPayment> CreatePaymentAsync(CustomerPayment payment);
        Task<CustomerPayment> UpdatePaymentAsync(CustomerPayment payment);
        Task<CustomerPayment?> GetPaymentByIdAsync(int id);
        Task<IEnumerable<CustomerPayment>> GetAllPaymentsAsync();
        Task DeletePaymentAsync(int id);

        // Payment queries
        Task<IEnumerable<CustomerPayment>> GetPaymentsBySaleAsync(int saleId);
        Task<IEnumerable<CustomerPayment>> GetPaymentsByCustomerAsync(int customerId);
        Task<IEnumerable<CustomerPayment>> GetPaymentsByMethodAsync(string paymentMethod);
        Task<IEnumerable<CustomerPayment>> GetPaymentsByDateRangeAsync(DateTime startDate, DateTime endDate);

        // Payment calculations
        Task<decimal> GetTotalPaymentsBySaleAsync(int saleId);
        Task<decimal> GetTotalPaymentsByCustomerAsync(int customerId);
        Task<decimal> GetRemainingBalanceAsync(int saleId);
        Task<bool> IsSaleFullyPaidAsync(int saleId);

        // Payment recording
        Task<CustomerPayment> RecordPaymentAsync(int saleId, decimal amount, string paymentMethod, 
            DateTime paymentDate, string? paymentReference = null, string? notes = null, string? createdBy = null);
        
        // Payment reversal
        Task<CustomerPayment> ReversePaymentAsync(int paymentId, string reason, string? reversedBy = null);

        // Validation
        Task<bool> ValidatePaymentAmountAsync(int saleId, decimal amount);
        Task<PaymentValidationResult> ValidatePaymentAsync(CustomerPayment payment);

        // Reporting
        Task<PaymentSummaryReport> GetPaymentSummaryAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<IEnumerable<CustomerPaymentSummary>> GetCustomerPaymentSummariesAsync();
    }

    public class PaymentValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> ValidationErrors { get; set; } = new();
        public string? WarningMessage { get; set; }
    }

    public class PaymentSummaryReport
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal TotalPayments { get; set; }
        public int PaymentCount { get; set; }
        public decimal AveragePayment { get; set; }
        public Dictionary<string, decimal> PaymentsByMethod { get; set; } = new();
        public List<DailyPaymentSummary> DailySummaries { get; set; } = new();
    }

    public class DailyPaymentSummary
    {
        public DateTime Date { get; set; }
        public decimal TotalAmount { get; set; }
        public int PaymentCount { get; set; }
    }

    public class CustomerPaymentSummary
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalPayments { get; set; }
        public int PaymentCount { get; set; }
        public DateTime? LastPaymentDate { get; set; }
        public decimal AveragePayment { get; set; }
    }
}