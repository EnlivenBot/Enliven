using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Common.Utils {
    public interface IDisposableBase : IDisposable {
        IObservable<DisposableBase> Disposed { get; }
        bool IsDisposed { get; }
    }
    
    public class DisposableBase : IDisposableBase {
        private readonly Subject<DisposableBase> _disposed = new Subject<DisposableBase>();
        public IObservable<DisposableBase> Disposed => _disposed.AsObservable();
        public bool IsDisposed { get; private set; }
        public virtual void Dispose() {
            if (!IsDisposed) DisposeInternal();
        }

        protected virtual void DisposeInternal() {
            IsDisposed = true;
            _disposed.OnNext(this);
            _disposed.Dispose();
        }
    }
}