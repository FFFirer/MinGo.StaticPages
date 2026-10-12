using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MinGo.StaticPages.Domain.Entities;
using MinGo.StaticPages.Infrastructure.Data;

namespace MinGo.StaticPages.Tests;

public class DatabaseBootstrapperTests
{
    [Fact]
    public async Task Upgrades_legacy_project_table_with_storage_instance_id()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"mingo-legacy-{Guid.NewGuid():N}.db");
        try
        {
            // Simulate a pre-storage-instances database (Projects without StorageInstanceId).
            await using (var raw = new SqliteConnection($"Data Source={dbPath}"))
            {
                await raw.OpenAsync();
                await using var cmd = raw.CreateCommand();
                cmd.CommandText = """
                    CREATE TABLE Projects (
                        Id TEXT PRIMARY KEY,
                        Name TEXT NOT NULL,
                        DisplayName TEXT NULL,
                        Description TEXT NULL,
                        CreatedAt TEXT NOT NULL,
                        UpdatedAt TEXT NOT NULL,
                        IsDeleted INTEGER NOT NULL
                    );
                    INSERT INTO Projects (Id, Name, CreatedAt, UpdatedAt, IsDeleted)
                    VALUES ('11111111-1111-1111-1111-111111111111', 'legacy', '2026-01-01', '2026-01-01', 0);
                    """;
                await cmd.ExecuteNonQueryAsync();
            }

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;
            await using var db = new AppDbContext(options);
            await DatabaseBootstrapper.InitializeAsync(db);

            // StorageInstances table now exists; Project has StorageInstanceId; backfill path can run.
            var hasInstances = await db.StorageInstances.AnyAsync();
            Assert.False(hasInstances); // empty table created; seed still happens in Program

            var project = await db.Projects.SingleAsync();
            Assert.Equal(Guid.Empty, project.StorageInstanceId);

            // Seed + backfill as Program.cs does
            var inst = new StorageInstance { Name = "local", Kind = StorageInstanceKind.Local, LocalRoot = "storage-data" };
            db.Add(inst);
            project.StorageInstanceId = inst.Id;
            await db.SaveChangesAsync();

            var reloaded = await db.Projects.Include(p => p.StorageInstance).SingleAsync();
            Assert.Equal("local", reloaded.StorageInstance!.Name);
        }
        finally
        {
            try { File.Delete(dbPath); } catch { }
        }
    }

    [Fact]
    public async Task Fresh_database_gets_full_schema()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"mingo-fresh-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;
            await using var db = new AppDbContext(options);
            await DatabaseBootstrapper.InitializeAsync(db);

            var inst = new StorageInstance { Name = "local", Kind = StorageInstanceKind.Local, LocalRoot = "x" };
            var project = new Project { Name = "p", StorageInstanceId = inst.Id };
            db.AddRange(inst, project);
            await db.SaveChangesAsync();

            Assert.Equal(inst.Id, (await db.Projects.SingleAsync()).StorageInstanceId);
        }
        finally
        {
            try { File.Delete(dbPath); } catch { }
        }
    }

    [Fact]
    public async Task Datetime_stored_as_unix_ms_long_and_roundtrips_utc()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"mingo-ts-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;
            await using var db = new AppDbContext(options);
            await DatabaseBootstrapper.InitializeAsync(db);

            var inst = new StorageInstance { Name = "local", Kind = StorageInstanceKind.Local, LocalRoot = "x" };
            db.Add(inst);
            await db.SaveChangesAsync();

            var created = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.FromHours(8));
            var project = new Project
            {
                Name = "ts",
                StorageInstanceId = inst.Id,
                CreatedAt = created,
                UpdatedAt = created
            };
            db.Add(project);
            await db.SaveChangesAsync();

            await using var raw = new SqliteConnection($"Data Source={dbPath}");
            await raw.OpenAsync();
            await using var cmd = raw.CreateCommand();
            cmd.CommandText = "SELECT CreatedAt, typeof(CreatedAt) FROM Projects WHERE Name = 'ts';";
            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            var stored = reader.GetInt64(0);
            Assert.Equal("integer", reader.GetString(1));
            Assert.Equal(created.ToUniversalTime().ToUnixTimeMilliseconds(), stored);

            db.ChangeTracker.Clear();
            var reloaded = await db.Projects.AsNoTracking().SingleAsync(p => p.Name == "ts");
            Assert.Equal(created.ToUniversalTime(), reloaded.CreatedAt.ToUniversalTime());
        }
        finally
        {
            try { File.Delete(dbPath); } catch { }
        }
    }

    [Fact]
    public async Task Legacy_text_dates_converted_to_unix_ms()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"mingo-ts-legacy-{Guid.NewGuid():N}.db");
        try
        {
            await using (var raw = new SqliteConnection($"Data Source={dbPath}"))
            {
                await raw.OpenAsync();
                await using var cmd = raw.CreateCommand();
                cmd.CommandText = """
                    CREATE TABLE Projects (
                        Id TEXT PRIMARY KEY,
                        Name TEXT NOT NULL,
                        DisplayName TEXT NULL,
                        Description TEXT NULL,
                        CreatedAt TEXT NOT NULL,
                        UpdatedAt TEXT NOT NULL,
                        IsDeleted INTEGER NOT NULL
                    );
                    INSERT INTO Projects (Id, Name, CreatedAt, UpdatedAt, IsDeleted)
                    VALUES ('22222222-2222-2222-2222-222222222222', 'old', '2026-03-01T04:00:00+00:00', '2026-03-01T04:00:00+00:00', 0);
                    """;
                await cmd.ExecuteNonQueryAsync();
            }

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;
            await using var db = new AppDbContext(options);
            await DatabaseBootstrapper.InitializeAsync(db);

            var project = await db.Projects.AsNoTracking().SingleAsync();
            Assert.Equal(DateTimeOffset.Parse("2026-03-01T04:00:00+00:00").ToUnixTimeMilliseconds(), project.CreatedAt.ToUnixTimeMilliseconds());

            await using var raw2 = new SqliteConnection($"Data Source={dbPath}");
            await raw2.OpenAsync();
            await using var cmd2 = raw2.CreateCommand();
            cmd2.CommandText = "SELECT typeof(CreatedAt) FROM Projects;";
            Assert.Equal("integer", (string)(await cmd2.ExecuteScalarAsync())!);
        }
        finally
        {
            try { File.Delete(dbPath); } catch { }
        }
    }
}
