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
using Common.Utils;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.Interactions.Builders;
using Discord.WebSocket;
using PreconditionAttribute = Discord.Interactions.PreconditionAttribute;
using RequireUserPermissionAttribute = Discord.Commands.RequireUserPermissionAttribute;
using SlashCommandBuilder = Discord.Interactions.Builders.SlashCommandBuilder;
using SummaryAttribute = Discord.Commands.SummaryAttribute;

namespace Bot.DiscordRelated.Interactions {
    public class CustomInteractionService : InteractionService, IService {
        private readonly IServiceProvider _serviceProvider;
        private readonly GlobalConfig _globalConfig;
        private readonly InstanceConfig _instanceConfig;

        public CustomInteractionService(DiscordShardedClient discordClient, ILifetimeScope serviceContainer,
                                        GlobalConfig globalConfig, InstanceConfig instanceConfig)
            : base(discordClient, new InteractionServiceConfig { UseCompiledLambda = true }) {
            _serviceProvider = new ServiceProviderAdapter(serviceContainer);
            _globalConfig = globalConfig;
            _instanceConfig = instanceConfig;
        }

        public async Task OnPreDiscordStart() {
            var textCommandGroups = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(type => typeof(IModuleBase).IsAssignableFrom(type) && typeof(IInteractionModuleBase).IsAssignableFrom(type))
                .Where(type => type.GetCustomAttribute<HiddenAttribute>() == null)
                .Where(type => type.GetCustomAttribute<SlashCommandAdapterAttribute>()?.ProcessSlashCommand == true)
                .Where(type => RegisterIf.ShouldRegisterType(type, _globalConfig, _instanceConfig));

            foreach (var type in textCommandGroups) {
                var name = type.GetCustomAttribute<GroupingAttribute>()?.GroupName ?? "Unspecified";
                await CreateModuleAsync(name, _serviceProvider, 
                    moduleBuilder => BuildModule(type, moduleBuilder, CreateLambdaBuilder(type.GetTypeInfo(), this)));
            }
        }

        private static PropertyInfo TypeInfoProperty = typeof(ModuleBuilder).GetDeclaredProperty("TypeInfo");
        private void BuildModule(Type module, ModuleBuilder moduleBuilder, Func<IServiceProvider, IInteractionModuleBase> createLambdaBuilder) {
            TypeInfoProperty.SetValue(moduleBuilder, module);

            var commandMethods = module
                .GetMethods()
                .Where(info => info.GetCustomAttribute<HiddenAttribute>() == null)
                .Where(info => info.GetCustomAttribute<SlashCommandAdapterAttribute>()?.ProcessSlashCommand ?? true)
                .Select(info => (info, info.GetCustomAttribute<CommandAttribute>()))
                .Where(tuple => tuple.Item2 != null);

            foreach (var (methodInfo, command) in commandMethods) {
                moduleBuilder.AddSlashCommand(builder => BuildSlashCommand(builder, command, methodInfo, createLambdaBuilder));
            }
        }

        private static PropertyInfo CallbackProperty = typeof(SlashCommandBuilder).GetProperty("Callback")!;
        private void BuildSlashCommand(SlashCommandBuilder builder, CommandAttribute? command, MethodInfo methodInfo, Func<IServiceProvider, IInteractionModuleBase> createLambdaBuilder) {
            var description = methodInfo.GetCustomAttribute<SummaryAttribute>()
                .Pipe(attribute => attribute?.Text)
                .Pipe(s => s == null ? null : EntryLocalized.Create("Help", s))
                .Pipe(localized => localized?.CanGet() == true ? localized.Get(LangLocalizationProvider.EnglishLocalizationProvider) : null)
                .Pipe(s => s ?? command!.Text);

            var adminCommand = (methodInfo.GetCustomAttribute<RequireUserPermissionAttribute>()?.GuildPermission ?? 0 & GuildPermission.Administrator) != 0
                            || (methodInfo.DeclaringType?.GetCustomAttribute<RequireUserPermissionAttribute>()?.GuildPermission ?? 0 & GuildPermission.Administrator) != 0;
            var preconditions = adminCommand ? new[] { new Discord.Interactions.RequireUserPermissionAttribute(GuildPermission.Administrator) } : Array.Empty<PreconditionAttribute>();
            
            builder
                .WithName(command!.Text.ToLower())
                .WithDescription(description)
                .SetEnabledInDm(false)
                .WithPreconditions(preconditions)
                .WithAttributes(methodInfo.GetCustomAttributes().ToArray());
            CallbackProperty.SetValue(builder, CreateCallback(createLambdaBuilder, methodInfo, this));

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

        #region Unsafe reflection driven bindings to DiscordNet internal classes
        private static MethodInfo? _reflectionUtilsCreateLambdaBuilderMethod;
        private static Func<IServiceProvider, IInteractionModuleBase> CreateLambdaBuilder(TypeInfo typeInfo, InteractionService commandService) {
            if (_reflectionUtilsCreateLambdaBuilderMethod == null) {
                var reflectionUtilsType = typeof(InteractionService).Assembly.GetType("Discord.Interactions.ReflectionUtils`1")!.MakeGenericType(typeof(IInteractionModuleBase));
                _reflectionUtilsCreateLambdaBuilderMethod = reflectionUtilsType.GetDeclaredMethod("CreateLambdaBuilder");
            }
            return (Func<IServiceProvider, IInteractionModuleBase>)_reflectionUtilsCreateLambdaBuilderMethod.Invoke(null, new object[]{typeInfo, commandService})!;
        }
        
        private static MethodInfo? _moduleClassBuilderCreateCallbackMethod;
        private static ExecuteCallback CreateCallback(Func<IServiceProvider, IInteractionModuleBase> createInstance, MethodInfo methodInfo, InteractionService commandService) {
            if (_moduleClassBuilderCreateCallbackMethod == null) {
                var moduleClassBuilderType = typeof(InteractionService).Assembly.GetType("Discord.Interactions.Builders.ModuleClassBuilder");
                _moduleClassBuilderCreateCallbackMethod = moduleClassBuilderType.GetDeclaredMethod("CreateCallback");
            }
            return (ExecuteCallback)_moduleClassBuilderCreateCallbackMethod.Invoke(null, new object[] { createInstance, methodInfo, commandService })!;
        }
        #endregion
    }
}