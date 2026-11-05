using PokerServer.Hubs;
using PokerServer.Services;
using PokerServer;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSignalR();
builder.Services.AddSingleton<GameService>();
builder.Services.AddSingleton<UserService>();
builder.Services.AddHostedService<TurnTimeoutService>();
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
app.UseStaticFiles();

var webClient = WebClientPage.Load();
app.MapGet("/", (HttpContext context) =>
{
    context.Response.Headers.CacheControl = "no-store";
    return Results.Content(webClient, "text/html; charset=utf-8");
});
app.MapGet("/index.html", () => Results.Redirect("/"));

// Map SignalR hub
app.MapHub<GameHub>("/gamehub");

// Health check endpoint
app.MapGet("/health", () => "Poker Server is running!");

app.Run();
