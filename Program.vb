Imports System
Imports Microsoft.AspNetCore.Builder
Imports Microsoft.Data.Sqlite

Module Program
    Sub Main(args As String())
        Dim builder = WebApplication.CreateBuilder(args)
        Dim app = builder.Build()
        Dim connectionString As String = "Data Source=tools.db"

        app.useDefaultFiles()
        app.useStaticFiles()

        Using connection As New SqliteConnection(connectionString)
            Connection.Open()
            Dim command = connection.CreateCommand()
            command.CommandText = "
            CREATE TABLE IF NOT EXISTS Tools (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Storage TEXT NOT NULL,
                IsAvailable INTEGER NOT NULL
                )
            "
            command.ExecuteNonQuery()
        End Using
        app.Run()
    End Sub
End Module
