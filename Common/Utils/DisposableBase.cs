using System;
using System.Reactive.Subjects;

namespace Common.Utils {
    public interface IDisposableBase : IDisposable {
        ISubject<DisposableBase> Disposed { get; }
        bool IsDisposed { get; }
    }
    public class DisposableBase : IDisposableBase {
        public ISubject<DisposableBase> Disposed { get; private set; } = new Subject<DisposableBase>();
        public bool IsDisposed { get; private set; }
        public virtual void Dispose() {
            if (!IsDisposed) DisposeInternal();
        }

        protected virtual void DisposeInternal() {
            IsDisposed = true;
            Disposed.OnNext(this);
            (Disposed as IDisposable)?.Dispose();
        }
    }
}