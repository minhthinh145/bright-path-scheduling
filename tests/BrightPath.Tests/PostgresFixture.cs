using BrightPath.Api;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BrightPath.Tests;

// One fresh test database per collection: dropped, re-created, migrated.
public abstract class PostgresFixture : IAsyncLifetime
{
    private const string Admin =
        "Host=localhost;Port=5433;Database=postgres;Username=postgres;Password=postgres";
    public static readonly string SeedDir =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "seed"));

    protected abstract string DbName { get; }
    private string Cs =>
        $"Host=localhost;Port=5433;Database={DbName};Username=postgres;Password=postgres";

    public BrightPathDb CreateDb() =>
        new(new DbContextOptionsBuilder<BrightPathDb>()
            .UseNpgsql(Cs).UseSnakeCaseNamingConvention().Options);

    public async Task InitializeAsync()
    {
        await using var admin = new NpgsqlConnection(Admin);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS {DbName} WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
        await using var create = new NpgsqlCommand($"CREATE DATABASE {DbName}", admin);
        await create.ExecuteNonQueryAsync();
        await using var db = CreateDb();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

public class SchemaDb : PostgresFixture
{
    protected override string DbName => "brightpath_schema_test";
}

public class SeedDb : PostgresFixture
{
    protected override string DbName => "brightpath_seed_test";
}

[CollectionDefinition("schema")]
public class SchemaCollection : ICollectionFixture<SchemaDb> { }

[CollectionDefinition("seed")]
public class SeedCollection : ICollectionFixture<SeedDb> { }
