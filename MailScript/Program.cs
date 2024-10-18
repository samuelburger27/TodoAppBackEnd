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
            SendMails(todoDic, server, username, password);
        }
    }

    private static async Task<Dictionary<String, List<Todo>>> GetTodos(ApplicationDbContext db)
    {
        var currDateTime = DateTime.Now;

        var query = from e in db.Users
            join d in db.Todos on e.Id equals d.user_id
            select new { e.Email, d.title, d.body, d.is_complete, d.deadline, d.notify_before_minutes };

        var data = await query.Where(d => !d.is_complete && d.notify_before_minutes != null) 
            .ToListAsync();
        // cant use Subtract on server side
        data = data.Where(d => d.deadline.Subtract(currDateTime).TotalMinutes <= d.notify_before_minutes).ToList();

        Dictionary<string, List<Todo>> result = new();
        foreach (var val in data)
        {
            Todo todo = new()
            {
                title = val.title, body = val.body, deadline = val.deadline,
                notify_before_minutes = val.notify_before_minutes
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

    private static void SendMails(Dictionary<string, List<Todo>> todoDic, string server, string username, string password)
    {
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
            }
            message.Dispose();
        }
        client.Dispose();
    }
}
