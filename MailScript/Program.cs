using System.Globalization;
using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Shared;
using TodoApp;

namespace MailScript;
class Program
{
    public static async Task Main()
    {
        // Run this script every 15 minutes
        // First get all Todos to send notification to
        //  Send mail to email associated with the account
        // Change notification_already_send in database for every notified todo 
        var ex = new Exception("Environment Variable not provided");
        string connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING") ?? throw ex;
        string server = Environment.GetEnvironmentVariable("SMTP_SERVER") ?? throw ex;
        string username = Environment.GetEnvironmentVariable("SMTP_USERNAME") ?? throw ex;
        string password = Environment.GetEnvironmentVariable("SMTP_PASSWORD") ?? throw ex;
        
        DbContextOptionsBuilder<ApplicationDbContext> options = new();
        options.UseNpgsql(connectionString);
        var context = new ApplicationDbContext(options.Options);

        var todoDic = await GetTodos(context);

        if (todoDic.Count > 0)
        {
            var notified = SendMails(todoDic, server, username, password);
            await ChangeNotificationAlreadySend(context, notified);
        }
        await context.DisposeAsync();
    }

    private static async Task<Dictionary<String, List<Todo>>> GetTodos(ApplicationDbContext db)
    {
        var currDateTime = DateTime.Now;

        var query = from usr in db.Users
            join todo in db.Todos on usr.Id equals todo.user_id
            select new { usr.Email, todo.id, todo.user_id, todo.title, todo.body, todo.is_complete, todo.deadline, 
                todo.notify_before_minutes, todo.notification_already_send };

        var data = await query.Where(d => !d.is_complete && 
                                          !d.notification_already_send && d.notify_before_minutes != null) 
            .ToListAsync();
        
        // cant use Subtract on server side
        data = data.Where(d => d.deadline.Subtract(currDateTime).TotalMinutes <= d.notify_before_minutes).ToList();
        
        Dictionary<string, List<Todo>> result = new();
        foreach (var val in data)
        {
            Todo todo = new()
            {
                id = val.id, user_id = val.user_id, title = val.title, body = val.body, is_complete = val.is_complete, deadline = val.deadline,
                notify_before_minutes = val.notify_before_minutes, notification_already_send = val.notification_already_send
            };
            if (!result.ContainsKey(val.Email))
            {
                result.Add(val.Email, new List<Todo>());
            }
            result[val.Email].Add(todo);
            
        }
        Console.WriteLine($"Got {result.Count} different users with nearing deadlines");
        return result;
    }

    private static List<Todo> SendMails(Dictionary<string, List<Todo>> todoDic, string server, string username, string password)
    {
        List<Todo> notified = new();
        int port = 587;
        string domain = username.Split('@')[1];
        
        SmtpClient client = new SmtpClient(server);
        client.Credentials = new NetworkCredential(username, password);
        client.Port = port;
        client.EnableSsl = true;
    
        var from = new MailAddress($"notify_todos@{domain}");
        foreach (var email in todoDic.Keys)
        {
            var to = new MailAddress(email);
            MailMessage message = new MailMessage(from, to);
            
            message.Subject = $"{todoDic[email].Count} Todo deadlines are approaching";
            string body = $"Todos: \n";
            foreach (var todo in todoDic[email])
            {
                body += $"\t{todo.body} Deadline: " +
                        $"{todo.deadline.ToString(new CultureInfo("de-DE"))}\n\n";
            }
            body += "Edit todos: https://app.samuelburger.me";
            message.Body = body;
            try
            {
                client.Send(message);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to send to {to} email: {e.Message}");
                message.Dispose();
                break;
            }
            notified.AddRange(todoDic[email]);
            
            message.Dispose();
        }
        client.Dispose();
        return notified;
    }

    private static async Task ChangeNotificationAlreadySend(ApplicationDbContext db, List<Todo> todos)
    {
        foreach (var todo in todos)
        {
            todo.notification_already_send = true;
        }

        db.Todos.UpdateRange(todos);
        await db.SaveChangesAsync();
    }
}
