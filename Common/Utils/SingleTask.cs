using System;
using System.Threading.Tasks;
using Common.Criteria;

#pragma warning disable 618

namespace Common.Utils {
    public class SingleTask : SingleTask<Task> {
        public SingleTask(Func<Task> action) : base(action) { }
        public SingleTask(Func<SingleTaskExecutionData, Task> action) : base(action) { }
    }

    public class SingleTask<T> : IDisposable {
        // ReSharper disable once StaticMemberInGenericType
        private static readonly object LockObject = new object();

        [Obsolete("Use Execute method instead this")]
        public readonly Func<SingleTaskExecutionData, Task<T>> Action;

        private HandyTimer _betweenExecutionsDelay = new HandyTimer();
        private bool _isDirtyNow;
        private DateTime _lastExecutionTime = DateTime.MinValue;
        private T _lastResult = default!;
        private DateTime _targetTime;
        private TaskCompletionSource<T>? _taskCompletionSource;

        public SingleTask(Func<SingleTaskExecutionData, T> action) : this(data => Task.FromResult(action(data))) { }

        public SingleTask(Func<T> action) : this(data => Task.FromResult(action())) { }

        public SingleTask(Func<Task<T>> action) : this(data => action()) { }

        public SingleTask(Func<SingleTaskExecutionData, Task<T>> action) {
            Action = action;
        }

        public TimeSpan? BetweenExecutionsDelay { get; set; }

        public ICriterion? NeedDirtyExecuteCriterion { get; set; }

        public bool IsDelayResetByExecute { get; set; }

        public bool IsExecuting { get; private set; }

        public bool CanBeDirty { get; set; }

        public Task<T> Execute(bool makesDirty = true, TimeSpan? delayOverride = null) {
            if (IsDisposed) throw new ObjectDisposedException(nameof(SingleTask));

            _isDirtyNow = false;
            return InternalExecute(makesDirty, delayOverride);
        }

        private Task<T> InternalExecute(bool makesDirty, TimeSpan? delayOverride) {
            lock (LockObject) {
                var localTaskCompletionSource = _taskCompletionSource;
                UpdateDelay(IsDelayResetByExecute, delayOverride);
                if (localTaskCompletionSource != null) {
                    if (!makesDirty || !CanBeDirty)
                        return localTaskCompletionSource.Task;

                    return QueueExecuteDirty(localTaskCompletionSource.Task);
                }

                _taskCompletionSource = new TaskCompletionSource<T>();
                new Task(async () => {
                    IsExecuting = true;
                    await _betweenExecutionsDelay.TimerElapsed;

                    T result;
                    SingleTaskExecutionData? singleTaskExecutionData = null;
                    if (_isDirtyNow && NeedDirtyExecuteCriterion != null && await NeedDirtyExecuteCriterion.JudgeAsync() || IsDisposed) {
                        result = _lastResult;
                    }
                    else {
                        singleTaskExecutionData = new SingleTaskExecutionData();
                        result = await Action(singleTaskExecutionData);
                        if (result is Task task) {
                            await task;
                        }
                    }

                    lock (LockObject) {
                        _lastResult = result;
                        IsExecuting = false;
                        var localTaskCompletionSource = _taskCompletionSource;
                        _taskCompletionSource = null;
                        _lastExecutionTime = DateTime.Now;
                        _isDirtyNow = false;
                        UpdateDelay(true, singleTaskExecutionData?.OverrideDelay);
                        localTaskCompletionSource.SetResult(result);
                    }
                }, TaskCreationOptions.LongRunning).Start();
                return _taskCompletionSource.Task;
            }
        }

        private void UpdateDelay(bool hardUpdate, TimeSpan? overrideDelay = null) {
            var tempTime = _lastExecutionTime + (overrideDelay ?? BetweenExecutionsDelay ?? TimeSpan.Zero);
            if (!hardUpdate && tempTime >= _targetTime) return;
            _targetTime = tempTime;
            _betweenExecutionsDelay.SetTargetTime(_targetTime);
        }

        private async Task<T> QueueExecuteDirty(Task first) {
            await first;
            _isDirtyNow = true;
            return await Execute(false);
        }

        public bool IsDisposed { get; private set; }

        public void Dispose() {
            IsDisposed = true;
            _betweenExecutionsDelay.Dispose();
        }
    }

    public class SingleTaskExecutionData {
        public TimeSpan? OverrideDelay { get; set; }
    }
}