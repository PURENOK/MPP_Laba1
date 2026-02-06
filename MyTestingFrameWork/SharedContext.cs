using System;
using System.Collections.Generic;

namespace MyTestingFramework;

public class SharedContext
{
    private readonly Dictionary<string, object> _data = new();
    public void Set<T>(string key, T value) => _data[key] = value!;
    public T Get<T>(string key) => (T)_data[key];
}