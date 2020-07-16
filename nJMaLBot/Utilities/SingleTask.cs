using System;
using System.Threading.Tasks;
#pragma warning disable 618

namespace Bot.Utilities {
    public class SingleTask : SingleTask<Task> {
        public SingleTask(Action action) : base(() => {
            action();
            return Task.CompletedTask;
        }) { }
    }

    public class SingleTask<T> {
        [Obsolete("Use Execute method instead this")]
        public readonly Func<Task<T>> Action;
        // ReSharper disable once StaticMemberInGenericType
        private static readonly object LockObject = new object();
        private TaskCompletionSource<T>? _taskCompletionSource;
        
        public bool IsExecuting { get; private set; }

        public bool CanBeDirty { get; set; } = false;

        public SingleTask(Func<T> action) {
            Action = () => Task.FromResult(action());
        }

        public SingleTask(Func<Task<T>> action) {
            Action = action;
        }

        public Task<T> Execute(bool makesDirty = true) {
            lock (LockObject) {
                if (_taskCompletionSource != null) {
                    if (!makesDirty || !CanBeDirty) 
                        return _taskCompletionSource.Task;

                    return InternalExecute(_taskCompletionSource.Task);
                }
                _taskCompletionSource = new TaskCompletionSource<T>();
                _ = Task.Run(async () => {
                    IsExecuting = true;
                    var result = await Action();
                    lock (LockObject) {
                        IsExecuting = false;
                        var localTaskCompletionSource = _taskCompletionSource;
                        _taskCompletionSource = null;
                        localTaskCompletionSource.SetResult(result);
                    }
                });
                return _taskCompletionSource.Task;
            }
        }

        public async Task<T> InternalExecute(Task first) {
            await first;
            return await Execute(false);
        }
    }
}