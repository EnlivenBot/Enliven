using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bot.DiscordRelated.Commands.Attributes;
using Bot.DiscordRelated.Commands.Modules.Contexts;
using Bot.DiscordRelated.Music;
using Common;
using Common.Config;
using Common.Localization.Entries;
using Common.Music.Cluster;
using Common.Music.Players;
using Common.Music.Players.Options;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Lavalink4NET.Players;
using Microsoft.Extensions.Options;

#pragma warning disable 4014

namespace Bot.DiscordRelated.Commands.Modules;

public partial class MusicModuleBase : AdvancedModuleBase
{
    private static readonly Dictionary<ulong, NonSpamMessageController> ErrorsMessagesControllers = new();

    private static readonly IEntry ChannelNotAllowedEntry = new EntryLocalized("Music.ChannelNotAllowed");
    private static readonly IEntry AwaitingNodeConnectionEntry = new EntryLocalized("Music.AwaitingNodeConnection");
    private static readonly IEntry NotInVoiceChannelEntry = new EntryLocalized("Music.NotInVoiceChannel");
    private static readonly IEntry OtherVoiceChannelEntry = new EntryLocalized("Music.OtherVoiceChannel");
    private static readonly IEntry NothingPlayingEntry = new EntryLocalized("Music.NothingPlaying");
    private static readonly IEntry CantConnectEntry = new EntryLocalized("Music.CantConnect");
    private static readonly IEntry PlaybackEntry = new EntryLocalized("Music.Playback");
    private static readonly IEntry PlaybackMovedEntry = new EntryLocalized("Music.PlaybackMoved");

    public EmbedPlayerDisplayProvider EmbedPlayerDisplayProvider { get; private set; } = null!;
    public EmbedPlayerQueueDisplayProvider EmbedPlayerQueueDisplayProvider { get; private set; } = null!;
    public IEnlivenClusterAudioService AudioService { get; private set; } = null!;

    public EnlivenLavalinkPlayer Player { get; private set; } = null!;

    public override async Task BeforeExecuteAsync(CommandInfo command)
    {
        await base.BeforeExecuteAsync(command);
        var shouldCreatePlayer = command.Attributes.Any(attribute => attribute is ShouldCreatePlayerAttribute);
        var requireNonEmptyPlaylist =
            command.Attributes.Any(attribute => attribute is RequireNonEmptyPlaylistAttribute);
        var requirePlayingTrack = command.Attributes.Any(attribute =>
            (attribute as RequireNonEmptyPlaylistAttribute)?.RequirePlayingTrack == true);
        await BeforeExecuteAsync(shouldCreatePlayer, requireNonEmptyPlaylist, requirePlayingTrack);
    }

    public override async Task BeforeExecuteAsync(ICommandInfo command)
    {
        await base.BeforeExecuteAsync(command);
        var shouldCreatePlayer = command.Attributes.Any(attribute => attribute is ShouldCreatePlayerAttribute);
        var requireNonEmptyPlaylist =
            command.Attributes.Any(attribute => attribute is RequireNonEmptyPlaylistAttribute);
        var requirePlayingTrack = command.Attributes.Any(attribute =>
            (attribute as RequireNonEmptyPlaylistAttribute)?.RequirePlayingTrack == true);
        await BeforeExecuteAsync(shouldCreatePlayer, requireNonEmptyPlaylist, requirePlayingTrack);
    }

    protected virtual async Task BeforeExecuteAsync(bool shouldCreatePlayer, bool requireNonEmptyPlaylist,
        bool requirePlayingTrack)
    {
        var channelInfo = GetChannelInfo();
        await ReplyAndThrowIfAsync(!channelInfo.IsCommandAllowed,
            ChannelNotAllowedEntry.WithArg(Context.User.Mention, channelInfo.MusicChannel!));

        var anyNodeAvailableTask = AudioService.WaitForAnyNodeAvailable();
        if (!anyNodeAvailableTask.IsCompleted)
        {
            var loadingEntry = await ReplyEntryAsync(AwaitingNodeConnectionEntry, TimeSpan.FromDays(1));
            await anyNodeAvailableTask;
            await loadingEntry.DeleteAsync();
        }

        var userVoiceChannel = (Context.User as IVoiceState)?.VoiceChannel;
        var userVoiceChannelId = userVoiceChannel?.Id;
        await ReplyAndThrowIfAsync(userVoiceChannelId == null, NotInVoiceChannelEntry.WithArg(Context.User.Mention));

        if (AudioService.Players.TryGetPlayer<EnlivenLavalinkPlayer>(Context.Guild.Id, out var player))
        {
            await ReplyAndThrowIfAsync(requirePlayingTrack && player!.CurrentTrack == null, NothingPlayingEntry);
            await ReplyAndThrowIfAsync(requireNonEmptyPlaylist && player!.Playlist.IsEmpty, NothingPlayingEntry);
            await ReplyAndThrowIfAsync(userVoiceChannelId != player!.VoiceChannelId,
                OtherVoiceChannelEntry.WithArg(Context.User.Mention));
        }
        else
        {
            await ReplyAndThrowIfAsync(!shouldCreatePlayer, NothingPlayingEntry);
            await ReplyAndThrowIfAsync(requireNonEmptyPlaylist, NothingPlayingEntry);

            var perms = (await Context.Guild.GetCurrentUserAsync()).GetPermissions(userVoiceChannel);
            await ReplyAndThrowIfAsync(!perms.Connect, CantConnectEntry.WithArg($"<#{userVoiceChannelId}>"));

            var optionsWrapper = new OptionsWrapper<PlaylistLavalinkPlayerOptions>(new PlaylistLavalinkPlayerOptions());
            var playerRetrieveOptions = new PlayerRetrieveOptions
            {
                ChannelBehavior = PlayerChannelBehavior.Join
            };
            var retrieveResult = await AudioService.Players.RetrieveAsync(Context.Guild.Id, userVoiceChannelId!.Value,
                EnlivenClusterAudioService.EnlivenPlayerFactory, optionsWrapper, playerRetrieveOptions);
            if (!retrieveResult.IsSuccess)
            {
                // TODO: Implement error handling
                return;
            }

            player = retrieveResult.Player;
            var embedPlayerDisplay = EmbedPlayerDisplayProvider.Provide(await channelInfo.GetTargetChannelAsync());
            if (channelInfo.IsCurrentChannelSuitable && Context.NeedResponse)
                await embedPlayerDisplay.ResendControlMessageWithOverride(OverrideSendingControlMessage, false);
            await embedPlayerDisplay.Initialize(player);
        }

        Player = player;

        if (!channelInfo.IsCurrentChannelSuitable)
            await this.ReplyFormattedAsync(PlaybackEntry, PlaybackMovedEntry.WithArg(channelInfo.MusicChannel!), true);
    }

    public override async Task AfterExecuteAsync(ICommandInfo command)
    {
        if (Context.NeedResponse)
        {
            var channelInfo = GetChannelInfo();
            if (channelInfo.IsCurrentChannelSuitable && Context.Channel is ITextChannel textChannel)
                EmbedPlayerDisplayProvider.Get(textChannel)
                    ?.ResendControlMessageWithOverride(OverrideSendingControlMessage);
        }

        await base.AfterExecuteAsync(command);
    }

    public override async Task AfterExecuteAsync(CommandInfo command)
    {
        var shouldRemoveMessage =
            command.Attributes.FirstOrDefault(attribute => attribute is ShouldCreatePlayerAttribute) == null;
        if (shouldRemoveMessage) await this.RemoveMessageInvokerIfPossible();
        await base.AfterExecuteAsync(command);
    }

    protected async Task<IUserMessage> OverrideSendingControlMessage(Embed embed, MessageComponent component)
    {
        var sentMessage = await Context.SendMessageAsync(null, embed, false, component);
        return await sentMessage.GetMessageAsync();
    }

    private async Task ReplyAndThrowIfAsync(bool condition, IEntry entry)
    {
        if (!condition) return;
        _ = await ReplyEntryAsync(entry, Constants.ShortTimeSpan);
        throw new CommandInterruptionException(entry);
    }

    protected MusicCommandChannelInfo GetChannelInfo()
    {
        var musicChannelId = GuildConfig.GetChannel(ChannelFunction.Music, out var m) ? m : (ulong?)null;
        var dedicatedMusicChannelId =
            GuildConfig.GetChannel(ChannelFunction.DedicatedMusic, out var d) ? d : (ulong?)null;
        var isMusicLimited = musicChannelId != null && GuildConfig.IsMusicLimited;
        return new MusicCommandChannelInfo(Context.Channel.Id, musicChannelId, dedicatedMusicChannelId, isMusicLimited,
            Context);
    }

    protected async Task<IRepliedEntry> ReplyEntryAsync(IEntry entry, TimeSpan? timeout = null)
    {
        var nonSpamMessageController = GetNonSpamMessageController();
        var repliedEntry = nonSpamMessageController.AddRepliedEntry(entry, timeout);
        if (Context.NeedResponse)
            await nonSpamMessageController.ResendWithOverride(OverrideSendingControlMessage);
        else
            await nonSpamMessageController.Update();
        return repliedEntry;
    }

    protected NonSpamMessageController GetNonSpamMessageController()
    {
        if (!ErrorsMessagesControllers.TryGetValue(Context.Channel.Id, out var nonSpamMessageController))
        {
            nonSpamMessageController = new NonSpamMessageController(Loc, Context.Channel, Loc.Get("Music.Fail"));
            ErrorsMessagesControllers[Context.Channel.Id] = nonSpamMessageController;
        }

        return nonSpamMessageController;
    }

    protected async Task<EmbedPlayerDisplay> GetMainPlayerDisplay()
    {
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
        ICommonModuleContext Context)
    {
        public bool IsCurrentChannelSuitable => MusicChannel == null || CurrentChannel == MusicChannel ||
                                                CurrentChannel == DedicatedMusicChannel;

        public bool IsCommandAllowed => IsCurrentChannelSuitable || !IsMusicLimited;
        public ulong TargetChannelId => MusicChannel ?? CurrentChannel;
        private ICommonModuleContext Context { get; init; } = Context;

        public async Task<ITextChannel> GetTargetChannelAsync()
        {
            if (Context.Channel.Id == TargetChannelId) return (ITextChannel)Context.Channel;

            return await Context.Guild.GetTextChannelAsync(TargetChannelId)
                .PipeAsync(channel => channel ?? (ITextChannel)Context.Channel);
        }
    }
}