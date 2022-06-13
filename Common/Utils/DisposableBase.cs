using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;

namespace Common.Utils {
    public interface IDisposableBase : IDisposable {
        Task DisposedTask { get; }
        IObservable<IDisposableBase> Disposed { get; }
        bool IsDisposed { get; }
    }

    public abstract class DisposableBase : IDisposableBase {
        private readonly Subject<IDisposableBase> _disposed = new();
        private Task? _disposedTask;
        public Task DisposedTask => _disposedTask ??= Disposed.ToTask();
        public IObservable<IDisposableBase> Disposed => _disposed.AsObservable();
        public bool IsDisposed { get; private set; }
        public virtual void Dispose() {
            if (IsDisposed) return;
            IsDisposed = true;
            try {
                DisposeInternal();
            }
            finally {
                _disposed.OnNext(this);
                _disposed.OnCompleted();
                _disposed.Dispose();
            }
        }

        protected void EnsureNotDisposed() {
            if (IsDisposed) throw new ObjectDisposedException(GetType().Name);
        }

        protected T EnsureNotDisposedAndReturn<T>(Func<T> func) {
            if (IsDisposed) throw new ObjectDisposedException(GetType().Name);
            return func();
        }

        protected abstract void DisposeInternal();
    }

    public abstract class AsyncDisposableBase : IDisposableBase, IAsyncDisposable {
        private readonly Subject<IDisposableBase> _disposed = new();
        private Task? _disposedTask;
        public Task DisposedTask => _disposedTask ??= Disposed.ToTask();
        public IObservable<IDisposableBase> Disposed => _disposed.AsObservable();
        public bool IsDisposed { get; private set; }
        public virtual void Dispose() {
            DisposeAsync().AsTask().Wait();
        }

        public async ValueTask DisposeAsync() {
            if (IsDisposed) return;
            IsDisposed = true;
            try {
                await DisposeInternalAsync();
            }
            finally {
                _disposed.OnNext(this);
                _disposed.OnCompleted();
                _disposed.Dispose();
            }
        }

        protected void EnsureNotDisposed() {
            if (IsDisposed) throw new ObjectDisposedException(GetType().Name);
        }

        protected T EnsureNotDisposedAndReturn<T>(Func<T> func) {
            if (IsDisposed) throw new ObjectDisposedException(GetType().Name);
            return func();
        }

        protected abstract Task DisposeInternalAsync();
    }
}