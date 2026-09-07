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
                CREATE TABLE IF NOT EXISTS ToolLogs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ToolId INTEGER NOT NULL,
                    Action TEXT NOT NULL,
                    Timestamp DATETIME NOT NULL,
                    UserName TEXT NOT NULL
                );
                "
            command.ExecuteNonQuery()
        End Using
    End Sub

    ' カテゴリ一覧の取得
    Public Async Function GetCategoriesAsync() As Task(Of List(Of CategoryItem))
        Dim categories As New List(Of CategoryItem)()
        Using connection As New SqliteConnection(_connectionString)
            Await connection.OpenAsync()
            Dim command = connection.CreateCommand()
            command.CommandText = "SELECT Id, Name FROM Categories ORDER BY Id"

            Using reader = Await command.ExecuteReaderAsync()
                While Await reader.ReadAsync()
                    categories.Add(New CategoryItem With {
                        .Id = reader.GetInt32(0),
                        .Name = reader.GetString(1)
                    })
                End While
            End Using
        End Using
        Return categories
    End Function

    ' 履歴一覧の取得
    Public Async Function GetLogsAsync() As Task(Of List(Of ToolLog))
        Dim logs As New List(Of ToolLog)()
        Using connection As New SqliteConnection(_connectionString)
            Await connection.OpenAsync()
            Dim command = connection.CreateCommand()
            command.CommandText = "SELECT ToolLogs.Id, ToolId,Tools.Name As ToolName,  Action, Timestamp, UserName FROM ToolLogs INNER JOIN Tools ON ToolLogs.ToolId = Tools.Id ORDER BY ToolLogs.Id DESC"
            Using reader = Await command.ExecuteReaderAsync()
                While Await reader.ReadAsync()
                    logs.Add(New ToolLog With {
                        .Id = reader.GetInt32(0),
                        .ToolId = reader.GetInt32(1),
                        .ToolName = reader.GetString(2),
                        .Action = reader.GetString(3),
                        .Timestamp = reader.GetDateTime(4),
                        .UserName = reader.GetString(5)
                    })
                End While
            End Using
        End Using
        Return logs
    End Function

    ' カテゴリ追加
    Public Async Function AddCategoryAsync(name As String) As Task(Of Boolean)
        Using connection As New SqliteConnection(_connectionString)
            Await connection.OpenAsync()
            Dim command = connection.CreateCommand()
            command.CommandText = "INSERT INTO Categories (Name) VALUES (@Name)"
            command.Parameters.AddWithValue("@Name", name)
            Try
                Await command.ExecuteNonQueryAsync()
                Return True
            Catch
                Return False
            End Try
        End Using
    End Function

    ' 履歴保存
    Public Async Function AddLogAsync(addLog As ToolLog) As Task(Of Boolean)
        Using connection As New SqliteConnection(_connectionString)
            Await connection.OpenAsync()
            Dim command = connection.CreateCommand()
            command.CommandText = "
                INSERT INTO ToolLogs (ToolId, Action, Timestamp, UserName)
                VALUES (@ToolId, @Action, @Timestamp, @UserName)
            "
            command.Parameters.AddWithValue("@ToolId", addLog.ToolId)
            command.Parameters.AddWithValue("@Action", addLog.Action)
            command.Parameters.AddWithValue("@Timestamp", addLog.Timestamp)
            command.Parameters.AddWithValue("@UserName", addLog.UserName)
            Try
                Await command.ExecuteNonQueryAsync()
                Return True
            Catch
                Return False
            End Try
        End Using
    End Function

    ' 新規追加（CategoryId対応）
    Public Async Function AddAsync(newTool As ToolItem) As Task(Of Integer)
        Using connection As New SqliteConnection(_connectionString)
            Await connection.OpenAsync()
            Dim command = connection.CreateCommand()
            command.CommandText = "
                INSERT INTO Tools (Name, Storage, IsAvailable,CategoryId)
                VALUES (@Name, @Storage, @IsAvailable, @CategoryId);
                SELECT Last_insert_rowid();
            "
            command.Parameters.AddWithValue("@Name", newTool.Name)
            command.Parameters.AddWithValue("@Storage", newTool.Storage)
            command.Parameters.AddWithValue("@IsAvailable", If(newTool.IsAvailable, 1, 0))
            ' CategoryIdが未指定（0など）の場合は初期値1を設定
            Dim catId As Integer = If(newTool.CategoryId <= 0, 1, newTool.CategoryId)
            command.Parameters.AddWithValue("@CategoryId", catId)
            Dim result = Await command.ExecuteScalarAsync()
            Dim newId As Integer = Convert.ToInt32(result)
            Return newId
        End Using
    End Function

    ' --- Update: CategoryId対応 ---
    Public Async Function UpdateAsync(id As Integer, updatedTool As ToolItem) As Task(Of Boolean)
        Using connection As New SqliteConnection(_connectionString)
            Await connection.OpenAsync()
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

            Dim rowsAffected As Integer = Await command.ExecuteNonQueryAsync()
            Return rowsAffected > 0
        End Using
    End Function

    ' --- Delete: Boolean を返す ---
    Public Async Function DeleteAsync(id As Integer) As Task(Of Boolean)
        Using connection As New SqliteConnection(_connectionString)
            Await connection.OpenAsync()
            Dim command = connection.CreateCommand()
            command.CommandText = "DELETE FROM Tools WHERE Id = @Id"
            command.Parameters.AddWithValue("@Id", id)

            Dim rowsAffected As Integer = Await command.ExecuteNonQueryAsync()
            Return rowsAffected > 0
        End Using
    End Function

    ' トランザクションを使った安全なカテゴリ削除
    Public Async Function DeleteCategorySafetyAsync(categoryId As Integer) As Task(Of Boolean)
        ' デフォルトカテゴリ（ID：1）は削除不可
        If categoryId = 1 Then Return False
        Using connection As New SqliteConnection(_connectionString)
            ' 非同期でDB接続を開く
            Await connection.OpenAsync()
            ' トランザクション開始
            Using transaction = connection.BeginTransaction()
                Try
                    ' 処理1：削除対象カテゴリの工具を初期カテゴリ（ID:1）へ付け替え
                    Dim updateCmd = connection.CreateCommand()
                    updateCmd.Transaction = transaction
                    updateCmd.CommandText = "UPDATE Tools SET CategoryId = 1 WHERE CategoryId = @CategoryId"
                    updateCmd.Parameters.AddWithValue("@CategoryId", categoryId)
                    ' 非同期実行
                    Await updateCmd.ExecuteNonQueryAsync()
                    ' 処理2：Categoriesテーブルから該当カテゴリを削除
                    Dim deleteCmd = connection.CreateCommand()
                    deleteCmd.Transaction = transaction
                    deleteCmd.CommandText = "DELETE FROM Categories WHERE Id = @Id"
                    deleteCmd.Parameters.AddWithValue("@Id", categoryId)
                    ' 非同期実行
                    Dim rowsAffected As Integer = Await deleteCmd.ExecuteNonQueryAsync()
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

    ' 複数条件組み合わせ検索
    Public Async Function SearchAsync(name As String, storage As String, categoryId As Integer?, isAvailable As Boolean?) As Task(Of List(Of ToolItem))
        Dim tools As New List(Of ToolItem)()
        Using connection As New SqliteConnection(_connectionString)
            Await connection.OpenAsync()
            Dim command = connection.CreateCommand()
            Dim sql As String = "
                SELECT
                    T.Id,
                    T.Name,
                    T.Storage,
                    T.IsAvailable,
                    T.CategoryId,
                    COALESCE(C.Name, '未分類') AS CategoryName FROM Tools T LEFT JOIN Categories C ON T.CategoryId = C.Id WHERE 1=1
                "

            If Not String.IsNullOrWhiteSpace(name) Then
                sql &= " AND (T.Name LIKE @name)"
            command.Parameters.AddWithValue("@name", "%" & name.Trim() & "%")
            End If
            If Not String.IsNullOrWhiteSpace(storage) Then
                sql &= " AND (T.Storage LIKE @storage)"
                command.Parameters.AddWithValue("@storage", "%" & storage.Trim() & "%")
            End If
            If categoryId.HasValue AndAlso categoryId.Value > 0 Then
                sql &= " AND (T.CategoryId = @categoryId)"
                command.Parameters.AddWithValue("@categoryId", categoryId.Value)
            End If
            If isAvailable.HasValue Then
                sql &= " AND (T.IsAvailable = @isAvailable)"
                command.Parameters.AddWithValue("@isAvailable", If(isAvailable.Value, 1, 0))
            End If

            command.CommandText = sql
            Using reader = Await command.ExecuteReaderAsync()
                While Await reader.ReadAsync()
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

End Class
