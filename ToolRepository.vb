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
                    IsAvailable INTEGER NOT NULL,
                    CategoryId INTEGER NOT NULL DEFAULT 1
                );
                CREATE TABLE IF NOT EXISTS Categories (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL
                );
                INSERT OR IGNORE INTO Categories (Id, Name) VALUES
                    (1, '電動工具'),
                    (2, '作業工具'),
                    (3, '測定工具');
                "
            command.ExecuteNonQuery()
        End Using
    End Sub

    ' 一覧取得（検索条件付き）
    Public Function GetAll(name As String, isAvailable As String, storage As String, categoryId As String) As List(Of ToolItem)
        Dim tools As New List(Of ToolItem)()
        Using connection As New SqliteConnection(_connectionString)
            connection.Open()
            Dim command = connection.CreateCommand()
            Dim sql As String = "
                SELECT
                    T.Id,
                    T.Name,
                    T.Storage,
                    T.IsAvailable,
                    T.CategoryId,
                    COALESCE(C.Name, '未分類') AS CategoryName
                FROM Tools T
                LEFT JOIN Categories C ON T.CategoryId = C.Id
                WHERE 1=1
            "

            If Not String.IsNullOrWhiteSpace(name) Then
                sql &= " AND T.Name LIKE @Name"
                command.Parameters.AddWithValue("@Name", "%" & name.Trim() & "%")
            End If
            If Not String.IsNullOrWhiteSpace(isAvailable) Then
                sql &= " AND T.IsAvailable = @IsAvailable"
                Dim isAvailBool As Boolean = (isAvailable.ToLower() = "true")
                command.Parameters.AddWithValue("@IsAvailable", If(isAvailBool, 1, 0))
            End If
            If Not String.IsNullOrWhiteSpace(storage) Then
                sql &= " AND T.Storage LIKE @Storage"
                command.Parameters.AddWithValue("@Storage", "%" & storage.Trim() & "%")
            End If
            Dim parsedCategoryId As Integer
            If Not String.IsNullOrWhiteSpace(categoryId) AndAlso Integer.TryParse(categoryId, parsedCategoryId) Then
                sql &= " AND T.CategoryId = @CategoryId"
                command.Parameters.AddWithValue("@CategoryId", parsedCategoryId)
            End If

            command.CommandText = sql
            Using reader = command.ExecuteReader()
                While reader.Read()
                    tools.Add(New ToolItem With {
                        .Id = reader.GetInt32(0),
                        .Name = reader.GetString(1),
                        .Storage = reader.GetString(2),
                        .IsAvailable = (reader.GetInt32(3) = 1),
                        .CategoryId = reader.GetInt32(4),
                        .CategoryName = reader.GetString(5)
                    })
                End While
            End Using
        End Using
        Return tools
    End Function

    ' カテゴリ一覧の取得
    Public Function GetCategories() As List(Of CategoryItem)
        Dim categories As New List(Of CategoryItem)()
        Using connection As New SqliteConnection(_connectionString)
            connection.Open()
            Dim command = connection.CreateCommand()
            command.CommandText = "SELECT Id, Name FROM Categories ORDER BY Id"

            Using reader = command.ExecuteReader()
                While reader.Read()
                    categories.Add(New CategoryItem With {
                        .Id = reader.GetInt32(0),
                        .Name = reader.GetString(1)
                    })
                End While
            End Using
        End Using
        Return categories
    End Function

    ' 新規追加（CategoryId対応）
    Public Sub Add(newTool As ToolItem)
        Using connection As New SqliteConnection(_connectionString)
            connection.Open()
            Dim command = connection.CreateCommand()
            command.CommandText = "
                INSERT INTO Tools (Name, Storage, IsAvailable,CategoryId)
                VALUES (@Name, @Storage, @IsAvailable, @CategoryId)
            "
            command.Parameters.AddWithValue("@Name", newTool.Name)
            command.Parameters.AddWithValue("@Storage", newTool.Storage)
            command.Parameters.AddWithValue("@IsAvailable", If(newTool.IsAvailable, 1, 0))
            ' CategoryIdが未指定（0など）の場合は初期値1を設定
            Dim catId As Integer = If(newTool.CategoryId <= 0, 1, newTool.CategoryId)
            command.Parameters.AddWithValue("@CategoryId", catId)
            command.ExecuteNonQuery()
        End Using
    End Sub

    ' --- Update: CategoryId対応 ---
    Public Function Update(id As Integer, updatedTool As ToolItem) As Boolean
        Using connection As New SqliteConnection(_connectionString)
            connection.Open()
            Dim command = connection.CreateCommand()
            command.CommandText = "
                UPDATE Tools
                SET Name = @Name, Storage = @Storage, IsAvailable = @IsAvailable, CategoryId = @CategoryId
                WHERE Id = @Id
            "
            command.Parameters.AddWithValue("@Name", updatedTool.Name)
            command.Parameters.AddWithValue("@Storage", updatedTool.Storage)
            command.Parameters.AddWithValue("@IsAvailable", If(updatedTool.IsAvailable, 1, 0))
            Dim catId As Integer = If(updatedTool.CategoryId <= 0, 1, updatedTool.CategoryId)
            command.Parameters.AddWithValue("@CategoryId", catId)
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

    ' トランザクションを使った安全なカテゴリ削除
    Public Function DeleteCategorySafety(categoryId As Integer) As Boolean
        ' デフォルトカテゴリ（ID：1）は削除不可
        If categoryId = 1 Then Return False
        Using connection As New SqliteConnection(_connectionString)
            connection.Open()
            ' トランザクション開始
            Using transaction = connection.BeginTransaction()
                Try
                    ' 処理1：削除対象カテゴリの工具を初期カテゴリ（ID:1）へ付け替え
                    Dim updateCmd = connection.CreateCommand()
                    updateCmd.Transaction = transaction
                    updateCmd.CommandText = "UPDATE Tools SET CategoryId = 1 WHERE CategoryId = @CategoryId"
                    updateCmd.Parameters.AddWithValue("@CategoryId", categoryId)
                    updateCmd.ExecuteNonQuery()
                    ' 処理2：Categoriesテーブルから該当カテゴリを削除
                    Dim deleteCmd = connection.CreateCommand()
                    deleteCmd.Transaction = transaction
                    deleteCmd.CommandText = "DELETE FROM Categories WHERE Id = @Id"
                    deleteCmd.Parameters.AddWithValue("@Id", categoryId)
                    Dim rowsAffected As Integer = deleteCmd.ExecuteNonQuery()
                    ' コミット（確定）
                    transaction.Commit()
                    Return rowsAffected > 0
                Catch ex As Exception
                    ' ロールバック（キャンセル）
                    transaction.Rollback()
                    Throw
                End Try
            End Using
        End Using
    End Function
End Class
