using BrightPath.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BrightPath.Tests;

public class SettableClock : IClock
{
    public DateTime Now { get; set; } = new(2026, 3, 5, 10, 0, 0);
}

// Real API against its own Postgres database, migrated and seeded once
// per test collection. IClock is swapped for the settable clock.
public class ApiFixture : PostgresFixture
{
    protected override string DbName => "brightpath_api_test";
    public SettableClock Clock { get; } = new();
    public HttpClient Client { get; private set; } = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await using var db = CreateDb();
        await Seed.Import(db, SeedDir, Clock.Now);
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Default"] = ConnString }));
            b.ConfigureServices(s => s.AddSingleton<IClock>(Clock));
        });
        Client = factory.CreateClient();
    }
}

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiFixture> { }
