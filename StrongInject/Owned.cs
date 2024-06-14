using System;
using System.Threading;
using System.Threading.Tasks;

namespace StrongInject
{
    public interface IOwned<out T> : IDisposable
    {
        T Value { get; }
    }

    public sealed class Owned<T> : IOwned<T>
    {
        private Action? _dispose;

        public Owned(T value, Action? dispose)
        {
            Value = value;
            _dispose = dispose;
        }

        public T Value { get; }

        public void Dispose()
        {
            Interlocked.Exchange(ref _dispose, null)?.Invoke();
        }
    }

    public interface IAsyncOwned<out T> : IAsyncDisposable
    {
        T Value { get; }
    }

    public sealed class AsyncOwned<T> : IAsyncOwned<T>
    {
        private Func<ValueTask>? _dispose;

        public AsyncOwned(T value, Func<ValueTask>? dispose)
        {
            Value = value;
            _dispose = dispose;
        }

        public T Value { get; }

        public ValueTask DisposeAsync()
        {
            return Interlocked.Exchange(ref _dispose, null)?.Invoke() ?? default;
        }
    }
}