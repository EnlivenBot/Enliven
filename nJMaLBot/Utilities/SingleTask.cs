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
        private static readonly object LockObject = new object();
        private TaskCompletionSource<T>? _taskCompletionSource;
        
        public bool IsExecuting { get; private set; }

        public SingleTask(Func<T> action) {
            Action = () => Task.FromResult(action());
        }

        public SingleTask(Func<Task<T>> action) {
            Action = action;
        }

        public Task<T> Execute() {
            lock (LockObject) {
                if (_taskCompletionSource != null) return _taskCompletionSource.Task;
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
    }
}