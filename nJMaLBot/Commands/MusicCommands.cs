using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Music;
using Bot.Music.Players;
using Bot.Utilities;
using Bot.Utilities.Commands;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordChatExporter.Core.Models;
using Lavalink4NET.Player;
using Lavalink4NET.Rest;
using Embed = Discord.Embed;

#pragma warning disable 4014

namespace Bot.Commands {
    [Grouping("music")]
    [RequireContext(ContextType.Guild)]
    public sealed class MusicCommands : AdvancedModuleBase {
        [Command("play", RunMode = RunMode.Async)]
        [Alias("p")]
        [Summary("play0s")]
        public async Task Play([Remainder] [Summary("play0_0s")] string query = null) {
            var replyMessageTask = ReplyAsync(Loc.Get("Music.Loading"));
            var player = await GetPlayerAsync();
            if (player == null) return;

            if (GetChannel(out var channel)) {
                var lastMessage = await replyMessageTask;
                #pragma warning disable 4014
                lastMessage.ModifyAsync(properties => {
                    #pragma warning restore 4014
                    properties.Content = Localization.Get(GuildConfig, "Music.PlaybackMoved").Format(channel.Id);
                    properties.Embed = Optional<Embed>.Unspecified;
                });
                lastMessage.DelayedDelete(TimeSpan.FromMinutes(5));

                replyMessageTask = ((ITextChannel) channel).SendMessageAsync(Localization.Get(GuildConfig, "Music.Loading"));
            }

            player.SetControlMessage(await replyMessageTask);
            try {
                var tracks = (await MusicUtils.GetMusic(Context.Message, query))
                            .Select(track => AuthoredLavalinkTrack.FromLavalinkTrack(track, Context.User))
                            .ToList();
                await player.PlayAsync(tracks.First(), true);
                player.Playlist.AddRange(tracks.Skip(1));
            }
            catch (TrackNotFoundException) {
                player.ControlMessage.ModifyAsync(properties => {
                    properties.Content = "";
                    properties.Embed = new EmbedBuilder().WithTitle(Loc.Get("Music.Fail"))
                                                         .WithDescription(Loc.Get("Music.NotFound").Format(query.SafeSubstring(0, 512)))
                                                         .WithColor(Color.Red).Build();
                });
            }
            catch (AttachmentAddFailException) {
                player.ControlMessage.ModifyAsync(properties => {
                    properties.Content = "";
                    properties.Embed = new EmbedBuilder().WithTitle(Loc.Get("Music.Fail")).WithDescription(Loc.Get("Music.AttachmentFail"))
                                                         .WithColor(Color.Red).Build();
                });
            }
        }

        [Command("stop", RunMode = RunMode.Async)]
        [Alias("s")]
        [Summary("stop0s")]
        public async Task Stop() {
            var player = await GetPlayerAsync();
            if (player == null) {
                Context.Message.SafeDelete();
                return;
            }

            if (player.CurrentTrack == null) {
                await ReplyAsync("Nothing playing!");
                return;
            }

            await player.StopAsync(true);
            Context.Message.SafeDelete();
            await ReplyAsync("Stopped playing.");
        }

        [Command("jump", RunMode = RunMode.Async)]
        [Alias("j", "skip", "next", "n")]
        [Summary("jump0s")]
        public async Task Jump([Summary("jump0_0s")] int index = 1) {
            var player = await GetPlayerAsync();
            if (player == null) {
                Context.Message.SafeDelete();
                return;
            }

            await player.SkipAsync(index, true);
            Context.Message.SafeDelete();
        }

        [Command("goto", RunMode = RunMode.Async)]
        [Alias("g")]
        [Summary("goto0s")]
        public async Task Goto([Summary("goto0_0s")] int index) {
            var player = await GetPlayerAsync();
            if (player == null) {
                Context.Message.SafeDelete();
                return;
            }

            if (player.Playlist.TryGetValue(index - 1, out var track)) {
                await player.PlayAsync(track, false, new TimeSpan?(), new TimeSpan?());
            }
            else {
                (await ReplyAsync(Loc.Get("Music.TrackIndexWrong").Format(Context.User.Mention, index, player.Playlist.Count))).DelayedDelete(
                    TimeSpan.FromMinutes(5));
            }
            
            Context.Message.SafeDelete();
        }

        [Command("volume", RunMode = RunMode.Async)]
        [Alias("v")]
        [Summary("volume0s")]
        public async Task Volume([Summary("volume0_0s")] int volume = 100) {
            if (volume > 150 || volume < 0) {
                await ReplyAsync("Volume out of range: 0% - 150%!");
                return;
            }

            var player = await GetPlayerAsync();
            if (player == null) {
                Context.Message.SafeDelete();
                return;
            }

            await player.SetVolumeAsync(volume / 100f);
            Context.Message.SafeDelete();
            await ReplyAsync($"Volume updated: {volume}%");
        }

        [Command("repeat", RunMode = RunMode.Async)]
        [Alias("r")]
        [Summary("repeat0s")]
        public async Task Repeat(LoopingState state) {
            var player = await GetPlayerAsync();
            if (player == null) {
                Context.Message.SafeDelete();
                return;
            }

            player.LoopingState = state;
            Context.Message.SafeDelete();
            ReplyAsync(Loc.Get("Music.RepeatSet").Format(player.LoopingState.ToString())).DelayedDelete(TimeSpan.FromMinutes(5));
        }

        [Command("repeat", RunMode = RunMode.Async)]
        [Alias("r")]
        [Summary("repeat0s")]
        public async Task Repeat() {
            var player = await GetPlayerAsync();
            if (player == null) {
                Context.Message.SafeDelete();
                return;
            }

            Repeat(player.LoopingState.Next());
        }

        private async Task<EmbedPlaybackPlayer> GetPlayerAsync(bool summonToUser = false) {
            var player = MusicUtils.Cluster.GetPlayer<EmbedPlaybackPlayer>(Context.Guild.Id);
            var userTask = Context.Guild.GetUserAsync(Context.User.Id);
            
            if (player != null
             && player.State != PlayerState.NotConnected
             && player.State != PlayerState.Destroyed
             && !summonToUser) {
                if (((SocketGuildUser) await userTask).VoiceChannel.Id == player.VoiceChannelId) return player;
                ReplyAsync(Loc.Get("Music.OtherVoiceChannel").Format(Context.User.Mention)).DelayedDelete(TimeSpan.FromMinutes(5));
                return null;
            }

            if (((SocketGuildUser) await userTask).VoiceState.HasValue)
                return await MusicUtils.Cluster.JoinAsync<EmbedPlaybackPlayer>(Context.Guild.Id, ((SocketGuildUser) await userTask).VoiceChannel.Id);
            ReplyAsync(Loc.Get("Music.NotInVoiceChannel").Format(Context.User.Mention)).DelayedDelete(TimeSpan.FromMinutes(5));
            return null;

        }

        private bool GetChannel(out IMessageChannel channel) {
            channel = Context.Channel;
            if (!GuildConfig.GetChannel(ChannelFunction.Music, out var musicChannel) || musicChannel.Id == channel.Id) return false;
            channel = (IMessageChannel) musicChannel;
            return true;
        }
    }
}