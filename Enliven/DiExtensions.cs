using System.Threading.Tasks;
using Autofac;
using Common.Config;
using NLog;
using YandexMusicResolver;
using YandexMusicResolver.Config;

namespace Bot {
    public static class DiExtensions {
        public static void AddEnlivenConfig(this ContainerBuilder builder, string configPath = "Config/config.json") {
            builder.Register(context => {
                var enlivenConfig = new EnlivenConfig(configPath);
                enlivenConfig.Load();

                var logger = context.ResolveOptional<ILogger>() ?? LogManager.GetLogger("EnlivenConfig");
                var authorizeAsync = enlivenConfig.AuthorizeAsync(false);
                authorizeAsync.ContinueWith(task => logger.Error(task.Exception, "Yandex Music auth failed. Yandex Music tracks cut to 30 seconds"), TaskContinuationOptions.OnlyOnFaulted);
                authorizeAsync.ContinueWith(task => logger.Info("Yandex Music auth completed"));

                return enlivenConfig;
            }).AsSelf().AsImplementedInterfaces().SingleInstance();
        }
    }
}