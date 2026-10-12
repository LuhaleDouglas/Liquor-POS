using LiquorPOS.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LiquorPOS.Api.Services;

public class PermissionService
{
    private readonly LiquorPosDbContext _db;

    public PermissionService(LiquorPosDbContext db)
    {
        _db = db;
    }

    public async Task<bool> HasPermission(
        int roleId,
        string permissionCode)
    {
        return await _db.RolePermissions
            .AnyAsync(rp =>
                rp.RoleId == roleId &&
                rp.Permission.PermissionCode == permissionCode &&
                rp.Permission.IsActive);
    }

    public async Task<List<string>> GetPermissionsAsync(
        int roleId)
    {
        return await _db.RolePermissions
            .Where(rp =>
                rp.RoleId == roleId &&
                rp.Permission.IsActive)
            .Select(rp =>
                rp.Permission.PermissionCode)
            .ToListAsync();
    }
}