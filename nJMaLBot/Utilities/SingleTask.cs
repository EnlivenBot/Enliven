using System;
using System.Threading.Tasks;

namespace Bot.Utilities {
    public class SingleTask : SingleTask<Task> {
        public SingleTask(Action action) : base(() => {
            action();
            return Task.CompletedTask;
        }) { }
    }

    public class SingleTask<T> {
        public readonly Func<Task<T>> Action;
        private readonly object _lockObject = new object();
        private TaskCompletionSource<T>? _taskCompletionSource;

        public SingleTask(Func<T> action) {
            Action = () => Task.FromResult(action());
        }

        public SingleTask(Func<Task<T>> action) {
            Action = action;
        }

        public Task<T> Execute() {
            lock (_lockObject) {
                if (_taskCompletionSource != null) return _taskCompletionSource.Task;
                _taskCompletionSource = new TaskCompletionSource<T>();
                _ = Task.Run(async () => {
                    var result = await Action();
                    lock (_lockObject) {
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