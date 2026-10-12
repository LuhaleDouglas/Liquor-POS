using LiquorPOS.Api.Data;
using LiquorPOS.Api.DTOs;
using LiquorPOS.Api.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LiquorPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly LiquorPosDbContext _db;

    public ProductsController(LiquorPosDbContext db)
    {
        _db = db;
    }

    // ============================================================
    // GET ALL PRODUCTS
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> GetProducts()
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
                p.CategoryId
            })
            .ToListAsync();

        return Ok(products);
    }


    // ============================================================
    // GET PRODUCT BY ID
    // ============================================================

    [HttpGet("{productId:int}")]
    public async Task<IActionResult> GetProduct(int productId)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p =>
                p.BusinessId == 1 &&
                p.ProductId == productId);

        if (product == null)
        {
            return NotFound(new
            {
                message = "Product not found."
            });
        }

        // --------------------------------------------------------
        // Get product units
        // --------------------------------------------------------

        var units = await
            (from pu in _db.ProductUnits
             join u in _db.UnitsOfMeasure
                 on pu.UnitId equals u.UnitId
             where pu.ProductId == product.ProductId
             select new
             {
                 pu.ProductUnitId,
                 u.UnitCode,
                 u.UnitName,
                 pu.ConversionFactor,
                 pu.Barcode,
                 pu.IsBaseUnit
             })
            .ToListAsync();

        // --------------------------------------------------------
        // Get active prices
        // --------------------------------------------------------

        var prices = await _db.ProductPrices
            .Where(pp =>
                pp.ProductId == product.ProductId &&
                pp.IsActive)
            .ToListAsync();

        // --------------------------------------------------------
        // Calculate current stock
        // --------------------------------------------------------

        var stock = await _db.StockTransactions
            .Where(st =>
                st.BusinessId == 1 &&
                st.BranchId == 1 &&
                st.ProductId == product.ProductId)
            .SumAsync(st => (decimal?)st.Quantity) ?? 0;

        // --------------------------------------------------------
        // Build response
        // --------------------------------------------------------

        var result = new ProductDetailsDto
        {
            ProductId = product.ProductId,
            ProductCode = product.ProductCode,
            ProductName = product.ProductName,
            Barcode = product.Barcode,
            Brand = product.Brand,

            // 0 = No Category assigned
            CategoryId = product.CategoryId,

            Stock = stock,

            Units = units.Select(unit =>
            {
                var retailPrice = prices
                    .Where(p =>
                        p.ProductUnitId == unit.ProductUnitId &&
                        p.PriceGroupId == 1)
                    .Select(p => p.UnitPrice)
                    .FirstOrDefault();

                var wholesalePrice = prices
                    .Where(p =>
                        p.ProductUnitId == unit.ProductUnitId &&
                        p.PriceGroupId == 2)
                    .Select(p => p.UnitPrice)
                    .FirstOrDefault();

                return new ProductUnitDto
                {
                    ProductUnitId = unit.ProductUnitId,
                    UnitCode = unit.UnitCode,
                    UnitName = unit.UnitName,
                    ConversionFactor = unit.ConversionFactor,
                    IsBaseUnit = unit.IsBaseUnit,

                    RetailPrice = retailPrice,
                    WholesalePrice = wholesalePrice
                };
            }).ToList()
        };

        return Ok(result);
    }


    // ============================================================
    // GET PRODUCT BY BARCODE
    // ============================================================

    [HttpGet("barcode/{barcode}")]
    public async Task<IActionResult> GetByBarcode(string barcode)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p =>
                p.BusinessId == 1 &&
                p.Barcode == barcode);

        // --------------------------------------------------------
        // If product barcode was not found,
        // check ProductUnits barcode
        // --------------------------------------------------------

        if (product == null)
        {
            product =
                await
                (from pu in _db.ProductUnits
                 join p in _db.Products
                     on pu.ProductId equals p.ProductId
                 where p.BusinessId == 1 &&
                       pu.Barcode == barcode
                 select p)
                .FirstOrDefaultAsync();
        }

        if (product == null)
        {
            return NotFound(new
            {
                message = "No product found for this barcode."
            });
        }

        // --------------------------------------------------------
        // Get units
        // --------------------------------------------------------

        var units =
            await
            (from pu in _db.ProductUnits
             join u in _db.UnitsOfMeasure
                 on pu.UnitId equals u.UnitId
             where pu.ProductId == product.ProductId
             select new
             {
                 pu.ProductUnitId,
                 u.UnitCode,
                 u.UnitName,
                 pu.ConversionFactor,
                 pu.Barcode,
                 pu.IsBaseUnit
             })
            .ToListAsync();

        // --------------------------------------------------------
        // Get prices
        // --------------------------------------------------------

        var prices =
            await _db.ProductPrices
                .Where(pp =>
                    pp.ProductId == product.ProductId &&
                    pp.IsActive)
                .ToListAsync();

        // --------------------------------------------------------
        // Get stock
        // --------------------------------------------------------

        var stock =
            await _db.StockTransactions
                .Where(st =>
                    st.BusinessId == 1 &&
                    st.BranchId == 1 &&
                    st.ProductId == product.ProductId)
                .SumAsync(st => (decimal?)st.Quantity) ?? 0;

        // --------------------------------------------------------
        // Build response
        // --------------------------------------------------------

        var result = new ProductDetailsDto
        {
            ProductId = product.ProductId,
            ProductCode = product.ProductCode,
            ProductName = product.ProductName,
            Barcode = product.Barcode,
            Brand = product.Brand,

            // 0 = No Category assigned
            CategoryId = product.CategoryId,

            Stock = stock,

            Units = units.Select(unit =>
            {
                var retailPrice = prices
                    .Where(p =>
                        p.ProductUnitId == unit.ProductUnitId &&
                        p.PriceGroupId == 1)
                    .Select(p => p.UnitPrice)
                    .FirstOrDefault();

                var wholesalePrice = prices
                    .Where(p =>
                        p.ProductUnitId == unit.ProductUnitId &&
                        p.PriceGroupId == 2)
                    .Select(p => p.UnitPrice)
                    .FirstOrDefault();

                return new ProductUnitDto
                {
                    ProductUnitId = unit.ProductUnitId,
                    UnitCode = unit.UnitCode,
                    UnitName = unit.UnitName,
                    ConversionFactor = unit.ConversionFactor,
                    IsBaseUnit = unit.IsBaseUnit,

                    RetailPrice = retailPrice,
                    WholesalePrice = wholesalePrice
                };
            }).ToList()
        };

        return Ok(result);
    }


    // ============================================================
    // GET UNITS OF MEASURE
    // ============================================================

    [HttpGet("units")]
    public async Task<IActionResult> GetUnits()
    {
        var units = await _db.UnitsOfMeasure
            .OrderBy(u => u.UnitName)
            .Select(u => new
            {
                unitId = u.UnitId,
                unitCode = u.UnitCode,
                unitName = u.UnitName
            })
            .ToListAsync();

        return Ok(units);
    }


    // ============================================================
    // CREATE PRODUCT
    // ============================================================

    [HttpPost]
    public async Task<IActionResult> CreateProduct(
        [FromBody] CreateProductRequest request)
    {
        if (request == null)
        {
            return BadRequest(new
            {
                message = "Product data is required."
            });
        }

        // --------------------------------------------------------
        // Validate required product fields
        // --------------------------------------------------------

        if (string.IsNullOrWhiteSpace(request.ProductCode))
        {
            return BadRequest(new
            {
                message = "Product code is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.ProductName))
        {
            return BadRequest(new
            {
                message = "Product name is required."
            });
        }

        if (request.Units == null || request.Units.Count == 0)
        {
            return BadRequest(new
            {
                message = "At least one product unit is required."
            });
        }

        // --------------------------------------------------------
        // Check duplicate product code
        // --------------------------------------------------------

        var existingCode = await _db.Products
            .AnyAsync(p =>
                p.BusinessId == 1 &&
                p.ProductCode == request.ProductCode);

        if (existingCode)
        {
            return Conflict(new
            {
                message = "A product with this product code already exists."
            });
        }

        // --------------------------------------------------------
        // Check duplicate product barcode
        // --------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(request.Barcode))
        {
            var existingBarcode = await _db.Products
                .AnyAsync(p =>
                    p.BusinessId == 1 &&
                    p.Barcode == request.Barcode);

            if (existingBarcode)
            {
                return Conflict(new
                {
                    message = "A product with this barcode already exists."
                });
            }
        }

        // --------------------------------------------------------
        // Create product
        // --------------------------------------------------------

        var product = new Product
        {
            BusinessId = 1,

            // 0 = No Category assigned yet
            CategoryId = request.CategoryId,

            ProductCode = request.ProductCode.Trim(),

            ProductName = request.ProductName.Trim(),

            Barcode = string.IsNullOrWhiteSpace(request.Barcode)
                ? null
                : request.Barcode.Trim(),

            Brand = string.IsNullOrWhiteSpace(request.Brand)
                ? null
                : request.Brand.Trim(),

            Description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim()
        };

        _db.Products.Add(product);

        await _db.SaveChangesAsync();

        // --------------------------------------------------------
        // Add product units and prices
        // --------------------------------------------------------

        foreach (var unitRequest in request.Units)
        {
            var unitExists = await _db.UnitsOfMeasure
                .AnyAsync(u =>
                    u.UnitId == unitRequest.UnitId);

            if (!unitExists)
            {
                return BadRequest(new
                {
                    message =
                        $"Unit ID {unitRequest.UnitId} does not exist."
                });
            }

            if (unitRequest.ConversionFactor <= 0)
            {
                return BadRequest(new
                {
                    message =
                        "Conversion factor must be greater than zero."
                });
            }

            var productUnit = new ProductUnit
            {
                ProductId = product.ProductId,

                UnitId = unitRequest.UnitId,

                ConversionFactor = unitRequest.ConversionFactor,

                Barcode = string.IsNullOrWhiteSpace(unitRequest.Barcode)
                    ? null
                    : unitRequest.Barcode.Trim(),

                IsBaseUnit = unitRequest.IsBaseUnit
            };

            _db.ProductUnits.Add(productUnit);

            await _db.SaveChangesAsync();

            // ----------------------------------------------------
            // Retail price
            // ----------------------------------------------------

            if (unitRequest.RetailPrice.HasValue)
            {
                _db.ProductPrices.Add(new ProductPrice
                {
                    ProductId = product.ProductId,

                    ProductUnitId = productUnit.ProductUnitId,

                    PriceGroupId = 1,

                    UnitPrice = unitRequest.RetailPrice.Value,

                    StartDate = DateTime.Now,

                    IsActive = true
                });
            }

            // ----------------------------------------------------
            // Wholesale price
            // ----------------------------------------------------

            if (unitRequest.WholesalePrice.HasValue)
            {
                _db.ProductPrices.Add(new ProductPrice
                {
                    ProductId = product.ProductId,

                    ProductUnitId = productUnit.ProductUnitId,

                    PriceGroupId = 2,

                    UnitPrice = unitRequest.WholesalePrice.Value,

                    StartDate = DateTime.Now,

                    IsActive = true
                });
            }
        }

        await _db.SaveChangesAsync();

        // --------------------------------------------------------
        // Return success
        // --------------------------------------------------------

        return CreatedAtAction(
            nameof(GetProduct),
            new { productId = product.ProductId },
            new
            {
                message = "Product created successfully.",
                productId = product.ProductId
            });
    }


    // ============================================================
    // UPDATE PRODUCT
    // ============================================================

   [HttpPut("{productId:int}")]
public async Task<IActionResult> UpdateProduct(
    int productId,
    [FromBody] UpdateProductRequest request)
{
    if (request == null)
    {
        return BadRequest(new
        {
            message = "Product data is required."
        });
    }

    // --------------------------------------------------------
    // Validate product
    // --------------------------------------------------------

    if (string.IsNullOrWhiteSpace(request.ProductCode))
    {
        return BadRequest(new
        {
            message = "Product code is required."
        });
    }

    if (string.IsNullOrWhiteSpace(request.ProductName))
    {
        return BadRequest(new
        {
            message = "Product name is required."
        });
    }

    if (request.Units == null || request.Units.Count == 0)
    {
        return BadRequest(new
        {
            message = "At least one product unit is required."
        });
    }

    // --------------------------------------------------------
    // Find product
    // --------------------------------------------------------

    var product = await _db.Products
        .FirstOrDefaultAsync(p =>
            p.BusinessId == 1 &&
            p.ProductId == productId);

    if (product == null)
    {
        return NotFound(new
        {
            message = "Product not found."
        });
    }

    // --------------------------------------------------------
    // Check duplicate product code
    // --------------------------------------------------------

    var duplicateCode = await _db.Products
        .AnyAsync(p =>
            p.BusinessId == 1 &&
            p.ProductId != productId &&
            p.ProductCode == request.ProductCode.Trim());

    if (duplicateCode)
    {
        return Conflict(new
        {
            message =
                "Another product already uses this product code."
        });
    }

    // --------------------------------------------------------
    // Check duplicate product barcode
    // --------------------------------------------------------

    if (!string.IsNullOrWhiteSpace(request.Barcode))
    {
        var duplicateBarcode = await _db.Products
            .AnyAsync(p =>
                p.BusinessId == 1 &&
                p.ProductId != productId &&
                p.Barcode == request.Barcode.Trim());

        if (duplicateBarcode)
        {
            return Conflict(new
            {
                message =
                    "Another product already uses this barcode."
            });
        }
    }

    // --------------------------------------------------------
    // Validate units
    // --------------------------------------------------------

    var baseUnitCount = request.Units.Count(u => u.IsBaseUnit);

    if (baseUnitCount != 1)
    {
        return BadRequest(new
        {
            message =
                "A product must have exactly one base unit."
        });
    }

    foreach (var unitRequest in request.Units)
    {
        if (unitRequest.UnitId <= 0)
        {
            return BadRequest(new
            {
                message = "A valid unit is required."
            });
        }

        if (unitRequest.ConversionFactor <= 0)
        {
            return BadRequest(new
            {
                message =
                    "Conversion factor must be greater than zero."
            });
        }

        var unitExists = await _db.UnitsOfMeasure
            .AnyAsync(u =>
                u.UnitId == unitRequest.UnitId);

        if (!unitExists)
        {
            return BadRequest(new
            {
                message =
                    $"Unit ID {unitRequest.UnitId} does not exist."
            });
        }
    }

    // --------------------------------------------------------
    // Update product header
    // --------------------------------------------------------

    product.ProductCode = request.ProductCode.Trim();

    product.ProductName = request.ProductName.Trim();

    product.Barcode =
        string.IsNullOrWhiteSpace(request.Barcode)
            ? null
            : request.Barcode.Trim();

    product.Brand =
        string.IsNullOrWhiteSpace(request.Brand)
            ? null
            : request.Brand.Trim();

    product.Description =
        string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();

    // 0 = No Category assigned
    product.CategoryId = request.CategoryId;

    // --------------------------------------------------------
    // Get existing product units
    // --------------------------------------------------------

    var existingUnits = await _db.ProductUnits
        .Where(pu => pu.ProductId == productId)
        .ToListAsync();

    var requestedUnitIds = request.Units
        .Where(u => u.ProductUnitId > 0)
        .Select(u => u.ProductUnitId)
        .ToHashSet();

    // --------------------------------------------------------
    // Delete units that were removed from the product
    // --------------------------------------------------------

    var unitsToDelete = existingUnits
        .Where(eu => !requestedUnitIds.Contains(eu.ProductUnitId))
        .ToList();

    foreach (var unitToDelete in unitsToDelete)
    {
        // Check if this unit has price records
        var pricesToDelete = await _db.ProductPrices
            .Where(pp =>
                pp.ProductUnitId == unitToDelete.ProductUnitId)
            .ToListAsync();

        if (pricesToDelete.Count > 0)
        {
            _db.ProductPrices.RemoveRange(pricesToDelete);
        }

        _db.ProductUnits.Remove(unitToDelete);
    }

    // --------------------------------------------------------
    // Update / Add units
    // --------------------------------------------------------

    foreach (var unitRequest in request.Units)
    {
        ProductUnit? productUnit = null;

        if (unitRequest.ProductUnitId > 0)
        {
            productUnit = existingUnits
                .FirstOrDefault(eu =>
                    eu.ProductUnitId ==
                    unitRequest.ProductUnitId);

            if (productUnit == null)
            {
                return BadRequest(new
                {
                    message =
                        $"Product unit ID {unitRequest.ProductUnitId} does not belong to this product."
                });
            }
        }

        // ----------------------------------------------------
        // New product unit
        // ----------------------------------------------------

        if (productUnit == null)
        {
            productUnit = new ProductUnit
            {
                ProductId = productId
            };

            _db.ProductUnits.Add(productUnit);
        }

        // ----------------------------------------------------
        // Update unit
        // ----------------------------------------------------

        productUnit.UnitId = unitRequest.UnitId;

        productUnit.ConversionFactor =
            unitRequest.ConversionFactor;

        productUnit.Barcode =
            string.IsNullOrWhiteSpace(unitRequest.Barcode)
                ? null
                : unitRequest.Barcode.Trim();

        productUnit.IsBaseUnit =
            unitRequest.IsBaseUnit;

        await _db.SaveChangesAsync();

        // ----------------------------------------------------
        // Update Retail Price
        // ----------------------------------------------------

        var retailPrice = await _db.ProductPrices
            .FirstOrDefaultAsync(pp =>
                pp.ProductUnitId ==
                    productUnit.ProductUnitId &&
                pp.PriceGroupId == 1 &&
                pp.IsActive);

        if (unitRequest.RetailPrice.HasValue)
        {
            if (retailPrice == null)
            {
                _db.ProductPrices.Add(new ProductPrice
                {
                    ProductId = productId,
                    ProductUnitId =
                        productUnit.ProductUnitId,
                    PriceGroupId = 1,
                    UnitPrice =
                        unitRequest.RetailPrice.Value,
                    StartDate = DateTime.Now,
                    IsActive = true
                });
            }
            else
            {
                retailPrice.UnitPrice =
                    unitRequest.RetailPrice.Value;
            }
        }
        else if (retailPrice != null)
        {
            retailPrice.IsActive = false;
        }

        // ----------------------------------------------------
        // Update Wholesale Price
        // ----------------------------------------------------

        var wholesalePrice = await _db.ProductPrices
            .FirstOrDefaultAsync(pp =>
                pp.ProductUnitId ==
                    productUnit.ProductUnitId &&
                pp.PriceGroupId == 2 &&
                pp.IsActive);

        if (unitRequest.WholesalePrice.HasValue)
        {
            if (wholesalePrice == null)
            {
                _db.ProductPrices.Add(new ProductPrice
                {
                    ProductId = productId,
                    ProductUnitId =
                        productUnit.ProductUnitId,
                    PriceGroupId = 2,
                    UnitPrice =
                        unitRequest.WholesalePrice.Value,
                    StartDate = DateTime.Now,
                    IsActive = true
                });
            }
            else
            {
                wholesalePrice.UnitPrice =
                    unitRequest.WholesalePrice.Value;
            }
        }
        else if (wholesalePrice != null)
        {
            wholesalePrice.IsActive = false;
        }
    }

    // --------------------------------------------------------
    // Save everything
    // --------------------------------------------------------

    await _db.SaveChangesAsync();

    return Ok(new
    {
        message = "Product updated successfully.",
        productId = product.ProductId
    });
}
    // ============================================================
// DELETE PRODUCT
// ============================================================

[HttpDelete("{productId:int}")]
public async Task<IActionResult> DeleteProduct(int productId)
{
    var product = await _db.Products
        .FirstOrDefaultAsync(p =>
            p.BusinessId == 1 &&
            p.ProductId == productId);

    if (product == null)
    {
        return NotFound(new
        {
            message = "Product not found."
        });
    }

    // --------------------------------------------------------
    // Check whether the product has stock transaction history
    // --------------------------------------------------------

    var hasStockTransactions = await _db.StockTransactions
        .AnyAsync(st =>
            st.BusinessId == 1 &&
            st.ProductId == productId);

    if (hasStockTransactions)
    {
        return BadRequest(new
        {
            message =
                "This product cannot be deleted because it has stock transaction history."
        });
    }

    // --------------------------------------------------------
    // Check whether the product has sales
    // --------------------------------------------------------

    var hasSales = await _db.SaleLines
        .AnyAsync(sl =>
            sl.ProductId == productId);

    if (hasSales)
    {
        return BadRequest(new
        {
            message =
                "This product cannot be deleted because it has sales history."
        });
    }

    // --------------------------------------------------------
    // Get product units
    // --------------------------------------------------------

    var productUnits = await _db.ProductUnits
        .Where(pu => pu.ProductId == productId)
        .ToListAsync();

    // --------------------------------------------------------
    // Get product prices
    // --------------------------------------------------------

    var productPrices = await _db.ProductPrices
        .Where(pp => pp.ProductId == productId)
        .ToListAsync();

    // --------------------------------------------------------
    // Delete prices
    // --------------------------------------------------------

    if (productPrices.Count > 0)
    {
        _db.ProductPrices.RemoveRange(productPrices);
    }

    // --------------------------------------------------------
    // Delete product units
    // --------------------------------------------------------

    if (productUnits.Count > 0)
    {
        _db.ProductUnits.RemoveRange(productUnits);
    }

    // --------------------------------------------------------
    // Delete product
    // --------------------------------------------------------

    _db.Products.Remove(product);

    await _db.SaveChangesAsync();

    return Ok(new
    {
        message = "Product deleted successfully.",
        productId = productId
    });
}
}