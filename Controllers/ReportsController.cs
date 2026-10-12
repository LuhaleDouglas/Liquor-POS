using LiquorPOS.Api.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiquorPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly ReportsService _reportsService;

    public ReportsController(
        ReportsService reportsService)
    {
        _reportsService = reportsService;
    }

    // ============================================================
    // SALES REPORT
    // ============================================================

    [HttpGet("sales")]
    public async Task<IActionResult> GetSalesReport(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate)
    {
        var result =
            await _reportsService.GetSalesReport(
                User,
                fromDate,
                toDate);

        if (!result.Success)
        {
            if (result.Message.Contains(
                    "permission",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            return BadRequest(new
            {
                message = result.Message
            });
        }

        return Ok(result.Data);
    }


    // ============================================================
    // POS SESSION REPORT
    // ============================================================

    [HttpGet("pos-sessions")]
    public async Task<IActionResult> GetPOSSessionsReport(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate)
    {
        var result =
            await _reportsService.GetPOSSessionsReport(
                User,
                fromDate,
                toDate);

        if (!result.Success)
        {
            if (result.Message.Contains(
                    "permission",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            return BadRequest(new
            {
                message = result.Message
            });
        }

        return Ok(result.Data);
    }
}
