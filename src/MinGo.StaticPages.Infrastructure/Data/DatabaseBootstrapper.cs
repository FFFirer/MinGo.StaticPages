using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MinGo.StaticPages.Infrastructure.Data;

namespace MinGo.StaticPages.Infrastructure.Data;

/// <summary>
/// Applies schema migrations automatically at startup.
/// Supports both empty DBs and legacy DBs created via EnsureCreated (pre-migrations).
/// </summary>
public static class DatabaseBootstrapper
{
    public static async Task InitializeAsync(AppDbContext db, CancellationToken ct = default)
    {
        // Create full schema for brand-new databases; no-op when file already has tables.
        await db.Database.EnsureCreatedAsync(ct);

        // Upgrade legacy databases that predate later model changes.
        await UpgradeLegacySchemaAsync(db, ct);
    }

    private static async Task UpgradeLegacySchemaAsync(AppDbContext db, CancellationToken ct)
    {
        if (!db.Database.IsSqlite())
            return;

        var conn = (SqliteConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        await ExecAsync(conn, ct, """
            CREATE TABLE IF NOT EXISTS StorageInstances (
                Id TEXT NOT NULL PRIMARY KEY,
                Name TEXT NOT NULL,
                Kind INTEGER NOT NULL,
                LocalRoot TEXT NOT NULL DEFAULT 'storage-data',
                Prefix TEXT NOT NULL DEFAULT '',
                ServiceUrl TEXT NULL,
                Region TEXT NOT NULL DEFAULT 'us-east-1',
                AccessKey TEXT NULL,
                SecretKey TEXT NULL,
                Bucket TEXT NOT NULL DEFAULT 'static-pages',
                ForcePathStyle INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            """);

        await ExecAsync(conn, ct, "CREATE UNIQUE INDEX IF NOT EXISTS IX_StorageInstances_Name ON StorageInstances (Name);");

        // Project.StorageInstanceId (added with storage instances)
        if (!await ColumnExistsAsync(conn, "Projects", "StorageInstanceId", ct))
        {
            await ExecAsync(conn, ct, "ALTER TABLE Projects ADD COLUMN StorageInstanceId TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';");
        }

        // Column length / optional refinements that EnsureCreated would not patch
        await EnsureColumnAsync(conn, "StorageInstances", "LocalRoot", "TEXT", "'storage-data'", ct);
        await EnsureColumnAsync(conn, "StorageInstances", "Prefix", "TEXT", "''", ct);

        // Convert legacy TEXT datetime columns to UTC Unix milliseconds (long).
        await ConvertTimestampColumnsAsync(conn, ct);
    }

    private static readonly (string Table, string Column)[] TimestampColumns =
    [
        ("Projects", "CreatedAt"),
        ("Projects", "UpdatedAt"),
        ("Versions", "CreatedAt"),
        ("VersionFiles", "CreatedAt"),
        ("Releases", "CreatedAt"),
        ("Users", "CreatedAt"),
        ("ApiKeys", "CreatedAt"),
        ("ApiKeys", "RevokedAt"),
        ("StorageSettings", "UpdatedAt"),
        ("StorageInstances", "CreatedAt"),
        ("StorageInstances", "UpdatedAt"),
    ];

    private static async Task ConvertTimestampColumnsAsync(SqliteConnection conn, CancellationToken ct)
    {
        foreach (var (table, column) in TimestampColumns)
        {
            if (!await TableExistsAsync(conn, table, ct) || !await ColumnExistsAsync(conn, table, column, ct))
                continue;

            // TEXT affinity stores numbers as text; rebuild column as INTEGER so ValueConverter(long) round-trips.
            if (!await ColumnHasIntegerAffinityAsync(conn, table, column, ct))
            {
                var tmp = column + "_unix_tmp";
                if (await ColumnExistsAsync(conn, table, tmp, ct))
                    await ExecAsync(conn, ct, $"ALTER TABLE {table} DROP COLUMN {tmp};");

                await ExecAsync(conn, ct, $"ALTER TABLE {table} ADD COLUMN {tmp} INTEGER;");

                await using (var select = conn.CreateCommand())
                {
                    select.CommandText = $"SELECT rowid, {column} FROM {table} WHERE {column} IS NOT NULL;";
                    var updates = new List<(long RowId, object? Value)>();
                    await using (var reader = await select.ExecuteReaderAsync(ct))
                    {
                        while (await reader.ReadAsync(ct))
                        {
                            var rowId = reader.GetInt64(0);
                            var raw = reader.GetValue(1);
                            var unix = DateTimeConverters.ParseToUnixMs(raw);
                            if (unix is long ms)
                                updates.Add((rowId, ms));
                            else
                                updates.Add((rowId, null));
                        }
                    }

                    foreach (var (rowId, value) in updates)
                    {
                        await using var update = conn.CreateCommand();
                        update.CommandText = $"UPDATE {table} SET {tmp} = $v WHERE rowid = $id;";
                        update.Parameters.AddWithValue("$v", value ?? DBNull.Value);
                        update.Parameters.AddWithValue("$id", rowId);
                        await update.ExecuteNonQueryAsync(ct);
                    }
                }

                await ExecAsync(conn, ct, $"ALTER TABLE {table} DROP COLUMN {column};");
                await ExecAsync(conn, ct, $"ALTER TABLE {table} RENAME COLUMN {tmp} TO {column};");
            }
            else
            {
                // Already INTEGER affinity: normalize any residual text numerics / date strings.
                await using var select = conn.CreateCommand();
                select.CommandText = $"SELECT rowid, {column} FROM {table} WHERE {column} IS NOT NULL AND typeof({column}) != 'integer';";
                var updates = new List<(long RowId, long UnixMs)>();
                await using (var reader = await select.ExecuteReaderAsync(ct))
                {
                    while (await reader.ReadAsync(ct))
                    {
                        var rowId = reader.GetInt64(0);
                        var unix = DateTimeConverters.ParseToUnixMs(reader.GetValue(1));
                        if (unix is long ms)
                            updates.Add((rowId, ms));
                    }
                }

                foreach (var (rowId, unixMs) in updates)
                {
                    await using var update = conn.CreateCommand();
                    update.CommandText = $"UPDATE {table} SET {column} = $v WHERE rowid = $id;";
                    update.Parameters.AddWithValue("$v", unixMs);
                    update.Parameters.AddWithValue("$id", rowId);
                    await update.ExecuteNonQueryAsync(ct);
                }
            }
        }
    }

    private static async Task<bool> ColumnHasIntegerAffinityAsync(SqliteConnection conn, string table, string column, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table});";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            {
                var declared = reader.GetString(2) ?? string.Empty;
                return declared.Contains("INT", StringComparison.OrdinalIgnoreCase);
            }
        }
        return false;
    }

    private static async Task<bool> ColumnExistsAsync(SqliteConnection conn, string table, string column, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table});";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static async Task EnsureColumnAsync(SqliteConnection conn, string table, string column, string sqlType, string defaultValue, CancellationToken ct)
    {
        if (await TableExistsAsync(conn, table, ct) && !await ColumnExistsAsync(conn, table, column, ct))
            await ExecAsync(conn, ct, $"ALTER TABLE {table} ADD COLUMN {column} {sqlType} DEFAULT {defaultValue};");
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection conn, string table, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=$name;";
        cmd.Parameters.AddWithValue("$name", table);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is not null;
    }

    private static async Task ExecAsync(SqliteConnection conn, CancellationToken ct, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
