using System.Security.Claims;

using LiquorPOS.Api.Data;
using LiquorPOS.Api.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LiquorPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PurchasesController : ControllerBase
{
    private readonly LiquorPosDbContext _db;

    public PurchasesController(LiquorPosDbContext db)
    {
        _db = db;
    }


    // ============================================================
    // GET ALL PURCHASES
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> GetPurchases()
    {
        var purchases = await _db.Purchases
            .AsNoTracking()
            .Where(p => p.BusinessId == 1)
            .Include(p => p.Supplier)
            .OrderByDescending(p => p.PurchaseDate)
            .ThenByDescending(p => p.PurchaseId)
            .Select(p => new
            {
                p.PurchaseId,
                p.PurchaseNumber,
                p.BusinessId,
                p.BranchId,
                p.SupplierId,
                SupplierName = p.Supplier != null
                    ? p.Supplier.SupplierName
                    : null,
                p.CreatedBy,
                p.PurchaseDate,
                p.SubTotal,
                p.TaxAmount,
                p.TotalAmount,
                p.Status,
                p.Notes,
                p.CreatedAt,
                LineCount = p.PurchaseLines.Count()
            })
            .ToListAsync();

        return Ok(purchases);
    }


    
// ============================================================
// GET PURCHASE BY ID
// ============================================================

[HttpGet("{purchaseId:long}")]
public async Task<IActionResult> GetPurchase(long purchaseId)
{
    var purchase = await _db.Purchases
        .AsNoTracking()
        .Where(p =>
            p.PurchaseId == purchaseId &&
            p.BusinessId == 1)
        .Include(p => p.Supplier)
        .Include(p => p.PurchaseLines)
            .ThenInclude(line => line.Product)
        .FirstOrDefaultAsync();

    if (purchase == null)
    {
        return NotFound(new
        {
            message = "Purchase not found."
        });
    }

    // Get the product-unit information together with
    // the actual unit name from UnitsOfMeasure.
    var productUnitIds = purchase.PurchaseLines
        .Select(line => line.ProductUnitId)
        .Distinct()
        .ToList();

    var productUnits = await
        (from pu in _db.ProductUnits
         join u in _db.UnitsOfMeasure
             on pu.UnitId equals u.UnitId
         where productUnitIds.Contains(pu.ProductUnitId)
         select new
         {
             pu.ProductUnitId,
             pu.ProductId,
             pu.UnitId,
             UnitCode = u.UnitCode,
             UnitName = u.UnitName,
             pu.ConversionFactor,
             pu.IsBaseUnit
         })
        .ToDictionaryAsync(x => x.ProductUnitId);

    var result = new
    {
        purchase.PurchaseId,
        purchase.PurchaseNumber,
        purchase.BusinessId,
        purchase.BranchId,
        purchase.SupplierId,

        Supplier = purchase.Supplier == null
            ? null
            : new
            {
                purchase.Supplier.SupplierId,
                purchase.Supplier.SupplierCode,
                purchase.Supplier.SupplierName,
                purchase.Supplier.Phone,
                purchase.Supplier.Email,
                purchase.Supplier.KRA_PIN,
                purchase.Supplier.Address
            },

        purchase.CreatedBy,
        purchase.PurchaseDate,
        purchase.SubTotal,
        purchase.TaxAmount,
        purchase.TotalAmount,
        purchase.Status,
        purchase.Notes,
        purchase.CreatedAt,

        Lines = purchase.PurchaseLines
            .Select(line =>
            {
                productUnits.TryGetValue(
                    line.ProductUnitId,
                    out var productUnit);

                return new
                {
                    line.PurchaseLineId,

                    line.ProductId,

                    ProductCode =
                        line.Product?.ProductCode,

                    ProductName =
                        line.Product?.ProductName,

                    line.ProductUnitId,

                    UnitCode =
                        productUnit?.UnitCode,

                    UnitName =
                        productUnit?.UnitName,

                    ConversionFactor =
                        productUnit?.ConversionFactor ?? 1,

                    line.Quantity,
                    line.UnitCost,
                    line.TaxAmount,
                    line.LineTotal
                };
            })
            .ToList()
    };

    return Ok(result);
}


    // ============================================================
    // GET ACTIVE SUPPLIERS
    // ============================================================

    [HttpGet("suppliers")]
    public async Task<IActionResult> GetSuppliers()
    {
        var suppliers = await _db.Suppliers
            .AsNoTracking()
            .Where(s =>
                s.BusinessId == 1 &&
                s.IsActive)
            .OrderBy(s => s.SupplierName)
            .Select(s => new
            {
                s.SupplierId,
                s.SupplierCode,
                s.SupplierName,
                s.Phone,
                s.Email,
                s.KRA_PIN,
                s.Address
            })
            .ToListAsync();

        return Ok(suppliers);
    }


    // ============================================================
    // GET PRODUCT UNITS
    // ============================================================

    [HttpGet("products/{productId:int}/units")]
    public async Task<IActionResult> GetProductUnits(int productId)
    {
        var product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p =>
                p.ProductId == productId &&
                p.BusinessId == 1);

        if (product == null)
        {
            return NotFound(new
            {
                message = "Product not found."
            });
        }

        var units = await
            (from pu in _db.ProductUnits
             join u in _db.UnitsOfMeasure
                 on pu.UnitId equals u.UnitId
             where pu.ProductId == productId
             select new
             {
                 pu.ProductUnitId,
                 pu.ProductId,
                 pu.UnitId,
                 u.UnitCode,
                 u.UnitName,
                 pu.ConversionFactor,
                 pu.IsBaseUnit,
                 pu.Barcode
             })
            .OrderByDescending(x => x.IsBaseUnit)
            .ThenBy(x => x.UnitName)
            .ToListAsync();

        return Ok(units);
    }


    // ============================================================
    // CREATE / RECEIVE PURCHASE
    // ============================================================

    [HttpPost]
    public async Task<IActionResult> CreatePurchase(
        [FromBody] CreatePurchaseRequest request)
    {
        if (request == null)
        {
            return BadRequest(new
            {
                message = "Purchase data is required."
            });
        }

        if (request.SupplierId <= 0)
        {
            return BadRequest(new
            {
                message = "A valid supplier is required."
            });
        }

        if (request.BranchId <= 0)
        {
            return BadRequest(new
            {
                message = "A valid branch is required."
            });
        }

        if (request.Lines == null || request.Lines.Count == 0)
        {
            return BadRequest(new
            {
                message = "At least one purchase line is required."
            });
        }

        // --------------------------------------------------------
        // Get current user
        // --------------------------------------------------------

        var userIdClaim =
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new
            {
                message = "Unable to determine the current user."
            });
        }


        // --------------------------------------------------------
        // Validate supplier
        // --------------------------------------------------------

        var supplier = await _db.Suppliers
            .FirstOrDefaultAsync(s =>
                s.SupplierId == request.SupplierId &&
                s.BusinessId == 1 &&
                s.IsActive);

        if (supplier == null)
        {
            return BadRequest(new
            {
                message = "Supplier not found or inactive."
            });
        }


        // --------------------------------------------------------
        // Validate all lines before starting transaction
        // --------------------------------------------------------

        var productIds = request.Lines
            .Select(x => x.ProductId)
            .Distinct()
            .ToList();

        var productUnitIds = request.Lines
            .Select(x => x.ProductUnitId)
            .Distinct()
            .ToList();

        var products = await _db.Products
            .Where(p =>
                p.BusinessId == 1 &&
                productIds.Contains(p.ProductId))
            .ToDictionaryAsync(p => p.ProductId);

        var productUnits = await
            (from pu in _db.ProductUnits
             join u in _db.UnitsOfMeasure
                 on pu.UnitId equals u.UnitId
             where productUnitIds.Contains(pu.ProductUnitId)
             select new ProductUnitInfo
             {
                 ProductUnitId = pu.ProductUnitId,
                 ProductId = pu.ProductId,
                 UnitId = pu.UnitId,
                 UnitName = u.UnitName,
                 ConversionFactor = pu.ConversionFactor,
                 IsBaseUnit = pu.IsBaseUnit
             })
            .ToDictionaryAsync(x => x.ProductUnitId);


        // --------------------------------------------------------
        // Validate each line
        // --------------------------------------------------------

        foreach (var line in request.Lines)
        {
            if (line.ProductId <= 0)
            {
                return BadRequest(new
                {
                    message = "Every purchase line must have a valid product."
                });
            }

            if (!products.ContainsKey(line.ProductId))
            {
                return BadRequest(new
                {
                    message =
                        $"Product {line.ProductId} was not found."
                });
            }

            if (line.ProductUnitId <= 0)
            {
                return BadRequest(new
                {
                    message =
                        $"Product '{products[line.ProductId].ProductName}' requires a valid unit."
                });
            }

            if (!productUnits.TryGetValue(
                    line.ProductUnitId,
                    out var productUnit))
            {
                return BadRequest(new
                {
                    message =
                        $"Product unit {line.ProductUnitId} was not found."
                });
            }

            if (productUnit.ProductId != line.ProductId)
            {
                return BadRequest(new
                {
                    message =
                        $"Unit '{productUnit.UnitName}' does not belong to product '{products[line.ProductId].ProductName}'."
                });
            }

            if (line.Quantity <= 0)
            {
                return BadRequest(new
                {
                    message =
                        $"Quantity for '{products[line.ProductId].ProductName}' must be greater than zero."
                });
            }

            if (line.UnitCost < 0)
            {
                return BadRequest(new
                {
                    message =
                        $"Unit cost for '{products[line.ProductId].ProductName}' cannot be negative."
                });
            }

            if (productUnit.ConversionFactor <= 0)
            {
                return BadRequest(new
                {
                    message =
                        $"Invalid conversion factor for '{productUnit.UnitName}'."
                });
            }
        }


        // --------------------------------------------------------
        // Calculate totals
        // --------------------------------------------------------

        decimal subTotal = 0;
        decimal taxTotal = 0;

        foreach (var line in request.Lines)
        {
            var lineSubTotal =
                line.Quantity * line.UnitCost;

            var lineTax =
                line.TaxAmount ?? 0;

            if (lineTax < 0)
            {
                return BadRequest(new
                {
                    message = "Tax amount cannot be negative."
                });
            }

            subTotal += lineSubTotal;
            taxTotal += lineTax;
        }

        var totalAmount = subTotal + taxTotal;


        // --------------------------------------------------------
        // Begin database transaction
        // --------------------------------------------------------

        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        try
        {
            // ----------------------------------------------------
            // Generate purchase number
            // ----------------------------------------------------

            var lastPurchaseId =
                await _db.Purchases
                    .MaxAsync(p => (long?)p.PurchaseId) ?? 0;

            var purchaseNumber =
                $"PUR-{(lastPurchaseId + 1):D6}";


            // ----------------------------------------------------
            // Create purchase header
            // ----------------------------------------------------

            var purchase = new Purchase
            {
                BusinessId = 1,
                BranchId = request.BranchId,
                SupplierId = request.SupplierId,
                CreatedBy = userId,

                PurchaseNumber = purchaseNumber,

                PurchaseDate =
                    request.PurchaseDate?.Date
                    ?? DateTime.Now.Date,

                SubTotal = subTotal,
                TaxAmount = taxTotal,
                TotalAmount = totalAmount,

                Status = "RECEIVED",

                Notes = request.Notes,

                CreatedAt = DateTime.Now
            };

            _db.Purchases.Add(purchase);

            await _db.SaveChangesAsync();


            // ----------------------------------------------------
            // Create purchase lines + stock transactions
            // ----------------------------------------------------

            foreach (var line in request.Lines)
            {
                var productUnit =
                    productUnits[line.ProductUnitId];

                var lineSubTotal =
                    line.Quantity * line.UnitCost;

                var lineTax =
                    line.TaxAmount ?? 0;

                var lineTotal =
                    lineSubTotal + lineTax;


                // ------------------------------------------------
                // Purchase line
                // ------------------------------------------------

                var purchaseLine = new PurchaseLine
                {
                    PurchaseId = purchase.PurchaseId,

                    ProductId = line.ProductId,

                    ProductUnitId =
                        line.ProductUnitId,

                    Quantity = line.Quantity,

                    UnitCost = line.UnitCost,

                    TaxAmount = lineTax,

                    LineTotal = lineTotal
                };

                _db.PurchaseLines.Add(purchaseLine);


                // ------------------------------------------------
                // Convert purchased quantity to base units
                // ------------------------------------------------

                var baseQuantity =
                    line.Quantity *
                    productUnit.ConversionFactor;


                // ------------------------------------------------
                // Convert unit cost to base-unit cost
                // ------------------------------------------------

                var baseUnitCost =
                    line.UnitCost /
                    productUnit.ConversionFactor;


                // ------------------------------------------------
                // Stock transaction
                //
                // Stock is stored in BASE units.
                // ------------------------------------------------

                var stockTransaction =
                    new StockTransaction
                    {
                        BusinessId = 1,

                        BranchId =
                            request.BranchId,

                        ProductId =
                            line.ProductId,

                        TransactionType =
                            "PURCHASE",

                        Quantity =
                            baseQuantity,

                        UnitCost =
                            baseUnitCost,

                        ReferenceType =
                            "PURCHASE",

                        ReferenceId =
                            purchase.PurchaseId,

                        TransactionDate =
                            DateTime.Now,

                        UserId =
                            userId,

                        Notes =
                            $"Purchase {purchase.PurchaseNumber} - {line.Quantity} {productUnit.UnitName}"
                    };

                _db.StockTransactions.Add(stockTransaction);
            }

            await _db.SaveChangesAsync();


            // ----------------------------------------------------
            // Commit transaction
            // ----------------------------------------------------

            await transaction.CommitAsync();


            // ----------------------------------------------------
            // Return result
            // ----------------------------------------------------

            return Ok(new
            {
                message =
                    "Purchase received successfully.",

                data = new
                {
                    purchaseId =
                        purchase.PurchaseId,

                    purchaseNumber =
                        purchase.PurchaseNumber,

                    supplierId =
                        purchase.SupplierId,

                    purchaseDate =
                        purchase.PurchaseDate,

                    subTotal =
                        purchase.SubTotal,

                    taxAmount =
                        purchase.TaxAmount,

                    totalAmount =
                        purchase.TotalAmount,

                    status =
                        purchase.Status,

                    lineCount =
                        request.Lines.Count
                }
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message =
                        "Unable to receive purchase.",

                    error =
                        ex.Message
                });
        }
    }
}


// ================================================================
// REQUEST MODELS
// ================================================================

public class CreatePurchaseRequest
{
    public int SupplierId { get; set; }

    public int BranchId { get; set; }

    public DateTime? PurchaseDate { get; set; }

    public string? Notes { get; set; }

    public List<CreatePurchaseLineRequest> Lines { get; set; }
        = new();
}


public class CreatePurchaseLineRequest
{
    public int ProductId { get; set; }

    public int ProductUnitId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitCost { get; set; }

    public decimal? TaxAmount { get; set; }
}


// ================================================================
// INTERNAL PRODUCT UNIT INFORMATION
// ================================================================

internal class ProductUnitInfo
{
    public int ProductUnitId { get; set; }

    public int ProductId { get; set; }

    public int UnitId { get; set; }

    public string UnitName { get; set; } = string.Empty;

    public decimal ConversionFactor { get; set; }

    public bool IsBaseUnit { get; set; }
}
