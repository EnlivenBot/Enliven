using System;
using System.Threading.Tasks;

namespace Common.Utils;

public interface IDisposableBase : IDisposable
{
    bool IsDisposed { get; }
    Task WaitForDisposeAsync();
}

public abstract class DisposableBase : IDisposableBase
{
    private TaskCompletionSource _disposeTask = new();
    public Task WaitForDisposeAsync() => _disposeTask.Task;
    public bool IsDisposed { get; private set; }

    public virtual void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        GC.SuppressFinalize(this);
        try
        {
            DisposeInternal();
        }
        finally
        {
            _disposeTask.TrySetResult();
        }
    }

    protected void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
    }

    protected T EnsureNotDisposedAndReturn<T>(Func<T> func)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return func();
    }

    protected abstract void DisposeInternal();
}

public abstract class AsyncDisposableBase : IDisposableBase, IAsyncDisposable
{
    private TaskCompletionSource _disposeTask = new();

    public async ValueTask DisposeAsync()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        GC.SuppressFinalize(this);
        try
        {
            await DisposeInternalAsync();
        }
        finally
        {
            _disposeTask.TrySetResult();
        }
    }

    public Task WaitForDisposeAsync() => _disposeTask.Task;
    public bool IsDisposed { get; private set; }

    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
        DisposeAsync().AsTask().Wait();
    }

    protected void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
    }

    protected T EnsureNotDisposedAndReturn<T>(Func<T> func)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return func();
    }

    protected abstract Task DisposeInternalAsync();
}