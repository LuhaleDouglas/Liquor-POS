using System.Security.Claims;

using LiquorPOS.Api.Data;
using LiquorPOS.Api.DTOs;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LiquorPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StockController : ControllerBase
{
    private readonly LiquorPosDbContext _db;

    public StockController(LiquorPosDbContext db)
    {
        _db = db;
    }


    // ============================================================
    // GET CURRENT STOCK
    // GET /api/Stock
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> GetStock()
    {
        var products = await _db.Products
            .Where(p => p.BusinessId == 1)
            .OrderBy(p => p.ProductName)
            .Select(p => new
            {
                p.ProductId,
                p.ProductCode,
                p.ProductName,
                p.Barcode,
                p.Brand,

                Stock = _db.StockTransactions
                    .Where(st =>
                        st.BusinessId == 1 &&
                        st.BranchId == 1 &&
                        st.ProductId == p.ProductId)
                    .Sum(st => (decimal?)st.Quantity) ?? 0,

                Units = _db.ProductUnits
                    .Where(pu => pu.ProductId == p.ProductId)
                    .Join(
                        _db.UnitsOfMeasure,
                        pu => pu.UnitId,
                        u => u.UnitId,
                        (pu, u) => new
                        {
                            pu.ProductUnitId,
                            u.UnitCode,
                            u.UnitName,
                            pu.ConversionFactor,
                            pu.IsBaseUnit
                        })
                    .OrderByDescending(u => u.IsBaseUnit)
                    .ThenBy(u => u.UnitName)
                    .ToList()
            })
            .ToListAsync();

        var result = products.Select(p =>
        {
            var baseUnit = p.Units
                .FirstOrDefault(u => u.IsBaseUnit);

            return new
            {
                p.ProductId,
                p.ProductCode,
                p.ProductName,
                p.Barcode,
                p.Brand,

                Stock = p.Stock,

                BaseUnit = baseUnit == null
                    ? null
                    : new
                    {
                        baseUnit.ProductUnitId,
                        baseUnit.UnitCode,
                        baseUnit.UnitName,
                        baseUnit.ConversionFactor
                    },

                Units = p.Units
            };
        });

        return Ok(result);
    }


    // ============================================================
    // GET STOCK FOR ONE PRODUCT
    // GET /api/Stock/{productId}
    // ============================================================

    [HttpGet("{productId:int}")]
    public async Task<IActionResult> GetProductStock(int productId)
    {
        var product = await _db.Products
            .Where(p =>
                p.BusinessId == 1 &&
                p.ProductId == productId)
            .Select(p => new
            {
                p.ProductId,
                p.ProductCode,
                p.ProductName,
                p.Barcode,
                p.Brand
            })
            .FirstOrDefaultAsync();

        if (product == null)
        {
            return NotFound(new
            {
                message = "Product not found."
            });
        }

        // --------------------------------------------------------
        // Calculate current stock
        // --------------------------------------------------------

        var stock = await _db.StockTransactions
            .Where(st =>
                st.BusinessId == 1 &&
                st.BranchId == 1 &&
                st.ProductId == productId)
            .SumAsync(st => (decimal?)st.Quantity) ?? 0;

        // --------------------------------------------------------
        // Get product units
        // --------------------------------------------------------

        var units = await
            (from pu in _db.ProductUnits
             join u in _db.UnitsOfMeasure
                 on pu.UnitId equals u.UnitId
             where pu.ProductId == productId
             select new
             {
                 pu.ProductUnitId,
                 u.UnitCode,
                 u.UnitName,
                 pu.ConversionFactor,
                 pu.IsBaseUnit
             })
            .OrderByDescending(u => u.IsBaseUnit)
            .ThenBy(u => u.UnitName)
            .ToListAsync();

        var baseUnit = units
            .FirstOrDefault(u => u.IsBaseUnit);

        // --------------------------------------------------------
        // Return stock
        // --------------------------------------------------------

        return Ok(new
        {
            product.ProductId,
            product.ProductCode,
            product.ProductName,
            product.Barcode,
            product.Brand,

            Stock = stock,

            BaseUnit = baseUnit == null
                ? null
                : new
                {
                    baseUnit.ProductUnitId,
                    baseUnit.UnitCode,
                    baseUnit.UnitName,
                    baseUnit.ConversionFactor
                },

            Units = units
        });
    }


    // ============================================================
    // GET STOCK MOVEMENTS
    // GET /api/Stock/movements
    // ============================================================

    [HttpGet("movements")]
    public async Task<IActionResult> GetStockMovements()
    {
        var movements = await
            (from st in _db.StockTransactions
             join p in _db.Products
                 on st.ProductId equals p.ProductId
             where st.BusinessId == 1 &&
                   st.BranchId == 1 &&
                   p.BusinessId == 1
             orderby st.TransactionDate descending
             select new
             {
                 st.StockTransactionId,

                 st.ProductId,

                 ProductCode = p.ProductCode,

                 ProductName = p.ProductName,

                 st.TransactionType,

                 st.Quantity,

                 st.UnitCost,

                 st.ReferenceType,

                 st.ReferenceId,

                 st.TransactionDate,

                 st.UserId,

                 st.Notes
             })
            .ToListAsync();

        return Ok(movements);
    }


    // ============================================================
    // ADJUST STOCK
    // POST /api/Stock/adjustment
    //
    // Positive Quantity  = Increase stock
    // Negative Quantity  = Decrease stock
    // ============================================================

    [HttpPost("adjustment")]
    public async Task<IActionResult> AdjustStock(
        [FromBody] StockAdjustmentRequest request)
    {
        // --------------------------------------------------------
        // Validate product
        // --------------------------------------------------------

        if (request.ProductId <= 0)
        {
            return BadRequest(new
            {
                message = "A valid product is required."
            });
        }

        // --------------------------------------------------------
        // Validate quantity
        // --------------------------------------------------------

        if (request.Quantity == 0)
        {
            return BadRequest(new
            {
                message = "Adjustment quantity cannot be zero."
            });
        }

        // --------------------------------------------------------
        // Validate reason
        // --------------------------------------------------------

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest(new
            {
                message = "Stock adjustment reason is required."
            });
        }

        // --------------------------------------------------------
        // Get product
        // --------------------------------------------------------

        var product = await _db.Products
            .FirstOrDefaultAsync(p =>
                p.BusinessId == 1 &&
                p.ProductId == request.ProductId);

        if (product == null)
        {
            return NotFound(new
            {
                message = "Product not found."
            });
        }

        // --------------------------------------------------------
        // Calculate current stock
        // --------------------------------------------------------

        var currentStock = await _db.StockTransactions
            .Where(st =>
                st.BusinessId == 1 &&
                st.BranchId == 1 &&
                st.ProductId == request.ProductId)
            .SumAsync(st => (decimal?)st.Quantity) ?? 0;

        // --------------------------------------------------------
        // Calculate new stock
        // --------------------------------------------------------

        var newStock = currentStock + request.Quantity;

        // --------------------------------------------------------
        // Prevent negative stock
        // --------------------------------------------------------

        if (newStock < 0)
        {
            return BadRequest(new
            {
                message =
                    $"Insufficient stock. Current stock is {currentStock:0.##}."
            });
        }

        // --------------------------------------------------------
        // Get logged-in user
        // --------------------------------------------------------

        var userIdClaim =
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new
            {
                message = "Unable to identify the logged-in user."
            });
        }

        // --------------------------------------------------------
        // Create stock transaction
        // --------------------------------------------------------

        var transaction = new Models.StockTransaction
        {
            BusinessId = 1,
            BranchId = 1,
            ProductId = request.ProductId,

            TransactionType = "STOCK_ADJUSTMENT",

            Quantity = request.Quantity,

            UnitCost = 0,

            ReferenceType = "STOCK_ADJUSTMENT",

            ReferenceId = null,

            TransactionDate = DateTime.Now,

            UserId = userId,

            Notes = string.IsNullOrWhiteSpace(request.Notes)
                ? request.Reason
                : $"{request.Reason} - {request.Notes}"
        };

        _db.StockTransactions.Add(transaction);

        await _db.SaveChangesAsync();

        // --------------------------------------------------------
        // Return result
        // --------------------------------------------------------

        return Ok(new
        {
            message = "Stock adjusted successfully.",

            data = new
            {
                stockTransactionId =
                    transaction.StockTransactionId,

                productId =
                    product.ProductId,

                productCode =
                    product.ProductCode,

                productName =
                    product.ProductName,

                previousStock =
                    currentStock,

                adjustment =
                    request.Quantity,

                newStock =
                    newStock,

                reason =
                    request.Reason,

                notes =
                    request.Notes,

                transactionDate =
                    transaction.TransactionDate
            }
        });
    }
}