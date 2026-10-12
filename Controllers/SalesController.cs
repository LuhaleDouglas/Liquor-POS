
using LiquorPOS.Api.DTOs;
using LiquorPOS.Api.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiquorPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SalesController : ControllerBase
{
    private readonly SalesService _salesService;

    public SalesController(
        SalesService salesService)
    {
        _salesService = salesService;
    }


    // ============================================================
    // CHECKOUT
    // ============================================================

    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout(
        [FromBody] CheckoutRequest request)
    {
        var result =
            await _salesService.Checkout(
                request,
                User);

        if (!result.Success)
        {
            return BadRequest(new
            {
                message =
                    result.Message
            });
        }

        return Ok(new
        {
            message =
                result.Message,

            saleId =
                result.SaleId
        });
    }


    // ============================================================
    // GET ALL SALES
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> GetSales()
    {
        var result =
            await _salesService.GetSales(
                User);

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
                message =
                    result.Message
            });
        }

        return Ok(result.Data);
    }


    // ============================================================
    // GET SINGLE SALE
    // ============================================================

    [HttpGet("{saleId:long}")]
    public async Task<IActionResult> GetSale(
        long saleId)
    {
        var result =
            await _salesService.GetSale(
                saleId,
                User);

        if (!result.Success)
        {
            if (result.Message.Contains(
                    "permission",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            return NotFound(new
            {
                message =
                    result.Message
            });
        }

        return Ok(result.Data);
    }
}
