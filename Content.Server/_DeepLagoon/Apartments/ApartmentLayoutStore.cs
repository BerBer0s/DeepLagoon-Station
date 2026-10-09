using System.IO;
using System.Text.Json;
using Content.Shared._DeepLagoon.Apartments;
using Microsoft.Data.Sqlite;

namespace Content.Server._DeepLagoon.Apartments;

/// <summary>Storage seam: an EF/account implementation can replace the isolated prototype store.</summary>
public interface IApartmentLayoutStore : IDisposable
{
    ApartmentDelta? Load(Guid owner);
    bool Save(Guid owner, ApartmentDelta delta, int expectedRevision);
}

/// <summary>Separate prototype DB. Never touches donation balances or the production schema.</summary>
public sealed class ApartmentLayoutStore : IApartmentLayoutStore
{
    private readonly SqliteConnection _connection;
    public ApartmentLayoutStore(string path)
    {
        if (path != ":memory:") Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
#if USE_SYSTEM_SQLITE
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_sqlite3());
#endif
        _connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        _connection.Open();
        using var command = _connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL; CREATE TABLE IF NOT EXISTS layouts (owner TEXT PRIMARY KEY, revision INTEGER NOT NULL, delta TEXT NOT NULL)";
        command.ExecuteNonQuery();
    }

    public ApartmentDelta? Load(Guid owner)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT delta FROM layouts WHERE owner=$owner";
        command.Parameters.AddWithValue("$owner", owner.ToString());
        return command.ExecuteScalar() is string json ? JsonSerializer.Deserialize<ApartmentDelta>(json) : null;
    }

    public bool Save(Guid owner, ApartmentDelta delta, int expectedRevision)
    {
        if (delta.Revision != expectedRevision + 1) throw new ArgumentException("Expected the next layout revision.");
        using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT INTO layouts(owner,revision,delta) SELECT $owner,$revision,$delta WHERE $expected=0
            ON CONFLICT(owner) DO UPDATE SET revision=$revision,delta=$delta WHERE layouts.revision=$expected;
            """;
        // UPDATE separately is required for existing rows with a non-zero expected revision.
        if (expectedRevision != 0)
            command.CommandText = "UPDATE layouts SET revision=$revision,delta=$delta WHERE owner=$owner AND revision=$expected";
        command.Parameters.AddWithValue("$owner", owner.ToString());
        command.Parameters.AddWithValue("$revision", delta.Revision);
        command.Parameters.AddWithValue("$expected", expectedRevision);
        command.Parameters.AddWithValue("$delta", JsonSerializer.Serialize(delta));
        return command.ExecuteNonQuery() == 1;
    }
    public void Dispose() => _connection.Dispose();
}
