using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using LiquorPOS.Api.Data;
using LiquorPOS.Api.DTOs;
using LiquorPOS.Api.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace LiquorPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly LiquorPosDbContext _db;
    private readonly IConfiguration _configuration;

    public AuthController(
        AuthService authService,
        LiquorPosDbContext db,
        IConfiguration configuration)
    {
        _authService = authService;
        _db = db;
        _configuration = configuration;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request)
    {
        // --------------------------------------------------
        // Validate request
        // --------------------------------------------------

        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new
            {
                message = "Username and password are required."
            });
        }


        // --------------------------------------------------
        // Validate username and password
        // --------------------------------------------------

        var user = await _authService.ValidateUser(
            request.Username,
            request.Password);

        if (user == null)
        {
            return Unauthorized(new
            {
                message = "Invalid username or password."
            });
        }


        // --------------------------------------------------
        // Get role
        // --------------------------------------------------

        var role = await _db.Roles
            .FirstOrDefaultAsync(r =>
                r.RoleId == user.RoleId);

        if (role == null)
        {
            return Unauthorized(new
            {
                message = "User role could not be found."
            });
        }


        // --------------------------------------------------
        // JWT configuration
        // --------------------------------------------------

        var jwtKey = _configuration["Jwt:Key"];
        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];

        if (string.IsNullOrWhiteSpace(jwtKey))
        {
            return StatusCode(500, new
            {
                message = "JWT key is not configured."
            });
        }


        // --------------------------------------------------
        // Create claims
        // --------------------------------------------------

        var claims = new List<Claim>
        {
            new Claim(
                JwtRegisteredClaimNames.Sub,
                user.UserId.ToString()),

            new Claim(
                JwtRegisteredClaimNames.UniqueName,
                user.Username),

            new Claim(
                ClaimTypes.Name,
                user.FullName),

            new Claim(
                ClaimTypes.Role,
                role.RoleName),

            new Claim(
                "BusinessId",
                user.BusinessId.ToString()),

            new Claim(
                "BranchId",
                user.BranchId?.ToString() ?? "")
        };


        // --------------------------------------------------
        // Create signing credentials
        // --------------------------------------------------

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);


        // --------------------------------------------------
        // Create token
        // --------------------------------------------------

        var expirationMinutes =
            _configuration.GetValue<int>(
                "Jwt:ExpirationMinutes",
                480);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
                expirationMinutes),
            signingCredentials: credentials);


        // --------------------------------------------------
        // Convert token to string
        // --------------------------------------------------

        var tokenString =
            new JwtSecurityTokenHandler()
                .WriteToken(token);


        // --------------------------------------------------
        // Return response
        // --------------------------------------------------

        return Ok(new
        {
            message = "Login successful.",

            token = tokenString,

            expiresInMinutes = expirationMinutes,

            user = new
            {
                user.UserId,
                user.Username,
                user.FullName,
                user.BusinessId,
                user.BranchId,
                role = role.RoleName
            }
        });
    }
}