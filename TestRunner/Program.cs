using MyTestingFramework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace TestRunner;

class Program
{
    
    private static readonly object _consoleLock = new object();

    
    private static readonly int MaxDegreeOfParallelism = 4;

    static async Task Main(string[] args)
    {
        Console.WriteLine("=== ЗАГРУЗКА ТЕСТОВ ===");
        string baseDir = AppContext.BaseDirectory;
        string dllPath = Path.Combine(baseDir, "TaskManagerSystem.Tests.dll");

        if (!File.Exists(dllPath)) { Console.WriteLine("DLL не найдена"); return; }

        Assembly asm = Assembly.LoadFrom(dllPath);
        var jobs = PrepareTestJobs(asm);

        Console.WriteLine($"Найдено тестов для запуска: {jobs.Count}\n");

       
        Console.WriteLine("--- СЕКВЕНЦИАЛЬНЫЙ ЗАПУСК (1 поток) ---");
        var swSeq = Stopwatch.StartNew();
        foreach (var job in jobs)
        {
            await ExecuteJobAsync(job);
        }
        swSeq.Stop();

        Console.WriteLine("\nОчистка консоли для параллельного запуска через 3 секунды...");
        await Task.Delay(3000);
        Console.Clear();

       
        Console.WriteLine($"--- ПАРАЛЛЕЛЬНЫЙ ЗАПУСК (Макс. потоков: {MaxDegreeOfParallelism}) ---");
        var swPar = Stopwatch.StartNew();
        var semaphore = new SemaphoreSlim(MaxDegreeOfParallelism);

        var parallelTasks = jobs.Select(async job =>
        {
            await semaphore.WaitAsync();
            try
            {
                await ExecuteJobAsync(job);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(parallelTasks);
        swPar.Stop();

       
        Console.WriteLine("\n=============================================");
        Console.WriteLine("ОТЧЕТ ОБ ЭФФЕКТИВНОСТИ:");
        Console.WriteLine($"Последовательно: {swSeq.ElapsedMilliseconds} мс");
        Console.WriteLine($"Параллельно:     {swPar.ElapsedMilliseconds} мс");

        double speedup = (double)swSeq.ElapsedMilliseconds / swPar.ElapsedMilliseconds;
        Console.WriteLine($"Ускорение в:     {Math.Round(speedup, 2)} раз(а)");
        Console.WriteLine("=============================================");
        Console.ReadLine();
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

    
    static async Task ExecuteJobAsync(TestJob job)
    {
        
        var instance = Activator.CreateInstance(job.TestType);
        job.SetupMethod?.Invoke(instance, null);

        string argsStr = job.Args != null ? $"({string.Join(", ", job.Args)})" : "()";
        string testName = $"{job.Method.Name}{argsStr}";

        try
        {
            
            Task testExecution = typeof(Task).IsAssignableFrom(job.Method.ReturnType)
                ? (Task)job.Method.Invoke(instance, job.Args)!
                : Task.Run(() => job.Method.Invoke(instance, job.Args));

           
            if (job.TimeoutAttr != null)
            {
                var timeoutTask = Task.Delay(job.TimeoutAttr.Milliseconds);
                var completedTask = await Task.WhenAny(testExecution, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    throw new TimeoutException($"Превышен лимит времени в {job.TimeoutAttr.Milliseconds} мс");
                }
            }

            await testExecution; 

            SafeConsoleWrite(ConsoleColor.Green, $"[PASS] [{job.Category}] {testName} [TID: {Thread.CurrentThread.ManagedThreadId}]");
        }
        catch (Exception ex)
        {
            var realError = ex.InnerException?.Message ?? ex.Message;
            SafeConsoleWrite(ConsoleColor.Red, $"[FAIL] [{job.Category}] {testName} [TID: {Thread.CurrentThread.ManagedThreadId}]: {realError}");
        }
    }

   
    static void SafeConsoleWrite(ConsoleColor color, string message)
    {
        lock (_consoleLock)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
}