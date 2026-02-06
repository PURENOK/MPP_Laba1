using MyTestingFramework;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace TestRunner;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
         
            string baseDir = AppContext.BaseDirectory;
            string dllPath = Path.Combine(baseDir, "TaskManagerSystem.Tests.dll");

            if (!File.Exists(dllPath))
            {
                Console.WriteLine($"Файл не найден: {dllPath}");
                Console.WriteLine("Нажмите Enter для выхода...");
                Console.ReadLine();
                return;
            }

           
            Assembly asm = Assembly.LoadFrom(dllPath);
            Console.WriteLine($"=== Запуск тестов: {asm.GetName().Name} ===\n");

            foreach (var type in asm.GetTypes())
            {
                var tests = type.GetMethods()
                    .Where(m => m.GetCustomAttribute<MyTestAttribute>() != null)
                    .ToList();

                if (tests.Count == 0) continue;

                var instance = Activator.CreateInstance(type);
                var beforeEach = type.GetMethods().FirstOrDefault(m => m.GetCustomAttribute<MyBeforeEachAttribute>() != null);

                foreach (var test in tests)
                {
                    string cat = test.GetCustomAttribute<MyCategoryAttribute>()?.Name ?? "General";

                    Console.ResetColor(); 

                    try
                    {
                     
                        beforeEach?.Invoke(instance, null);

                      
                        if (typeof(Task).IsAssignableFrom(test.ReturnType))
                        {
                            await (Task)test.Invoke(instance, null)!;
                        }
                        else
                        {
                            test.Invoke(instance, null);
                        }

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[PASS] [{cat}] {test.Name}");
                    }
                   
                    catch (TargetInvocationException ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                   
                        var realError = ex.InnerException?.Message ?? ex.Message;
                        Console.WriteLine($"[FAIL] [{cat}] {test.Name}: {realError}");
                    }
                    catch (Exception ex)
                    {
             
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[ERR ] [{cat}] {test.Name}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception globalEx)
        {
          
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine($"\nКРИТИЧЕСКАЯ ОШИБКА РАННЕРА: {globalEx.Message}");
        }
        finally
        {
          
            Console.ResetColor();
            Console.WriteLine("\n=============================================");
            Console.WriteLine("Тестирование завершено. Нажмите ENTER для выхода.");
            Console.WriteLine("=============================================");
            Console.ReadLine(); 
        }
    }
}