using System.Collections.Concurrent;

namespace Pouchy.Helpers
{
    /// <summary>
    /// A background STA thread for COM work (shell thumbnail handlers expect STA).
    /// <see cref="Invoke{T}"/> blocks the caller, so don't call it from the UI thread.
    /// </summary>
    public sealed class StaWorker : IDisposable
    {
        private readonly BlockingCollection<Action> _queue = new();
        private readonly Thread _thread;

        public StaWorker(string name)
        {
            _thread = new Thread(Run) { IsBackground = true, Name = name };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        public T Invoke<T>(Func<T> work)
        {
            if (Thread.CurrentThread == _thread) return work();

            var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            _queue.Add(() =>
            {
                try
                {
                    result.SetResult(work());
                }
                catch (Exception ex)
                {
                    result.SetException(ex);
                }
            });
            return result.Task.GetAwaiter().GetResult();
        }

        public void Dispose() => _queue.CompleteAdding();

        private void Run()
        {
            foreach (var work in _queue.GetConsumingEnumerable())
            {
                work();
            }
        }
    }
}
