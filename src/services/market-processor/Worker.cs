using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;

namespace market_processor;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    private IConnection _connection;
    private IModel _channel;

    private IConnectionMultiplexer _redis;
    private IDatabase _db;

    private const string ExchangeName = "market.data";
    private const string QueueName = "market.processor.queue";

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;

        // RabbitMQ
        var factory = new ConnectionFactory()
        {
            HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost"
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.ExchangeDeclare(exchange: ExchangeName, type: ExchangeType.Fanout);

        _channel.QueueDeclare(queue: QueueName, durable: false, exclusive: false, autoDelete: false);

        _channel.QueueBind(queue: QueueName, exchange: ExchangeName, routingKey: "");

        // Redis
        var redisHost = Environment.GetEnvironmentVariable("REDIS_HOST") ?? "localhost";
        _redis = ConnectionMultiplexer.Connect(redisHost);
        _db = _redis.GetDatabase();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new EventingBasicConsumer(_channel);

        consumer.Received += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);

                var data = JsonSerializer.Deserialize<PriceUpdated>(json);

                if (data != null)
                {
                    var key = $"quote:{data.Symbol}";

                    await _db.StringSetAsync(key, data.Price);

                    _logger.LogInformation("Stored {Symbol}: {Price}", data.Symbol, data.Price);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message");
            }
        };

        _channel.BasicConsume(queue: QueueName, autoAck: true, consumer: consumer);

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _channel.Close();
        _connection.Close();
        _redis.Dispose();
        base.Dispose();
    }

    private class PriceUpdated
    {
        [JsonPropertyName("symbol")]
        public string Symbol { get; set; }

        [JsonPropertyName("price")]
        public double Price { get; set; }

        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }
    }
}