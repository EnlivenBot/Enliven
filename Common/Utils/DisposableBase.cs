using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace Common.Utils {
    public interface IDisposableBase : IDisposable {
        IObservable<IDisposableBase> Disposed { get; }
        bool IsDisposed { get; }
    }

    public abstract class DisposableBase : IDisposableBase {
        private readonly Subject<IDisposableBase> _disposed = new();
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

        protected abstract void DisposeInternal();
    }

    public abstract class AsyncDisposableBase : IDisposableBase, IAsyncDisposable {
        private readonly Subject<IDisposableBase> _disposed = new();
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

        protected abstract Task DisposeInternalAsync();
    }
}