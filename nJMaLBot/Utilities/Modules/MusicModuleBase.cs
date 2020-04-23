using System;
using System.Linq;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Config.Localization;
using Bot.Music;
using Bot.Music.Players;
using Bot.Utilities.Commands;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Lavalink4NET.Player;

namespace Bot.Utilities.Modules {
    public class MusicModuleBase : AdvancedModuleBase {
        public IMessageChannel ResponseChannel;
        public EmbedPlaybackPlayer Player;
        public Task<bool> IsPreconditionsValid;

        protected override async void BeforeExecute(CommandInfo command) {
            base.BeforeExecute(command);
            GetChannel(out ResponseChannel);
            IsPreconditionsValid = InitialSetup(command);
        }

        private async Task<bool> InitialSetup(CommandInfo command) {
            try {
                Player = MusicUtils.Cluster.GetPlayer<EmbedPlaybackPlayer>(Context.Guild.Id);
            }
            catch (NullReferenceException e) {
                Reply(Loc.Get("Music.MusicDisabled"), true).DelayedDelete(TimeSpan.FromMinutes(5));
                return false;
            }
            catch (InvalidOperationException e) {
                switch (e.Message) {
                    case "The cluster has not been initialized.":
                        Reply(Loc.Get("Music.ClusterInitializing"), true).DelayedDelete(TimeSpan.FromMinutes(5));
                        break;
                    case "No node available.":
                        Reply(Loc.Get("Music.NoNodesAvailable"), true).DelayedDelete(TimeSpan.FromMinutes(5));
                        break;
                }

                return false;
            }

            if (GetChannel(out var musicChannel)) {
                var needSummon = command.Attributes.FirstOrDefault(attribute => attribute is SummonToUserAttribute) != null;
                var user = Context.User as SocketGuildUser;
                if (Player != null && Player.State != PlayerState.NotConnected && Player.State != PlayerState.Destroyed && !needSummon) {
                    if (user.VoiceChannel.Id == Player.VoiceChannelId) return true;
                    Reply(Loc.Get("Music.OtherVoiceChannel").Format(Context.User.Mention), true).DelayedDelete(TimeSpan.FromMinutes(5));
                    return false;
                }

                if (!needSummon) return true;
                if (user.VoiceState.HasValue) {
                    var perms = (await Context.Guild.GetCurrentUserAsync()).GetPermissions(user.VoiceChannel);
                    if (!perms.Connect) {
                        Reply(Loc.Get("Music.CantConnect").Format(user.VoiceChannel.Name), true).DelayedDelete(TimeSpan.FromMinutes(2));
                        return false;
                    }

                    Player = await MusicUtils.JoinChannel(Context.Guild.Id, user.VoiceChannel.Id);
                    return true;
                }

                Reply(Loc.Get("Music.NotInVoiceChannel").Format(Context.User.Mention), true).DelayedDelete(TimeSpan.FromMinutes(5));
                return false;
            }

            if (GuildConfig.IsMusicLimited) {
                Reply(Loc.Get("Music.ChannelNotAllowed").Format(Context.User.Mention, musicChannel.Id), true).DelayedDelete(TimeSpan.FromMinutes(5));
                return false;
            }

            Reply(Loc.Get("Music.PlaybackMoved").Format(musicChannel.Id), false).DelayedDelete(TimeSpan.FromMinutes(5));
            return true;


            async Task<IUserMessage> Reply(string description, bool isFail) {
                var embed = this.GetAuthorEmbedBuilder().WithTitle(Loc.Get(isFail ? "Music.Fail" : "Music.Playback"))
                                .WithDescription(description).WithColor(isFail ? Color.Orange : Color.Gold).Build();
                try {
                    return await Context.Channel.SendMessageAsync(null, false, embed);
                }
                catch (Exception) {
                    return await (await Context.User.GetOrCreateDMChannelAsync()).SendMessageAsync(null, false, embed);
                }
            }
        }

        protected override async Task<IUserMessage> ReplyAsync(string message = null, bool isTTS = false, Embed embed = null, RequestOptions options = null) {
            return await ResponseChannel.SendMessageAsync(message, isTTS, embed, options).ConfigureAwait(false);
        }

        protected async Task<IUserMessage> ReplyFormattedAsync(string description, bool isFail = false, IUserMessage previous = null, IMessageChannel channel = null) {
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
            if (!GuildConfig.GetChannel(ChannelFunction.Music, out var musicChannel) || musicChannel.Id == channel.Id) return true;
            channel = (IMessageChannel) musicChannel;
            return false;
        }

        public Task<IUserMessage> GetLogMessage() {
            return GetChannel(out var channel) ? ReplyAsync(Loc.Get("Music.Loading")) : channel.SendMessageAsync(Loc.Get("Music.Loading"));
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