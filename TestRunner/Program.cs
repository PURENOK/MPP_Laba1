using MyTestingFramework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace TestRunner;

public class Program
{
    private static readonly object _consoleLock = new object();

    static void Main(string[] args)
    {
        Console.WriteLine("=== ЛАБОРАТОРНАЯ РАБОТА 3: СОБСТВЕННЫЙ DYNAMIC THREAD POOL ===");
        string baseDir = AppContext.BaseDirectory;
        string dllPath = Path.Combine(baseDir, "TaskManagerSystem.Tests.dll");

        if (!File.Exists(dllPath)) { Console.WriteLine("DLL не найдена"); return; }

        Assembly asm = Assembly.LoadFrom(dllPath);
        var baseJobs = PrepareTestJobs(asm);

        
        var jobs = new List<TestJob>();
        for (int i = 0; i < 15; i++) jobs.AddRange(baseJobs);

        Console.WriteLine($"Найдено/Сгенерировано тестов для запуска: {jobs.Count}\n");

      
        using var pool = new DynamicThreadPool(minThreads: 2, maxThreads: 10, idleTimeoutMs: 3000, hangTimeoutMs: 5000);

        Console.WriteLine("--- СЦЕНАРИЙ 1: Единичные подачи ---");
        for (int i = 0; i < 3; i++)
        {
            var job = jobs[i];
            pool.Enqueue(() => ExecuteJob(job));
            Thread.Sleep(800); 
        }

        Console.WriteLine("\n--- СЦЕНАРИЙ 2: Период бездействия (наблюдаем адаптивное сжатие) ---");
        Thread.Sleep(5000); 

        Console.WriteLine("\n--- СЦЕНАРИЙ 3: Пиковая нагрузка (шквал задач) ---");
       
        for (int i = 3; i < 43; i++)
        {
            var job = jobs[i];
            pool.Enqueue(() => ExecuteJob(job));
        }

        Console.WriteLine("\n--- СЦЕНАРИЙ 4: Добиваем оставшиеся тесты ---");
        for (int i = 43; i < jobs.Count; i++)
        {
            var job = jobs[i];
            pool.Enqueue(() => ExecuteJob(job));
            Thread.Sleep(100);
        }

       
        while (!pool.IsIdle())
        {
            Thread.Sleep(500);
        }

        Console.WriteLine("\n=============================================");
        Console.WriteLine("ВСЕ ТЕСТЫ ЗАВЕРШЕНЫ");
        Console.ReadLine();
    }

   
    static List<TestJob> PrepareTestJobs(Assembly asm)
    {
        var jobs = new List<TestJob>();
        foreach (var type in asm.GetTypes())
        {
            var setup = type.GetMethods().FirstOrDefault(m => m.GetCustomAttribute<MyBeforeEachAttribute>() != null);
            var methods = type.GetMethods()
                .Select(m => new { Method = m, Attr = m.GetCustomAttribute<MyTestAttribute>() })
                .Where(x => x.Attr != null && !x.Attr.Skip)
                .OrderByDescending(x => x.Attr!.Priority);

            foreach (var item in methods)
            {
                var cases = item.Method.GetCustomAttributes<MyTestCaseAttribute>().ToList();
                var timeout = item.Method.GetCustomAttribute<MyTimeoutAttribute>();
                var cat = item.Method.GetCustomAttribute<MyCategoryAttribute>()?.Name ?? "General";

                var dataSets = cases.Any() ? cases.Select(c => c.Args).ToList() : new List<object[]?> { null };

                foreach (var args in dataSets)
                {
                    jobs.Add(new TestJob
                    {
                        TestType = type,
                        Method = item.Method,
                        SetupMethod = setup,
                        Attr = item.Attr!,
                        TimeoutAttr = timeout,
                        Category = cat,
                        Args = args
                    });
                }
            }
        }
        return jobs;
    }

    
    static void ExecuteJob(TestJob job)
    {
        var instance = Activator.CreateInstance(job.TestType);
        job.SetupMethod?.Invoke(instance, null);

        string argsStr = job.Args != null ? $"({string.Join(", ", job.Args)})" : "()";
        string testName = $"{job.Method.Name}{argsStr}";

        try
        {
            
            if (typeof(Task).IsAssignableFrom(job.Method.ReturnType))
            {
                var task = (Task)job.Method.Invoke(instance, job.Args)!;
                task.Wait(); 
            }
            else
            {
                job.Method.Invoke(instance, job.Args);
            }

           
            if (job.Method.Name.Contains("Delay") || job.Method.Name.Contains("Timeout"))
            {
                Thread.Sleep(6000);
            }

            SafeConsoleWrite(ConsoleColor.Green, $"[PASS] [{job.Category}] {testName} [TID: {Thread.CurrentThread.ManagedThreadId}]");
        }
        catch (Exception ex)
        {
            var realError = ex.InnerException?.Message ?? ex.Message;
            SafeConsoleWrite(ConsoleColor.Red, $"[FAIL] [{job.Category}] {testName} [TID: {Thread.CurrentThread.ManagedThreadId}]: {realError}");
        }
    }

    public static void SafeConsoleWrite(ConsoleColor color, string message)
    {
        lock (_consoleLock)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }

    class TestJob
    {
        public Type TestType { get; set; } = null!;
        public MethodInfo Method { get; set; } = null!;
        public MethodInfo? SetupMethod { get; set; }
        public MyTestAttribute Attr { get; set; } = null!;
        public MyTimeoutAttribute? TimeoutAttr { get; set; }
        public string Category { get; set; } = "General";
        public object[]? Args { get; set; }
    }
}