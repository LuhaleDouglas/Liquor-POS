using LiquorPOS.Api.DTOs;
using LiquorPOS.Api.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiquorPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class POSSessionController : ControllerBase
{
    private readonly POSSessionService _sessionService;

    public POSSessionController(
        POSSessionService sessionService)
    {
        _sessionService = sessionService;
    }


    // ============================================================
    // OPEN POS SESSION
    // ============================================================

    [HttpPost("open")]
    public async Task<IActionResult> OpenSession(
        [FromBody] OpenPOSSessionRequest request)
    {
        var result =
            await _sessionService.OpenSession(
                request,
                User);

        if (!result.Success)
        {
            return BadRequest(new
            {
                message = result.Message,
                sessionId = result.SessionId
            });
        }

        return Ok(new
        {
            message = result.Message,
            sessionId = result.SessionId
        });
    }
    // ============================================================
// GET POS SESSION SUMMARY
// ============================================================

[HttpGet("{sessionId:long}")]
public async Task<IActionResult> GetSessionSummary(
    long sessionId)
{
    var result =
        await _sessionService.GetSessionSummary(
            sessionId,
            User);

    if (!result.Success)
    {
        return NotFound(new
        {
            message = result.Message
        });
    }

    return Ok(result.Data);
}
// ============================================================
// CLOSE POS SESSION
// ============================================================

[HttpPost("{sessionId:long}/close")]
public async Task<IActionResult> CloseSession(
    long sessionId,
    [FromBody] ClosePOSSessionRequest request)
{
    var result =
        await _sessionService.CloseSession(
            sessionId,
            request,
            User);

    if (!result.Success)
    {
        return BadRequest(new
        {
            message = result.Message
        });
    }

    return Ok(new
    {
        message = result.Message,
        data = result.Data
    });
}
// ============================================================
// GET CURRENT OPEN POS SESSION
// ============================================================

[HttpGet("current")]
public async Task<IActionResult> GetCurrentSession(
    [FromQuery] int posTerminalId)
{
    var result =
        await _sessionService.GetCurrentOpenSession(
            User,
            posTerminalId);

    if (!result.Success)
    {
        return BadRequest(new
        {
            message = result.Message
        });
    }

    return Ok(new
    {
        message = result.Message,
        data = result.Data
    });
}
}