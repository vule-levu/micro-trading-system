
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using System.Diagnostics;

namespace order_service.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private static readonly List<Order> Orders = new();
    private static readonly List<Trade> Trades = new();
    private readonly IDatabase _db;
    public OrdersController(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    [HttpPost]
    public async Task<IActionResult> PlaceOrder([FromBody] Order order)
    {
        if (string.IsNullOrWhiteSpace(order.Symbol) ||
        string.IsNullOrWhiteSpace(order.Side) ||
        order.Quantity <= 0)
        {
            return BadRequest("Invalid order");
        }

        order.Id = Guid.NewGuid().ToString();
        var key = $"quote:{order.Symbol.ToUpper()}";
        
        var value = await _db.StringGetAsync(key);

        if (value.IsNullOrEmpty)
        {
            return BadRequest($"No market data for {order.Symbol}");
        }

        if (!double.TryParse(value, out var marketPrice))
        {
            return StatusCode(500, "Invalid market data");
        }

        order.Status = "FILLED";
        order.ExecutedPrice = marketPrice;

        var trade = new Trade
        {
            OrderId = order.Id,
            Symbol = order.Symbol,
            Price = order.ExecutedPrice,
            Quantity = order.Quantity,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        Orders.Add(order);
        Trades.Add(trade);

        return Ok(new
        {
            order,
            trade
        });
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(Orders);
    }

    [HttpGet("trades")]
    public IActionResult GetTrades()
    {
        return Ok(Trades);
    }
}


public class Order
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Symbol { get; set; }
    public string Side { get; set; }
    public double Price { get; set; }
    public int Quantity { get; set; }

    public string Status { get; set; } = "NEW";
    public double ExecutedPrice { get; set; }
}

public class Trade
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string OrderId { get; set; }
    public string Symbol { get; set; }
    public double Price { get; set; }
    public int Quantity { get; set; }
    public long Timestamp { get; set; }
}