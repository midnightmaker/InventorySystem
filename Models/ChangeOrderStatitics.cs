namespace InventorySystem.Models
{
  /// <summary>
  /// Statistical information about change orders in the system
  /// </summary>
  public class ChangeOrderStatistics
  {
    /// <summary>
    /// Total number of change orders in the system
    /// </summary>
    public int TotalChangeOrders { get; set; }

    /// <summary>
    /// Number of change orders with Pending status
    /// </summary>
    public int PendingCount { get; set; }

    /// <summary>
    /// Number of change orders with Implemented status
    /// </summary>
    public int ImplementedCount { get; set; }

    /// <summary>
    /// Number of change orders with Cancelled status
    /// </summary>
    public int CancelledCount { get; set; }

    /// <summary>
    /// Total number of documents across all change orders
    /// </summary>
    public int TotalDocuments { get; set; }

    /// <summary>
    /// Number of change orders that have at least one document
    /// </summary>
    public int ChangeOrdersWithDocuments { get; set; }

    /// <summary>
    /// Number of change orders for Items
    /// </summary>
    public int ItemChangeOrders { get; set; }

    /// <summary>
    /// Number of change orders for BOMs
    /// </summary>
    public int BomChangeOrders { get; set; }

    /// <summary>
    /// Percentage of change orders that have documents
    /// </summary>
    public double DocumentationRate => TotalChangeOrders > 0
        ? Math.Round((double)ChangeOrdersWithDocuments / TotalChangeOrders * 100, 1)
        : 0;

    /// <summary>
    /// Average number of documents per change order
    /// </summary>
    public double AverageDocumentsPerChangeOrder => TotalChangeOrders > 0
        ? Math.Round((double)TotalDocuments / TotalChangeOrders, 2)
        : 0;

    /// <summary>
    /// Percentage of pending change orders
    /// </summary>
    public double PendingPercentage => TotalChangeOrders > 0
        ? Math.Round((double)PendingCount / TotalChangeOrders * 100, 1)
        : 0;

    /// <summary>
    /// Percentage of implemented change orders
    /// </summary>
    public double ImplementedPercentage => TotalChangeOrders > 0
        ? Math.Round((double)ImplementedCount / TotalChangeOrders * 100, 1)
        : 0;

    /// <summary>
    /// Percentage of cancelled change orders
    /// </summary>
    public double CancelledPercentage => TotalChangeOrders > 0
        ? Math.Round((double)CancelledCount / TotalChangeOrders * 100, 1)
        : 0;
  }
}