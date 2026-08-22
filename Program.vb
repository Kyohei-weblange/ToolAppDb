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
        app.MapGet("/api/tools", New Func(Of String, String, String, String,IResult)(Function(name As String, isAvailable As String, storage As String, categoryId As String)
            Dim tools = repository.GetAll(name, isAvailable, storage, categoryId)
            Return Results.Ok(tools)
        End Function))

        ' GET: カテゴリ一覧の取得
        app.MapGet("/api/categories", New Func(Of IResult)(Function()
            Dim categories = repository.GetCategories()
            Return Results.Ok(categories)
        End Function))

        ' --- POST: 工具の新規登録 ---
        app.MapPost("/api/tools", New Func(Of ToolItem, IResult)(Function(newTool As ToolItem)
            ' 入力チェック実行
            Dim errors = newTool.Validate()
            If errors.Count > 0 Then
                ' エラーがある場合は400 Bad Request を返す
                Return Results.BadRequest(New With {Key .Errors = errors})
            End If

            ' 保存時にNameとStorageの前後の余分な空白を削除する
            newTool.Name = newTool.Name.Trim()
            newTool.Storage = newTool.Storage.Trim()

            repository.Add(newTool)
            Return Results.Created("/api/tools", newTool)
        End Function))

        ' --- PUT: 工具情報の更新 ---
        app.MapPut("/api/tools/{id}", New Func(Of Integer, ToolItem, IResult)(Function(id As Integer, updatedTool As ToolItem)
            ' 入力チェック実行
            Dim errors = updatedTool.Validate()
            If errors.Count > 0 Then
                Return Results.BadRequest(New With {Key .Errors = errors})
            End If

            ' 保存時にNameとStorageの前後の余分な空白を削除する
            updatedTool.Name = updatedTool.Name.Trim()
            updatedTool.Storage = updatedTool.Storage.Trim()

            Dim success = repository.Update(id, updatedTool)
            If success Then
                Return Results.Ok()
            Else
                Return Results.NotFound($"ID: {id} の工具は見つかりませんでした。")
            End If
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
