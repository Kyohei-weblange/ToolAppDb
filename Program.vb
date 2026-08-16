Imports System
Imports Microsoft.AspNetCore.Builder
Imports Microsoft.AspNetCore.Http
Imports Microsoft.Data.Sqlite

Public Class ToolItem
    Public Property Id As Integer
    Public Property Name As String = ""
    Public Property Storage As String = ""
    Public Property IsAvailable As Boolean
End Class

Module Program
    Sub Main(args As String())
        Dim builder = WebApplication.CreateBuilder(args)
        Dim app = builder.Build()
        ' ここからDB初期化処理
        Dim connectionString As String = "Data Source=tools.db"

        app.useDefaultFiles()
        app.useStaticFiles()

        ' Usingブロックを使用すると、処理が終わった後に自動でDBとの接続を安全に切断してくれる
        ' Using connection As New SqliteConnection(connectionString)
        '     Connection.Open()
        '     Dim command = connection.CreateCommand()
            ' Toolsテーブルが存在しない場合は作成するSQL文を実行
        '     command.CommandText = "
        '     CREATE TABLE IF NOT EXISTS Tools (
        '         Id INTEGER PRIMARY KEY AUTOINCREMENT,
        '         Name TEXT NOT NULL,
        '         Storage TEXT NOT NULL,
        '         IsAvailable INTEGER NOT NULL
        '         )
        '     "
        '     command.ExecuteNonQuery()
        ' End Using

        ' --- GET: 工具一覧の取得（検索・絞り込み対応） ---
        app.MapGet("/api/tools", New Func(Of String, String, String, IResult)(Function(name As String, isAvailable As String, storage As String)
            Dim tools As New List(Of ToolItem)()

            Using connection As New SqliteConnection(connectionString)
                connection.Open()
                Dim command = connection.CreateCommand()

                ' 条件を動的に追加しやすくするためのベースSQL（WHERE 1=1 は常に真となる定番の手法）
                Dim sql As String = "SELECT Id, Name, Storage, IsAvailable FROM Tools WHERE 1=1"

                ' 1. 工具名（Name）の部分一致検索（検索文字が渡された場合のみ）
                If Not String.IsNullOrWhiteSpace(name) Then
                    sql &= " AND Name LIKE @Name"
                    ' %文字% にすることで「部分一致（あいまい検索）」になります
                    command.Parameters.AddWithValue("@Name", "%" & name.Trim() & "%")
                End If

                ' 2. 利用状況（isAvailable）の絞り込み（"true" または "false" が渡された場合）
                If Not String.IsNullOrWhiteSpace(isAvailable) Then
                    sql &= " AND IsAvailable = @IsAvailable"
                    Dim isAvailBool As Boolean = (isAvailable.ToLower() = "true")
                    command.Parameters.AddWithValue("@IsAvailable", If(isAvailBool, 1, 0))
                End If

                ' 3.保管場所（Storage）の部分一致検索（検索文字が渡された場合のみ）
                If Not String.IsNullOrWhiteSpace(storage) Then
                    sql &= " AND Storage LIKE @Storage"
                    command.Parameters.AddWithValue("@Storage", "%" & storage.Trim() & "%")
                End IF

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

            Return Results.Ok(tools)
        End Function))

        ' --- POST: 工具の新規登録 ---
        app.MapPost("/api/tools", New Func(Of ToolItem, IResult)(Function(newTool As ToolItem)

            '工具名が空白、Null、スペースのみの場合はエラーを返す
            If String.IsNullOrWhiteSpace(newTool.Name) Then
                Return Results.BadRequest("工具名は必須です")
            ElseIf newTool.Name.Length > 50 Then
                Return Results.BadRequest("工具名は50文字以内で入力してください")
            End If

            '保管場所が空白、Null、スペースのみの場合はエラーを返す
            If String.IsNullOrWhiteSpace(newTool.Storage) Then
                Return Results.BadRequest("保管場所は必須です")
            End If

            ' 保存時にNameとStorageの前後の余分な空白を削除する
            newTool.Name = newTool.Name.Trim()
            newTool.Storage = newTool.Storage.Trim()

            Using connection As New SqliteConnection(connectionString)
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

            Return Results.OK()
        End Function))

        ' --- PUT: 工具情報の更新 ---
        app.MapPut("/api/tools/{id}", New Func(Of Integer, ToolItem, IResult)(Function(id As Integer, updatedTool As ToolItem)

            '工具名が空白、Null、スペースのみの場合はエラーを返す
            If String.IsNullOrWhiteSpace(updatedTool.Name) Then
                Return Results.BadRequest("工具名は必須です")
            ElseIf updatedTool.Name.Length > 50 Then
                Return Results.BadRequest("工具名は50文字以内で入力してください")
            End If

            '保管場所が空白、Null、スペースのみの場合はエラーを返す
            If String.IsNullOrWhiteSpace(updatedTool.Storage) Then
                Return Results.BadRequest("保管場所は必須です")
            End If

            ' 保存時にNameとStorageの前後の余分な空白を削除する
            updatedTool.Name = updatedTool.Name.Trim()
            updatedTool.Storage = updatedTool.Storage.Trim()

            Using connection As New SqliteConnection(connectionString)
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

                Dim rowsAffected = command.ExecuteNonQuery()
                If rowsAffected = 0 Then
                    Return Results.NotFound()
                End If
            End Using

            Return Results.OK()
        End Function
        ))

        ' --- DELETE: 工具の削除 ---
        app.MapDelete("/api/tools/{id}", New Func(Of Integer, IResult)(Function(id As Integer)
            Using connection As New SqliteConnection(connectionString)
                connection.Open()
                Dim command = connection.CreateCommand()
                command.CommandText = "DELETE FROM Tools WHERE Id = @Id"
                command.Parameters.AddWithValue("@Id", id)

                Dim rowAffected = command.ExecuteNonQuery()
                If rowAffected = 0 Then
                    Return Results.NotFound()
                End If
            End Using

            Return Results.OK()
        End Function
        ))

        app.Run()
    End Sub
End Module
