using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Bot.DiscordRelated.Commands.Modules.Contexts;
using Bot.DiscordRelated.Music;
using Bot.Music.Cluster;
using Bot.Music.Players;
using Bot.Music.Players.Options;
using Common;
using Common.Config;
using Common.Localization.Entries;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Lavalink4NET.Players;
using Microsoft.Extensions.Options;

#pragma warning disable 4014

namespace Bot.DiscordRelated.Commands.Modules;

public abstract class MusicModuleBase : AdvancedModuleBase {
    private static readonly ConcurrentDictionary<ulong, NonSpamMessageController> ErrorsMessagesControllers = new();

    protected static readonly IEntry ChannelNotAllowedEntry = new EntryLocalized("Music.ChannelNotAllowed");
    protected static readonly IEntry AwaitingNodeConnectionEntry = new EntryLocalized("Music.AwaitingNodeConnection");
    protected static readonly IEntry NotInVoiceChannelEntry = new EntryLocalized("Music.NotInVoiceChannel");
    protected static readonly IEntry OtherVoiceChannelEntry = new EntryLocalized("Music.OtherVoiceChannel");
    protected static readonly IEntry NothingPlayingEntry = new EntryLocalized("Music.NothingPlaying");
    protected static readonly IEntry CantConnectEntry = new EntryLocalized("Music.CantConnect");
    protected static readonly IEntry CantRetrievePlayerEntry = new EntryLocalized("Music.CantRetrievePlayer");
    protected static readonly IEntry PlaybackEntry = new EntryLocalized("Music.Playback");
    protected static readonly IEntry PlaybackMovedEntry = new EntryLocalized("Music.PlaybackMoved");

    public EmbedPlayerDisplayProvider EmbedPlayerDisplayProvider { get; set; } = null!;
    public EmbedPlayerQueueDisplayProvider EmbedPlayerQueueDisplayProvider { get; set; } = null!;
    public IEnlivenClusterAudioService AudioService { get; set; } = null!;
    protected EnlivenLavalinkPlayer? Player { get; private set; }

    public sealed override async Task BeforeExecuteAsync(CommandInfo command) {
        await base.BeforeExecuteAsync(command);
        await BeforeExecuteAsync(command.Attributes);
    }

    public sealed override async Task BeforeExecuteAsync(ICommandInfo command) {
        await base.BeforeExecuteAsync(command);
        await BeforeExecuteAsync(command.Attributes);
    }

    private async Task BeforeExecuteAsync(IReadOnlyCollection<Attribute> attributes) {
        var userVoiceChannel = (Context.User as IVoiceState)?.VoiceChannel;
        var userVoiceChannelId = userVoiceChannel?.Id;

        var channelInfo = GetChannelInfo();
        Player = await ResolvePlayerBeforeExecuteAsync(attributes, channelInfo, userVoiceChannelId);

        // Probably here we also need to check if player isn't null actually
        if (!channelInfo.IsCurrentChannelSuitable)
            await this.ReplyFormattedAsync(PlaybackEntry, PlaybackMovedEntry.WithArg(channelInfo.MusicChannel!), true);

        if (Context.NeedResponse && channelInfo.IsCurrentChannelSuitable &&
            Context.Channel is ITextChannel textChannel && Player is not null) {
            var embedPlayerDisplay = EmbedPlayerDisplayProvider.Get(textChannel);
            if (embedPlayerDisplay != null) {
                await embedPlayerDisplay.ResendControlMessageWithOverride(OverrideSendingControlMessage, false);
            }
        }
    }

    protected virtual async Task<EnlivenLavalinkPlayer?> ResolvePlayerBeforeExecuteAsync(
        IReadOnlyCollection<Attribute> attributes,
        MusicCommandChannelInfo channelInfo, ulong? userVoiceChannelId) {
        await ReplyAndThrowIfAsync(!channelInfo.IsCommandAllowed,
            ChannelNotAllowedEntry.WithArg(Context.User.Mention, channelInfo.MusicChannel!));

        var anyNodeAvailableTask = AudioService.WaitForAnyNodeAvailable();
        if (!anyNodeAvailableTask.IsCompleted) {
            var loadingEntry = await ReplyEntryAsync(AwaitingNodeConnectionEntry, TimeSpan.FromDays(1));
            await anyNodeAvailableTask;
            await loadingEntry.DeleteAsync();
        }

        await ReplyAndThrowIfAsync(userVoiceChannelId == null, NotInVoiceChannelEntry.WithArg(Context.User.Mention));

        if (AudioService.Players.TryGetPlayer<EnlivenLavalinkPlayer>(Context.Guild.Id, out var player)) {
            await ReplyAndThrowIfAsync(userVoiceChannelId != player!.VoiceChannelId,
                OtherVoiceChannelEntry.WithArg(Context.User.Mention));
        }

        return player;
    }

    protected async Task<EnlivenLavalinkPlayer> CheckUserAndCreatePlayerAsync(
        PlayerRetrieveOptions playerRetrieveOptions, PlaylistLavalinkPlayerOptions? options = null,
        MusicCommandChannelInfo? channelInfo = null) {
        var userVoiceChannel = (Context.User as IVoiceState)?.VoiceChannel;
        var userVoiceChannelId = userVoiceChannel?.Id;

        var currentUser = await Context.Guild.GetCurrentUserAsync();
        var perms = currentUser.GetPermissions((Context.User as IVoiceState)?.VoiceChannel);
        await ReplyAndThrowIfAsync(!perms.Connect, CantConnectEntry.WithArg($"<#{userVoiceChannelId}>"));

        return await CreatePlayerAsync(userVoiceChannelId!.Value, playerRetrieveOptions, options, channelInfo);
    }

    protected async Task<EnlivenLavalinkPlayer> CreatePlayerAsync(ulong userVoiceChannelId,
        PlayerRetrieveOptions playerRetrieveOptions, PlaylistLavalinkPlayerOptions? options = null,
        MusicCommandChannelInfo? channelInfo = null) {
        var playlistLavalinkPlayerOptions = options ?? new PlaylistLavalinkPlayerOptions();
        var optionsWrapper = new OptionsWrapper<PlaylistLavalinkPlayerOptions>(playlistLavalinkPlayerOptions);
        var retrieveResult = await AudioService.Players.RetrieveAsync(Context.Guild.Id, userVoiceChannelId,
            EnlivenClusterAudioService.EnlivenPlayerFactory, optionsWrapper, playerRetrieveOptions);

        if (!retrieveResult.IsSuccess) {
            await ReplyEntryAsync(CantRetrievePlayerEntry);
            throw new CommandInterruptionException(CantRetrievePlayerEntry);
        }

        Player = retrieveResult.Player;

        channelInfo = GetChannelInfo();
        var embedPlayerDisplay = EmbedPlayerDisplayProvider.Provide(await channelInfo.GetTargetChannelAsync());
        if (channelInfo.IsCurrentChannelSuitable && Context.NeedResponse)
            await embedPlayerDisplay.ResendControlMessageWithOverride(OverrideSendingControlMessage, false);
        await embedPlayerDisplay.Initialize(Player);

        return Player;
    }

    protected async Task<IUserMessage> OverrideSendingControlMessage(Embed embed, MessageComponent? component) {
        var sentMessage = await Context.SendMessageAsync(null, embed, false, component);
        return await sentMessage.GetMessageAsync();
    }

    protected async Task ReplyAndThrowIfAsync(bool condition, IEntry entry) {
        if (!condition) return;
        _ = await ReplyEntryAsync(entry, Constants.ShortTimeSpan);
        throw new CommandInterruptionException(entry);
    }

    protected MusicCommandChannelInfo GetChannelInfo() {
        var musicChannelId = GuildConfig.GetChannel(ChannelFunction.Music, out var m) ? m : (ulong?)null;
        var dedicatedMusicChannelId =
            GuildConfig.GetChannel(ChannelFunction.DedicatedMusic, out var d) ? d : (ulong?)null;
        var isMusicLimited = musicChannelId != null && GuildConfig.IsMusicLimited;
        return new MusicCommandChannelInfo(Context.Channel.Id, musicChannelId, dedicatedMusicChannelId, isMusicLimited,
            Context);
    }

    protected async Task<IRepliedEntry> ReplyEntryAsync(IEntry entry, TimeSpan? timeout = null) {
        var nonSpamMessageController = ErrorsMessagesControllers
            .GetOrAdd(Context.Channel.Id, CreateNonSpanMessageController);
        var repliedEntry = nonSpamMessageController.AddRepliedEntry(entry, timeout);
        if (Context.NeedResponse)
            await nonSpamMessageController.ResendWithOverride(OverrideSendingControlMessage);
        else
            await nonSpamMessageController.Update();
        return repliedEntry;

        NonSpamMessageController CreateNonSpanMessageController(ulong arg) {
            return new NonSpamMessageController(Loc, Context.Channel, Loc.Get("Music.Fail"));
        }
    }

    protected async Task<EmbedPlayerDisplay> GetMainPlayerDisplay() {
        Debug.Assert(Player is not null);
        var channelInfo = GetChannelInfo();
        var embedPlayerDisplay = EmbedPlayerDisplayProvider.Provide(await channelInfo.GetTargetChannelAsync());
        if (!embedPlayerDisplay.IsInitialized) await embedPlayerDisplay.Initialize(Player);
        return embedPlayerDisplay;
    }

    protected record MusicCommandChannelInfo(
        ulong CurrentChannel,
        ulong? MusicChannel,
        ulong? DedicatedMusicChannel,
        bool IsMusicLimited,
        ICommonModuleContext Context) {
        public bool IsCurrentChannelSuitable => MusicChannel == null || CurrentChannel == MusicChannel ||
                                                CurrentChannel == DedicatedMusicChannel;

        public bool IsCommandAllowed => IsCurrentChannelSuitable || !IsMusicLimited;
        public ulong TargetChannelId => MusicChannel ?? CurrentChannel;
        private ICommonModuleContext Context { get; init; } = Context;

        public async Task<ITextChannel> GetTargetChannelAsync() {
            if (Context.Channel.Id == TargetChannelId) return (ITextChannel)Context.Channel;

            return await Context.Guild.GetTextChannelAsync(TargetChannelId)
                .PipeAsync(channel => channel ?? (ITextChannel)Context.Channel);
        }
    }
}