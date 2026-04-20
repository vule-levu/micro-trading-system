using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace quote_service.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QuotesController : ControllerBase
{
    private readonly IDatabase _db;

    public QuotesController(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    [HttpGet("{symbol}")]
    public async Task<IActionResult> Get(string symbol)
    {
        var key = $"quote:{symbol.ToUpper()}";

        var value = await _db.StringGetAsync(key);

        if (value.IsNullOrEmpty)
        {
            return NotFound(new { message = $"No data for {symbol}" });
        }

        return Ok(new
        {
            symbol = symbol.ToUpper(),
            price = double.Parse(value!)
        });
    }
}