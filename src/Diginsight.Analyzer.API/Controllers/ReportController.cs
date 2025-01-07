#if FEATURE_REPORTS
using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Diginsight.Analyzer.API.Controllers;

public sealed class ReportController : ControllerBase
{
    private readonly IReportService reportService;

    public ReportController(IReportService reportService)
    {
        this.reportService = reportService;
    }

    [HttpGet("execution/{executionId:guid}/report")]
    public async Task<IActionResult> GetReport([FromRoute] Guid executionId)
    {
        return await reportService.GetReportAsync(executionId, HttpContext.RequestAborted) is { } report
            ? Ok(report) : throw AnalysisExceptions.NoSuchExecution;
    }

    [HttpGet("analysis/{analysisId:guid}/attempt/{attempt:int}/report")]
    public async Task<IActionResult> GetReport([FromRoute] Guid analysisId, [FromRoute] int attempt)
    {
        return await reportService.GetReportAsync(new AnalysisCoord(analysisId, attempt), HttpContext.RequestAborted) is { } report
            ? Ok(report) : throw AnalysisExceptions.NoSuchAnalysis;
    }
}
#endif
