using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Bot.Config.Emoji;
using Bot.DiscordRelated.MessageComponents;
using Common;
using Common.Config;
using Common.Localization.Entries;
using Common.Localization.Providers;
using Common.Music.Effects;
using Common.Music.Players;
using Common.Utils;
using Discord;
using Discord.WebSocket;
using HarmonyLib;
using Tyrrrz.Extensions;

namespace Bot.DiscordRelated.Music {
    public class EmbedPlayerEffectsDisplay : PlayerDisplayBase {
        private ILocalizationProvider _loc;
        private IMessageChannel _targetChannel;
        private PaginatedMessage _paginatedMessage = null!;
        private CompositeDisposable _disposable = new CompositeDisposable();
        private EnlivenEmbedBuilder _enlivenEmbedBuilder = new EnlivenEmbedBuilder();
        private EnlivenComponentBuilder _enlivenComponentBuilder;
        private readonly SingleTask _updateControlMessageTask;
        private IUserMessage? _controlMessage;

        public EmbedPlayerEffectsDisplay(IMessageChannel targetChannel, ILocalizationProvider loc, MessageComponentService messageComponentService,
                                         IGuildConfigProvider guildConfigProvider) {
            _loc = loc;
            _targetChannel = targetChannel;
            IPrefixProvider prefixProvider = _targetChannel is ITextChannel textChannel
                ? guildConfigProvider.Get(textChannel.Guild.Id).PrefixProvider
                : (IPrefixProvider)new BotPrefixProvider();
            
            _enlivenComponentBuilder = messageComponentService.GetBuilder();
            _enlivenEmbedBuilder.Title = _loc.Get("Music.Effects");

            var applyEffectTitle = _loc.Get("Effects.ApplyEffectTitle");
            var effects = PlayerEffectSource.DefaultEffects.Select(source => $"`{source.GetSourceName()}`").JoinToString(", ");
            var applyEffectDescription = _loc.Get("Effects.ApplyEffectDescription", prefixProvider.GetPrefix(), effects);
            _enlivenEmbedBuilder.AddField("ApplyEffects", applyEffectTitle, applyEffectDescription);

            var addCustomEffectTitle = _loc.Get("Effects.AddCustomEffectTitle");
            var addCustomEffectDescription = _loc.Get("Effects.AddCustomEffectDescription", prefixProvider.GetPrefix());
            _enlivenEmbedBuilder.AddField("CustomEffects", addCustomEffectTitle, addCustomEffectDescription);

            var firstButton = new EnlivenButtonBuilder().WithStyle(ButtonStyle.Primary).WithDisabled(true);
            _enlivenComponentBuilder.WithButton(firstButton.Clone().WithEmote(CommonEmoji.NoEntry).WithCustomId("rm").WithLabel(_loc.Get("Effects.Remove")));
            _enlivenComponentBuilder.WithButton(firstButton.Clone().WithTargetRow(1).WithEmote(CommonEmoji.Level).WithCustomId("add").WithLabel(_loc.Get("Effects.Add")));
            for (var i = 0; i < 4; i++) {
                _enlivenComponentBuilder.WithButton(new EnlivenButtonBuilder().WithStyle(ButtonStyle.Secondary).WithCustomId($"rm{i}").WithLabel((i + 1).ToString()));
            }
            for (var i = 0; i < 4; i++) {
                var effect = PlayerEffectSource.DefaultEffects[i];
                var builder = new EnlivenButtonBuilder().WithStyle(ButtonStyle.Secondary)
                    .WithTargetRow(1)
                    .WithCustomId($"add{i}").WithLabel(effect.GetSourceName())
                    .WithCallback(component => AddEffect(component, effect));
                _enlivenComponentBuilder.WithButton(builder);
            }
            
            _updateControlMessageTask = new SingleTask(async () => {
                if (_controlMessage != null) {
                    try {
                        await _controlMessage.ModifyAsync(properties => {
                            properties.Embed = _enlivenEmbedBuilder.Build();
                            properties.Content = "";
                            properties.Components = _enlivenComponentBuilder.Build();
                        });
                    }
                    catch (Exception) {
                        if (_controlMessage != null) {
                            (await _targetChannel.GetMessageAsync(_controlMessage.Id)).SafeDelete();
                            _controlMessage = null;
                        }

                        _ = ExecuteShutdown(null!, null!);
                    }
                }
            }) {BetweenExecutionsDelay = TimeSpan.FromSeconds(1.5), CanBeDirty = true};
            _disposable.Add(_updateControlMessageTask);
        }

        public override async Task Initialize(FinalLavalinkPlayer finalLavalinkPlayer) {
            await base.Initialize(finalLavalinkPlayer);
            _controlMessage = await _targetChannel.SendMessageAsync("", embed: _enlivenEmbedBuilder.Build(), component: _enlivenComponentBuilder.Build());
        }

        private void AddEffect(SocketInteraction interaction, IPlayerEffectSource playerEffectSource) {
            Task.Run(async () => {
                var sourceName = playerEffectSource.GetSourceName();
                if (Player.Effects.All(use => use.Effect.SourceName != sourceName)) {
                    var effect = await playerEffectSource.CreateEffect(null);
                    await Player.ApplyEffect(effect, interaction.User);
                }
            });
        }

        public override async Task ChangePlayer(FinalLavalinkPlayer newPlayer) {
            await base.ChangePlayer(newPlayer);
            newPlayer.FiltersChanged.Subscribe(_ => UpdateMessageDescription());
            UpdateMessageDescription();
        }

        private void UpdateMessageDescription() {
            var effects = Player.Effects;
            if (effects.Count == 0) {
                _enlivenEmbedBuilder.Description = _loc.Get("Music.Empty");
                _enlivenComponentBuilder.Entries.Where(pair => pair.Key.StartsWith("rm"))
                    .Do(pair => pair.Value.IsVisible = false);
            }
            else {
                _enlivenEmbedBuilder.Description = effects
                    .Select((use, i) => $"{i + 1}. {use.Effect.DisplayName} by {use.User?.Mention ?? "Unknown"}")
                    .JoinToString("\n");
                _enlivenComponentBuilder.Entries["rm"].IsVisible = true;
                for (var i = 0; i < 4; i++) {
                    var entry = _enlivenComponentBuilder.Entries[$"rm{i}"];
                    if (i < effects.Count) {
                        entry.IsVisible = true;
                        var playerEffectUse = effects[i];
                        // ReSharper disable once AsyncVoidLambda
                        entry.Callback = async component => {
                            await Player.RemoveEffect(playerEffectUse, component.User);
                        };
                    }
                    else {
                        entry.IsVisible = false;
                    }
                }
            }

            for (var i = 0; i < 4; i++) {
                var entry = _enlivenComponentBuilder.Entries[$"add{i}"];
                entry.IsVisible = effects.All(use => use.Effect.SourceName != entry.Label);
                entry.Disabled = false;
            }
            
            if (effects.Count >= 4) {
                _enlivenComponentBuilder.Entries.Where(pair => pair.Key.StartsWith("add"))
                    .Do(pair => pair.Value.Disabled = true);
            }

            _updateControlMessageTask.Execute();
        }

        public override async Task ExecuteShutdown(IEntry header, IEntry body) {
            await base.ExecuteShutdown(header, body);
            _controlMessage.SafeDelete();
            _disposable?.Dispose();
            _paginatedMessage.Dispose();
        }

        public override Task LeaveNotification(IEntry header, IEntry body) {
            return Task.CompletedTask;
        }
    }
}