// Helpers/BadgeHelper.cs
using InventorySystem.Models;
using InventorySystem.Models.Enums;

namespace InventorySystem.Helpers
{
    /// <summary>
    /// Static helper class for generating Bootstrap badge CSS classes based on status and priority enums
    /// </summary>
    public static class BadgeHelper
    {
        /// <summary>
        /// Gets the Bootstrap CSS class for ServiceOrderStatus badges
        /// </summary>
        /// <param name="status">The service order status</param>
        /// <returns>Bootstrap CSS class name (e.g., "success", "warning", "danger")</returns>
        public static string GetServiceOrderStatusBadgeColor(ServiceOrderStatus status)
        {
            return status switch
            {
                ServiceOrderStatus.Requested => "secondary",
                ServiceOrderStatus.Quoted => "info",
                ServiceOrderStatus.Approved => "primary",
                ServiceOrderStatus.Scheduled => "warning",
                ServiceOrderStatus.InProgress => "primary",
                ServiceOrderStatus.AwaitingParts => "warning",
                ServiceOrderStatus.QualityCheck => "info",
                ServiceOrderStatus.Completed => "success",
                ServiceOrderStatus.Delivered => "success",
                ServiceOrderStatus.Cancelled => "danger",
                ServiceOrderStatus.OnHold => "secondary",
                _ => "secondary"
            };
        }

        /// <summary>
        /// Gets the Bootstrap CSS class for ServicePriority badges
        /// </summary>
        /// <param name="priority">The service priority</param>
        /// <returns>Bootstrap CSS class name (e.g., "success", "warning", "danger")</returns>
        public static string GetServicePriorityBadgeColor(ServicePriority priority)
        {
            return priority switch
            {
                ServicePriority.Low => "success",
                ServicePriority.Normal => "secondary",
                ServicePriority.High => "warning",
                ServicePriority.Urgent => "danger",
                ServicePriority.Emergency => "danger",
                _ => "secondary"
            };
        }

        /// <summary>
        /// Gets the Bootstrap CSS class for ProjectStatus badges
        /// </summary>
        /// <param name="status">The project status</param>
        /// <returns>Bootstrap CSS class name (e.g., "success", "warning", "danger")</returns>
        public static string GetProjectStatusBadgeColor(ProjectStatus status)
        {
            return status switch
            {
                ProjectStatus.Planning => "secondary",
                ProjectStatus.Active => "success",
                ProjectStatus.On_Hold => "warning",
                ProjectStatus.Completed => "primary",
                ProjectStatus.Cancelled => "danger",
                ProjectStatus.Suspended => "dark",
                ProjectStatus.Under_Review => "info",
                _ => "secondary"
            };
        }

        /// <summary>
        /// Gets the Bootstrap CSS class for ProjectPriority badges
        /// </summary>
        /// <param name="priority">The project priority</param>
        /// <returns>Bootstrap CSS class name (e.g., "success", "warning", "danger")</returns>
        public static string GetProjectPriorityBadgeColor(ProjectPriority priority)
        {
            return priority switch
            {
                ProjectPriority.Low => "success",
                ProjectPriority.Medium => "info",
                ProjectPriority.High => "warning",
                ProjectPriority.Critical => "danger",
                ProjectPriority.Strategic => "primary",
                _ => "secondary"
            };
        }

        /// <summary>
        /// Gets the Bootstrap CSS class for SaleStatus badges
        /// </summary>
        /// <param name="status">The sale status</param>
        /// <returns>Bootstrap CSS class name (e.g., "success", "warning", "danger")</returns>
        public static string GetSaleStatusBadgeColor(SaleStatus status)
        {
            return status switch
            {
                SaleStatus.Processing => "primary",
                SaleStatus.Backordered => "warning",
                SaleStatus.PartiallyShipped => "info",
                SaleStatus.Shipped => "success",
                SaleStatus.Delivered => "info",
                SaleStatus.Cancelled => "danger",
                SaleStatus.Returned => "warning",
                _ => "secondary"
            };
        }

        /// <summary>
        /// Gets the Bootstrap CSS class for PaymentStatus badges
        /// </summary>
        /// <param name="status">The payment status</param>
        /// <returns>Bootstrap CSS class name (e.g., "success", "warning", "danger")</returns>
        public static string GetPaymentStatusBadgeColor(PaymentStatus status)
        {
            return status switch
            {
                PaymentStatus.Paid => "success",
                PaymentStatus.Pending => "warning",
                PaymentStatus.Overdue => "danger",
                PaymentStatus.PartiallyPaid => "info",
                _ => "secondary"
            };
        }

        /// <summary>
        /// Gets the Bootstrap CSS class for PurchaseStatus badges
        /// </summary>
        /// <param name="status">The purchase status</param>
        /// <returns>Bootstrap CSS class name (e.g., "success", "warning", "danger")</returns>
        public static string GetPurchaseStatusBadgeColor(PurchaseStatus status)
        {
            return status switch
            {
                PurchaseStatus.Pending => "warning",
                PurchaseStatus.Ordered => "primary",
                PurchaseStatus.Shipped => "info",
                PurchaseStatus.Received => "success",
                PurchaseStatus.Cancelled => "danger",
                _ => "secondary"
            };
        }

        /// <summary>
        /// Gets the Bootstrap CSS class for any generic status enum by converting to string and using common patterns
        /// </summary>
        /// <param name="status">Any status enum</param>
        /// <returns>Bootstrap CSS class name (e.g., "success", "warning", "danger")</returns>
        public static string GetGenericStatusBadgeColor(Enum status)
        {
            var statusString = status.ToString().ToLower();

            return statusString switch
            {
                // Success states
                var s when s.Contains("completed") || s.Contains("success") || s.Contains("delivered") || 
                          s.Contains("received") || s.Contains("paid") || s.Contains("active") => "success",
                
                // Warning states  
                var s when s.Contains("pending") || s.Contains("warning") || s.Contains("hold") || 
                          s.Contains("backordered") || s.Contains("overdue") || s.Contains("scheduled") => "warning",
                
                // Danger states
                var s when s.Contains("cancelled") || s.Contains("error") || s.Contains("failed") || 
                          s.Contains("emergency") || s.Contains("urgent") || s.Contains("critical") => "danger",
                
                // Info states
                var s when s.Contains("processing") || s.Contains("progress") || s.Contains("shipped") || 
                          s.Contains("info") || s.Contains("review") => "info",
                
                // Primary states
                var s when s.Contains("primary") || s.Contains("approved") || s.Contains("ordered") => "primary",
                
                // Default
                _ => "secondary"
            };
        }
    }
}