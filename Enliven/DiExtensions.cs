using System;
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
        public static void AddEnlivenConfig(this ContainerBuilder builder, string configPath = "Config/config.json", bool isFirstGenThrowsException = true) {
            var enlivenConfigProvider = new EnlivenConfigProvider(configPath);
            if (isFirstGenThrowsException && !enlivenConfigProvider.IsConfigExists()) {
                enlivenConfigProvider.Load();
                throw new Exception($"New config file generated at {enlivenConfigProvider.FullConfigPath}. Consider check it.");
            }
            builder.Register(context => enlivenConfigProvider.Load()).AsSelf().AsImplementedInterfaces()
                   .SingleInstance()
                   .OnActivating(args => args.Instance.Load())
                   .OnActivated(args => args.Context.Resolve<IEnumerable<IConfigDependent>>()
                                            .ParallelForEachAsync(dependent => dependent.OnConfigLoaded()).Wait());
        }
    }
}