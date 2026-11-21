using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace osu.Game.Beatmaps
{
    internal sealed class PerfectPerformanceStorage
    {
        private readonly string connectionString;

        public PerfectPerformanceStorage(string path)
        {
            SQLitePCL.Batteries_V2.Init();
            connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
            using var connection = open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                PRAGMA journal_mode = WAL;
                CREATE TABLE IF NOT EXISTS performance (
                    key TEXT PRIMARY KEY, preset TEXT NOT NULL, pp REAL NOT NULL
                );
                CREATE INDEX IF NOT EXISTS performance_preset ON performance(preset);
                CREATE TABLE IF NOT EXISTS metadata (key TEXT PRIMARY KEY);
                """;
            command.ExecuteNonQuery();
        }

        public static string Preset(string[] parts) =>
            JsonConvert.SerializeObject(new[] { parts[0], parts[1], parts[2], parts[4] });

        public void ImportJson(System.Func<Dictionary<string, double>> read)
        {
            const string marker = "json-v2";
            if (HasMarker(marker))
                return;

            Save(read());
            SaveMarker(marker);
        }

        internal bool HasMarker(string marker)
        {
            using var connection = open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1 FROM metadata WHERE key = $key";
            command.Parameters.AddWithValue("$key", marker);
            return command.ExecuteScalar() != null;
        }

        internal void SaveMarker(string marker)
        {
            using var connection = open();
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT OR IGNORE INTO metadata(key) VALUES ($key)";
            command.Parameters.AddWithValue("$key", marker);
            command.ExecuteNonQuery();
        }

        public double? Read(string key)
        {
            using var connection = open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT pp FROM performance WHERE key = $key";
            command.Parameters.AddWithValue("$key", key);
            return command.ExecuteScalar() is double value ? value : null;
        }

        public Dictionary<string, double> ReadPreset(string preset)
        {
            using var connection = open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT key, pp FROM performance WHERE preset = $preset";
            command.Parameters.AddWithValue("$preset", preset);
            using var reader = command.ExecuteReader();
            var values = new Dictionary<string, double>();
            while (reader.Read())
                values.Add(reader.GetString(0), reader.GetDouble(1));
            return values;
        }

        public void Save(IReadOnlyDictionary<string, double> values)
        {
            using var connection = open();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO performance(key, preset, pp) VALUES ($key, $preset, $pp) ON CONFLICT(key) DO NOTHING";
            var key = command.Parameters.Add("$key", SqliteType.Text);
            var preset = command.Parameters.Add("$preset", SqliteType.Text);
            var pp = command.Parameters.Add("$pp", SqliteType.Real);
            foreach (var (id, value) in values)
            {
                if (!double.IsFinite(value) || value < 0)
                    continue;

                string[]? parts;
                try
                {
                    parts = JsonConvert.DeserializeObject<string[]>(id);
                }
                catch (JsonException)
                {
                    continue;
                }

                if (parts is not { Length: 5 })
                    continue;
                key.Value = id;
                preset.Value = Preset(parts);
                pp.Value = value;
                command.ExecuteNonQuery();
            }
            transaction.Commit();
        }

        private SqliteConnection open()
        {
            var connection = new SqliteConnection(connectionString);
            try
            {
                connection.Open();
                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }
    }
}
