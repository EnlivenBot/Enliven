using System.Collections.Generic;
using System.Threading.Tasks;
using Autofac;
using Common.Config;
using NLog;
using Tyrrrz.Extensions;
using YandexMusicResolver;
using YandexMusicResolver.Config;

namespace Bot
{
    public static class DiExtensions
    {
        public static void AddEnlivenConfig(this ContainerBuilder builder, string configPath = "Config/config.json") {
            var enlivenConfigProvider = new EnlivenConfigProvider(configPath);
            builder.Register(context => enlivenConfigProvider.Load()).AsSelf().AsImplementedInterfaces()
                   .SingleInstance()
                   .OnActivating(args => args.Instance.Load())
                   .OnActivated(args => args.Context.Resolve<IEnumerable<IConfigDependent>>()
                                            .ParallelForEachAsync(dependent => dependent.OnConfigLoaded()).Wait());
        }
    }
}