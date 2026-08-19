' ToolRepository.vb
Imports Microsoft.Data.Sqlite

Public Class ToolRepository
    Private ReadOnly _connectionString As String

    Public Sub New(connectionString As String)
        _connectionString = connectionString
    End Sub

    ' --- DB初期化の追加 ---
    Public Sub InitializeDatabase()
        Using connection As New SqliteConnection(_connectionString)
            connection.Open()
            Dim command = connection.CreateCommand()
            command.CommandText = "
                CREATE TABLE IF NOT EXISTS Tools (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Storage TEXT NOT NULL,
                    IsAvailable INTEGER NOT NULL
                );
            "
            command.ExecuteNonQuery()
        End Using
    End Sub

    ' 一覧取得（検索条件付き）
    Public Function GetAll(name As String, isAvailable As String, storage As String) As List(Of ToolItem)
        Dim tools As New List(Of ToolItem)()
        Using connection As New SqliteConnection(_connectionString)
            connection.Open()
            Dim command = connection.CreateCommand()
            Dim sql As String = "SELECT Id, Name, Storage, IsAvailable FROM Tools WHERE 1=1"

            If Not String.IsNullOrWhiteSpace(name) Then
                sql &= " AND Name LIKE @Name"
                command.Parameters.AddWithValue("@Name", "%" & name.Trim() & "%")
            End If
            If Not String.IsNullOrWhiteSpace(isAvailable) Then
                sql &= " AND IsAvailable = @IsAvailable"
                Dim isAvailBool As Boolean = (isAvailable.ToLower() = "true")
                command.Parameters.AddWithValue("@IsAvailable", If(isAvailBool, 1, 0))
            End If
            If Not String.IsNullOrWhiteSpace(storage) Then
                sql &= " AND Storage LIKE @Storage"
                command.Parameters.AddWithValue("@Storage", "%" & storage.Trim() & "%")
            End If

            command.CommandText = sql
            Using reader = command.ExecuteReader()
                While reader.Read()
                    tools.Add(New ToolItem With {
                        .Id = reader.GetInt32(0),
                        .Name = reader.GetString(1),
                        .Storage = reader.GetString(2),
                        .IsAvailable = (reader.GetInt32(3) = 1)
                    })
                End While
            End Using
        End Using
        Return tools
    End Function

    ' 新規追加
    Public Sub Add(newTool As ToolItem)
        Using connection As New SqliteConnection(_connectionString)
            connection.Open()
            Dim command = connection.CreateCommand()
            command.CommandText = "
                INSERT INTO Tools (Name, Storage, IsAvailable)
                VALUES (@Name, @Storage, @IsAvailable)
            "
            command.Parameters.AddWithValue("@Name", newTool.Name)
            command.Parameters.AddWithValue("@Storage", newTool.Storage)
            command.Parameters.AddWithValue("@IsAvailable", If(newTool.IsAvailable, 1, 0))
            command.ExecuteNonQuery()
        End Using
    End Sub

    ' --- Update: Boolean を返す ---
    Public Function Update(id As Integer, updatedTool As ToolItem) As Boolean
        Using connection As New SqliteConnection(_connectionString)
            connection.Open()
            Dim command = connection.CreateCommand()
            command.CommandText = "
                UPDATE Tools
                SET Name = @Name, Storage = @Storage, IsAvailable = @IsAvailable
                WHERE Id = @Id
            "
            command.Parameters.AddWithValue("@Name", updatedTool.Name)
            command.Parameters.AddWithValue("@Storage", updatedTool.Storage)
            command.Parameters.AddWithValue("@IsAvailable", If(updatedTool.IsAvailable, 1, 0))
            command.Parameters.AddWithValue("@Id", id)

            Dim rowsAffected As Integer = command.ExecuteNonQuery()
            Return rowsAffected > 0
        End Using
    End Function

    ' --- Delete: Boolean を返す ---
    Public Function Delete(id As Integer) As Boolean
        Using connection As New SqliteConnection(_connectionString)
            connection.Open()
            Dim command = connection.CreateCommand()
            command.CommandText = "DELETE FROM Tools WHERE Id = @Id"
            command.Parameters.AddWithValue("@Id", id)

            Dim rowsAffected As Integer = command.ExecuteNonQuery()
            Return rowsAffected > 0
        End Using
    End Function
End Class
