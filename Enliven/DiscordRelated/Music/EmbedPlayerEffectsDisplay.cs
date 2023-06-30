using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Bot.DiscordRelated.MessageComponents;
using Common;
using Common.Config.Emoji;
using Common.Localization.Entries;
using Common.Localization.Providers;
using Common.Music.Effects;
using Common.Music.Players;
using Common.Utils;
using Discord;
using Discord.WebSocket;
using Tyrrrz.Extensions;

namespace Bot.DiscordRelated.Music;

public class EmbedPlayerEffectsDisplay : PlayerDisplayBase {
    private readonly SingleTask _updateControlMessageTask;
    private IUserMessage? _controlMessage;
    private CompositeDisposable _disposable = new();
    private EnlivenComponentBuilder _enlivenComponentBuilder;
    private EnlivenEmbedBuilder _enlivenEmbedBuilder = new();
    private ILocalizationProvider _loc;
    private IMessageChannel _targetChannel;

    public EmbedPlayerEffectsDisplay(IMessageChannel targetChannel, ILocalizationProvider loc, MessageComponentService messageComponentService) {
        _loc = loc;
        _targetChannel = targetChannel;

        _enlivenComponentBuilder = messageComponentService.GetBuilder();
        _enlivenEmbedBuilder.Title = _loc.Get("Music.Effects");

        var applyEffectTitle = _loc.Get("Effects.ApplyEffectTitle");
        var effects = PlayerEffectSource.DefaultEffects.Select(source => $"`{source.GetSourceName()}`").JoinToString(", ");
        var applyEffectDescription = _loc.Get("Effects.ApplyEffectDescription", effects);
        _enlivenEmbedBuilder.AddField("ApplyEffects", applyEffectTitle, applyEffectDescription);

        var addCustomEffectTitle = _loc.Get("Effects.AddCustomEffectTitle");
        var addCustomEffectDescription = _loc.Get("Effects.AddCustomEffectDescription");
        _enlivenEmbedBuilder.AddField("CustomEffects", addCustomEffectTitle, addCustomEffectDescription);

        var firstButton = new EnlivenButtonBuilder().WithStyle(ButtonStyle.Primary).WithDisabled(true);
        _enlivenComponentBuilder.WithButton(firstButton.Clone().WithEmote(CommonEmoji.NoEntry).WithCustomId("rm").WithLabel(_loc.Get("Effects.Remove")));
        _enlivenComponentBuilder.WithButton(firstButton.Clone().WithTargetRow(1).WithEmote(CommonEmoji.Level).WithCustomId("add").WithLabel(_loc.Get("Effects.Add")));
        for (var i = 0; i < 4; i++) _enlivenComponentBuilder.WithButton(new EnlivenButtonBuilder().WithStyle(ButtonStyle.Secondary).WithCustomId($"rm{i}").WithLabel((i + 1).ToString()));
        for (var i = 0; i < 4; i++) {
            var effect = PlayerEffectSource.EffectsForButtons[i];
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

                    await ExecuteShutdown(null!, null!);
                }
            }
        }) { BetweenExecutionsDelay = TimeSpan.FromSeconds(1.5), CanBeDirty = true };
        _disposable.Add(_updateControlMessageTask);
    }

    public override async Task Initialize(FinalLavalinkPlayer finalLavalinkPlayer) {
        await base.Initialize(finalLavalinkPlayer);
        _controlMessage = await _targetChannel.SendMessageAsync("", embed: _enlivenEmbedBuilder.Build(), components: _enlivenComponentBuilder.Build());
    }

    private void AddEffect(SocketInteraction interaction, IPlayerEffectSource playerEffectSource) {
        Task.Run(async () => {
            var effects = Player.Effects;
            if (effects.Count >= AdvancedLavalinkPlayer.MaxEffectsCount) return;
            var sourceName = playerEffectSource.GetSourceName();
            if (effects.All(use => use.Effect.SourceName != sourceName)) {
                var effect = await playerEffectSource.CreateEffect(null);
                await Player.ApplyEffect(effect, interaction.User);
            }
        });
    }

    public async Task<bool> EnsureCorrectnessAsync() {
        await _updateControlMessageTask.Execute(false);
        return !IsShutdowned;
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
                entry.IsVisible = i < effects.Count;
                entry.Callback = i < effects.Count ? GetRemoveButtonCallback(i) : null;
            }
        }

        var addButtons = Enumerable.Range(0, 4).Select(i => _enlivenComponentBuilder.Entries[$"add{i}"]).ToList();
        addButtons.Do(builder => builder.IsVisible = effects.All(use => use.Effect.SourceName != builder.Label));
        addButtons.Do(builder => builder.IsDisabled = effects.Count >= AdvancedLavalinkPlayer.MaxEffectsCount);
        _enlivenComponentBuilder.Entries["add"].IsVisible = addButtons.Any(builder => builder.IsVisible);

        _updateControlMessageTask.Execute();

        Action<SocketMessageComponent> GetRemoveButtonCallback(int i) {
            var playerEffectUse = effects[i];
            // ReSharper disable once AsyncVoidLambda
            return async component => {
                await Player.RemoveEffect(playerEffectUse, component.User);
            };
        }
    }

    public override async Task ExecuteShutdown(IEntry header, IEntry body) {
        await base.ExecuteShutdown(header, body);
        _controlMessage.SafeDelete();
        _disposable?.Dispose();
    }

    public override Task LeaveNotification(IEntry header, IEntry body) {
        return Task.CompletedTask;
    }
}