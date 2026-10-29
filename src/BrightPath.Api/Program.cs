using BrightPath.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IClock>(_ =>
    new PinnedClock(DateTime.Parse(builder.Configuration["Clock:Now"]!)));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.MapGet("/health", () => Results.Ok("ok"));
app.Run();

public partial class Program { }
