using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bot.DiscordRelated.Music;
using Bot.Utilities;
using Bot.Utilities.Music;
using Common;
using Common.Config;
using Common.Music;
using Common.Music.Controller;
using Common.Music.Players;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Lavalink4NET.Lyrics;
using Lavalink4NET.Player;

#pragma warning disable 4014

namespace Bot.DiscordRelated.Commands.Modules {
    public class MusicModuleBase : AdvancedModuleBase {
        public IMessageChannel ResponseChannel = null!;
        // Actually it can be null but only if IsPreconditionsValid is false
        public FinalLavalinkPlayer? Player;
        public Task<bool> IsPreconditionsValid = null!;
        public static Dictionary<ulong, NonSpamMessageController> ErrorsMessagesControllers = new Dictionary<ulong, NonSpamMessageController>();
        public NonSpamMessageController ErrorMessageController = null!;
        internal EmbedPlayerDisplay? MainDisplay;
        public IMusicController MusicController { get; set; } = null!;
        public EmbedPlayerDisplayProvider EmbedPlayerDisplayProvider { get; set; } = null!;
        public LyricsService LyricsService { get; set; } = null!;

        protected override void BeforeExecute(CommandInfo command) {
            base.BeforeExecute(command);
            GetChannel(out ResponseChannel);
            IsPreconditionsValid = InitialSetup(command);
        }

        private async Task<bool> InitialSetup(CommandInfo command) {
            if (!ErrorsMessagesControllers.TryGetValue(Context.Channel.Id, out var nonSpamMessageController)) {
                nonSpamMessageController = new NonSpamMessageController(Context.Channel, Loc.Get("Music.Fail"))
                                          .AddChannel(await Context.User.GetOrCreateDMChannelAsync()).UpdateTimeout(Constants.StandardTimeSpan);
                nonSpamMessageController.ResetTimeoutOnUpdate = true;
                ErrorsMessagesControllers[Context.Channel.Id] = nonSpamMessageController;
            }

            ErrorMessageController = nonSpamMessageController;

            try {
                if (!MusicController.Cluster.IsInitialized)
                {
                    nonSpamMessageController.AddEntry(Loc.Get("Music.ClusterInitializing")).Update();
                    return false;
                }
                
                if (!MusicController.Cluster.Nodes.Any(node => node.IsConnected))
                {
                    nonSpamMessageController.AddEntry(Loc.Get("Music.NoNodesAvailable")).Update();
                    return false;
                }
                
                Player = MusicController.GetPlayer(Context.Guild.Id)!;
            }
            catch (NullReferenceException) {
                nonSpamMessageController.AddEntry(Loc.Get("Music.MusicDisabled")).Update();
                return false;
            }

            if (GetChannel(out var musicChannel)) {
                var user = Context.User as SocketGuildUser;
                MainDisplay = EmbedPlayerDisplayProvider.Get((ITextChannel) musicChannel);
                if (user?.VoiceChannel?.Id == Player?.VoiceChannelId && user?.VoiceChannel?.Id != null &&
                    Player!.State != PlayerState.NotConnected && Player.State != PlayerState.Destroyed) return true;

                var needSummon = command.Attributes.FirstOrDefault(attribute => attribute is SummonToUserAttribute) != null;

                if (Player != null && Player.State != PlayerState.NotConnected && Player.State != PlayerState.Destroyed && !needSummon) {
                    nonSpamMessageController.AddEntry(Loc.Get("Music.OtherVoiceChannel").Format(Context.User.Mention)).Update();
                    return false;
                }

                if (!needSummon) return true;
                if (user!.VoiceState.HasValue) {
                    var perms = (await Context.Guild.GetCurrentUserAsync()).GetPermissions(user.VoiceChannel);
                    if (!perms.Connect) {
                        nonSpamMessageController.AddEntry(Loc.Get("Music.CantConnect").Format(user.VoiceChannel!.Name)).Update();
                        return false;
                    }

                    Player = await MusicController.ProvidePlayer(Context.Guild.Id, user.VoiceChannel!.Id);
                    MainDisplay = EmbedPlayerDisplayProvider.Provide((ITextChannel) musicChannel, Player);

                    return true;
                }

                nonSpamMessageController.AddEntry(Loc.Get("Music.NotInVoiceChannel").Format(Context.User.Mention)).Update();
                return false;
            }

            if (GuildConfig.IsMusicLimited) {
                nonSpamMessageController.AddEntry(Loc.Get("Music.ChannelNotAllowed").Format(Context.User.Mention, musicChannel.Id)).Update();
                return false;
            }

            nonSpamMessageController.AddEntry(Loc.Get("Music.PlaybackMoved").Format(musicChannel.Id)).Update();
            return true;
        }

        // ReSharper disable once InconsistentNaming
        protected override async Task<IUserMessage> ReplyAsync(string? message = null, bool isTTS = false, Embed? embed = null, RequestOptions? options = null, AllowedMentions? allowedMentions = null, MessageReference? messageReference = null) {
            return await ResponseChannel.SendMessageAsync(message, isTTS, embed, options).ConfigureAwait(false);
        }

        protected async Task<IUserMessage> ReplyFormattedAsync(string description, bool isFail = false, IUserMessage? previous = null,
                                                               IMessageChannel? channel = null) {
            var embed = this.GetAuthorEmbedBuilder().WithTitle(Loc.Get(isFail ? "Music.Fail" : "Music.Playback"))
                            .WithDescription(description).WithColor(isFail ? Color.Orange : Color.Gold).Build();
            if (previous == null) {
                return await (channel ?? ResponseChannel).SendMessageAsync(null, false, embed).ConfigureAwait(false);
            }

            await previous.ModifyAsync(properties => {
                properties.Content = "";
                properties.Embed = embed;
            });
            return previous;
        }

        public bool GetChannel(out IMessageChannel channel) {
            channel = Context.Channel;
            if (!GuildConfig.GetChannel(ChannelFunction.Music, out var musicChannelId) || musicChannelId == channel.Id) return true;
            channel = Context.Guild.GetTextChannelAsync(musicChannelId).Result;
            return false;
        }
        protected override void AfterExecute(CommandInfo command) {
            // By a lucky coincidence of circumstances, it is only necessary to clear the message-command when it does not require the player’s summon
            // That is, it is a command for ordering music
            var needSummon = command.Attributes.FirstOrDefault(attribute => attribute is SummonToUserAttribute) != null;
            if (!needSummon) {
                Context.Message.SafeDelete();
            }

            base.AfterExecute(command);
        }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    sealed class SummonToUserAttribute : Attribute { }
}