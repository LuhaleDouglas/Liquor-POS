using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using LiquorPOS.Api.Data;
using LiquorPOS.Api.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LiquorPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PermissionsController : ControllerBase
{
    private readonly PermissionService _permissionService;
    private readonly LiquorPosDbContext _db;

    public PermissionsController(
        PermissionService permissionService,
        LiquorPosDbContext db)
    {
        _permissionService = permissionService;
        _db = db;
    }

    [HttpGet("my-permissions")]
    public async Task<IActionResult> GetMyPermissions()
    {
        var userIdClaim =
            User.FindFirst(
                JwtRegisteredClaimNames.Sub)?.Value
            ??
            User.FindFirst(
                ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(
                userIdClaim,
                out var userId))
        {
            return Unauthorized(new
            {
                message =
                    "User identity could not be determined."
            });
        }

        var user = await _db.Users
            .Where(u => u.UserId == userId)
            .Select(u => new
            {
                u.UserId,
                u.RoleId
            })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return Unauthorized(new
            {
                message =
                    "User could not be found."
            });
        }

        var permissions =
            await _permissionService
                .GetPermissionsAsync(user.RoleId);

        return Ok(new
        {
            userId = user.UserId,
            roleId = user.RoleId,
            permissions
        });
    }
}