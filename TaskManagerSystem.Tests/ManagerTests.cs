using System;
using System.Threading.Tasks;
using MyTestingFramework;
using TaskManagerSystem;

namespace TaskManagerSystem.Tests;

[MySharedContext]
public class ManagerTests
{
    private TaskManager _manager = null!;

    // Выполняется перед каждым тестом
    [MyBeforeEach]
    public void Setup()
    {
        _manager = new TaskManager();
    }

    // 1. Асинхронный тест + AreEqual + IsNotNull
    [MyTest]
    [MyCategory("Critical")]
    public async Task Test_CreateTask_Async_Success()
    {
        await _manager.CreateTaskAsync("Написать отчет", 5);
        var task = _manager.GetTask("Написать отчет");

        MyAssert.IsNotNull(task);
        MyAssert.AreEqual("Написать отчет", task!.Title);
        MyAssert.AreEqual(5, task.Priority);
    }

    // 2. IsTrue + логика выполнения
    [MyTest]
    [MyCategory("Logic")]
    public void Test_CompleteTask_SetsStatusTrue()
    {
        _manager.CreateTaskAsync("Тест", 1).Wait();
        _manager.CompleteTask("Тест");
        var task = _manager.GetTask("Тест");

        MyAssert.IsTrue(task!.IsCompleted);
    }

    // 3. IsFalse + AreEqual (счетчик)
    [MyTest]
    [MyCategory("Validation")]
    public void Test_InitialState_IsEmpty()
    {
        MyAssert.AreEqual(0, _manager.GetTaskCount());
        MyAssert.IsFalse(_manager.GetTaskCount() > 0);
    }

    // 4. IsNull
    [MyTest]
    [MyCategory("Validation")]
    public void Test_GetNonExistentTask_ReturnsNull()
    {
        var task = _manager.GetTask("Призрак");
        MyAssert.IsNull(task);
    }

    // 5. Contains (поиск подстроки)
    [MyTest]
    [MyCategory("Strings")]
    public void Test_TaskTitle_ContainsKeyword()
    {
        string title = "Срочная покупка молока";
        MyAssert.Contains("покупка", title);
    }

    // 6. IsEmpty (проверка пустой строки)
    [MyTest]
    [MyCategory("Strings")]
    public void Test_NewTask_ShouldNotHaveEmptyTitle()
    {
        string title = "Работа";
        MyAssert.AreNotEqual("", title);
    }

    // 7. GreaterThan (проверка приоритета)
    [MyTest]
    [MyCategory("Logic")]
    public void Test_HighPriority_IsGreaterThanLow()
    {
        int high = 5;
        int low = 1;
        MyAssert.GreaterThan(high, low);
    }

    // 8. IsInstanceOf (проверка типа)
    [MyTest]
    [MyCategory("Types")]
    public void Test_Manager_IsCorrectType()
    {
        MyAssert.IsInstanceOf<TaskManager>(_manager);
    }

    // 9. AreNotEqual
    [MyTest]
    [MyCategory("Logic")]
    public void Test_DifferentTasks_AreNotEqual()
    {
        var t1 = new TaskItem { Title = "A" };
        var t2 = new TaskItem { Title = "B" };
        MyAssert.AreNotEqual(t1, t2);
    }

    // 10. Сложный тест: Проверка исключения самой системы
    [MyTest]
    [MyCategory("Exceptions")]
    public async Task Test_EmptyTitle_ThrowsException()
    {
        bool caught = false;
        try
        {
            await _manager.CreateTaskAsync("", 1);
        }
        catch (ArgumentException)
        {
            caught = true;
        }
        MyAssert.IsTrue(caught);
    }

    // 11. НАМЕРЕННЫЙ ПРОВАЛ (для демонстрации в консоли)
    [MyTest]
    [MyCategory("Demonstration")]
    public void Test_ThisIsExpectedToFail()
    {
        // Специально проваливаем тест, чтобы показать [FAIL] в отчете
        MyAssert.AreEqual("Ожидание", "Реальность");
    }
}