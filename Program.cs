using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TodoApp;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(config =>
{
    config.DocumentName = "TodoAPI";
    config.Title = "TodoAPI v1";
    config.Version = "v1";
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin",
        policy => 
                policy.WithOrigins("https://app.samuelburger.me", "http://app.samuelburger.me")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()); // Allow credentials (cookies)
});

builder.Services.AddAuthorization();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddCookie(IdentityConstants.ApplicationScheme)
    .AddBearerToken(IdentityConstants.BearerScheme);

builder.Services.AddIdentityCore<User>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddApiEndpoints();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("WebApiDatabase")));

builder.Services.AddSingleton<HttpContextAccessor, HttpContextAccessor>();

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

app.UseCors("AllowSpecificOrigin");

app.MapIdentityApi<User>();

app.MapPost("/logout", async ( HttpContext context, ApplicationDbContext db) =>
{
    //await manager.SignOutAsync().ConfigureAwait(false);
    await context.SignOutAsync(IdentityConstants.ApplicationScheme);

    return Results.Ok(new { message = "Logged out successfully" });

}).RequireAuthorization();


app.MapGet("/user", async (ClaimsPrincipal claims, ApplicationDbContext db) =>
{
    string userId = claims.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value;

    return await db.Users.FindAsync(userId);

}).RequireAuthorization();

app.MapGet("/todoitems", async (ClaimsPrincipal claims, ApplicationDbContext db) =>
{
    string userId = claims.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value;

    return await db.Todos.Where(todo => todo.user_id == userId).ToListAsync();
}).RequireAuthorization();

app.MapGet("/todoitems/complete", async (ClaimsPrincipal claims, ApplicationDbContext db) =>
{
    string userId = claims.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value;

    await db.Todos.Where(t => t.user_id == userId && t.is_complete).ToListAsync();
}).RequireAuthorization();

app.MapGet("/todoitems/{id}", async (ClaimsPrincipal claims, int id, ApplicationDbContext db) =>
{
    string userId = claims.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value;
    var todo = await db.Todos.FindAsync(id);
    if (todo is null || todo.user_id != userId)
        return Results.NotFound();
    return Results.Ok(todo);
}).RequireAuthorization();

app.MapPost("/todoitems", async (ClaimsPrincipal claims, Todo todo, ApplicationDbContext db) =>
{
    todo.id = null;
    todo.user_id = claims.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value;
    db.Todos.Add(todo);
    await db.SaveChangesAsync();

    return Results.Ok(todo);
}).RequireAuthorization();

app.MapPut("/todoitems/{id}", async (ClaimsPrincipal claims, int id, Todo inputTodo, ApplicationDbContext db) =>
{
    var userId = claims.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value;

    var todo = await db.Todos.FindAsync(id);
    if (todo is null || todo.user_id != userId)
        return Results.NotFound();
    
    todo.update_todo(inputTodo);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization();

app.MapDelete("/todoitems/{id}", async (ClaimsPrincipal claims, int id, ApplicationDbContext db) =>
{
    var userId = claims.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value;

    var todo = await db.Todos.FindAsync(id);
    if (todo is null || todo.user_id != userId)
        return Results.NotFound();

    db.Todos.Remove(todo);

    await db.SaveChangesAsync();

    return Results.NoContent();
}).RequireAuthorization();


app.Run();