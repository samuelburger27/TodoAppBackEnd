namespace TodoApp;

public class Todo
{
    public int? id { get; set; }
    public int user_id { get; set; }
    public string title { get; set; } = "";
    public string body { get; set; } = "";
    public bool is_complete { get; set; }

    public void update_todo(Todo newTodo)
    {
        title = newTodo.title;
        body = newTodo.body;
        is_complete = newTodo.is_complete;
    }
}