using RabbitMQ.Client;
using System.Text.Json;
using System.Text;

namespace market_simulator;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private IConnection _connection;
    private IModel _channel;

    private const string ExchangeName = "market.data";

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;

        var factory = new ConnectionFactory()
        {
            HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost"
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.ExchangeDeclare(
            exchange: ExchangeName,
            type: ExchangeType.Fanout
        );
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var random = new Random();

        var symbols = new[] { "AAPL", "TSLA", "MSFT" };

        var prices = new Dictionary<string, double>
        {
            ["AAPL"] = 180,
            ["TSLA"] = 250,
            ["MSFT"] = 320
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var symbol in symbols)
            {
                // simple random walk
                var change = (random.NextDouble() - 0.5) * 2;
                prices[symbol] += change;

                var message = new
                {
                    eventType = "PriceUpdated",
                    symbol = symbol,
                    price = Math.Round(prices[symbol], 2),
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

                _channel.BasicPublish(
                    exchange: ExchangeName,
                    routingKey: "",
                    basicProperties: null,
                    body: body
                );

                _logger.LogInformation("Published {Symbol}: {Price}", symbol, message.price);
            }

            await Task.Delay(500, stoppingToken);
        }
    }

    public override void Dispose()
    {
        _channel.Close();
        _connection.Close();
        base.Dispose();
    }
}
