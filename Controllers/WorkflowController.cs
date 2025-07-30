// Controllers/WorkflowController.cs
using Microsoft.AspNetCore.Mvc;
using InventorySystem.Domain.Commands;
using InventorySystem.Domain.Queries;
using InventorySystem.Domain.Services;
using InventorySystem.Domain.Enums;

namespace InventorySystem.Controllers
{
  [Route("api/[controller]")]
  [ApiController]
  public class WorkflowController : ControllerBase
  {
    private readonly IProductionOrchestrator _orchestrator;
    private readonly IWorkflowEngine _workflowEngine;
    private readonly ILogger<WorkflowController> _logger;

    public WorkflowController(
        IProductionOrchestrator orchestrator,
        IWorkflowEngine workflowEngine,
        ILogger<WorkflowController> logger)
    {
      _orchestrator = orchestrator;
      _workflowEngine = workflowEngine;
      _logger = logger;
    }

    [HttpPost("start/{productionId}")]
    public async Task<IActionResult> StartProduction(int productionId, [FromBody] StartProductionRequest request)
    {
      try
      {
        var command = new StartProductionCommand(
            productionId,
            request.AssignedTo,
            request.EstimatedCompletion,
            User.Identity?.Name);

        var result = await _orchestrator.StartProductionAsync(command);

        if (result.Success)
        {
          return Ok(new { success = true, data = result.Data });
        }

        return BadRequest(new { success = false, error = result.ErrorMessage });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error starting production {ProductionId}", productionId);
        return StatusCode(500, new { success = false, error = "Internal server error" });
      }
    }

    [HttpPut("status/{productionId}")]
    public async Task<IActionResult> UpdateStatus(int productionId, [FromBody] UpdateStatusRequest request)
    {
      try
      {
        var command = new UpdateProductionStatusCommand(
            productionId,
            request.NewStatus,
            request.Reason,
            request.Notes,
            User.Identity?.Name);

        var result = await _orchestrator.UpdateProductionStatusAsync(command);

        if (result.Success)
        {
          return Ok(new { success = true, data = result.Data });
        }

        return BadRequest(new { success = false, error = result.ErrorMessage });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error updating status for production {ProductionId}", productionId);
        return StatusCode(500, new { success = false, error = "Internal server error" });
      }
    }

    [HttpPut("assign/{productionId}")]
    public async Task<IActionResult> AssignProduction(int productionId, [FromBody] AssignProductionRequest request)
    {
      try
      {
        var command = new AssignProductionCommand(
            productionId,
            request.AssignedTo,
            User.Identity?.Name);

        var result = await _orchestrator.AssignProductionAsync(command);

        if (result.Success)
        {
          return Ok(new { success = true, data = result.Data });
        }

        return BadRequest(new { success = false, error = result.ErrorMessage });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error assigning production {ProductionId}", productionId);
        return StatusCode(500, new { success = false, error = "Internal server error" });
      }
    }

    [HttpPost("quality-check/{productionId}")]
    public async Task<IActionResult> CompleteQualityCheck(int productionId, [FromBody] QualityCheckRequest request)
    {
      try
      {
        var command = new CompleteQualityCheckCommand(
            productionId,
            request.Passed,
            request.Notes,
            request.QualityCheckerId,
            User.Identity?.Name);

        var result = await _orchestrator.ProcessQualityCheckAsync(command);

        if (result.Success)
        {
          return Ok(new { success = true, data = result.Data });
        }

        return BadRequest(new { success = false, error = result.ErrorMessage });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error completing quality check for production {ProductionId}", productionId);
        return StatusCode(500, new { success = false, error = "Internal server error" });
      }
    }

    [HttpPost("hold/{productionId}")]
    public async Task<IActionResult> PutOnHold(int productionId, [FromBody] PutOnHoldRequest request)
    {
      try
      {
        var command = new PutProductionOnHoldCommand(
            productionId,
            request.Reason,
            User.Identity?.Name);

        var result = await _workflowEngine.PutOnHoldAsync(command);

        if (result.Success)
        {
          return Ok(new { success = true, data = result.Data });
        }

        return BadRequest(new { success = false, error = result.ErrorMessage });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error putting production {ProductionId} on hold", productionId);
        return StatusCode(500, new { success = false, error = "Internal server error" });
      }
    }

    [HttpPost("resume/{productionId}")]
    public async Task<IActionResult> ResumeFromHold(int productionId)
    {
      try
      {
        var result = await _workflowEngine.ResumeFromHoldAsync(productionId, User.Identity?.Name);

        if (result.Success)
        {
          return Ok(new { success = true, data = result.Data });
        }

        return BadRequest(new { success = false, error = result.ErrorMessage });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error resuming production {ProductionId} from hold", productionId);
        return StatusCode(500, new { success = false, error = "Internal server error" });
      }
    }

    [HttpPost("cancel/{productionId}")]
    public async Task<IActionResult> CancelProduction(int productionId, [FromBody] CancelProductionRequest request)
    {
      try
      {
        var result = await _orchestrator.CancelProductionAsync(
            productionId,
            request.Reason,
            User.Identity?.Name);

        if (result.Success)
        {
          return Ok(new { success = true, data = result.Data });
        }

        return BadRequest(new { success = false, error = result.ErrorMessage });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error cancelling production {ProductionId}", productionId);
        return StatusCode(500, new { success = false, error = "Internal server error" });
      }
    }

    [HttpGet("{productionId}/workflow")]
    public async Task<IActionResult> GetProductionWorkflow(int productionId)
    {
      try
      {
        var query = new GetProductionWorkflowQuery(productionId);
        var result = await _orchestrator.GetProductionWorkflowAsync(query);

        if (result == null)
        {
          return NotFound(new { success = false, error = "Production workflow not found" });
        }

        return Ok(new { success = true, data = result });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting workflow for production {ProductionId}", productionId);
        return StatusCode(500, new { success = false, error = "Internal server error" });
      }
    }

    [HttpGet("{productionId}/timeline")]
    public async Task<IActionResult> GetProductionTimeline(int productionId)
    {
      try
      {
        var query = new GetProductionTimelineQuery(productionId);
        var result = await _orchestrator.GetProductionTimelineAsync(query);

        return Ok(new { success = true, data = result });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting timeline for production {ProductionId}", productionId);
        return StatusCode(500, new { success = false, error = "Internal server error" });
      }
    }

    [HttpGet("{productionId}/valid-statuses")]
    public async Task<IActionResult> GetValidNextStatuses(int productionId)
    {
      try
      {
        var validStatuses = await _workflowEngine.GetValidNextStatusesAsync(productionId);
        return Ok(new { success = true, data = validStatuses });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting valid statuses for production {ProductionId}", productionId);
        return StatusCode(500, new { success = false, error = "Internal server error" });
      }
    }

    [HttpGet("workload")]
    public async Task<IActionResult> GetEmployeeWorkload()
    {
      try
      {
        var workload = await _orchestrator.GetEmployeeWorkloadAsync();
        return Ok(new { success = true, data = workload });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting employee workload");
        return StatusCode(500, new { success = false, error = "Internal server error" });
      }
    }
  }

  // Request DTOs
  public class StartProductionRequest
  {
    public string? AssignedTo { get; set; }
    public DateTime? EstimatedCompletion { get; set; }
  }

  public class UpdateStatusRequest
  {
    public ProductionStatus NewStatus { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
  }

  public class AssignProductionRequest
  {
    public string AssignedTo { get; set; } = null!;
  }

  public class QualityCheckRequest
  {
    public bool Passed { get; set; }
    public string? Notes { get; set; }
    public int? QualityCheckerId { get; set; }
  }

  public class PutOnHoldRequest
  {
    public string Reason { get; set; } = null!;
  }

  public class CancelProductionRequest
  {
    public string Reason { get; set; } = null!;
  }
}
