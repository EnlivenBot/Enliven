using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Autofac;
using Bot.DiscordRelated.Commands;
using Common;
using Common.Config;
using Common.Localization.Entries;
using Common.Localization.Providers;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.Interactions.Builders;
using Discord.WebSocket;
using PreconditionAttribute = Discord.Interactions.PreconditionAttribute;
using RequireUserPermissionAttribute = Discord.Commands.RequireUserPermissionAttribute;
using SlashCommandBuilder = Discord.SlashCommandBuilder;
using SummaryAttribute = Discord.Commands.SummaryAttribute;

namespace Bot.DiscordRelated.Interactions {
    public class CustomInteractionService : InteractionService, IService {
        private readonly ILifetimeScope _serviceContainer;
        private readonly GlobalConfig _globalConfig;
        private readonly InstanceConfig _instanceConfig;
        public CustomInteractionService(DiscordShardedClient discordClient, ILifetimeScope serviceContainer,
                                        GlobalConfig globalConfig, InstanceConfig instanceConfig)
            : base(discordClient, new InteractionServiceConfig { UseCompiledLambda = true }) {
            _serviceContainer = serviceContainer;
            _globalConfig = globalConfig;
            _instanceConfig = instanceConfig;
        }

        public async Task OnPreDiscordStart() {
            var textCommandGroups = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(type => typeof(IModuleBase).IsAssignableFrom(type) && typeof(IInteractionModuleBase).IsAssignableFrom(type))
                .Where(type => type.GetCustomAttribute<HiddenAttribute>() == null)
                .Where(type => type.GetCustomAttribute<SlashCommandAdapterAttribute>()?.ProcessSlashCommand == true)
                .Where(type => RegisterIf.ShouldRegisterType(type, _globalConfig, _instanceConfig))
                .GroupBy(type => type.GetCustomAttribute<GroupingAttribute>()?.GroupName ?? "Unspecified");

            foreach (var textCommandGroup in textCommandGroups) {
                await CreateModuleAsync(textCommandGroup.Key, new ServiceProviderAdapter(_serviceContainer), moduleBuilder => BuildModule(textCommandGroup, moduleBuilder));
            }
        }

        private static PropertyInfo TypeInfoProperty = typeof(ModuleBuilder).GetDeclaredProperty("TypeInfo");
        private static void BuildModule(IGrouping<string, Type> textCommandGroup, ModuleBuilder moduleBuilder) {
            TypeInfoProperty.SetValue(moduleBuilder, textCommandGroup.First());
            
            var commandMethods = textCommandGroup
                .SelectMany(type => type.GetMethods())
                .Where(info => info.GetCustomAttribute<HiddenAttribute>() == null)
                .Where(info => info.GetCustomAttribute<SlashCommandAdapterAttribute>()?.ProcessSlashCommand ?? true)
                .Select(info => (info, info.GetCustomAttribute<CommandAttribute>()))
                .Where(tuple => tuple.Item2 != null);

            foreach (var (methodInfo, command) in commandMethods) {
                var description = methodInfo.GetCustomAttribute<SummaryAttribute>()
                    .Pipe(attribute => attribute?.Text)
                    .Pipe(s => s == null ? null : EntryLocalized.Create("Help", s))
                    .Pipe(localized => localized?.CanGet() == true ? localized.Get(LangLocalizationProvider.EnglishLocalizationProvider) : null)
                    .Pipe(s => s ?? command!.Text);

                var adminCommand = (methodInfo.GetCustomAttribute<RequireUserPermissionAttribute>()?.GuildPermission & GuildPermission.Administrator) != 0
                                || (methodInfo.DeclaringType?.GetCustomAttribute<RequireUserPermissionAttribute>()?.GuildPermission & GuildPermission.Administrator) != 0;
                var preconditions = adminCommand ? new[] { new Discord.Interactions.RequireUserPermissionAttribute(GuildPermission.Administrator) } : Array.Empty<PreconditionAttribute>();

                moduleBuilder.AddSlashCommand(builder => BuildSlashCommand(builder, command, description, preconditions, methodInfo));
            }
        }

        private static void BuildSlashCommand(Discord.Interactions.Builders.SlashCommandBuilder builder, CommandAttribute? command, string? description, PreconditionAttribute[] preconditions, MethodInfo methodInfo) {
            builder
                .WithName(command!.Text.ToLower())
                .WithDescription(description)
                .WithDefaultPermission(true)
                .WithPreconditions(preconditions);

            foreach (var parameterInfo in methodInfo.GetParameters()) {
                var pDescription = methodInfo.GetCustomAttribute<SummaryAttribute>()
                    .Pipe(attribute => attribute?.Text)
                    .Pipe(s => s == null ? null : EntryLocalized.Create("Help", s))
                    .Pipe(localized => localized?.CanGet() == true ? localized.Get(LangLocalizationProvider.EnglishLocalizationProvider) : null)
                    .Pipe(s => s ?? command.Text);
                builder.AddParameter(parameterBuilder =>
                    parameterBuilder
                        .WithName(parameterInfo.Name!.ToLower())
                        .WithDescription(pDescription)
                        .SetParameterType(parameterInfo.ParameterType)
                );
            }
        }

        public async Task OnDiscordReady() {
            await RegisterCommandsGloballyAsync();
        }
    }
}