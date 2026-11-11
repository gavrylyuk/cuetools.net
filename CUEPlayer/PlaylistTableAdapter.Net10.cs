using System;
using System.Data;
using System.IO;
using Microsoft.Data.Sqlite;

namespace CUEPlayer.DataSet1TableAdapters
{
    // Preserves the form's typed-table API while storing the modern playlist in SQLite.
    public sealed class PlaylistTableAdapter
    {
        private readonly string databasePath;

        public PlaylistTableAdapter() : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CUETools", "CUEPlayer", "playlist.sqlite")) { }

        public PlaylistTableAdapter(string databasePath)
        {
            this.databasePath = Path.GetFullPath(databasePath);
        }

        private SqliteConnection Open()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath));
            var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Pooling = false
            }.ToString());
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"CREATE TABLE IF NOT EXISTS Playlist (
                    id INTEGER PRIMARY KEY, ordinal INTEGER NOT NULL,
                    path TEXT NOT NULL, artist TEXT NOT NULL, title TEXT NOT NULL,
                    album TEXT NOT NULL, length INTEGER NOT NULL, track INTEGER NOT NULL)";
                command.ExecuteNonQuery();
            }
            return connection;
        }

        public int Fill(DataSet1.PlaylistDataTable table)
        {
            using (var connection = Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT id,path,artist,title,album,length,track FROM Playlist ORDER BY ordinal";
                using (var reader = command.ExecuteReader())
                {
                    table.Clear();
                    while (reader.Read())
                        table.LoadDataRow(new object[] { reader.GetInt32(0), reader.GetString(1),
                            reader.GetString(2), reader.GetString(3), reader.GetString(4),
                            reader.GetInt32(5), reader.GetInt32(6) }, true);
                }
                return table.Count;
            }
        }

        public int Update(DataSet1.PlaylistDataTable table)
        {
            int changed = 0;
            var inserted = new System.Collections.Generic.List<Tuple<DataRow, int>>();
            using (var connection = Open())
            using (var transaction = connection.BeginTransaction())
            {
                foreach (DataRow row in table.Rows)
                {
                    if (row.RowState == DataRowState.Unchanged)
                        continue;
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        if (row.RowState == DataRowState.Deleted)
                        {
                            command.CommandText = "DELETE FROM Playlist WHERE id=$id AND path=$originalPath";
                        }
                        else
                        {
                            command.CommandText = row.RowState == DataRowState.Added
                                ? @"INSERT INTO Playlist (ordinal,path,artist,title,album,length,track)
                                    VALUES ((SELECT COALESCE(MAX(ordinal),-1)+1 FROM Playlist),
                                        $path,$artist,$title,$album,$length,$track) RETURNING id"
                                : @"UPDATE Playlist SET path=$path,artist=$artist,title=$title,
                                    album=$album,length=$length,track=$track WHERE id=$id AND path=$originalPath";
                            command.Parameters.AddWithValue("$path", row["path"]);
                            foreach (string field in new[] { "artist", "title", "album" })
                                command.Parameters.AddWithValue("$" + field, row.IsNull(field) ? "" : row[field]);
                            foreach (string field in new[] { "length", "track" })
                                command.Parameters.AddWithValue("$" + field, row.IsNull(field) ? 0 : row[field]);
                        }
                        if (row.RowState == DataRowState.Added)
                        {
                            inserted.Add(Tuple.Create(row, checked(Convert.ToInt32(command.ExecuteScalar()))));
                        }
                        else
                        {
                            command.Parameters.AddWithValue("$id", row["id", DataRowVersion.Original]);
                            command.Parameters.AddWithValue("$originalPath", row["path", DataRowVersion.Original]);
                            if (command.ExecuteNonQuery() != 1)
                                throw new DBConcurrencyException("The playlist item was changed or deleted by another instance.");
                        }
                        changed++;
                    }
                }
                transaction.Commit();
            }
            // Apply database-assigned IDs only after the transaction commits, so
            // a failed save retains every pending in-memory change for retry.
            bool readOnly = table.idColumn.ReadOnly;
            table.idColumn.ReadOnly = false;
            try
            {
                foreach (var entry in inserted)
                    entry.Item1["id"] = entry.Item2;
            }
            finally { table.idColumn.ReadOnly = readOnly; }
            table.AcceptChanges();
            return changed;
        }
    }
}
