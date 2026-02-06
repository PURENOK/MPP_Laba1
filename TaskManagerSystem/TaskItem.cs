namespace TaskManagerSystem;

public class TaskItem
{
    public string Title { get; set; } = "";
    public int Priority { get; set; } // 1 - Низкий, 5 - Высокий
    public bool IsCompleted { get; set; } = false;
}