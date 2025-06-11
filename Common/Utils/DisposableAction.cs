using System;
using System.Threading;

namespace Common.Utils;

public class DisposableAction(Action onDispose) : IDisposable
{
    private Action? _onDispose = onDispose;

    public void Dispose()
    {
        var onDispose = Interlocked.Exchange(ref _onDispose, null);
        onDispose?.Invoke();
    }
}