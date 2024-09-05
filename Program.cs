using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TodoApp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("WebApiDatabase")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(config =>
{
    config.DocumentName = "TodoAPI";
    config.Title = "TodoAPI v1";
    config.Version = "v1";
});

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUi(config =>
    {
        config.DocumentTitle = "TodoAPI";
        config.Path = "/swagger";
        config.DocumentPath = "/swagger/{documentName}/swagger.json";
        config.DocExpansion = "list";
    });
}

app.MapGet("/todoitems", async (AppDbContext db) => 
    await db.Todos.ToListAsync());

app.MapGet("/todoitems/complete", async (AppDbContext db) =>
    await db.Todos.Where(t => t.is_complete).ToListAsync());

app.MapGet("/todoitems/{id}", async (int id, AppDbContext db) =>
{
    var todo = await db.Todos.FindAsync(id);
    if (todo is null)
        return Results.NotFound();
    return Results.Ok(todo);
});

app.MapPost("/todoitems", async (Todo todo, AppDbContext db) =>
{
    todo.id = null;
    db.Todos.Add(todo);
    await db.SaveChangesAsync();

    return Results.Ok(todo);
});

app.MapPut("/todoitems/{id}", async (int id, Todo inputTodo, AppDbContext db) =>
{
    var todo = await db.Todos.FindAsync(id);
    if (todo is null)
        return Results.NotFound();
    
    todo.update_todo(inputTodo);

    await db.SaveChangesAsync();

    return Results.NoContent();
});

app.MapDelete("/todoitems/{id}", async (int id, AppDbContext db) =>
{
    var todo = await db.Todos.FindAsync(id);
    if (todo is null)
        return Results.NotFound();

    db.Todos.Remove(todo);

    await db.SaveChangesAsync();

    return Results.NoContent();
});


app.Run();