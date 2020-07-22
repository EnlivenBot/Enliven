using System;
using System.Threading.Tasks;
using Bot.DiscordRelated.Criteria;

#pragma warning disable 618

namespace Bot.Utilities {
    public class SingleTask : SingleTask<Task> {
        public SingleTask(Func<Task> action) : base(action) { }
    }

    public class SingleTask<T> {
        // ReSharper disable once StaticMemberInGenericType
        private static readonly object LockObject = new object();

        [Obsolete("Use Execute method instead this")]
        public readonly Func<Task<T>> Action;

        private Task _betweenExecutionsDelayTask = Task.CompletedTask;
        private bool _isDirtyNow;
        private DateTime _lastExecutionTime = DateTime.MinValue;
        private T _lastResult = default!;
        private DateTime _targetTime;
        private TaskCompletionSource<T>? _taskCompletionSource;

        public SingleTask(Func<T> action) {
            Action = () => Task.FromResult(action());
        }

        public SingleTask(Func<Task<T>> action) {
            Action = action;
        }

        public ICriterion? NeedDirtyExecuteCriterion { get; set; }

        public TimeSpan? BetweenExecutionsDelay { get; set; }

        public bool IsDelayResetByExecute { get; set; } = false;

        public bool IsExecuting { get; private set; }

        public bool CanBeDirty { get; set; } = false;

        public Task<T> Execute(bool makesDirty = true, TimeSpan? delayOverride = null) {
            return InternalExecute(makesDirty, delayOverride);
        }

        private Task<T> InternalExecute(bool makesDirty, TimeSpan? delayOverride) {
            lock (LockObject) {
                if (_taskCompletionSource != null) {
                    UpdateDelay(IsDelayResetByExecute, delayOverride);
                    if (!makesDirty || !CanBeDirty)
                        return _taskCompletionSource.Task;

                    return QueueExecuteDirty(_taskCompletionSource.Task);
                }

                _taskCompletionSource = new TaskCompletionSource<T>();
                _ = Task.Run(async () => {
                    IsExecuting = true;
                    await _betweenExecutionsDelayTask;

                    T result;
                    if (!_isDirtyNow && NeedDirtyExecuteCriterion != null && await NeedDirtyExecuteCriterion.JudgeAsync()) {
                        result = _lastResult;
                    }
                    else {
                        result = await Action();
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
                        UpdateDelay(true);
                        localTaskCompletionSource.SetResult(result);
                    }
                });
                return _taskCompletionSource.Task;
            }
        }

        private void UpdateDelay(bool hardUpdate, TimeSpan? overrideDelay = null) {
            var tempTime = _lastExecutionTime + (overrideDelay ?? BetweenExecutionsDelay ?? TimeSpan.Zero);
            if (!hardUpdate && tempTime >= _targetTime) return;
            _targetTime = tempTime;
            var delay = DateTime.Now - _targetTime;
            if (delay.TotalMilliseconds < 0) {
                delay = TimeSpan.Zero;
            }

            _betweenExecutionsDelayTask = Task.Delay(delay);
        }

        private async Task<T> QueueExecuteDirty(Task first) {
            await first;
            _isDirtyNow = true;
            return await Execute(false);
        }
    }
}