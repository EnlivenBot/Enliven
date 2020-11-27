using System;
using System.Reactive.Subjects;

namespace Common.Utils {
    public class DisposableBase : IDisposable {
        public ISubject<DisposableBase> Disposed { get; private set; } = new Subject<DisposableBase>();
        public bool IsDisposed { get; private set; }
        public void Dispose() {
            if (!IsDisposed) DisposeInternal();
        }

        protected virtual void DisposeInternal() {
            IsDisposed = true;
            Disposed.OnNext(this);
            (Disposed as IDisposable)?.Dispose();
        }
    }
}