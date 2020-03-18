using System;
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

        protected override async void BeforeExecute(CommandInfo command) {
            base.BeforeExecute(command);
            GetChannel(out ResponseChannel);
        }

        protected override async Task<IUserMessage> ReplyAsync(string message = null, bool isTTS = false, Embed embed = null, RequestOptions options = null) {
            return await ResponseChannel.SendMessageAsync(message, isTTS, embed, options).ConfigureAwait(false);
        }

        protected async Task<IUserMessage> ReplyFormattedAsync(string description, bool isFail = false, IUserMessage previous = null) {
            var embed = this.GetAuthorEmbedBuilder().WithTitle(Loc.Get(isFail ? "Music.Fail" : "Music.Playback"))
                            .WithDescription(description).WithColor(isFail ? Color.Orange : Color.Gold).Build();
            if (previous == null) {
                return await ReplyAsync(null, false, embed);
            }

            await previous.ModifyAsync(properties => {
                properties.Content = "";
                properties.Embed = embed;
            });
            return previous;
        }

        public async Task<EmbedPlaybackPlayer> GetPlayerAsync(bool summonToUser = false) {
            var player = GetPlayer();
            var userTask = Context.Guild.GetUserAsync(Context.User.Id);

            if (player != null && player.State != PlayerState.NotConnected && player.State != PlayerState.Destroyed && !summonToUser) {
                if (((SocketGuildUser) await userTask).VoiceChannel.Id == player.VoiceChannelId) return player;
                ReplyAsync(Loc.Get("Music.OtherVoiceChannel").Format(Context.User.Mention)).DelayedDelete(TimeSpan.FromMinutes(5));
                return null;
            }

            if (((SocketGuildUser) await userTask).VoiceState.HasValue) {
                var embedPlaybackPlayer =
                    await MusicUtils.Cluster.JoinAsync<EmbedPlaybackPlayer>(Context.Guild.Id, ((SocketGuildUser) await userTask).VoiceChannel.Id);
                EmbedPlaybackControl.PlaybackPlayers.Add(embedPlaybackPlayer);
                return embedPlaybackPlayer;
            }

            ReplyAsync(Loc.Get("Music.NotInVoiceChannel").Format(Context.User.Mention)).DelayedDelete(TimeSpan.FromMinutes(5));
            return null;
        }

        private EmbedPlaybackPlayer GetPlayer() {
            try {
                return MusicUtils.Cluster.GetPlayer<EmbedPlaybackPlayer>(Context.Guild.Id);
            }
            catch (NullReferenceException e) {
                ReplyFormattedAsync(Loc.Get("Music.MusicDisabled"), true).DelayedDelete(TimeSpan.FromMinutes(5));
                throw;
            }
            catch (InvalidOperationException e) {
                if (e.Message == "The cluster has not been initialized.") {
                    ReplyFormattedAsync(Loc.Get("Music.ClusterInitializing"), true).DelayedDelete(TimeSpan.FromMinutes(5));
                }

                if (e.Message == "No node available.") {
                    ReplyFormattedAsync(Loc.Get("Music.NoNodesAvailable"), true).DelayedDelete(TimeSpan.FromMinutes(5));
                }

                throw;
            }
        }

        public bool GetChannel(out IMessageChannel channel) {
            channel = Context.Channel;
            if (!GuildConfig.GetChannel(ChannelFunction.Music, out var musicChannel) || musicChannel.Id == channel.Id) return true;
            channel = (IMessageChannel) musicChannel;
            return false;
        }

        public Task<IUserMessage> GetLogMessage() {
            if (GetChannel(out var channel)) return ReplyAsync(Loc.Get("Music.Loading"));
            if (GuildConfig.IsMusicLimited) {
                var eb = this.GetAuthorEmbedBuilder()
                             .WithTitle(Loc.Get("Music.Fail"))
                             .WithDescription(Loc.Get("Music.ChannelNotAllowed").Format(Context.User.Mention, channel.Id));
                Context.Message.SafeDelete();
                Context.Channel.SendMessageAsync(null, false, eb.Build()).DelayedDelete(TimeSpan.FromMinutes(5));
                return null;
            }
            else {
                var eb = this.GetAuthorEmbedBuilder()
                             .WithTitle(Loc.Get("Music.Playback"))
                             .WithDescription(Localization.Get(GuildConfig, "Music.PlaybackMoved").Format(channel.Id));
                Context.Channel.SendMessageAsync(null, false, eb.Build()).DelayedDelete(TimeSpan.FromMinutes(5));
                return channel.SendMessageAsync(Loc.Get("Music.Loading"));
            }
        }
    }
}