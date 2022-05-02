using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bot.DiscordRelated.Music;
using Bot.Utilities;
using Common;
using Common.Config;
using Common.Localization.Entries;
using Common.Music.Controller;
using Common.Music.Players;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Lavalink4NET.Lyrics;

#pragma warning disable 4014

namespace Bot.DiscordRelated.Commands.Modules {
    public partial class MusicModuleBase : AdvancedModuleBase {
        private static Dictionary<ulong, NonSpamMessageController> ErrorsMessagesControllers = new();
        
        public IMusicController MusicController { get; set; } = null!;
        public EmbedPlayerDisplayProvider EmbedPlayerDisplayProvider { get; set; } = null!;
        public LyricsService LyricsService { get; set; } = null!;
        public EmbedPlayerQueueDisplayProvider EmbedPlayerQueueDisplayProvider { get; set; } = null!;
        
        public FinalLavalinkPlayer Player { get; private set; } = null!;

        protected override void BeforeExecute(CommandInfo command) {
            base.BeforeExecute(command);
            var shouldCreatePlayer = command.Attributes.Any(attribute => attribute is ShouldCreatePlayerAttribute);
            var requireNonEmptyPlaylist = command.Attributes.Any(attribute => attribute is RequireNonEmptyPlaylistAttribute);
            var requirePlayingTrack = command.Attributes.Any(attribute => (attribute as RequireNonEmptyPlaylistAttribute)?.RequirePlayingTrack == true);
            BeforeExecuteAsync(shouldCreatePlayer, requireNonEmptyPlaylist, requirePlayingTrack).GetAwaiter().GetResult();
        }

        public override async Task BeforeExecuteAsync(ICommandInfo command) {
            await base.BeforeExecuteAsync(command);
            var shouldCreatePlayer = command.Attributes.Any(attribute => attribute is ShouldCreatePlayerAttribute);
            var requireNonEmptyPlaylist = command.Attributes.Any(attribute => attribute is RequireNonEmptyPlaylistAttribute);
            var requirePlayingTrack = command.Attributes.Any(attribute => (attribute as RequireNonEmptyPlaylistAttribute)?.RequirePlayingTrack == true);
            await BeforeExecuteAsync(shouldCreatePlayer, requireNonEmptyPlaylist, requirePlayingTrack);
        }

        private static readonly IEntry MusicDisabledEntry = new EntryLocalized("Music.MusicDisabled");
        private static readonly IEntry ChannelNotAllowedEntry = new EntryLocalized("Music.ChannelNotAllowed");
        private static readonly IEntry ClusterNotReadyCommandIgnoredEntry = new EntryLocalized("Music.ClusterNotReadyCommandIgnored");
        private static readonly IEntry AwaitingClusterInitializingEntry = new EntryLocalized("Music.AwaitingClusterInitializing");
        private static readonly IEntry AwaitingNodeConnectionEntry = new EntryLocalized("Music.AwaitingNodeConnection");
        private static readonly IEntry NotInVoiceChannelEntry = new EntryLocalized("Music.NotInVoiceChannel");
        private static readonly IEntry OtherVoiceChannelEntry = new EntryLocalized("Music.OtherVoiceChannel");
        private static readonly IEntry NothingPlayingEntry = new EntryLocalized("Music.NothingPlaying");
        private static readonly IEntry CantConnectEntry = new EntryLocalized("Music.CantConnect");
        protected virtual async Task BeforeExecuteAsync(bool shouldCreatePlayer, bool requireNonEmptyPlaylist, bool requirePlayingTrack) {
            await ReplyAndThrowIfAsync(!MusicController.IsMusicEnabled, MusicDisabledEntry);

            var channelInfo = GetChannelInfo();
            await ReplyAndThrowIfAsync(!channelInfo.IsCommandAllowed, ChannelNotAllowedEntry.WithArg(Context.User.Mention, channelInfo.MusicChannel!));

            if (!MusicController.ClusterTask.IsCompleted) {
                await ReplyAndThrowIfAsync(!shouldCreatePlayer, ClusterNotReadyCommandIgnoredEntry);
                var loadingEntry = await ReplyEntryAsync(AwaitingClusterInitializingEntry, TimeSpan.FromDays(1));
                await MusicController.ClusterTask;
                await loadingEntry.DeleteAsync();
            }

            var controller = await MusicController.ClusterTask;
            if (!controller.IsAnyNodeAvailable) {
                var loadingEntry = await ReplyEntryAsync(AwaitingNodeConnectionEntry, TimeSpan.FromDays(1));
                await controller.NodeAvailableTask;
                await loadingEntry.DeleteAsync();
            }

            var userVoiceChannel = (Context.User as IVoiceState)?.VoiceChannel;
            var userVoiceChannelId = userVoiceChannel?.Id;
            await ReplyAndThrowIfAsync(userVoiceChannelId == null, NotInVoiceChannelEntry.WithArg(Context.User.Mention));

            var player = MusicController.GetPlayer(Context.Guild.Id);
            if (player == null) {
                await ReplyAndThrowIfAsync(!shouldCreatePlayer, NothingPlayingEntry);
                await ReplyAndThrowIfAsync(requireNonEmptyPlaylist, NothingPlayingEntry);

                var perms = (await Context.Guild.GetCurrentUserAsync()).GetPermissions(userVoiceChannel);
                await ReplyAndThrowIfAsync(!perms.Connect, CantConnectEntry.WithArg($"<#{userVoiceChannelId}>"));

                player = await MusicController.ProvidePlayer(Context.Guild.Id, userVoiceChannelId!.Value);
                EmbedPlayerDisplayProvider.Provide(await channelInfo.GetTargetChannelAsync(), player);
            }
            else {
                await ReplyAndThrowIfAsync(requirePlayingTrack && player.CurrentTrack == null, NothingPlayingEntry);
                await ReplyAndThrowIfAsync(requireNonEmptyPlaylist && player.Playlist.IsEmpty, NothingPlayingEntry);
                await ReplyAndThrowIfAsync(userVoiceChannelId != player.VoiceChannelId, OtherVoiceChannelEntry);
            }
            Player = player;

            if (!channelInfo.IsCurrentChannelSuitable) {
                await ReplyFormattedAsync(Loc.Get("Music.PlaybackMoved").Format(channelInfo.MusicChannel));
            }
        }

        private async Task ReplyAndThrowIfAsync(bool condition, IEntry entry) {
            if (!condition) return;
            _ = await ReplyEntryAsync(entry, Constants.ShortTimeSpan);
            throw new CommandInterruptionException(entry);
        }

        protected MusicCommandChannelInfo GetChannelInfo() {
            var musicChannelId = GuildConfig.GetChannel(ChannelFunction.Music, out var m) ? m : (ulong?)null;
            var isMusicLimited = musicChannelId != null && GuildConfig.IsMusicLimited;
            return new MusicCommandChannelInfo(Context.Channel.Id, musicChannelId, isMusicLimited, Context);
        }

        protected async Task<IRepliedEntry> ReplyEntryAsync(IEntry entry, TimeSpan? timeout = null) {
            var nonSpamMessageController = GetNonSpamMessageController();
            var repliedEntry = nonSpamMessageController.AddRepliedEntry(entry, timeout);
            await nonSpamMessageController.Update();
            return repliedEntry;
        }

        protected NonSpamMessageController GetNonSpamMessageController() {
            if (!ErrorsMessagesControllers.TryGetValue(Context.Channel.Id, out var nonSpamMessageController)) {
                nonSpamMessageController = new NonSpamMessageController(Loc, Context.Channel, Loc.Get("Music.Fail"));
                ErrorsMessagesControllers[Context.Channel.Id] = nonSpamMessageController;
            }
            return nonSpamMessageController;
        }

        protected async Task<EmbedPlayerDisplay> GetMainPlayerDisplay() {
            var channelInfo = GetChannelInfo();
            return EmbedPlayerDisplayProvider.Provide(await channelInfo.GetTargetChannelAsync(), Player);
        }

        protected async Task<IUserMessage> ReplyFormattedAsync(string description, bool isFail = false, IUserMessage? previous = null,
                                                               IMessageChannel? channel = null) {
            var embed = this.GetAuthorEmbedBuilder().WithTitle(Loc.Get(isFail ? "Music.Fail" : "Music.Playback"))
                .WithDescription(description).WithColor(isFail ? Color.Orange : Color.Gold).Build();
            if (previous == null) {
                return await (channel ?? Context.Channel).SendMessageAsync(null, false, embed).ConfigureAwait(false);
            }

            await previous.ModifyAsync(properties => {
                properties.Content = "";
                properties.Embed = embed;
            });
            return previous;
        }

        protected override void AfterExecute(CommandInfo command) {
            // By a lucky coincidence of circumstances, it is only necessary to clear the message-command when it does not require the player’s summon
            // That is, it is a command for ordering music
            var needSummon = command.Attributes.FirstOrDefault(attribute => attribute is ShouldCreatePlayerAttribute) != null;
            if (!needSummon) {
                Context.Message.SafeDelete();
            }

            base.AfterExecute(command);
        }

        protected record MusicCommandChannelInfo(ulong CurrentChannel, ulong? MusicChannel, bool IsMusicLimited, ICommandContext Context) {
            public bool IsCurrentChannelSuitable => MusicChannel == null || CurrentChannel == MusicChannel;
            public bool IsCommandAllowed => IsCurrentChannelSuitable || !IsMusicLimited;
            public ulong TargetChannelId => MusicChannel ?? CurrentChannel;
            private ICommandContext Context { get; init; } = Context;
            public async Task<ITextChannel> GetTargetChannelAsync() {
                if (Context.Channel.Id == TargetChannelId) {
                    return (ITextChannel)Context.Channel;
                }

                return await Context.Guild.GetTextChannelAsync(TargetChannelId)
                    .PipeAsync(channel => channel ?? (ITextChannel)Context.Channel);
            }
        }
    }
}