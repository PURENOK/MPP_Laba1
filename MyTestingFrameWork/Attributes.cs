using System;

namespace MyTestingFramework;

// Обновленный атрибут с параметрами и свойствами
[AttributeUsage(AttributeTargets.Method)]
public class MyTestAttribute : Attribute
{
    // Свойство для установки приоритета (чем выше число, тем раньше запустится)
    public int Priority { get; set; } = 0;

    // Свойство для пропуска теста
    public bool Skip { get; set; } = false;

    // Можно добавить описание теста через конструктор (параметр атрибута)
    public string Description { get; }
    public MyTestAttribute(string description = "") => Description = description;
}

[AttributeUsage(AttributeTargets.Method)]
public class MyCategoryAttribute : Attribute
{
    public string Name { get; }
    public MyCategoryAttribute(string name) => Name = name;
}

public class MyBeforeEachAttribute : Attribute { }
public class MyAfterEachAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class)]
public class MySharedContextAttribute : Attribute { }