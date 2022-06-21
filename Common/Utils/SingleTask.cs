using System;
using System.Reactive;
using System.Threading.Tasks;
using Discord;

#pragma warning disable 618

namespace Common.Utils {
    public class SingleTask : SingleTask<Unit> {
        public SingleTask(Func<Task> action) : base(async () => {
            await action();
            return Unit.Default;
        }) { }
        public SingleTask(Func<SingleTaskExecutionData, Task> action) : base(async data => {
            await action(data);
            return Unit.Default;
        }) { }
    }

    public class SingleTask<T> : DisposableBase {
        [Obsolete("Use Execute method instead this")]
        private readonly Func<SingleTaskExecutionData, Task<T>> _action;

        private readonly HandyShortestTimer _handyTimer = new();
        private TaskCompletionSource<T>? _currentTaskCompletionSource;
        private TaskCompletionSource<T>? _dirtyTaskCompletionSource;
        private Optional<T> _lastResult = Optional<T>.Unspecified;
        private readonly object _lock = new();

        public SingleTask(Func<SingleTaskExecutionData, T> action) : this(data => Task.FromResult(action(data))) { }

        public SingleTask(Func<T> action) : this(data => Task.FromResult(action())) { }

        public SingleTask(Func<Task<T>> action) : this(data => action()) { }

        public SingleTask(Func<SingleTaskExecutionData, Task<T>> action) {
            if (typeof(T).Name is "Task" or "Task`1")
                throw new InvalidOperationException($"{nameof(SingleTask<T>)} cannot contain generic type Task. Check for await in your ctor");
            _action = action;
        }

        public TimeSpan? BetweenExecutionsDelay { get; set; }

        public bool IsExecuting => _currentTaskCompletionSource?.Task.IsCompleted == true;

        public bool CanBeDirty { get; set; } = true;
        
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
                        // If current call non dirty and something already executed
                        if (!makesDirty && _lastResult.IsSpecified) {
                            return Task.FromResult(_lastResult.Value);
                        }
                        // Start execution
                        var taskCompletionSource = _currentTaskCompletionSource = new TaskCompletionSource<T>();
                        _ = ExecuteActionWrapper();
                        return taskCompletionSource.Task;
                    }

                    // If currently executing, but dirty execution not planned
                    // If not dirty - return current execution result
                    if (!makesDirty) {
                        return _currentTaskCompletionSource.Task;
                    }
            
                    // If dirty - queue dirty execution
                    var dirtyTaskCompletionSource = _dirtyTaskCompletionSource = new TaskCompletionSource<T>();
                    return dirtyTaskCompletionSource.Task;
                }
                finally {
                    if (delayOverride != null) _handyTimer.SetDelay((TimeSpan)delayOverride);
                }
            }
        }

        private async Task ExecuteActionWrapper() {
            try {
                EnsureNotDisposed();
                do {
                    await Task.WhenAny(_handyTimer.TimerElapsed, DisposedTask);
                    if (IsDisposed) return;

                    var singleTaskExecutionData = new SingleTaskExecutionData(BetweenExecutionsDelay);
                    try {
                        var result = await _action(singleTaskExecutionData);
                        if (result is Task task) await task;
                        _lastResult = result;
                        _currentTaskCompletionSource!.SetResult(result);
                    }
                    catch (Exception e) {
                        _currentTaskCompletionSource!.SetException(e);
                    }

                    if (IsDisposed) return;
                    _handyTimer.Reset();
                    var delay = singleTaskExecutionData.OverrideDelay.GetValueOrDefault(BetweenExecutionsDelay ?? TimeSpan.Zero);
                    _handyTimer.SetDelay(delay);

                    lock (_lock) {
                        _currentTaskCompletionSource = _dirtyTaskCompletionSource;
                        if (_dirtyTaskCompletionSource == null) break;
                        _dirtyTaskCompletionSource = null;
                    }
                } while (!IsDisposed);
            }
            finally {
                _currentTaskCompletionSource?.TrySetException(new TaskCanceledException("Task cancelled due to SingleTask disposing"));
                _dirtyTaskCompletionSource?.TrySetException(new TaskCanceledException("Task cancelled due to SingleTask disposing"));
            }
        }

        protected override void DisposeInternal() {
            _handyTimer.Dispose();
        }
    }

    public class SingleTaskExecutionData {
        public SingleTaskExecutionData(TimeSpan? betweenExecutionsDelay) {
            BetweenExecutionsDelay = betweenExecutionsDelay;
        }
        public TimeSpan? BetweenExecutionsDelay { get; }
        public TimeSpan? OverrideDelay { get; set; }
    }
}