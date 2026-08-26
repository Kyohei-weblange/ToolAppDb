Imports Microsoft.AspNetCore.Builder
Imports Microsoft.AspNetCore.Http
Imports Microsoft.Extensions.Hosting

' --- モデルクラス定義 ---
Public Class CategoryItem
    Public Property Id As Integer
    Public Property Name As String
End Class

Public Class ToolItem
    Public Property Id As Integer
    Public Property Name As String
    Public Property Storage As String
    Public Property IsAvailable As Boolean
    Public Property CategoryId As Integer
    Public Property CategoryName As String

    Public Function Validate() As List(Of String)
        Dim errors As New List(Of String)()
        If String.IsNullOrWhiteSpace(Name) Then
            errors.Add("工具名は必須入力です。")
        End If
        If String.IsNullOrWhiteSpace(Storage) Then
            errors.Add("保管場所は必須入力です。")
        End If
        Return errors
    End Function
End Class

' --- メイン処理 ---
Module Program
    Sub Main(args As String())
        Dim builder = WebApplication.CreateBuilder(args)
        Dim app = builder.Build()

        ' 静的ファイル（wwwroot/index.html）を有効化
        app.UseDefaultFiles()
        app.UseStaticFiles()

        ' データベースの初期化とリポジトリ準備
        Dim connectionString As String = "Data Source=tools.db"
        Dim repository As New ToolRepository(connectionString)
        repository.InitializeDatabase()

        ' --- API エンドポイント定義 ---

        ' 1. カテゴリ一覧の取得
        app.MapGet("/api/categories", Function()
            Dim categories = repository.GetCategoriesAsync().GetAwaiter().GetResult()
            Return Results.Ok(categories)
        End Function)

        ' 2. 工具一覧の取得（検索対応）
        app.MapGet("/api/tools", Function(name As String, isAvailable As String, storage As String, categoryId As String)
            Dim tools = repository.GetAllAsync(name, isAvailable, storage, categoryId).GetAwaiter().GetResult()
            Return Results.Ok(tools)
        End Function)

        ' 3. 工具の新規登録
        app.MapPost("/api/tools", Function(newTool As ToolItem)
            Dim errors = newTool.Validate()
            If errors.Count > 0 Then
                Return Results.BadRequest(New With {Key .Errors = errors})
            End If

            newTool.Name = If(newTool.Name, "").Trim()
            newTool.Storage = If(newTool.Storage, "").Trim()

            repository.AddAsync(newTool).GetAwaiter().GetResult()
            Return Results.Created("/api/tools", newTool)
        End Function)

        ' 4. 工具の更新
        app.MapPut("/api/tools/{id:int}", Function(id As Integer, updatedTool As ToolItem)
            Dim errors = updatedTool.Validate()
            If errors.Count > 0 Then
                Return Results.BadRequest(New With {Key .Errors = errors})
            End If

            updatedTool.Name = If(updatedTool.Name, "").Trim()
            updatedTool.Storage = If(updatedTool.Storage, "").Trim()

            Dim success = repository.UpdateAsync(id, updatedTool).GetAwaiter().GetResult()
            If success Then
                Return Results.Ok()
            Else
                Return Results.NotFound($"ID: {id} の工具は見つかりませんでした。")
            End If
        End Function)

        ' 5. 工具の削除
        app.MapDelete("/api/tools/{id:int}", Function(id As Integer)
            Dim success = repository.DeleteAsync(id).GetAwaiter().GetResult()
            If success Then
                Return Results.Ok()
            Else
                Return Results.NotFound($"ID: {id} の工具は見つかりませんでした。")
            End If
        End Function)

        ' 6. カテゴリの安全削除
        app.MapDelete("/api/categories/{id:int}", Function(id As Integer)
            If id = 1 Then
                Return Results.BadRequest("初期カテゴリ（ID:1）は削除できません。")
            End If

            Dim success = repository.DeleteCategorySafetyAsync(id).GetAwaiter().GetResult()
            If success Then
                Return Results.Ok()
            Else
                Return Results.NotFound($"指定されたカテゴリID: {id} は見つかりませんでした。")
            End If
        End Function)

        app.Run()
    End Sub
End Module
