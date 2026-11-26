using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace osu.Game.Beatmaps
{
    internal sealed class PerfectPerformanceStorage
    {
        internal readonly record struct Entry(double PP, double? Stars = null, int MaxCombo = 0)
        {
            public StarDifficulty? Preview => Stars is double stars ? new StarDifficulty(stars, MaxCombo) : null;
        }

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
                    key TEXT PRIMARY KEY, preset TEXT NOT NULL, pp REAL NOT NULL, stars REAL, max_combo INTEGER
                );
                CREATE INDEX IF NOT EXISTS performance_preset ON performance(preset);
                CREATE TABLE IF NOT EXISTS metadata (key TEXT PRIMARY KEY);
                """;
            command.ExecuteNonQuery();

            using var migration = connection.BeginTransaction();
            command.Transaction = migration;
            command.CommandText = "SELECT 1 FROM pragma_table_info('performance') WHERE name = 'stars'";
            if (command.ExecuteScalar() == null)
            {
                command.CommandText = "ALTER TABLE performance ADD COLUMN stars REAL; ALTER TABLE performance ADD COLUMN max_combo INTEGER;";
                command.ExecuteNonQuery();
            }
            migration.Commit();
        }

        public static string Preset(string[] parts) =>
            JsonConvert.SerializeObject(new[] { parts[0], parts[1], parts[2], parts[4] });

        public void ImportJson(Func<Dictionary<string, double>> read)
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

        public double? Read(string key) => ReadEntry(key)?.PP;

        public Entry? ReadEntry(string key)
        {
            using var connection = open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT pp, stars, max_combo FROM performance WHERE key = $key";
            command.Parameters.AddWithValue("$key", key);
            using var reader = command.ExecuteReader();
            return reader.Read() ? readEntry(reader, 0) : null;
        }

        public Dictionary<string, Entry> ReadPreset(string preset)
        {
            using var connection = open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT key, pp, stars, max_combo FROM performance WHERE preset = $preset";
            command.Parameters.AddWithValue("$preset", preset);
            using var reader = command.ExecuteReader();
            var values = new Dictionary<string, Entry>();
            while (reader.Read())
                values.Add(reader.GetString(0), readEntry(reader, 1));
            return values;
        }

        private static Entry readEntry(SqliteDataReader reader, int column)
            => reader.IsDBNull(column + 1) ? new Entry(reader.GetDouble(column))
                : new Entry(reader.GetDouble(column), reader.GetDouble(column + 1), reader.GetInt32(column + 2));

        public void Save(IReadOnlyDictionary<string, double> values)
            => Save(values.Select(pair => new KeyValuePair<string, Entry>(pair.Key, new Entry(pair.Value))));

        public void Save(IEnumerable<KeyValuePair<string, Entry>> values)
        {
            using var connection = open();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO performance(key, preset, pp, stars, max_combo) VALUES ($key, $preset, $pp, $stars, $max_combo)
                ON CONFLICT(key) DO UPDATE SET stars = excluded.stars, max_combo = excluded.max_combo
                WHERE performance.stars IS NULL AND excluded.stars IS NOT NULL
                """;
            var key = command.Parameters.Add("$key", SqliteType.Text);
            var preset = command.Parameters.Add("$preset", SqliteType.Text);
            var pp = command.Parameters.Add("$pp", SqliteType.Real);
            var stars = command.Parameters.Add("$stars", SqliteType.Real);
            var maxCombo = command.Parameters.Add("$max_combo", SqliteType.Integer);
            foreach (var (id, value) in values)
            {
                if (!double.IsFinite(value.PP) || value.PP < 0)
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
                pp.Value = value.PP;
                stars.Value = (object?)value.Stars ?? DBNull.Value;
                maxCombo.Value = value.Stars.HasValue ? value.MaxCombo : DBNull.Value;
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
