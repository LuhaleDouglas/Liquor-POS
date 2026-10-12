using System.Security.Claims;

using LiquorPOS.Api.Services;
using LiquorPOS.Api.Data;
using LiquorPOS.Api.DTOs;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LiquorPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly LiquorPosDbContext _db;
    private readonly PasswordHasher<Models.User> _passwordHasher;

    public UsersController(LiquorPosDbContext db)
    {
        _db = db;
        _passwordHasher = new PasswordHasher<Models.User>();
    }

    // ============================================================
    // GET: api/Users
    // ADMIN and OWNER only
    // ============================================================

    [HttpGet]
    [Authorize(Roles = "ADMIN,OWNER")]
    public async Task<IActionResult> GetUsers()
    {
        var businessIdClaim =
            User.FindFirst("BusinessId")?.Value;

        if (!int.TryParse(
                businessIdClaim,
                out int businessId))
        {
            return Unauthorized(new
            {
                message =
                    "Business information could not be determined."
            });
        }

        var users = await _db.Users
            .AsNoTracking()
            .Where(u => u.BusinessId == businessId)
            .Select(u => new
            {
                u.UserId,
                u.Username,
                u.FullName,
                u.BusinessId,
                u.BranchId,
                u.RoleId,

                RoleName = _db.Roles
                    .Where(r => r.RoleId == u.RoleId)
                    .Select(r => r.RoleName)
                    .FirstOrDefault(),

                u.IsActive,
                u.CreatedAt
            })
            .OrderBy(u => u.FullName)
            .ToListAsync();

        return Ok(new
        {
            message = "Users retrieved successfully.",
            data = users
        });
    }


    // ============================================================
    // GET: api/Users/{id}
    // ADMIN and OWNER only
    // ============================================================

    [HttpGet("{id:int}")]
    [Authorize(Roles = "ADMIN,OWNER")]
    public async Task<IActionResult> GetUser(int id)
    {
        var businessId = GetBusinessId();

        if (businessId == null)
            return Unauthorized(new
            {
                message =
                    "Business information could not be determined."
            });

        var user = await _db.Users
            .AsNoTracking()
            .Where(u =>
                u.UserId == id &&
                u.BusinessId == businessId.Value)
            .Select(u => new
            {
                u.UserId,
                u.Username,
                u.FullName,
                u.BusinessId,
                u.BranchId,
                u.RoleId,

                RoleName = _db.Roles
                    .Where(r => r.RoleId == u.RoleId)
                    .Select(r => r.RoleName)
                    .FirstOrDefault(),

                u.IsActive,
                u.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return NotFound(new
            {
                message = "User not found."
            });
        }

        return Ok(new
        {
            message = "User retrieved successfully.",
            data = user
        });
    }


    // ============================================================
    // GET: api/Users/roles
    // ADMIN and OWNER only
    // ============================================================

    [HttpGet("roles")]
    [Authorize(Roles = "ADMIN,OWNER")]
    public async Task<IActionResult> GetRoles()
    {
        var roles = await _db.Roles
            .AsNoTracking()
            .OrderBy(r => r.RoleName)
            .Select(r => new
            {
                roleId = r.RoleId,
                roleName = r.RoleName,
                description = r.Description
            })
            .ToListAsync();

        return Ok(new
        {
            message = "Roles retrieved successfully.",
            data = roles
        });
    }


    // ============================================================
    // POST: api/Users
    // ADMIN and OWNER only
    // ============================================================

    [HttpPost]
    [Authorize(Roles = "ADMIN,OWNER")]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateUserRequest request)
    {
        // ----------------------------------------------
        // Validate input
        // ----------------------------------------------

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return BadRequest(new
            {
                message = "Username is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new
            {
                message = "Password is required."
            });
        }

        if (request.Password.Length < 6)
        {
            return BadRequest(new
            {
                message =
                    "Password must be at least 6 characters long."
            });
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest(new
            {
                message = "Full name is required."
            });
        }

        if (request.RoleId <= 0)
        {
            return BadRequest(new
            {
                message = "Role is required."
            });
        }


        // ----------------------------------------------
        // Get BusinessId
        // ----------------------------------------------

        var businessId = GetBusinessId();

        if (businessId == null)
        {
            return Unauthorized(new
            {
                message =
                    "Business information could not be determined."
            });
        }


        // ----------------------------------------------
        // Check username
        // ----------------------------------------------

        var username = request.Username.Trim();

        var existingUser = await _db.Users
            .FirstOrDefaultAsync(u =>
                u.Username == username &&
                u.BusinessId == businessId.Value);

        if (existingUser != null)
        {
            return Conflict(new
            {
                message = "Username already exists."
            });
        }


        // ----------------------------------------------
        // Check role
        // ----------------------------------------------

        var role = await _db.Roles
            .FirstOrDefaultAsync(r =>
                r.RoleId == request.RoleId);

        if (role == null)
        {
            return BadRequest(new
            {
                message = "Invalid role."
            });
        }


        // ----------------------------------------------
        // Validate branch
        // ----------------------------------------------

        if (request.BranchId.HasValue &&
            request.BranchId.Value <= 0)
        {
            return BadRequest(new
            {
                message = "Invalid branch."
            });
        }


        // ----------------------------------------------
        // Create user
        // ----------------------------------------------

        var user = new Models.User
        {
            BusinessId = businessId.Value,
            BranchId = request.BranchId,
            RoleId = request.RoleId,
            Username = username,
            FullName = request.FullName.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };


        // ----------------------------------------------
        // Hash password
        // ----------------------------------------------

        user.PasswordHash =
            _passwordHasher.HashPassword(
                user,
                request.Password);


        // ----------------------------------------------
        // Save
        // ----------------------------------------------

        _db.Users.Add(user);

        await _db.SaveChangesAsync();


        // ----------------------------------------------
        // Return created user
        // ----------------------------------------------

        return Ok(new
        {
            message = "User created successfully.",

            user = new
            {
                user.UserId,
                user.Username,
                user.FullName,
                user.BusinessId,
                user.BranchId,
                user.RoleId,
                roleName = role.RoleName,
                user.IsActive,
                user.CreatedAt
            }
        });
    }


    // ============================================================
    // PUT: api/Users/{id}
    // ADMIN and OWNER only
    // ============================================================

    [HttpPut("{id:int}")]
    [Authorize(Roles = "ADMIN,OWNER")]
    public async Task<IActionResult> UpdateUser(
        int id,
        [FromBody] UpdateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return BadRequest(new
            {
                message = "Username is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest(new
            {
                message = "Full name is required."
            });
        }

        if (request.RoleId <= 0)
        {
            return BadRequest(new
            {
                message = "Role is required."
            });
        }

        var businessId = GetBusinessId();

        if (businessId == null)
        {
            return Unauthorized(new
            {
                message =
                    "Business information could not be determined."
            });
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(u =>
                u.UserId == id &&
                u.BusinessId == businessId.Value);

        if (user == null)
        {
            return NotFound(new
            {
                message = "User not found."
            });
        }

        var username = request.Username.Trim();

        var duplicateUsername = await _db.Users
            .AnyAsync(u =>
                u.UserId != id &&
                u.BusinessId == businessId.Value &&
                u.Username == username);

        if (duplicateUsername)
        {
            return Conflict(new
            {
                message = "Username already exists."
            });
        }

        var roleExists = await _db.Roles
            .AnyAsync(r =>
                r.RoleId == request.RoleId);

        if (!roleExists)
        {
            return BadRequest(new
            {
                message = "Invalid role."
            });
        }

        if (request.BranchId.HasValue &&
            request.BranchId.Value <= 0)
        {
            return BadRequest(new
            {
                message = "Invalid branch."
            });
        }

        user.Username = username;
        user.FullName = request.FullName.Trim();
        user.RoleId = request.RoleId;
        user.BranchId = request.BranchId;
        user.IsActive = request.IsActive;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "User updated successfully."
        });
    }


    // ============================================================
    // PATCH: api/Users/{id}/activate
    // ADMIN and OWNER only
    // ============================================================

    [HttpPatch("{id:int}/activate")]
    [Authorize(Roles = "ADMIN,OWNER")]
    public async Task<IActionResult> ActivateUser(int id)
    {
        var businessId = GetBusinessId();

        if (businessId == null)
        {
            return Unauthorized(new
            {
                message =
                    "Business information could not be determined."
            });
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(u =>
                u.UserId == id &&
                u.BusinessId == businessId.Value);

        if (user == null)
        {
            return NotFound(new
            {
                message = "User not found."
            });
        }

        user.IsActive = true;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "User activated successfully."
        });
    }


    // ============================================================
    // PATCH: api/Users/{id}/deactivate
    // ADMIN and OWNER only
    // ============================================================

    [HttpPatch("{id:int}/deactivate")]
    [Authorize(Roles = "ADMIN,OWNER")]
    public async Task<IActionResult> DeactivateUser(int id)
    {
        var businessId = GetBusinessId();

        if (businessId == null)
        {
            return Unauthorized(new
            {
                message =
                    "Business information could not be determined."
            });
        }

        var currentUserId = GetCurrentUserId();

        if (currentUserId == id)
        {
            return BadRequest(new
            {
                message =
                    "You cannot deactivate your own account."
            });
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(u =>
                u.UserId == id &&
                u.BusinessId == businessId.Value);

        if (user == null)
        {
            return NotFound(new
            {
                message = "User not found."
            });
        }

        user.IsActive = false;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "User deactivated successfully."
        });
    }


    // ============================================================
    // PATCH: api/Users/{id}/password
    // ADMIN and OWNER only
    // ============================================================

    [HttpPatch("{id:int}/password")]
    [Authorize(Roles = "ADMIN,OWNER")]
    public async Task<IActionResult> ChangePassword(
        int id,
        [FromBody] ChangeUserPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new
            {
                message = "New password is required."
            });
        }

        if (request.NewPassword.Length < 6)
        {
            return BadRequest(new
            {
                message =
                    "Password must be at least 6 characters long."
            });
        }

        var businessId = GetBusinessId();

        if (businessId == null)
        {
            return Unauthorized(new
            {
                message =
                    "Business information could not be determined."
            });
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(u =>
                u.UserId == id &&
                u.BusinessId == businessId.Value);

        if (user == null)
        {
            return NotFound(new
            {
                message = "User not found."
            });
        }

        user.PasswordHash =
            _passwordHasher.HashPassword(
                user,
                request.NewPassword);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Password changed successfully."
        });
    }


    // ============================================================
    // GET: api/Users/check-permission
    // Any authenticated user
    // ============================================================

    [HttpGet("check-permission")]
    public async Task<IActionResult> CheckPermission(
        [FromQuery] string permissionCode,
        [FromServices] PermissionService permissionService)
    {
        if (string.IsNullOrWhiteSpace(permissionCode))
        {
            return BadRequest(new
            {
                message = "Permission code is required."
            });
        }

        var roleName = User.FindFirst(
            ClaimTypes.Role)?.Value;

        if (string.IsNullOrWhiteSpace(roleName))
        {
            return Unauthorized(new
            {
                message =
                    "Role information could not be determined."
            });
        }

        var role = await _db.Roles
            .FirstOrDefaultAsync(r =>
                r.RoleName == roleName);

        if (role == null)
        {
            return Unauthorized(new
            {
                message =
                    "User role could not be found."
            });
        }

        var hasPermission =
            await permissionService.HasPermission(
                role.RoleId,
                permissionCode.Trim());

        return Ok(new
        {
            role = role.RoleName,
            permission = permissionCode.Trim(),
            allowed = hasPermission
        });
    }


    // ============================================================
    // HELPER: GET BUSINESS ID
    // ============================================================

    private int? GetBusinessId()
    {
        var claim =
            User.FindFirst("BusinessId")?.Value;

        if (int.TryParse(
                claim,
                out int businessId))
        {
            return businessId;
        }

        return null;
    }


    // ============================================================
    // HELPER: GET CURRENT USER ID
    // ============================================================

    private int? GetCurrentUserId()
    {
        var claim =
            User.FindFirst(
                ClaimTypes.NameIdentifier);

        if (claim == null)
        {
            claim =
                User.FindFirst("sub");
        }

        if (claim == null)
            return null;

        if (int.TryParse(
                claim.Value,
                out int userId))
        {
            return userId;
        }

        return null;
    }
}