using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using Bot.Utilities;
using Common;
using Common.Config;
using Common.Localization.Providers;
using Common.Utils;
using Discord;
using Discord.Commands;
using NLog;

namespace Bot.DiscordRelated.Commands {
    public class CustomCommandService : CommandService, IService {
        public ILookup<string, CommandInfo> Aliases { get; private set; } = null!;
        private readonly IEnumerable<CustomTypeReader> _typeReaders;
        private readonly ILifetimeScope _serviceContainer;
        private readonly GlobalConfig _globalConfig;
        private readonly InstanceConfig _instanceConfig;

        public CustomCommandService(IEnumerable<CustomTypeReader> typeReaders, ILifetimeScope serviceContainer,
                                    GlobalConfig globalConfig, InstanceConfig instanceConfig,
                                    ILogger logger)
            : base(new CommandServiceConfig() { LogLevel = LogSeverity.Debug }) {
            _serviceContainer = serviceContainer;
            _globalConfig = globalConfig;
            _instanceConfig = instanceConfig;
            _typeReaders = typeReaders;
            Log += message => Common.Utilities.OnDiscordLog(logger, message);
        }

        public async Task OnPreDiscordStart() {
            await AddModulesAsync(Assembly.GetEntryAssembly()!, new ServiceProviderAdapter(_serviceContainer));
            foreach (var customTypeReader in _typeReaders) {
                AddTypeReader(customTypeReader.GetTargetType(), customTypeReader);
            }

            Aliases = Commands
                .SelectMany(info => info.Aliases.Select(s => (s, info)))
                .ToLookup(tuple => tuple.s, tuple => tuple.info);

            CommandsGroups = new Lazy<Dictionary<string, CommandGroup>>(() => {
                return Commands
                    .Where(info => !info.IsHiddenCommand())
                    .GroupBy(info => info.GetGroup()?.GroupName ?? "")
                    .Where(grouping => !grouping.Key.IsBlank())
                    .Select(infos =>
                        new CommandGroup {
                            Commands = infos.ToList(),
                            GroupId = infos.Key,
                            GroupNameTemplate = $"{{0}} ({{1}}help {infos.Key}):",
                            GroupTextTemplate = string.Join(' ', infos
                                .Select(info => info.Name)
                                .GroupBy(s => s).Select(grouping => grouping.First())
                                .Select(s => $"`{{0}}{s}`")
                            )
                        }).ToDictionary(group => @group.GroupId);
            });
        }

        private new async Task AddModulesAsync(Assembly assembly, IServiceProvider services) {
            foreach (var definedType in assembly.DefinedTypes) {
                if (!definedType.IsPublic
                 && !definedType.IsNestedPublic
                 || !typeof(IModuleBase).IsAssignableFrom(definedType)
                 || definedType.IsAbstract
                 || definedType.ContainsGenericParameters
                 || definedType.IsDefined(typeof(DontAutoLoadAttribute))
                 || definedType.GetCustomAttribute<RegisterIf>()?.CanBeRegistered(_globalConfig, _instanceConfig) == false)
                    continue;
                await AddModuleAsync(definedType, services).ConfigureAwait(false);
            }
        }

        public Task<(KeyValuePair<CommandMatch, ParseResult>?, IResult)> FindAsync(ICommandContext context, int argPos, IServiceProvider services,
                                                                                   MultiMatchHandling multiMatchHandling = MultiMatchHandling.Exception)
            => FindAsync(context, context.Message.Content.Substring(argPos), services, multiMatchHandling);

        public async Task<(KeyValuePair<CommandMatch, ParseResult>?, IResult)> FindAsync(ICommandContext context, string input, IServiceProvider? services,
                                                                                         MultiMatchHandling multiMatchHandling = MultiMatchHandling.Exception) {
            services ??= new ServiceProviderAdapter(_serviceContainer);

            var searchResult = Search(input);
            if (!searchResult.IsSuccess) {
                return (null, searchResult);
            }

            var commands = searchResult.Commands;
            var preconditionResults = new Dictionary<CommandMatch, PreconditionResult>();

            foreach (var match in commands) {
                preconditionResults[match] = await match.Command.CheckPreconditionsAsync(context, services).ConfigureAwait(false);
            }

            var successfulPreconditions = preconditionResults
                .Where(x => x.Value.IsSuccess)
                .ToArray();

            if (successfulPreconditions.Length == 0) {
                //All preconditions failed, return the one from the highest priority command
                var bestCandidate = preconditionResults
                    .OrderByDescending(x => x.Key.Command.Priority)
                    .FirstOrDefault(x => !x.Value.IsSuccess);

                return (null, bestCandidate.Value);
            }

            //If we get this far, at least one precondition was successful.

            var parseResultsDict = new Dictionary<CommandMatch, ParseResult>();
            foreach (var pair in successfulPreconditions) {
                var parseResult = await pair.Key.ParseAsync(context, searchResult, pair.Value, services).ConfigureAwait(false);

                if (parseResult.Error == CommandError.MultipleMatches) {
                    IReadOnlyList<TypeReaderValue> argList, paramList;
                    switch (multiMatchHandling) {
                        case MultiMatchHandling.Best:
                            argList = parseResult.ArgValues.Select(x => x.Values.OrderByDescending(y => y.Score).First()).ToImmutableArray();
                            paramList = parseResult.ParamValues.Select(x => x.Values.OrderByDescending(y => y.Score).First()).ToImmutableArray();
                            parseResult = ParseResult.FromSuccess(argList, paramList);
                            break;
                    }
                }

                parseResultsDict[pair.Key] = parseResult;
            }

            // Calculates the 'score' of a command given a parse result
            float CalculateScore(CommandMatch match, ParseResult parseResult) {
                float argValuesScore = 0, paramValuesScore = 0;

                if (match.Command.Parameters.Count > 0) {
                    var argValuesSum = parseResult.ArgValues?.Sum(x => x.Values.OrderByDescending(y => y.Score).FirstOrDefault().Score) ?? 0;
                    var paramValuesSum = parseResult.ParamValues?.Sum(x => x.Values.OrderByDescending(y => y.Score).FirstOrDefault().Score) ?? 0;

                    argValuesScore = argValuesSum / match.Command.Parameters.Count;
                    paramValuesScore = paramValuesSum / match.Command.Parameters.Count;
                }

                var totalArgsScore = (argValuesScore + paramValuesScore) / 2;
                return match.Command.Priority + totalArgsScore * 0.99f;
            }

            //Order the parse results by their score so that we choose the most likely result to execute
            var parseResults = parseResultsDict
                .OrderByDescending(x => CalculateScore(x.Key, x.Value)).ToList();

            var successfulParses = parseResults
                .Where(x => x.Value.IsSuccess)
                .ToArray();

            if (successfulParses.Length == 0) {
                //All parses failed, return the one from the highest priority command, using score as a tie breaker
                var bestMatch = parseResults
                    .FirstOrDefault(x => !x.Value.IsSuccess);

                return (bestMatch, bestMatch.Value);
            }

            //If we get this far, at least one parse was successful. Execute the most likely overload.
            var chosenOverload = successfulParses[0];
            return (chosenOverload, CustomDiscordResult.FromSuccess());
            // var result = await chosenOverload.Key.ExecuteAsync(context, chosenOverload.Value, services).ConfigureAwait(false);
            // if (!result.IsSuccess && !(result is RuntimeResult || result is ExecuteResult)) // succesful results raise the event in CommandInfo#ExecuteInternalAsync (have to raise it there b/c deffered execution)
            //     await _commandExecutedEvent.InvokeAsync(chosenOverload.Key.Command, context, result);
            // return result;
        }

        public Lazy<Dictionary<string, CommandGroup>> CommandsGroups = null!;

        public IEnumerable<EmbedFieldBuilder> BuildHelpFields(string command, string prefix, ILocalizationProvider loc) {
            return Aliases[command].Select(info => new EmbedFieldBuilder {
                Name = loc.Get("Help.CommandTitle", command, GetAliasesString(info.Aliases, loc)),
                Value = $"{loc.Get($"Help.{info.Summary}")}\n" +
                        "```css\n" +
                        $"{prefix}{info.Name} {(info.Parameters.Count == 0 ? "" : $"[{string.Join("] [", info.Parameters.Select(x => x.Name))}]")}```" +
                        (info.Parameters.Count == 0
                            ? ""
                            : "\n" + string.Join("\n",
                                info.Parameters.Select(x => $"`{x.Name}` - {(string.IsNullOrWhiteSpace(x.Summary) ? "" : loc.Get("Help." + x.Summary))}")))
            });
        }

        public static string GetAliasesString(IEnumerable<string> aliases, ILocalizationProvider loc, bool skipFirst = true) {
            aliases = skipFirst ? aliases.Skip(1) : aliases;
            var enumerable = aliases.ToList();
            return !enumerable.Any() ? "" : $"({GetAliases(enumerable)})";
        }

        private static string GetAliases(IEnumerable<string> aliases) {
            var s = new StringBuilder();
            foreach (var alias in aliases) {
                s.Append($" `{alias}` ");
            }

            return s.ToString().Trim();
        }
    }
}