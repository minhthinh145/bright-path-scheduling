using BrightPath.Api;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<BrightPathDb>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
     .UseSnakeCaseNamingConvention());
builder.Services.AddSingleton<IClock>(_ =>
    new PinnedClock(DateTime.Parse(builder.Configuration["Clock:Now"]!)));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (args is ["seed", ..])
{
    Console.WriteLine(await Seed.RunAsync(app.Services, Path.Combine(AppContext.BaseDirectory, "seed")));
    return;
}

app.UseSwagger();
app.UseSwaggerUI();
app.MapGet("/health", () => Results.Ok("ok"));
app.Run();

public partial class Program { }
