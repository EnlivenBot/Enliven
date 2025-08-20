using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Criteria;
using Bot.DiscordRelated.Interactions.Handlers;
using Bot.DiscordRelated.MessageComponents;
using Bot.Music.Cluster;
using Bot.Music.Players;
using Common;
using Common.Config.Emoji;
using Common.History;
using Common.Localization.Entries;
using Common.Localization.Providers;
using Common.Music;
using Common.Music.Tracks;
using Common.Utils;
using Discord;
using Lavalink4NET.Artwork;
using Lavalink4NET.Players;
using Microsoft.Extensions.Logging;
using Tyrrrz.Extensions;

#pragma warning disable 4014

namespace Bot.DiscordRelated.Music;

public class EmbedPlayerDisplay : PlayerDisplayBase {
    public const int TrackAuthorMaxLength = 70;
    public const int TrackTitleMaxLength = 170;
    private const int MobileTextChopLimit = 56;
    private readonly IArtworkService _artworkService;

    private readonly IEnlivenClusterAudioService _audioService;
    private readonly CommandHandlerService _commandHandlerService;
    private readonly SingleTask _controlMessageSendTask;
    private readonly IDiscordClient _discordClient;
    private readonly EnlivenEmbedBuilder _embedBuilder;
    private readonly ILocalizationProvider _loc;
    private readonly ILogger<EmbedPlayerDisplay> _logger;
    private readonly MessageComponentInteractionsHandler _messageComponentInteractionsHandler;
    private readonly IGuild? _targetGuild;
    private readonly SingleTask _updateControlMessageTask;

    private CancellationTokenSource? _cancellationTokenSource;
    private IUserMessage? _controlMessage;
    private string? _effectsInParameters;
    private bool _isExternalEmojiAllowed;
    private MessageComponent? _messageComponent;
    private EnlivenComponentBuilder? _messageComponentManager;
    private Disposables? _playerSubscriptions;
    private bool _resendInsteadOfUpdate;
    private SendControlMessageOverride? _sendControlMessageOverride;
    private IMessageChannel _targetChannel;
    public bool NextResendForced;

    public EmbedPlayerDisplay(IMessageChannel targetChannel, IDiscordClient discordClient, ILocalizationProvider loc,
        CommandHandlerService commandHandlerService,
        MessageComponentInteractionsHandler messageComponentInteractionsHandler,
        ILogger<EmbedPlayerDisplay> logger, IArtworkService artworkService, IEnlivenClusterAudioService audioService) {
        _targetGuild = (targetChannel as ITextChannel)?.Guild;
        _messageComponentInteractionsHandler = messageComponentInteractionsHandler;
        _logger = logger;
        _artworkService = artworkService;
        _audioService = audioService;
        _loc = loc;
        _commandHandlerService = commandHandlerService;
        _targetChannel = targetChannel;
        _discordClient = discordClient;

        _embedBuilder = new EnlivenEmbedBuilder();
        _embedBuilder.AddField("State", loc.Get("Music.Empty"), loc.Get("Music.Empty"), true);
        _embedBuilder.AddField("Parameters", loc.Get("Music.Parameters"), loc.Get("Music.Empty"), true);
        _embedBuilder.AddField("Effects", loc.Get("Music.Effects"), loc.Get("Music.Empty"), isEnabled: false);
        _embedBuilder.AddField("Queue", loc.Get("Music.Queue").Format(0, 0, 0), loc.Get("Music.Empty"));
        _embedBuilder.AddField("RequestHistory", loc.Get("Music.RequestHistory"), loc.Get("Music.Empty"));
        _embedBuilder.AddField("Warnings", loc.Get("Music.Warning"), loc.Get("Music.Empty"), false, 100, false);

        _controlMessageSendTask = new SingleTask(SendControlMessageInternal)
            { BetweenExecutionsDelay = TimeSpan.FromSeconds(30), CanBeDirty = false };
        _updateControlMessageTask = new SingleTask(UpdateControlMessageInternal) {
            BetweenExecutionsDelay = TimeSpan.FromSeconds(1.5), CanBeDirty = true,
            ShouldExecuteNonDirtyIfNothingRunning = true
        };
    }

    private async Task SendControlMessageInternal(SingleTaskExecutionData<Unit> data) {
        try {
            var shouldResend =
                NextResendForced
                || _resendInsteadOfUpdate
                || _controlMessage == null
                || await EnsureMessage.NotExists(_targetChannel, _controlMessage.Id, 3);
            if (shouldResend) {
                _resendInsteadOfUpdate = false;
                NextResendForced = false;
                var oldCts = Interlocked.Exchange(ref _cancellationTokenSource, new CancellationTokenSource());
                oldCts?.Cancel();

                UpdateParameters();
                UpdateProgress();
                UpdateQueue();
                UpdateTrackInfo();

                if (_targetGuild != null) {
                    var guildUser = await _targetGuild.GetUserAsync(_discordClient.CurrentUser.Id);
                    var channelPerms = guildUser.GetPermissions((IGuildChannel)_targetChannel);
                    _isExternalEmojiAllowed = channelPerms.UseExternalEmojis;
                    _embedBuilder.Fields["Warnings"].IsEnabled = !channelPerms.UseExternalEmojis;
                    if (_embedBuilder.Fields["Warnings"].IsEnabled)
                        _embedBuilder.Fields["Warnings"].Value = _loc.Get("Music.WarningCustomEmoji");
                }

                var oldControlMessage = _controlMessage;
                await SendControlMessage_WithMessageComponents();
                _logger.LogTrace(
                    "Sent player embed control message. Guild: {TargetGuildId}. Channel: {TargetChannelId}. Message id: {ControlMessageId}",
                    _targetGuild?.Id, _targetChannel.Id, _controlMessage?.Id);
                oldControlMessage.SafeDelete();
            }
            else
                data.OverrideDelay = TimeSpan.FromSeconds(5);
        }
        catch (Exception) {
            // ignored
        }
    }

    private async Task UpdateControlMessageInternal(SingleTaskExecutionData<Unit> data) {
        var internalCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellationTokenSource?.Token ?? CancellationToken.None);
        if (internalCancellationTokenSource.Token.IsCancellationRequested) data.OverrideDelay = TimeSpan.Zero;
        if (_controlMessage != null) {
            try {
                if (_resendInsteadOfUpdate) {
                    _resendInsteadOfUpdate = false;
                    await _controlMessageSendTask.Execute(false, TimeSpan.Zero);
                    return;
                }

                _logger.LogTrace(
                    "Modifying embed control message. Guild: {TargetGuildId}. Channel: {TargetChannelId}. Message id: {ControlMessageId}",
                    _targetGuild?.Id, _targetChannel.Id, _controlMessage.Id);
                await _controlMessage.ModifyAsync(properties => {
                    properties.Embed = _embedBuilder!.Build();
                    properties.Content = "";
                    properties.Components = _messageComponent;
                }, new RequestOptions {
                    CancelToken = internalCancellationTokenSource.Token,
                    RatelimitCallback = RatelimitCallback,
                    RetryMode = RetryMode.AlwaysFail
                });
            }
            catch (TimeoutException) {
                // Ignore all timeouts because weird Discord (Discord.NET?) logic.
                // Updating only one message every 5 seconds will trigger ratelimit exceptions.
                // Fuck it.
            }
            catch (Exception e) {
                _logger.LogTrace(e,
                    "Failed to update embed control message. Guild: {TargetGuildId}. Channel: {TargetChannelId}. Message id: {ControlMessageId}",
                    _targetGuild?.Id, _targetChannel.Id, _controlMessage?.Id);
                if (_controlMessage != null) {
                    _targetChannel.GetMessageAsync(_controlMessage.Id).SafeDelete();
                    _controlMessage = null;
                }

                ControlMessageResend(_targetChannel);
            }
        }

        Task RatelimitCallback(IRateLimitInfo info) {
            if (info.RetryAfter is { } retryAfter) {
                internalCancellationTokenSource.Cancel();
                data.OverrideDelay = retryAfter > data.BetweenExecutionsDelay.GetValueOrDefault().TotalSeconds
                    ? TimeSpan.FromSeconds(retryAfter + 1)
                    : null;
            }

            return Task.CompletedTask;
        }
    }

    public override async Task LeaveNotification(IEntry header, IEntry body) {
        try {
            var embedBuilder = new EmbedBuilder().WithColor(Color.Gold).WithTitle(header.Get(_loc))
                .WithDescription(body.Get(_loc));
            await _targetChannel.SendMessageAsync(null, false, embedBuilder.Build());
        }
        catch (Exception) {
            // ignored
        }
    }

    public override async Task ExecuteShutdown(IEntry header, IEntry body) {
        base.ExecuteShutdown(header, body);
        var oldCts = Interlocked.Exchange(ref _cancellationTokenSource, null);
        // ReSharper disable once MethodHasAsyncOverload
        oldCts?.Cancel();
        var message = _controlMessage;
        _controlMessage = null;

        _playerSubscriptions?.Dispose();
        oldCts?.Dispose();
        _controlMessageSendTask.Dispose();
        _updateControlMessageTask.Dispose();

        if (message != null) {
            var embed = new EmbedBuilder()
                .WithColor(Color.Gold)
                .WithTitle(header.Get(_loc))
                .WithDescription(body.Get(_loc))
                .Build();
            var components = new ComponentBuilder()
                .WithButton(_loc.Get("Music.RestoreStoppedPlayerButton"), "restoreStoppedPlayer")
                .Build();
            await message.ModifyAsync(properties => {
                properties.Content = Optional<string>.Unspecified;
                properties.Embed = embed;
                properties.Components = components;
            });
        }
        else
            await LeaveNotification(header, body);
    }

    public override async Task ChangePlayer(EnlivenLavalinkPlayer newPlayer) {
        _playerSubscriptions?.Dispose();
        await base.ChangePlayer(newPlayer);

        var updateControlMessageSubj = new Subject<Unit>();
        updateControlMessageSubj
            .Throttle(TimeSpan.FromMilliseconds(100))
            .Subscribe(_ => UpdateControlMessage());

        _playerSubscriptions = new Disposables(
            updateControlMessageSubj,
            newPlayer.QueueHistory.HistoryChanged
                .Select(OnHistoryChanged)
                .Where(isChanged => isChanged)
                .Select(_ => Unit.Default)
                .Subscribe(updateControlMessageSubj),
            newPlayer.Playlist.Changed.Subscribe(_ => UpdateQueue()),
            newPlayer.VolumeChanged.Subscribe(_ => UpdateParameters()),
            newPlayer.StateChanged
                .Do(OnStateChanged)
                .Select(_ => Unit.Default)
                .Subscribe(updateControlMessageSubj),
            newPlayer.FiltersChanged.Subscribe(_ => UpdateEffects()),
            newPlayer.CurrentTrackIndexChanged.Subscribe(OnCurrentTrackIndexChanged),
            newPlayer.LoopingStateChanged.Subscribe(OnLoopingStateChanged)
        );

        UpdateNode();
        UpdateQueue();
        UpdateEffects();
        UpdateParameters();
        UpdateMessageComponents();
        UpdateTrackInfo();
        UpdateProgress();
        await ControlMessageResend();
        return;

        void OnCurrentTrackIndexChanged(int _) {
            UpdateProgress();
            UpdateTrackInfo();
            UpdateQueue();
        }

        void OnStateChanged(PlayerState _) {
            UpdateProgress();
            UpdateTrackInfo();
            UpdateMessageComponents();
        }

        void OnLoopingStateChanged(LoopingState _) {
            UpdateProgress();
            UpdateMessageComponents();
        }

        bool OnHistoryChanged(HistoryCollection collection) {
            _embedBuilder.Fields["RequestHistory"].Value =
                collection.GetLastHistory(_loc, out var isChanged).IsBlank(_loc.Get("Music.Empty"));
            return isChanged;
        }
    }

    private async Task SendControlMessage_WithMessageComponents() {
        _messageComponentManager?.Dispose();
        _messageComponentManager = _messageComponentInteractionsHandler.GetBuilder();
        var btnTemplate = new EnlivenButtonBuilder().WithTargetRow(0).WithStyle(ButtonStyle.Secondary);
        _messageComponentManager.WithButton(btnTemplate.Clone().WithEmote(CommonEmoji.LegacyTrackPrevious)
            .WithCustomId("TrackPrevious"));
        _messageComponentManager.WithButton(btnTemplate.Clone().WithEmote(CommonEmoji.LegacyFastReverse)
            .WithCustomId("FastReverse"));
        _messageComponentManager.WithButton(btnTemplate.Clone().WithEmote(CommonEmoji.LegacyPlayPause)
            .WithCustomId("PlayPause").WithStyle(ButtonStyle.Primary));
        _messageComponentManager.WithButton(btnTemplate.Clone().WithEmote(CommonEmoji.LegacyFastForward)
            .WithCustomId("FastForward"));
        _messageComponentManager.WithButton(btnTemplate.Clone().WithEmote(CommonEmoji.LegacyTrackNext)
            .WithCustomId("TrackNext"));
        btnTemplate.WithTargetRow(1);
        _messageComponentManager.WithButton(btnTemplate.Clone().WithEmote(CommonEmoji.BookmarkTabs)
            .WithCustomId("Queue"));
        _messageComponentManager.WithButton(btnTemplate.Clone().WithEmote(CommonEmoji.LegacyShuffle)
            .WithCustomId("Shuffle"));
        _messageComponentManager.WithButton(btnTemplate.Clone().WithEmote(CommonEmoji.LegacyStop).WithCustomId("Stop")
            .WithStyle(ButtonStyle.Danger));
        _messageComponentManager.WithButton(btnTemplate.Clone().WithCustomId("Repeat"));
        _messageComponentManager.WithButton(btnTemplate.Clone().WithEmote(CommonEmoji.E).WithCustomId("Effects"));
        _messageComponentManager.SetCallback(async callback => {
            Debug.Assert(Player is not null);
            var command = callback.CustomId switch {
                "TrackPrevious" => (Player.Position?.Position.TotalSeconds ?? 0) > 15 ? "seek 0s" : "skip -1",
                "FastReverse" => "rw 20s",
                "PlayPause" => Player.State == PlayerState.Paused ? "resume" : "pause",
                "FastForward" => "ff 20s",
                "TrackNext" => "skip",
                "Queue" => "queue",
                "Shuffle" => "shuffle",
                "Stop" => "stop",
                "Repeat" => "repeat",
                "Effects" => "effects current",
                _ => throw new ArgumentOutOfRangeException()
            };

            if (!string.IsNullOrEmpty(command)) {
                await _commandHandlerService.ExecuteCommand(command,
                    new ComponentCommandContext(_discordClient, callback.Context),
                    callback.Context.User.Id.ToString());
            }
        });

        UpdateMessageComponents();

        _messageComponent = _messageComponentManager.Build();
        _controlMessage = await _sendControlMessageOverride.ExecuteAndFallbackWith(_embedBuilder.Build(),
            _messageComponent, _targetChannel, _logger);
        _sendControlMessageOverride = null;
    }

    public async Task ResendControlMessageWithOverride(SendControlMessageOverride sendControlMessageOverride,
        bool executeResend = true) {
        _sendControlMessageOverride = sendControlMessageOverride;
        NextResendForced = true;
        _resendInsteadOfUpdate = true;
        if (!executeResend) return;

        await _controlMessageSendTask.Execute(false, TimeSpan.Zero);
    }

    public void UpdateMessageComponents() {
        if (_messageComponentManager == null) return;
        var entries = _messageComponentManager.Entries;
        var updated = false;

        Debug.Assert(Player is not null);
        var targetPlayPauseEmoji = Player.State == PlayerState.Paused
            ? CommonEmoji.LegacyPlay
            : CommonEmoji.LegacyPause;
        updated = updated || Equals(entries["PlayPause"].Emote, targetPlayPauseEmoji);
        entries["PlayPause"].Emote = targetPlayPauseEmoji;

        var nextRepeatState = Player.LoopingState.Next() switch {
            LoopingState.One => CommonEmoji.RepeatOneBox,
            LoopingState.All => CommonEmoji.RepeatBox,
            LoopingState.Off => CommonEmoji.RepeatOffBox,
            _ => throw new ArgumentOutOfRangeException()
        };
        updated = updated || Equals(entries["Repeat"].Emote, nextRepeatState);
        entries["Repeat"].Emote = nextRepeatState;

        if (updated) _messageComponent = _messageComponentManager.Build();
    }

    public void UpdateProgress(bool background = false) {
        Debug.Assert(Player is not null);
        if (Player.CurrentItem != null) {
            var track = Player.CurrentItem.Track;
            _embedBuilder.Fields["State"].Name = _loc.Get("Music.RequestedBy")
                .Format(Player.CurrentItem?.Requester.ToString(false));

            var progressPercentage = Convert.ToInt32(Player.Position?.Position.TotalSeconds /
                track.Duration.TotalSeconds * 100);
            var emojiPack = _isExternalEmojiAllowed ? ProgressEmoji.CustomEmojiPack : ProgressEmoji.TextEmojiPack;
            var progressBar = emojiPack.GetProgress(progressPercentage);

            var stateString = Player.State switch {
                PlayerState.Playing => _isExternalEmojiAllowed ? CommonEmojiStrings.Instance.Play : "▶",
                PlayerState.Paused => _isExternalEmojiAllowed ? CommonEmojiStrings.Instance.Pause : "⏸",
                _ => _isExternalEmojiAllowed ? CommonEmojiStrings.Instance.Stop : "⏹"
            };
            var loopingStateString = Player.LoopingState switch {
                LoopingState.One => _isExternalEmojiAllowed ? CommonEmojiStrings.Instance.RepeatOne : "🔂",
                LoopingState.All => _isExternalEmojiAllowed ? CommonEmojiStrings.Instance.Repeat : "🔁",
                LoopingState.Off => _isExternalEmojiAllowed ? CommonEmojiStrings.Instance.RepeatOff : "❌",
                _ => throw new InvalidEnumArgumentException()
            };
            var needCustomSourceEmoji = track is ITrackHasCustomSource && _isExternalEmojiAllowed;
            var sb = new StringBuilder(Player.Position?.Position.FormattedToString());
            if (track.IsSeekable) {
                sb.Append(" / ");
                sb.Append(track.Duration.FormattedToString());
            }

            var space = new string(' ', Math.Max(0, ((needCustomSourceEmoji ? 18 : 22) - sb.Length) / 2));
            var detailsBar = stateString + '`' + space + sb + space + '`' + loopingStateString;
            if (needCustomSourceEmoji) {
                var customSourceTrack = (ITrackHasCustomSource)track;
                detailsBar += $"[{customSourceTrack.CustomSourceEmote}]({customSourceTrack.CustomSourceUrl})";
            }

            _embedBuilder.Fields["State"].Value = progressBar + "\n" + detailsBar;
        }
        else {
            _embedBuilder.Fields["State"].Name = _loc.Get("Music.Playback");
            _embedBuilder.Fields["State"].Value = _loc.Get("Music.PlaybackNothingPlaying");
        }
    }

    private async Task UpdateTrackInfo() {
        Debug.Assert(Player is not null);
        var track = Player.CurrentItem?.Track;
        if (Player.CurrentTrackIndex >= Player.Playlist.Count && Player.Playlist.Count != 0) {
            _embedBuilder.Author = new EmbedAuthorBuilder();
            _embedBuilder.Title = _loc.Get("Music.QueueEnd");
            _embedBuilder.Url = "";
            return;
        }

        if (track == null || Player.State is PlayerState.NotPlaying or PlayerState.Destroyed) {
            _embedBuilder.Author = new EmbedAuthorBuilder();
            _embedBuilder.Title = _loc.Get("Music.Waiting");
            _embedBuilder.Url = "";
            return;
        }

        var artwork = await track.ResolveArtwork(_artworkService);
        _embedBuilder
            .WithAuthor(track!.Author.SafeSubstring(TrackAuthorMaxLength, "...").IsBlank("Unknown"),
                artwork?.ToString())
            .WithTitle(track.Title.RemoveNonPrintableChars().SafeSubstring(TrackTitleMaxLength, "...")!)
            .WithUrl(track.Uri?.ToString()!);
    }

    private void UpdateEffects() {
        Debug.Assert(Player is not null);
        var effectsText = ProcessEffectsText(Player.Effects);
        _embedBuilder.Fields["Effects"].Value = effectsText.Or("Placeholder");
        _embedBuilder.Fields["Effects"].IsEnabled = !effectsText.IsNullOrWhiteSpace();
        return;

        string ProcessEffectsText(IReadOnlyList<PlayerEffectUse> effects) {
            try {
                var text = effects.Select(use => use.Effect.DisplayName).JoinToString(" > ");
                if (text.Length < 20) {
                    _effectsInParameters = "\n" + text;
                    return "";
                }

                _effectsInParameters = null;
                return effects.Select(use => $"`{use.Effect.DisplayName}`").JoinToString(" > ");
            }
            finally {
                UpdateParameters();
            }
        }
    }

    private void UpdateParameters() {
        Debug.Assert(Player is not null);
        var volume = (int)(Player.Volume * 200);
        var volumeText = volume is < 50 or > 150 ? $"🔉 ***{volume}%***\n" : $"🔉 {volume}%";
        _embedBuilder.Fields["Parameters"].Value = volumeText + _effectsInParameters;
    }

    private void UpdateQueue() {
        Debug.Assert(Player is not null);
        if (Player.Playlist.Count == 0) {
            _embedBuilder.Fields["Queue"].Name = _loc.Get("Music.QueueEmptyTitle");
            _embedBuilder.Fields["Queue"].Value = _loc.Get("Music.QueueEmpty");
        }
        else {
            _embedBuilder.Fields["Queue"].Name =
                _loc.Get("Music.Queue").Format(Player.CurrentTrackIndex + 1, Player.Playlist.Count,
                    Player.Playlist.TotalPlaylistLength.FormattedToString());
            _embedBuilder.Fields["Queue"].Value = $"```glsl\n{GetPlaylistString()}```";
        }
    }

    // desktop max length in code embed:
    // ├aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
    // mobile:
    // 183 ├AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
    private string GetPlaylistString() {
        Debug.Assert(Player is not null);

        var builder = new StringBuilder();
        var helper = new PlaylistQueueHelper(Player.Playlist, Player.CurrentTrackIndex - 1, 6);
        foreach (var group in helper) {
            var numberMaxLength = (int)Math.Floor(Math.Log10(group.CurrentNumber + group.Count) + 1);

            builder.Append('─', numberMaxLength);
            builder.AppendLine($"─┬────{group.Requester.ToString(false)}");
            foreach (var queueItem in group) {
                var isCurrent = group.CurrentIndex == Player.CurrentTrackIndex;

                var title = queueItem.Track is ITrackHasCustomQueueTitle customQueueTitle
                    ? customQueueTitle.GetQueueTitle()
                    : queueItem.Track.Title;

                FormatTrackString(builder, title,
                    group.CurrentNumber, isCurrent, group.CurrentIsLastButGroupContinues,
                    numberMaxLength);
            }
        }

        return builder.ToString();

        static void FormatTrackString(StringBuilder builder, string title, int trackNumber, bool isCurrent,
            bool isLastInGroup, int numberMaxLength) {
            var trackNumberString = trackNumber.ToString();
            builder.Append(trackNumberString);
            builder.Append(' ', numberMaxLength - trackNumberString.Length);
            builder.Append(' ');
            var groupChar = isLastInGroup switch {
                _ when isCurrent => '#',
                true => '└',
                false => '├'
            };
            builder.Append(groupChar);

            var leftCharsCount = numberMaxLength + 1 + 1;
            var remainingSpace = MobileTextChopLimit - leftCharsCount;

            title = title.RemoveNonPrintableChars();
            if (title.Length <= remainingSpace) {
                builder.AppendLine(title);
                return;
            }

            var potentialFirstLine = title.AsSpan(0, remainingSpace);
            var lastSpaceInFirstLine = potentialFirstLine.LastIndexOf(' ');
            // If space close to the end - lets not force chop text
            if (lastSpaceInFirstLine >= remainingSpace - 8) {
                potentialFirstLine = title.AsSpan(0, lastSpaceInFirstLine + 1);
            }

            builder.Append(potentialFirstLine);
            builder.AppendLine();

            builder.Append(' ', numberMaxLength + 1);
            var groupCharSecond = isLastInGroup switch {
                _ when isCurrent => '#',
                true => ' ',
                _ => '│'
            };
            builder.Append(groupCharSecond);

            builder.AppendLine(title.AsSpan(potentialFirstLine.Length).SafeSubstring(remainingSpace, "..."));
        }
    }

    private void UpdateNode() {
        Debug.Assert(Player is not null);
        var node = _audioService.GetPlayerNode(Player);
        _embedBuilder.WithFooter($"Powered by {_discordClient.CurrentUser.Username} | {node?.Label}");
    }

    public Task UpdateControlMessage(bool background = false) {
        return _updateControlMessageTask.IsDisposed
            ? Task.CompletedTask
            : _updateControlMessageTask.Execute(!background);
    }

    public async Task ControlMessageResend(IMessageChannel? channel = null) {
        if (_controlMessageSendTask.IsDisposed) return;

        _targetChannel = channel ?? _targetChannel;
        await _controlMessageSendTask.Execute(false);
    }
}