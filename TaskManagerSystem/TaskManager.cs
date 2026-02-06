using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TaskManagerSystem;

public class TaskManager
{
    private readonly List<TaskItem> _tasks = new();

    // Асинхронное добавление с имитацией сохранения в БД
    public async Task CreateTaskAsync(string title, int priority)
    {
        await Task.Delay(10); // Имитация задержки
        if (string.IsNullOrEmpty(title)) throw new ArgumentException("Title cannot be empty");

        _tasks.Add(new TaskItem { Title = title, Priority = priority });
    }

    public void CompleteTask(string title)
    {
        var task = _tasks.Find(t => t.Title == title);
        if (task != null) task.IsCompleted = true;
    }

    public int GetTaskCount() => _tasks.Count;
    public TaskItem? GetTask(string title) => _tasks.Find(t => t.Title == title);
}