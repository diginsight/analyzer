using Diginsight.Analyzer.Business;
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
    // TODO AssignPermissionAssignment
    // TODO RemovePermissionAssignment
    // TODO AssignAnalysisPermissionAssignment
    // TODO RemoveAnalysisPermissionAssignment
}
