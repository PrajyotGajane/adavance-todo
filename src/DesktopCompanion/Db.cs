using Microsoft.Data.Sqlite;
using IO = System.IO;

namespace DesktopCompanion;

// One long-lived SQLite connection (WAL); every write is committed immediately.
public static class Db
{
    static SqliteConnection _c = null!;

    public static string FilePath
    {
        get
        {
            var dir = Environment.GetEnvironmentVariable("DESKTOPCOMPANION_DATA");
            if (string.IsNullOrEmpty(dir))
                dir = IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopCompanion");
            return IO.Path.Combine(dir, "data.db");
        }
    }

    public static void Open()
    {
        IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(FilePath)!);
        _c = new SqliteConnection($"Data Source={FilePath};Pooling=False");
        _c.Open();
        Exec("PRAGMA journal_mode=WAL");
        Exec("PRAGMA synchronous=NORMAL");
        Exec(@"CREATE TABLE IF NOT EXISTS tasks(
                 id INTEGER PRIMARY KEY AUTOINCREMENT, title TEXT NOT NULL,
                 due_date TEXT, due_time TEXT, tag TEXT, done INTEGER NOT NULL DEFAULT 0, created TEXT NOT NULL)");
        Exec(@"CREATE TABLE IF NOT EXISTS notes(
                 id INTEGER PRIMARY KEY AUTOINCREMENT, body TEXT NOT NULL, created TEXT NOT NULL, updated TEXT NOT NULL)");
        Exec("CREATE TABLE IF NOT EXISTS settings(key TEXT PRIMARY KEY, value TEXT NOT NULL)");
    }

    public static void Close()
    {
        try { _c?.Close(); _c?.Dispose(); } catch { }
    }

    static SqliteCommand Cmd(string sql, (string, object?)[] p)
    {
        var cmd = _c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (k, v) in p) cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
        return cmd;
    }

    public static int Exec(string sql, params (string, object?)[] p)
    {
        using var cmd = Cmd(sql, p);
        return cmd.ExecuteNonQuery();
    }

    public static long Insert(string sql, params (string, object?)[] p)
    {
        Exec(sql, p);
        using var cmd = Cmd("SELECT last_insert_rowid()", Array.Empty<(string, object?)>());
        return (long)cmd.ExecuteScalar()!;
    }

    public static List<object?[]> Query(string sql, params (string, object?)[] p)
    {
        using var cmd = Cmd(sql, p);
        using var r = cmd.ExecuteReader();
        var rows = new List<object?[]>();
        while (r.Read())
        {
            var row = new object?[r.FieldCount];
            for (int i = 0; i < row.Length; i++) row[i] = r.IsDBNull(i) ? null : r.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }
}
