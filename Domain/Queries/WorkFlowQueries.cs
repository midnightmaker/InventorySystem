using InventorySystem.Domain.Enums;
using InventorySystem.ViewModels;

namespace InventorySystem.Domain.Queries
{
  public class GetProductionWorkflowQuery : IQuery<ProductionWorkflowResult>
  {
    public GetProductionWorkflowQuery(int productionId)
    {
      ProductionId = productionId;
      RequestedAt = DateTime.UtcNow;
    }

    public int ProductionId { get; }
    public DateTime RequestedAt { get; }
  }

  public class GetWipDashboardQuery : IQuery<WipDashboardResult>
  {
    public GetWipDashboardQuery(DateTime? fromDate = null, DateTime? toDate = null, string? assignedTo = null)
    {
      FromDate = fromDate;
      ToDate = toDate;
      AssignedTo = assignedTo;
      RequestedAt = DateTime.UtcNow;
    }

    public DateTime? FromDate { get; }
    public DateTime? ToDate { get; }
    public string? AssignedTo { get; }
    public DateTime RequestedAt { get; }
  }

  public class GetProductionTimelineQuery : IQuery<ProductionTimelineResult>
  {
    public GetProductionTimelineQuery(int productionId)
    {
      ProductionId = productionId;
      RequestedAt = DateTime.UtcNow;
    }

    public int ProductionId { get; }
    public DateTime RequestedAt { get; }
  }

  public class GetActiveProductionsQuery : IQuery<List<ProductionSummary>>
  {
    public GetActiveProductionsQuery(string? assignedTo = null, ProductionStatus? status = null)
    {
      AssignedTo = assignedTo;
      Status = status;
      RequestedAt = DateTime.UtcNow;
    }

    public string? AssignedTo { get; }
    public ProductionStatus? Status { get; }
    public DateTime RequestedAt { get; }
  }

  public class GetOverdueProductionsQuery : IQuery<List<ProductionSummary>>
  {
    public GetOverdueProductionsQuery()
    {
      RequestedAt = DateTime.UtcNow;
    }

    public DateTime RequestedAt { get; }
  }

  
}