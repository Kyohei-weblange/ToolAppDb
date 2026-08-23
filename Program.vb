Imports System
Imports Microsoft.AspNetCore.Builder
Imports Microsoft.AspNetCore.Http
Imports Microsoft.Data.Sqlite

Public Class ToolItem
    Public Property Id As Integer
    Public Property Name As String = ""
    Public Property Storage As String = ""
    Public Property IsAvailable As Boolean
    Public Property CategoryId As Integer
    Public Property CategoryName As String = ""

    ' 入力値検証ロジック
    Public Function Validate() As List(Of String)
        Dim errors As New List(Of String)()
        ' 工具名チェック
        If String.IsNullOrWhiteSpace(Name) Then
            errors.Add("工具名は必須項目です。")
        ElseIf Name.Trim().Length > 50 Then
            errors.Add("工具名は50文字以内で入力してください。")
        End If
        ' 保管場所のチェック
        If String.IsNullOrWhiteSpace(Storage) Then
            errors.Add("保管場所は必須項目です。")
        ElseIf Storage.Trim().Length > 50 Then
            errors.Add("保管場所は50文字以内で入力してください。")
        End If
        ' カテゴリIDのチェック
        If CategoryId <= 0 Then
            errors.Add("有効なカテゴリを選択してください。")
        End If
        Return errors
    End Function
End Class

Public Class CategoryItem
    Public Property Id As Integer
    Public Property Name As String = ""
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
        app.MapGet("/api/tools", Async Function(name As String, isAvailable As String, storage As String, categoryId As String) As Task(Of IResult)
            Dim tools = Await repository.GetAllAsync(name, isAvailable, storage, categoryId)
            Return Results.Ok(tools)
        End Function)

        ' GET: カテゴリ一覧の取得
        app.MapGet("/api/categories", Async Function() As Task(Of IResult)
            Dim categories = Await repository.GetCategoriesAsync()
            Return Results.Ok(categories)
        End Function)

        ' --- POST: 工具の新規登録 ---
        app.MapPost("/api/tools", Async Function(newTool As ToolItem) As Task(Of IResult)
            ' 入力チェック実行
            Dim errors = newTool.Validate()
            If errors.Count > 0 Then
                ' エラーがある場合は400 Bad Request を返す
                Return Results.BadRequest(New With {Key .Errors = errors})
            End If

            ' 保存時にNameとStorageの前後の余分な空白を削除する
            newTool.Name = newTool.Name.Trim()
            newTool.Storage = newTool.Storage.Trim()

            Await repository.AddAsync(newTool)
            Return Results.Created("/api/tools", newTool)
        End Function)

        ' --- PUT: 工具情報の更新 ---
        app.MapPut("/api/tools/{id}", Async Function(id As Integer, updatedTool As ToolItem) As Task(Of IResult)
            ' 入力チェック実行
            Dim errors = updatedTool.Validate()
            If errors.Count > 0 Then
                Return Results.BadRequest(New With {Key .Errors = errors})
            End If

            ' 保存時にNameとStorageの前後の余分な空白を削除する
            updatedTool.Name = updatedTool.Name.Trim()
            updatedTool.Storage = updatedTool.Storage.Trim()

            Dim success = Await repository.UpdateAsync(id, updatedTool)
            If success Then
                Return Results.Ok()
            Else
                Return Results.NotFound($"ID: {id} の工具は見つかりませんでした。")
            End If
        End Function
        )

        ' --- DELETE: 工具の削除 ---
        app.MapDelete("/api/tools/{id}", Async Function(id As Integer) As Task(Of IResult)
            Dim success As Boolean = Await repository.DeleteAsync(id)
            If Not success Then
                Return Results.NotFound($"ID: {id} の工具は見つかりませんでした。")
            End If

            Return Results.Ok()
        End Function
        )

        ' DELETE：カテゴリの安全削除（トランザクション実行）
        app.MapDelete("/api/categories/{id:int}", Async Function(id As Integer) As Task(Of IResult)
            If id = 1 Then
                Return Results.BadRequest("初期カテゴリ（ID:1）は削除できません。")
            End If

            Dim success = Await repository.DeleteCategorySafetyAsync(id)
            If success Then
                Return Results.Ok()
            Else
                Return Results.NotFound($"指定されたカテゴリID: {id} は見つかりませんでした。")
            End If
        End Function)

        app.Run()
    End Sub
End Module
