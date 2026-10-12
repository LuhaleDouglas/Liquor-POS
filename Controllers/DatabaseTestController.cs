using LiquorPOS.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LiquorPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DatabaseTestController : ControllerBase
{
    private readonly LiquorPosDbContext _db;

    public DatabaseTestController(LiquorPosDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> TestDatabase()
    {
        try
        {
            var result = await _db.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM Products")
                .ToListAsync();

            return Ok(new
            {
                connected = true,
                productCount = result.FirstOrDefault()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                connected = false,
                error = ex.Message
            });
        }
    }
}
