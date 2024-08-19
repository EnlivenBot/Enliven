using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Autofac;
using Bot.DiscordRelated.Commands;
using Bot.Utilities.Logging;
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
using NLog;
using Tyrrrz.Extensions;
using ExecuteResult = Discord.Interactions.ExecuteResult;
using IResult = Discord.Interactions.IResult;
using PreconditionAttribute = Discord.Interactions.PreconditionAttribute;
using RequireUserPermissionAttribute = Discord.Commands.RequireUserPermissionAttribute;
using RuntimeResult = Discord.Interactions.RuntimeResult;
using SlashCommandBuilder = Discord.Interactions.Builders.SlashCommandBuilder;
using SummaryAttribute = Discord.Commands.SummaryAttribute;

namespace Bot.DiscordRelated.Interactions;

public class CustomInteractionService : InteractionService, IService
{
    private static PropertyInfo _typeInfoProperty = typeof(ModuleBuilder).GetDeclaredProperty("TypeInfo");

    private static PropertyInfo _callbackProperty = typeof(SlashCommandBuilder).GetProperty("Callback")!;
    private readonly InstanceConfig _instanceConfig;
    private readonly IServiceProvider _serviceProvider;

    public CustomInteractionService(DiscordShardedClient discordClient, ILifetimeScope serviceContainer,
        InstanceConfig instanceConfig, ILogger logger)
        : base(discordClient, new InteractionServiceConfig { UseCompiledLambda = true, LogLevel = LogSeverity.Debug })
    {
        _serviceProvider = new ServiceProviderAdapter(serviceContainer);
        _instanceConfig = instanceConfig;
        Log += message => LoggingUtilities.OnDiscordLog(logger, message);
    }

    public async Task OnPreDiscordStart()
    {
        var textCommandGroups = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(type => typeof(IModuleBase).IsAssignableFrom(type) &&
                           typeof(IInteractionModuleBase).IsAssignableFrom(type))
            .Where(type => type.GetCustomAttribute<HiddenAttribute>() == null)
            .Where(type => type.GetCustomAttribute<SlashCommandAdapterAttribute>()?.ProcessSlashCommand == true)
            .Where(type => RegisterIf.ShouldRegisterType(type, _instanceConfig));

        var subcommandSets = textCommandGroups.SelectMany(type => type.GetMethods())
            .Where(info => info.GetCustomAttribute<HiddenAttribute>() == null)
            .Where(info => info.GetCustomAttribute<SlashCommandAdapterAttribute>()?.ProcessSlashCommand ?? true)
            .Select(info => (info, info.GetCustomAttribute<CommandAttribute>()))
            .Where(tuple => tuple.Item2 != null)
            .GroupBy(tuple =>
                tuple.Item2!.Text.Contains(' ')
                    ? tuple.Item2.Text.SubstringUntilLast(" ", StringComparison.InvariantCulture)
                    : null);
        foreach (var subcommandSet in subcommandSets)
            await CreateModuleAsync(subcommandSet.Key ?? "Unspecified", _serviceProvider,
                moduleBuilder => BuildTopLevelModules(moduleBuilder, subcommandSet));
    }

    private void BuildTopLevelModules(ModuleBuilder moduleBuilder,
        IGrouping<string?, (MethodInfo info, CommandAttribute?)> subcommandSet)
    {
        moduleBuilder.SlashGroupName = subcommandSet.Key;
        moduleBuilder.Description = subcommandSet.Key;

        foreach (var commandInClass in subcommandSet.GroupBy(tuple => tuple.info.DeclaringType))
            moduleBuilder.AddModule(builder =>
            {
                BuildClassModule(commandInClass.Key!, builder,
                    CreateLambdaBuilder(commandInClass.Key!.GetTypeInfo(), this), commandInClass.AsEnumerable());
            });
    }

    private void BuildClassModule(Type module, ModuleBuilder moduleBuilder,
        Func<IServiceProvider, IInteractionModuleBase> createLambdaBuilder,
        IEnumerable<(MethodInfo info, CommandAttribute?)> commandMethods)
    {
        _typeInfoProperty.SetValue(moduleBuilder, module);
        moduleBuilder.AddAttributes(module.GetCustomAttributes<Attribute>().ToArray());

        foreach (var (methodInfo, command) in commandMethods)
            moduleBuilder.AddSlashCommand(builder =>
                BuildSlashCommand(builder, command, methodInfo, createLambdaBuilder));
    }

    private void BuildSlashCommand(SlashCommandBuilder builder, CommandAttribute? command, MethodInfo methodInfo,
        Func<IServiceProvider, IInteractionModuleBase> createLambdaBuilder)
    {
        var commandText = command!.Text.Contains(' ') ? command.Text.SubstringAfterLast(" ") : command.Text;
        var description = methodInfo.GetCustomAttribute<SummaryAttribute>()
            .Pipe(attribute => attribute?.Text)
            .Pipe(s => s == null ? null : EntryLocalized.Create("Help", s))
            .Pipe(localized => localized?.CanGet() == true
                ? localized.Get(LangLocalizationProvider.EnglishLocalizationProvider)
                : null)
            .Pipe(s => s ?? commandText);

        var adminCommand =
            (methodInfo.GetCustomAttribute<RequireUserPermissionAttribute>()?.GuildPermission.GetValueOrDefault(0)
                .HasFlag(GuildPermission.Administrator) ?? false)
            || (methodInfo.DeclaringType?.GetCustomAttribute<RequireUserPermissionAttribute>()?.GuildPermission
                .GetValueOrDefault(0).HasFlag(GuildPermission.Administrator) ?? false);

        var preconditions = adminCommand
            ? new[] { new Discord.Interactions.RequireUserPermissionAttribute(GuildPermission.Administrator) }
            : Array.Empty<PreconditionAttribute>();

        builder
            .WithName(commandText.ToLower())
            .WithDescription(description)
            .SetEnabledInDm(false)
            .WithPreconditions(preconditions)
            .WithAttributes(methodInfo.GetCustomAttributes().ToArray());
        _callbackProperty.SetValue(builder, CreateCallback(createLambdaBuilder, methodInfo));

        foreach (var parameterInfo in methodInfo.GetParameters())
        {
            var pDescription = parameterInfo.GetCustomAttribute<SummaryAttribute>()
                .Pipe(attribute => attribute?.Text)
                .Pipe(s => s == null ? null : EntryLocalized.Create("Help", s))
                .Pipe(localized => localized?.CanGet() == true
                    ? localized.Get(LangLocalizationProvider.EnglishLocalizationProvider)
                    : null)
                .Pipe(s => s ?? command.Text)
                .Pipe(s => s.SafeSubstring(100, "..."));
            var isOptional = parameterInfo.GetCustomAttribute<SlashCommandOptionalAttribute>()?.IsOptional ??
                             parameterInfo.IsOptional;
            builder.AddParameter(parameterBuilder =>
            {
                parameterBuilder
                    .WithName(parameterInfo.Name!.ToLower())
                    .WithDescription(pDescription)
                    .SetParameterType(parameterInfo.ParameterType)
                    .SetRequired(!isOptional)
                    .SetDefaultValue(parameterInfo.DefaultValue);
            });
        }
    }

    #region Unsafe reflection driven bindings to DiscordNet internal classes

    private static MethodInfo? _reflectionUtilsCreateLambdaBuilderMethod;

    private static Func<IServiceProvider, IInteractionModuleBase> CreateLambdaBuilder(TypeInfo typeInfo,
        InteractionService commandService)
    {
        if (_reflectionUtilsCreateLambdaBuilderMethod == null)
        {
            var reflectionUtilsType =
                typeof(InteractionService).Assembly.GetType("Discord.Interactions.ReflectionUtils`1")!.MakeGenericType(
                    typeof(IInteractionModuleBase));
            _reflectionUtilsCreateLambdaBuilderMethod = reflectionUtilsType.GetDeclaredMethod("CreateLambdaBuilder");
        }

        return (Func<IServiceProvider, IInteractionModuleBase>)_reflectionUtilsCreateLambdaBuilderMethod.Invoke(null,
            new object[] { typeInfo, commandService })!;
    }

    private static ExecuteCallback CreateCallback(Func<IServiceProvider, IInteractionModuleBase> createInstance,
        MethodInfo methodInfo)
    {
        Func<IInteractionModuleBase, object[], Task> commandInvoker =
            CreateMethodInvoker<IInteractionModuleBase>(methodInfo);

        async Task<IResult> ExecuteCallback(IInteractionContext context, object[] args,
            IServiceProvider serviceProvider, ICommandInfo commandInfo)
        {
            var instance = createInstance(serviceProvider);
            instance.SetContext(context);

            try
            {
                await instance.BeforeExecuteAsync(commandInfo).ConfigureAwait(false);
                instance.BeforeExecute(commandInfo);
                var task = commandInvoker(instance, args) ?? Task.Delay(0);

                if (task is Task<RuntimeResult> runtimeTask)
                    return await runtimeTask.ConfigureAwait(false);
                else
                {
                    await task.ConfigureAwait(false);
                    return ExecuteResult.FromSuccess();
                }
            }
            catch (CommandInterruptionException)
            {
                return ExecuteResult.FromSuccess();
            }
            catch (Exception ex)
            {
                return ExecuteResult.FromError(ex);
            }
            finally
            {
                await instance.AfterExecuteAsync(commandInfo).ConfigureAwait(false);
                instance.AfterExecute(commandInfo);
                (instance as IDisposable)?.Dispose();
            }
        }

        return ExecuteCallback;
    }

    internal static Func<T, object[], Task> CreateMethodInvoker<T>(MethodInfo methodInfo)
    {
        var parameters = methodInfo.GetParameters();
        var paramsExp = new Expression[parameters.Length];

        var instanceExp = Expression.Parameter(typeof(T), "instance");
        var argsExp = Expression.Parameter(typeof(object[]), "args");

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];

            var indexExp = Expression.Constant(i);
            var accessExp = Expression.ArrayIndex(argsExp, indexExp);
            paramsExp[i] = Expression.Convert(accessExp, parameter.ParameterType);
        }

        var callExp = Expression.Call(Expression.Convert(instanceExp, methodInfo.ReflectedType), methodInfo, paramsExp);
        var finalExp = Expression.Convert(callExp, typeof(Task));
        var lambda = Expression.Lambda<Func<T, object[], Task>>(finalExp, instanceExp, argsExp).Compile();

        return lambda;
    }

    #endregion
}