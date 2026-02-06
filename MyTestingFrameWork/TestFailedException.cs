using System;

namespace MyTestingFramework;

public class TestFailedException : Exception
{
    public TestFailedException(string message) : base(message) { }
}