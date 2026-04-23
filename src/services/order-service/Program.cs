using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Redis
var redisHost = Environment.GetEnvironmentVariable("REDIS_HOST") ?? "localhost";
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisHost)
);

builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();
app.Run();