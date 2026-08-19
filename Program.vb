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
        Dim ConnectionString As String = "Data Source=tools.db"
        Dim repository As New ToolRepository(connectionString)
        repository.InitializeDatabase()

        app.useDefaultFiles()
        app.useStaticFiles()

        ' --- GET: 工具一覧の取得（検索・絞り込み対応） ---
        app.MapGet("/api/tools", New Func(Of String, String, String, IResult)(Function(name As String, isAvailable As String, storage As String)
            Dim tools = repository.GetAll(name, isAvailable, storage)
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

            repository.Add(newTool)
            Return Results.Ok()
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

            Dim success As Boolean = repository.Update(id, updatedTool)
            If Not success Then
                Return Results.NotFound($"ID: {id} の工具は見つかりませんでした。")
            End If

            Return Results.Ok()
        End Function
        ))

        ' --- DELETE: 工具の削除 ---
        app.MapDelete("/api/tools/{id}", New Func(Of Integer, IResult)(Function(id As Integer)
            Dim success As Boolean = repository.Delete(id)
            If Not success Then
                Return Results.NotFound($"ID: {id} の工具は見つかりませんでした。")
            End If

            Return Results.Ok()
        End Function
        ))

        app.Run()
    End Sub
End Module
