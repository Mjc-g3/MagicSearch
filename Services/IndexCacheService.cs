using System.IO;
using MagicSearch.Models;
using Microsoft.Data.Sqlite;

namespace MagicSearch.Services
{
    public sealed class IndexCacheService
    {
        private readonly string _databasePath;

        public IndexCacheService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var folder = Path.Combine(appData, "MagicSearch");
            Directory.CreateDirectory(folder);

            _databasePath = Path.Combine(folder, "index.db");
        }

        public async Task<IReadOnlyList<IndexedItem>> LoadAsync()
        {
            EnsureDatabase();

            var items = new List<IndexedItem>();

            await using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
            """
            SELECT Name, Path, Type, Extension, Drive, LastModified, Size
            FROM IndexedItems
            """;

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                items.Add(new IndexedItem
                {
                    Name = reader.GetString(0),
                    Path = reader.GetString(1),
                    Type = reader.GetString(2),
                    Extension = reader.GetString(3),
                    Drive = reader.GetString(4),
                    LastModified = reader.IsDBNull(5) ? null : DateTime.Parse(reader.GetString(5)),
                    Size = reader.IsDBNull(6) ? null : reader.GetInt64(6),
                    Icon = null
                });
            }

            return items;
        }

        public async Task SaveAsync(IReadOnlyList<IndexedItem> items)
        {
            EnsureDatabase();

            await using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();

            var clearCommand = connection.CreateCommand();
            clearCommand.Transaction = transaction;
            clearCommand.CommandText = "DELETE FROM IndexedItems";
            await clearCommand.ExecuteNonQueryAsync();

            foreach (var item in items)
            {
                var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText =
                """
                INSERT OR REPLACE INTO IndexedItems
                (Name, Path, Type, Extension, Drive, LastModified, Size)
                VALUES
                ($name, $path, $type, $extension, $drive, $lastModified, $size)
                """;

                command.Parameters.AddWithValue("$name", item.Name);
                command.Parameters.AddWithValue("$path", item.Path);
                command.Parameters.AddWithValue("$type", item.Type);
                command.Parameters.AddWithValue("$extension", item.Extension);
                command.Parameters.AddWithValue("$drive", item.Drive);
                command.Parameters.AddWithValue("$lastModified", item.LastModified.HasValue ? item.LastModified.Value.ToString("O") : DBNull.Value);
                command.Parameters.AddWithValue("$size", item.Size.HasValue ? item.Size.Value : DBNull.Value);

                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }

        private void EnsureDatabase()
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS IndexedItems
            (
                Path TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Type TEXT NOT NULL,
                Extension TEXT NOT NULL,
                Drive TEXT NOT NULL,
                LastModified TEXT NULL,
                Size INTEGER NULL
            )
            """;

            command.ExecuteNonQuery();
        }
    }
}