using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace TestRunner
{
    public class DynamicThreadPool : IDisposable
    {
        // --- СОБЫТИЯ ЖИЗНЕННОГО ЦИКЛА ---
        public event Action<int> OnThreadSpawned;
        public event Action<int, string> OnThreadTerminated;
        public event Action<int> OnTaskStarted;
        public event Action<int> OnTaskCompleted;
        public event Action OnPoolDisposed;

        private readonly int _minThreads;
        private readonly int _maxThreads;
        private readonly int _idleTimeoutMs;
        private readonly int _hangTimeoutMs;

        private readonly object _lock = new object();
        private readonly Queue<Action> _queue = new Queue<Action>();
        private readonly List<Thread> _threads = new List<Thread>();
        private readonly Dictionary<Thread, DateTime> _workerStates = new Dictionary<Thread, DateTime>();

        private int _idleThreadsCount = 0;
        private bool _isDisposed = false;
        private Thread _supervisorThread;

        public DynamicThreadPool(int minThreads, int maxThreads, int idleTimeoutMs, int hangTimeoutMs)
        {
            _minThreads = minThreads;
            _maxThreads = maxThreads;
            _idleTimeoutMs = idleTimeoutMs;
            _hangTimeoutMs = hangTimeoutMs;

            for (int i = 0; i < _minThreads; i++) SpawnWorker();

            _supervisorThread = new Thread(SupervisorLoop) { IsBackground = true, Name = "PoolSupervisor" };
            _supervisorThread.Start();
        }

        public void Enqueue(Action task)
        {
            lock (_lock)
            {
                if (_isDisposed) throw new ObjectDisposedException(nameof(DynamicThreadPool));
                _queue.Enqueue(task);

                if (_idleThreadsCount == 0 && _threads.Count < _maxThreads)
                    SpawnWorker();

                Monitor.Pulse(_lock);
            }
        }

        private void SpawnWorker()
        {
            var thread = new Thread(WorkerLoop) { IsBackground = true };
            _threads.Add(thread);
            _workerStates[thread] = DateTime.MinValue;
            thread.Start();

            // Вызов события
            OnThreadSpawned?.Invoke(thread.ManagedThreadId);
        }

        private void WorkerLoop()
        {
            var currentThread = Thread.CurrentThread;

            while (true)
            {
                Action task = null!;
                lock (_lock)
                {
                    while (_queue.Count == 0 && !_isDisposed)
                    {
                        _idleThreadsCount++;
                        bool signaled = Monitor.Wait(_lock, _idleTimeoutMs);
                        _idleThreadsCount--;

                        if (_isDisposed) return;

                        if (!signaled && _threads.Count > _minThreads)
                        {
                            RemoveWorker(currentThread, "Простой (Idle)");
                            return;
                        }
                    }

                    if (_isDisposed) return;
                    task = _queue.Dequeue();
                }

                try
                {
                    lock (_lock) _workerStates[currentThread] = DateTime.UtcNow;

                    OnTaskStarted?.Invoke(currentThread.ManagedThreadId); // Событие
                    task();
                    OnTaskCompleted?.Invoke(currentThread.ManagedThreadId); // Событие
                }
                catch (ThreadInterruptedException)
                {
                    return;
                }
                catch (Exception) { /* Игнорируем ошибки тестов в пуле */ }
                finally
                {
                    lock (_lock)
                    {
                        if (_workerStates.ContainsKey(currentThread))
                            _workerStates[currentThread] = DateTime.MinValue;
                    }
                }
            }
        }

        private void SupervisorLoop()
        {
            while (!_isDisposed)
            {
                Thread.Sleep(1000);
                lock (_lock)
                {
                    if (_isDisposed) break;
                    var now = DateTime.UtcNow;
                    var hungThreads = _workerStates
                        .Where(kvp => kvp.Value != DateTime.MinValue && (now - kvp.Value).TotalMilliseconds > _hangTimeoutMs)
                        .Select(kvp => kvp.Key)
                        .ToList();

                    foreach (var hungThread in hungThreads)
                    {
                        RemoveWorker(hungThread, "Зависание (Hang)");
                        hungThread.Interrupt();
                        SpawnWorker();
                    }
                }
            }
        }

        private void RemoveWorker(Thread thread, string reason)
        {
            _threads.Remove(thread);
            _workerStates.Remove(thread);
            OnThreadTerminated?.Invoke(thread.ManagedThreadId, reason); // Событие
        }

        public void Dispose()
        {
            _isDisposed = true;
            OnPoolDisposed?.Invoke(); // Событие
            lock (_lock) Monitor.PulseAll(_lock);
        }

        public bool IsIdle()
        {
            lock (_lock) return _queue.Count == 0 && _idleThreadsCount == _threads.Count;
        }
    }
}