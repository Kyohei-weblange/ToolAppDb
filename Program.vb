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

        ' --- GET: 工具一覧の取得 ---
        app.MapGet("/api/tools", New Func(Of IResult)(Function()
            Dim result As New List(Of ToolItem)()

            Using connection As New SqliteConnection(connectionString)
                connection.Open()
                Dim command = connection.CreateCommand()
                command.CommandText = "SELECT Id, Name, Storage, IsAvailable FROM Tools"

                Using reader = command.ExecuteReader()
                    While reader.Read()
                        result.Add(New ToolItem With {
                            .Id = reader.GetInt32(0),
                            .Name = reader.GetString(1),
                            .Storage = reader.GetString(2),
                            .IsAvailable = If(reader.GetInt32(3) = 1, True, False)
                        })
                    End While
                End Using
            End Using

            Return Results.Ok(result)
        End Function))

        ' --- POST: 工具の新規登録 ---
        app.MapPost("/api/tools", New Func(Of ToolItem, IResult)(Function(newTool As ToolItem)
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

            Return Results.Ok()
        End Function))

        ' --- PUT: 工具情報の更新 ---
        app.MapPut("/api/tools/{id}", New Func(Of Integer, ToolItem, IResult)(Function(id As Integer, updatedTool As ToolItem)
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
