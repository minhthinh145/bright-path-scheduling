using BrightPath.Api;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BrightPath.Tests;

// One fresh brightpath_test database per test class: dropped, re-created, migrated.
public class PostgresFixture : IAsyncLifetime
{
    private const string Admin =
        "Host=localhost;Port=5433;Database=postgres;Username=postgres;Password=postgres";
    public const string TestDb =
        "Host=localhost;Port=5433;Database=brightpath_test;Username=postgres;Password=postgres";
    public static readonly string SeedDir =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "seed"));

    public BrightPathDb CreateDb() =>
        new(new DbContextOptionsBuilder<BrightPathDb>()
            .UseNpgsql(TestDb).UseSnakeCaseNamingConvention().Options);

    public async Task InitializeAsync()
    {
        await using var admin = new NpgsqlConnection(Admin);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand(
            "DROP DATABASE IF EXISTS brightpath_test WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
        await using var create = new NpgsqlCommand("CREATE DATABASE brightpath_test", admin);
        await create.ExecuteNonQueryAsync();
        await using var db = CreateDb();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition("pg")]
public class PgCollection : ICollectionFixture<PostgresFixture> { }
