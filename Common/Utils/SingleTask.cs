using System;
using System.Collections.Generic;
using System.Reactive;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Discord;

#pragma warning disable 618

namespace Common.Utils;

public class SingleTask : SingleTask<Unit> {
    public SingleTask(Func<Task> action) : base(async () => {
        await action();
        return Unit.Default;
    }) {
    }

    public SingleTask(Func<SingleTaskExecutionData<Unit>, Task> action) : base(async data => {
        await action(data);
        return Unit.Default;
    }) {
    }
}

public class SingleTask<T> : SingleTask<T, Unit> {
    public SingleTask(Func<SingleTaskExecutionData<Unit>, T> action) : base(action) {
    }

    public SingleTask(Func<T> action) : base(action) {
    }

    public SingleTask(Func<Task<T>> action) : base(action) {
    }

    public SingleTask(Func<SingleTaskExecutionData<Unit>, Task<T>> action) : base(action) {
    }
}

public class SingleTask<T, TForcedArg> : DisposableBase {
    [Obsolete("Use Execute method instead this")]
    private readonly Func<SingleTaskExecutionData<TForcedArg>, Task<T>> _action;

    private readonly Channel<Unit> _signal = Channel.CreateUnbounded<Unit>(
        new UnboundedChannelOptions { SingleReader = true });

    private readonly HandyShortestTimer _handyTimer = new();
    private readonly Queue<(TaskCompletionSource<T>?, TForcedArg?)> _forcedParams = new();
    private readonly Lock _lock = new();
    private TaskCompletionSource<T>? _currentTaskCompletionSource; // Executing RIGHT NOW
    private TaskCompletionSource<T>? _dirtyTaskCompletionSource; // Dirty or waiting for execution
    private Optional<T> _lastResult = Optional<T>.Unspecified;

    public SingleTask(Func<SingleTaskExecutionData<TForcedArg>, T> action) :
        this(data => Task.FromResult(action(data))) {
    }

    public SingleTask(Func<T> action) : this(_ => Task.FromResult(action())) {
    }

    public SingleTask(Func<Task<T>> action) : this(_ => action()) {
    }

    public SingleTask(Func<SingleTaskExecutionData<TForcedArg>, Task<T>> action) {
        if (typeof(T).Name is "Task" or "Task`1")
            throw new InvalidOperationException(
                $"{nameof(SingleTask<T>)} cannot contain generic type Task. Check for await in your ctor");
        _action = action;
        _ = ExecuteActionWrapper(DisposeCancellationToken).ObserveException();
    }

    public TimeSpan? BetweenExecutionsDelay { get; set; }

    public bool IsExecuting => _currentTaskCompletionSource?.Task.IsCompleted == true;

    public bool CanBeDirty { get; set; } = true;
    public bool ShouldExecuteNonDirtyIfNothingRunning { get; set; }

    public Task WaitForCurrent() => _currentTaskCompletionSource?.Task ?? Task.CompletedTask;

    public Task<T> Execute(bool makesDirty = true, TimeSpan? delayOverride = null) {
        EnsureNotDisposed();
        makesDirty = makesDirty || !CanBeDirty;

        lock (_lock) {
            try {
                // If dirty execution planned - await it
                if (_dirtyTaskCompletionSource != null) {
                    return _dirtyTaskCompletionSource.Task;
                }

                // If nothing executing now
                if (_currentTaskCompletionSource == null) {
                    // If current call non-dirty and something already executed
                    if (!ShouldExecuteNonDirtyIfNothingRunning && !makesDirty && _lastResult.IsSpecified) {
                        return Task.FromResult(_lastResult.Value);
                    }

                    // Start execution
                    var taskCompletionSource = _dirtyTaskCompletionSource = new TaskCompletionSource<T>();
                    _signal.Writer.TryWrite(Unit.Default);
                    return taskCompletionSource.Task;
                }

                // If currently executing, but dirty execution not planned
                // If not dirty - return current execution result
                if (!makesDirty) {
                    return _currentTaskCompletionSource.Task;
                }

                // If dirty - queue dirty execution (currently dirty not planned)
                var dirtyTaskCompletionSource = _dirtyTaskCompletionSource = new TaskCompletionSource<T>();
                _signal.Writer.TryWrite(Unit.Default);
                return dirtyTaskCompletionSource.Task;
            }
            finally {
                if (delayOverride != null) _handyTimer.SetDelay((TimeSpan)delayOverride);
            }
        }
    }

    public async Task<T> ForcedExecute(TForcedArg? forcedArg) {
        EnsureNotDisposed();
        var taskCompletionSource = new TaskCompletionSource<T>();
        _forcedParams.Enqueue((taskCompletionSource, forcedArg));
        _handyTimer.SetDelay(TimeSpan.Zero);
        _signal.Writer.TryWrite(Unit.Default);
        return await taskCompletionSource.Task;
    }

    private async Task ExecuteActionWrapper(CancellationToken cancellationToken) {
        try {
            while (!cancellationToken.IsCancellationRequested) {
                if (_dirtyTaskCompletionSource is null && _forcedParams.Count == 0) {
                    try {
                        await _signal.Reader.ReadAsync(cancellationToken);
                        // Make sure what operation actually requested
                        if (_dirtyTaskCompletionSource is null && _forcedParams.Count == 0)
                            continue;
                    }
                    catch (OperationCanceledException) { break; }
                }

                if (_forcedParams.Count == 0) {
                    await Task.WhenAny(_handyTimer.TimerElapsed, WaitForDisposeAsync());
                }

                if (IsDisposed) return;

                _forcedParams.TryDequeue(out var forcedExecution);
                using var currentCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var singleTaskExecutionData = new SingleTaskExecutionData<TForcedArg>(
                    BetweenExecutionsDelay, forcedExecution.Item2, currentCts.Token);
                lock (_lock) {
                    _currentTaskCompletionSource = _dirtyTaskCompletionSource ?? forcedExecution.Item1
                        ?? new TaskCompletionSource<T>();
                    _dirtyTaskCompletionSource = null;
                }

                try {
                    var result = await _action(singleTaskExecutionData);
                    if (result is Task task) await task;
                    _lastResult = result;
                    _currentTaskCompletionSource!.SetResult(result);
                    forcedExecution.Item1?.TrySetResult(result);
                }
                catch (OperationCanceledException) when (currentCts.Token.IsCancellationRequested) {
                    _currentTaskCompletionSource?.TrySetCanceled(currentCts.Token);
                    forcedExecution.Item1?.TrySetCanceled(currentCts.Token);
                }
                catch (Exception e) {
                    _currentTaskCompletionSource!.SetException(e);
                    forcedExecution.Item1?.TrySetException(e);
                }

                lock (_lock) {
                    _currentTaskCompletionSource = null;
                    if (IsDisposed) return;
                    _handyTimer.Reset();
                    var delay = singleTaskExecutionData.OverrideDelay.GetValueOrDefault(BetweenExecutionsDelay ??
                        TimeSpan.Zero);
                    _handyTimer.SetDelay(delay);
                }
            }
        }
        finally {
            var exception = new TaskCanceledException("Task cancelled due to SingleTask disposing");
            _currentTaskCompletionSource?.TrySetException(exception);
            _dirtyTaskCompletionSource?.TrySetException(exception);
            while (_forcedParams.TryDequeue(out var forcedExecution)) {
                forcedExecution.Item1?.TrySetException(exception);
            }
        }
    }

    protected override void DisposeInternal() {
        _handyTimer.Dispose();
    }
}

public class SingleTaskExecutionData<TForcedParam>(
    TimeSpan? betweenExecutionsDelay,
    TForcedParam? parameter,
    CancellationToken cancellationToken) {
    public TimeSpan? BetweenExecutionsDelay { get; } = betweenExecutionsDelay;
    public TimeSpan? OverrideDelay { get; set; }
    public TForcedParam? Parameter { get; } = parameter;
    public CancellationToken CancellationToken { get; } = cancellationToken;
}