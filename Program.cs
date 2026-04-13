using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var notes = new List<Note>();
var notesLock = new object();
var nextId = 1;

app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        status = "ok",
        time = DateTime.UtcNow
    });
})
.WithName("Health")
.WithTags("Diagnostics");

app.MapGet("/version", (IConfiguration config) =>
{
    var appName = config["App:Name"] ?? "IsLabApp";
    var appVersion = config["App:Version"] ?? "0.0.0";

    return Results.Ok(new
    {
        name = appName,
        version = appVersion
    });
})
.WithName("Version")
.WithTags("Diagnostics");

app.MapGet("/api/notes", () =>
{
    lock (notesLock)
    {
        return Results.Ok(notes.OrderByDescending(n => n.CreatedAt).ToList());
    }
})
.WithName("GetNotes")
.WithTags("Notes");

app.MapGet("/api/notes/{id:int}", (int id) =>
{
    lock (notesLock)
    {
        var note = notes.FirstOrDefault(n => n.Id == id);

        return note is null
            ? Results.NotFound(new { message = $"Note with id={id} not found" })
            : Results.Ok(note);
    }
})
.WithName("GetNoteById")
.WithTags("Notes");

app.MapPost("/api/notes", (CreateNoteRequest request) =>
{
    if (request is null)
    {
        return Results.BadRequest(new { message = "Request body is required" });
    }

    var title = request.Title?.Trim();
    var text = request.Text?.Trim();

    if (string.IsNullOrWhiteSpace(title))
    {
        return Results.BadRequest(new { message = "Title is required" });
    }

    if (title.Length > 200)
    {
        return Results.BadRequest(new { message = "Title must be 200 characters or less" });
    }

    if (text is not null && text.Length > 4000)
    {
        return Results.BadRequest(new { message = "Text must be 4000 characters or less" });
    }

    Note note;

    lock (notesLock)
    {
        note = new Note
        {
            Id = nextId++,
            Title = title,
            Text = text ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        notes.Add(note);
    }

    return Results.Created($"/api/notes/{note.Id}", note);
})
.WithName("CreateNote")
.WithTags("Notes");

app.MapDelete("/api/notes/{id:int}", (int id) =>
{
    lock (notesLock)
    {
        var note = notes.FirstOrDefault(n => n.Id == id);

        if (note is null)
        {
            return Results.NotFound(new { message = $"Note with id={id} not found" });
        }

        notes.Remove(note);
        return Results.Ok(new { message = $"Note with id={id} deleted" });
    }
})
.WithName("DeleteNote")
.WithTags("Notes");

app.MapGet("/db/ping", async (IConfiguration config) =>
{
    var connectionString = config.GetConnectionString("Mssql");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.Problem(
            title: "Database connection string is missing",
            detail: "ConnectionStrings:Mssql is not configured",
            statusCode: 500);
    }

    try
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        return Results.Ok(new
        {
            status = "ok",
            database = "reachable"
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Database connection failed",
            detail: ex.Message,
            statusCode: 500);
    }
})
.WithName("DatabasePing")
.WithTags("Diagnostics");

app.Run();

public class Note
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreateNoteRequest
{
    public string? Title { get; set; }
    public string? Text { get; set; }
}