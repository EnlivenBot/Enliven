using System;
using System.Collections.Generic;
using System.Linq;

namespace Common.Utils;

public class Disposables : IDisposable
{
    private readonly List<IDisposable> _disposables = new();

    public Disposables()
    {
    }

    public Disposables(params IDisposable[] disposables)
    {
        _disposables = disposables.ToList();
    }

    public Disposables(IEnumerable<IDisposable> disposables)
    {
        _disposables = disposables.ToList();
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables.ToList())
        {
            disposable.Dispose();
        }
    }
}