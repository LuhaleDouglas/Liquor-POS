using LiquorPOS.Api.Data;
using LiquorPOS.Api.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LiquorPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SuppliersController : ControllerBase
{
    private readonly LiquorPosDbContext _db;

    public SuppliersController(LiquorPosDbContext db)
    {
        _db = db;
    }


    // ============================================================
    // GET ALL SUPPLIERS
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> GetSuppliers()
    {
        var suppliers = await _db.Suppliers
            .AsNoTracking()
            .Where(s => s.BusinessId == 1)
            .OrderBy(s => s.SupplierName)
            .Select(s => new
            {
                s.SupplierId,
                s.BusinessId,
                s.SupplierCode,
                s.SupplierName,
                s.Phone,
                s.Email,
                s.KRA_PIN,
                s.Address,
                s.IsActive,

                PurchaseCount = s.Purchases.Count()
            })
            .ToListAsync();

        return Ok(suppliers);
    }


    // ============================================================
    // GET SUPPLIER BY ID
    // ============================================================

    [HttpGet("{supplierId:int}")]
    public async Task<IActionResult> GetSupplier(int supplierId)
    {
        var supplier = await _db.Suppliers
            .AsNoTracking()
            .Where(s =>
                s.SupplierId == supplierId &&
                s.BusinessId == 1)
            .Select(s => new
            {
                s.SupplierId,
                s.BusinessId,
                s.SupplierCode,
                s.SupplierName,
                s.Phone,
                s.Email,
                s.KRA_PIN,
                s.Address,
                s.IsActive,

                PurchaseCount = s.Purchases.Count(),

                Purchases = s.Purchases
                    .OrderByDescending(p => p.PurchaseDate)
                    .ThenByDescending(p => p.PurchaseId)
                    .Select(p => new
                    {
                        p.PurchaseId,
                        p.PurchaseNumber,
                        p.PurchaseDate,
                        p.TotalAmount,
                        p.Status
                    })
                    .Take(20)
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (supplier == null)
        {
            return NotFound(new
            {
                message = "Supplier not found."
            });
        }

        return Ok(supplier);
    }


    // ============================================================
    // CREATE SUPPLIER
    // ============================================================

    [HttpPost]
    public async Task<IActionResult> CreateSupplier(
        [FromBody] CreateSupplierRequest request)
    {
        if (request == null)
        {
            return BadRequest(new
            {
                message = "Supplier data is required."
            });
        }

        var supplierCode =
            request.SupplierCode?.Trim() ?? string.Empty;

        var supplierName =
            request.SupplierName?.Trim() ?? string.Empty;


        // --------------------------------------------------------
        // Validation
        // --------------------------------------------------------

        if (string.IsNullOrWhiteSpace(supplierCode))
        {
            return BadRequest(new
            {
                message = "Supplier code is required."
            });
        }

        if (string.IsNullOrWhiteSpace(supplierName))
        {
            return BadRequest(new
            {
                message = "Supplier name is required."
            });
        }


        // --------------------------------------------------------
        // Check duplicate supplier code
        // --------------------------------------------------------

        var duplicateCode = await _db.Suppliers
            .AnyAsync(s =>
                s.BusinessId == 1 &&
                s.SupplierCode == supplierCode);

        if (duplicateCode)
        {
            return Conflict(new
            {
                message =
                    $"Supplier code '{supplierCode}' already exists."
            });
        }


        // --------------------------------------------------------
        // Create supplier
        // --------------------------------------------------------

        var supplier = new Supplier
        {
            BusinessId = 1,

            SupplierCode = supplierCode,

            SupplierName = supplierName,

            Phone = CleanNullable(request.Phone),

            Email = CleanNullable(request.Email),

            KRA_PIN = CleanNullable(request.KRA_PIN),

            Address = CleanNullable(request.Address),

            IsActive = request.IsActive
        };

        _db.Suppliers.Add(supplier);

        await _db.SaveChangesAsync();


        // --------------------------------------------------------
        // Return created supplier
        // --------------------------------------------------------

        return Ok(new
        {
            message = "Supplier created successfully.",

            data = new
            {
                supplier.SupplierId,
                supplier.BusinessId,
                supplier.SupplierCode,
                supplier.SupplierName,
                supplier.Phone,
                supplier.Email,
                supplier.KRA_PIN,
                supplier.Address,
                supplier.IsActive
            }
        });
    }


    // ============================================================
    // UPDATE SUPPLIER
    // ============================================================

    [HttpPut("{supplierId:int}")]
    public async Task<IActionResult> UpdateSupplier(
        int supplierId,
        [FromBody] UpdateSupplierRequest request)
    {
        if (request == null)
        {
            return BadRequest(new
            {
                message = "Supplier data is required."
            });
        }

        var supplier = await _db.Suppliers
            .FirstOrDefaultAsync(s =>
                s.SupplierId == supplierId &&
                s.BusinessId == 1);

        if (supplier == null)
        {
            return NotFound(new
            {
                message = "Supplier not found."
            });
        }


        // --------------------------------------------------------
        // Validation
        // --------------------------------------------------------

        var supplierCode =
            request.SupplierCode?.Trim() ?? string.Empty;

        var supplierName =
            request.SupplierName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(supplierCode))
        {
            return BadRequest(new
            {
                message = "Supplier code is required."
            });
        }

        if (string.IsNullOrWhiteSpace(supplierName))
        {
            return BadRequest(new
            {
                message = "Supplier name is required."
            });
        }


        // --------------------------------------------------------
        // Check duplicate supplier code
        // --------------------------------------------------------

        var duplicateCode = await _db.Suppliers
            .AnyAsync(s =>
                s.BusinessId == 1 &&
                s.SupplierId != supplierId &&
                s.SupplierCode == supplierCode);

        if (duplicateCode)
        {
            return Conflict(new
            {
                message =
                    $"Supplier code '{supplierCode}' already exists."
            });
        }


        // --------------------------------------------------------
        // Update supplier
        // --------------------------------------------------------

        supplier.SupplierCode = supplierCode;

        supplier.SupplierName = supplierName;

        supplier.Phone =
            CleanNullable(request.Phone);

        supplier.Email =
            CleanNullable(request.Email);

        supplier.KRA_PIN =
            CleanNullable(request.KRA_PIN);

        supplier.Address =
            CleanNullable(request.Address);

        supplier.IsActive =
            request.IsActive;

        await _db.SaveChangesAsync();


        return Ok(new
        {
            message = "Supplier updated successfully.",

            data = new
            {
                supplier.SupplierId,
                supplier.BusinessId,
                supplier.SupplierCode,
                supplier.SupplierName,
                supplier.Phone,
                supplier.Email,
                supplier.KRA_PIN,
                supplier.Address,
                supplier.IsActive
            }
        });
    }


    // ============================================================
    // DEACTIVATE SUPPLIER
    // ============================================================

    [HttpDelete("{supplierId:int}")]
    public async Task<IActionResult> DeactivateSupplier(
        int supplierId)
    {
        var supplier = await _db.Suppliers
            .FirstOrDefaultAsync(s =>
                s.SupplierId == supplierId &&
                s.BusinessId == 1);

        if (supplier == null)
        {
            return NotFound(new
            {
                message = "Supplier not found."
            });
        }

        if (!supplier.IsActive)
        {
            return BadRequest(new
            {
                message = "Supplier is already inactive."
            });
        }


        // --------------------------------------------------------
        // Do NOT physically delete the supplier.
        //
        // Existing purchases must continue to reference
        // this supplier.
        // --------------------------------------------------------

        supplier.IsActive = false;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Supplier deactivated successfully.",

            data = new
            {
                supplier.SupplierId,
                supplier.SupplierCode,
                supplier.SupplierName,
                supplier.IsActive
            }
        });
    }


    // ============================================================
    // ACTIVATE SUPPLIER
    // ============================================================

    [HttpPatch("{supplierId:int}/activate")]
    public async Task<IActionResult> ActivateSupplier(
        int supplierId)
    {
        var supplier = await _db.Suppliers
            .FirstOrDefaultAsync(s =>
                s.SupplierId == supplierId &&
                s.BusinessId == 1);

        if (supplier == null)
        {
            return NotFound(new
            {
                message = "Supplier not found."
            });
        }

        if (supplier.IsActive)
        {
            return BadRequest(new
            {
                message = "Supplier is already active."
            });
        }

        supplier.IsActive = true;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Supplier activated successfully.",

            data = new
            {
                supplier.SupplierId,
                supplier.SupplierCode,
                supplier.SupplierName,
                supplier.IsActive
            }
        });
    }


    // ============================================================
    // HELPER
    // ============================================================

    private static string? CleanNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}


// ================================================================
// REQUEST MODELS
// ================================================================

public class CreateSupplierRequest
{
    public string SupplierCode { get; set; } = string.Empty;

    public string SupplierName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? KRA_PIN { get; set; }

    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;
}


public class UpdateSupplierRequest
{
    public string SupplierCode { get; set; } = string.Empty;

    public string SupplierName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? KRA_PIN { get; set; }

    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;
}

