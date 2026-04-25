using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace TestRunner
{
    class Program
    {
        // Делегат для фильтрации тестов
        public delegate bool TestFilter(MethodInfo method);

        static readonly object _consoleLock = new object();
        public static void SafeConsoleWrite(ConsoleColor color, string message)
        {
            lock (_consoleLock)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(message);
                Console.ResetColor();
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("=== ЛАБОРАТОРНАЯ РАБОТА 4: ФИЛЬТРЫ, ПАРАМЕТРИЗАЦИЯ, СОБЫТИЯ ===");

            // 1. Инициализация пула и подписка на СОБЫТИЯ
            using var pool = new DynamicThreadPool(minThreads: 2, maxThreads: 5, idleTimeoutMs: 3000, hangTimeoutMs: 5000);

            pool.OnThreadSpawned += tid => SafeConsoleWrite(ConsoleColor.DarkGray, $"[EVENT] Поток {tid} создан");
            pool.OnThreadTerminated += (tid, reason) => SafeConsoleWrite(ConsoleColor.DarkGray, $"[EVENT] Поток {tid} удален ({reason})");
            pool.OnTaskStarted += tid => SafeConsoleWrite(ConsoleColor.DarkYellow, $"[EVENT] Поток {tid} начал работу");
            pool.OnTaskCompleted += tid => SafeConsoleWrite(ConsoleColor.DarkGreen, $"[EVENT] Поток {tid} завершил задачу");
            pool.OnPoolDisposed += () => SafeConsoleWrite(ConsoleColor.Magenta, $"[EVENT] Пул потоков уничтожен");

            // 2. Делегат фильтрации (Выбираем только тесты с Category = Math)
            TestFilter myFilter = method =>
            {
                var props = method.GetCustomAttributes<TestPropertyAttribute>();
                // Если атрибутов нет, тест не проходит фильтр. Если есть - ищем нужную категорию.
                return props.Any(p => p.Name == "Category" && p.Value == "Math");
            };

            // 3. Подготовка задач (Заменил MyTests на ManagerTests)
            // Убедись, что ManagerTests доступен (проверь public и namespace)
            var jobs = PrepareJobs(typeof(ManagerTests), myFilter);
            Console.WriteLine($"\nОтобрано тестов для запуска: {jobs.Count}\n");

            // 4. Запуск тестов
            foreach (var job in jobs)
            {
                // Исправлено: передаем саму задачу 'job', так как это уже Action
                pool.Enqueue(job);
            }

            // Ждем завершения
            while (!pool.IsIdle()) Thread.Sleep(500);
            Console.WriteLine("\n=== ВСЕ ТЕСТЫ ВЫПОЛНЕНЫ ===");
        }

        static List<Action> PrepareJobs(Type testClassType, TestFilter filter)
        {
            var jobs = new List<Action>();
            // Ищем все методы с атрибутом [Test]
            var methods = testClassType.GetMethods().Where(m => m.GetCustomAttribute<TestAttribute>() != null);

            foreach (var method in methods)
            {
                // Применяем делегат фильтрации
                if (!filter(method)) continue;

                // Проверяем параметризацию (ValueSource / yield return)
                var valueSourceAttr = method.GetCustomAttribute<ValueSourceAttribute>();
                if (valueSourceAttr != null)
                {
                    var sourceMethod = testClassType.GetMethod(valueSourceAttr.SourceMethodName, BindingFlags.Static | BindingFlags.Public);
                    if (sourceMethod != null)
                    {
                        var testCases = (IEnumerable<object[]>)sourceMethod.Invoke(null, null);
                        foreach (var args in testCases)
                        {
                            jobs.Add(() => ExecuteMethod(testClassType, method, args));
                        }
                    }
                }
                else
                {
                    // Обычный тест без параметров
                    jobs.Add(() => ExecuteMethod(testClassType, method, null));
                }
            }
            return jobs;
        }

        static void ExecuteMethod(Type classType, MethodInfo method, object[] args)
        {
            var instance = Activator.CreateInstance(classType);
            string argsStr = args != null ? $"({string.Join(", ", args)})" : "()";
            string testName = $"{method.Name}{argsStr}";

            try
            {
                method.Invoke(instance, args);
                SafeConsoleWrite(ConsoleColor.Green, $"[PASS] {testName} [TID: {Thread.CurrentThread.ManagedThreadId}]");
            }
            catch (Exception ex)
            {
                var realError = ex.InnerException?.Message ?? ex.Message;
                SafeConsoleWrite(ConsoleColor.Red, $"[FAIL] {testName} [TID: {Thread.CurrentThread.ManagedThreadId}] -> {realError}");
            }
        }
    }
}