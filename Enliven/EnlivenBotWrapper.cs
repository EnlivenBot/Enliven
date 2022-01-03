using System;
using System.IO;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Common;
using Common.Config;
using NLog;

namespace Bot {
    public class EnlivenBotWrapper {
        private static ILogger Logger = LogManager.GetCurrentClassLogger();
        private TaskCompletionSource<bool>? _firstStartResult;
        private readonly EnlivenConfigProvider _enlivenConfigProvider;
        public EnlivenBotWrapper(EnlivenConfigProvider enlivenConfigProvider) {
            _enlivenConfigProvider = enlivenConfigProvider;
        }

        /// <summary>
        /// Attempts to start bot instance
        /// </summary>
        /// <returns>True if start successful, otherwise False</returns>
        public Task<bool> StartAsync(IContainer container, CancellationToken cancellationToken) {
            if (_firstStartResult != null) throw new Exception("Current instance already started");

            _firstStartResult = new TaskCompletionSource<bool>();
            _enlivenConfigProvider.Load();

            _ = RunLoopAsync(container, cancellationToken);

            return _firstStartResult!.Task;
        }

        private async Task RunLoopAsync(IContainer container, CancellationToken cancellationToken) {
            var isFirst = true;
            while (!cancellationToken.IsCancellationRequested) {
                try {
                    await using var lifetimeScope = container.BeginLifetimeScope(builder => {
                        builder.Register(context => _enlivenConfigProvider.Load())
                            .AsSelf().AsImplementedInterfaces().As<EnlivenConfig>()
                            .SingleInstance();
                    });
                    var bot = lifetimeScope.Resolve<EnlivenBot>();
                    await bot.StartAsync();
                    _firstStartResult!.TrySetResult(true);

                    try {
                        await bot.Disposed.ToTask(cancellationToken);
                    }
                    catch (Exception) {
                        // ignored
                    }
                }
                catch (Exception e) {
                    Logger.Fatal(e, $"Failed to start bot instance with config {Path.GetFileName(_enlivenConfigProvider.ConfigPath)}");
                    _firstStartResult!.TrySetResult(false);
                    if (isFirst) return;
                }
                
                isFirst = false;
            }
        }
    }
}