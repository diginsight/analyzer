using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Entities;
using Diginsight.Analyzer.Entities.Permissions;
using Microsoft.AspNetCore.Mvc;

namespace Diginsight.Analyzer.API.Controllers;

public sealed class PermissionController : ControllerBase
{
    private readonly IPermissionService permissionService;

    public PermissionController(IPermissionService permissionService)
    {
        this.permissionService = permissionService;
    }

    // TODO GetPermissionAssignments

    [HttpPut("permissions")]
    public async Task<IActionResult> AssignPermission([FromBody] IPermissionAssignment assignment)
    {
        await permissionService.AssignPermissionAsync(assignment, HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> RemovePermission([FromBody] IPermissionAssignment assignment)
    {
        await permissionService.RemovePermissionAsync(assignment, HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpPut("execution/{executionId:guid}/permissions")]
    public async Task<IActionResult> AssignAnalysisPermission(
        [FromRoute] Guid executionId, [FromBody] AnalysisSpecificPermissionAssignment assignment
    )
    {
        await permissionService.AssignAnalysisPermissionAsync(executionId, assignment, HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpPut("analysis/{analysisId:guid}/attempt/{attempt:int}/permissions")]
    public async Task<IActionResult> AssignAnalysisPermission(
        [FromRoute] Guid analysisId, [FromRoute] int attempt, [FromBody] AnalysisSpecificPermissionAssignment assignment
    )
    {
        await permissionService.AssignAnalysisPermissionAsync(new AnalysisCoord(analysisId, attempt), assignment, HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpDelete("execution/{executionId:guid}/permissions")]
    public async Task<IActionResult> RemoveAnalysisPermission(
        [FromRoute] Guid executionId, [FromBody] AnalysisSpecificPermissionAssignment assignment
    )
    {
        await permissionService.RemoveAnalysisPermissionAsync(executionId, assignment, HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpDelete("analysis/{analysisId:guid}/attempt/{attempt:int}/permissions")]
    public async Task<IActionResult> RemoveAnalysisPermission(
        [FromRoute] Guid analysisId, [FromRoute] int attempt, [FromBody] AnalysisSpecificPermissionAssignment assignment
    )
    {
        await permissionService.RemoveAnalysisPermissionAsync(new AnalysisCoord(analysisId, attempt), assignment, HttpContext.RequestAborted);
        return NoContent();
    }
}
