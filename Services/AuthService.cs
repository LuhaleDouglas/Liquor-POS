using LiquorPOS.Api.Data;
using LiquorPOS.Api.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LiquorPOS.Api.Services;

public class AuthService
{
    private readonly LiquorPosDbContext _db;
    private readonly PasswordHasher<Models.User> _passwordHasher;

    public AuthService(LiquorPosDbContext db)
    {
        _db = db;
        _passwordHasher = new PasswordHasher<Models.User>();
    }

    public async Task<Models.User?> ValidateUser(
        string username,
        string password)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u =>
                u.Username == username &&
                u.IsActive);

        if (user == null)
            return null;

        if (user.PasswordHash == "TEMP_PASSWORD_HASH")
            return null;

        var result = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            password);

        if (result == PasswordVerificationResult.Success ||
            result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            return user;
        }

        return null;
    }
}