namespace TodoApp;

public class Todo
{
    public int? id { get; set; }
    public string user_id { get; set; } = "";
    public string title { get; set; } = "";
    public string body { get; set; } = "";
    public bool is_complete { get; set; }
    public DateTime deadline { get; set; }
    
    public int? notify_before_minutes { get; set;  }
    
    public bool notification_already_send { get; set; }

    public void update_todo(Todo newTodo)
    {
        title = newTodo.title;
        body = newTodo.body;
        is_complete = newTodo.is_complete;
        deadline = newTodo.deadline;
        notify_before_minutes = newTodo.notify_before_minutes;
        notification_already_send = newTodo.notification_already_send;
    }
}