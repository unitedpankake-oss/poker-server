using PokerServer.Hubs;
using PokerServer.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSignalR();
builder.Services.AddSingleton<GameService>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .SetIsOriginAllowed(_ => true);
    });
});

var app = builder.Build();

app.UseCors();

// Map SignalR hub
app.MapHub<GameHub>("/gamehub");

// Health check endpoint
app.MapGet("/", () => "Poker Server is running!");

app.Run();
