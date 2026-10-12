using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using LiquorPOS.Api.Models;

namespace LiquorPOS.Api.Controllers;

[ApiController]
[Route("api/password")]
public class PasswordController : ControllerBase
{
    [HttpPost("hash")]
    public IActionResult GenerateHash([FromBody] PasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new
            {
                message = "Password is required."
            });
        }

        var hasher = new PasswordHasher<User>();

        var user = new User
        {
            Username = "admin"
        };

        var hash = hasher.HashPassword(
            user,
            request.Password);

        return Ok(new
        {
            hash
        });
    }
}

public class PasswordRequest
{
    public string Password { get; set; } = string.Empty;
}