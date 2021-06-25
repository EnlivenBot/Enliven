using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bot.Utilities.Collector;
using Common;
using Common.Config;
using Common.Localization.Providers;
using Common.Music.Controller;
using Common.Music.Players;
using Common.Music.Tracks;
using Discord;
using Lavalink4NET.Player;
using Lavalink4NET.Rest;

namespace Bot.Commands.Chains {
    public class AdvancedMusicSearchChain : ChainBase {
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private string _query = null!;
        private IUser _requester = null!;
        private SearchMode _searchMode;
        private IMessageChannel _targetChannel = null!;
        private FinalLavalinkPlayer _player = null!;
        private CollectorsGroup? _collectorGroup;
        private IMusicController _controller = null!;

        private AdvancedMusicSearchChain(string? uid, ILocalizationProvider loc) : base(uid, loc) { }

        public static AdvancedMusicSearchChain CreateInstance(GuildConfig guildConfig, FinalLavalinkPlayer player, IMessageChannel targetChannel,
                                                              IUser requester, SearchMode searchMode,
                                                              string query, IMusicController controller) {
            
            var advancedMusicSearchChain = new AdvancedMusicSearchChain(
                $"{nameof(AdvancedMusicSearchChain)}_{guildConfig.GuildId}_{requester.Id}", guildConfig.Loc) {
                _requester = requester, _query = query, _searchMode = searchMode, _targetChannel = targetChannel, 
                _player = player, _controller = controller
            };
            advancedMusicSearchChain.MainBuilder
                                    .WithColor(Color.Gold)
                                    .WithTitle(guildConfig.Loc.Get("Music.SearchResultsTitle"));
            advancedMusicSearchChain.SetTimeout(Constants.VeryShortTimeSpan);
            return advancedMusicSearchChain;
        }

        public async Task Start() {
            var tracks = (await _controller.Cluster.GetTracksAsync(_query, _searchMode)).ToList();
            // Repeat search, if fail
            if (!tracks.Any()) {
                tracks = (await _controller.Cluster.GetTracksAsync(_query, _searchMode)).ToList();
            }

            MainBuilder.Description = Loc.Get("Music.SearchResultsDescription", _searchMode, _query.SafeSubstring(100, "...") ?? "");
            if (!tracks.Any()) {
                MainBuilder.Description += Loc.Get("Music.NothingFound");
            }
            else {
                var builder = new StringBuilder();
                // 1500 - the maximum number of characters to be within the embed description limit. Taken with a margin
                // 10 - max number of tracks
                for (var i = 0; i < tracks.Count && builder.Length < 1500 && i < 10; i++) {
                    var track = tracks[i];
                    builder.AppendLine($"{i + 1}. [{track.Title}]({track.Source})\n");
                }

                MainBuilder.Description += builder.ToString();
            }

            var msg = await _targetChannel.SendMessageAsync(null, false, MainBuilder.Build());
            if (!tracks.Any())
                return;

            _collectorGroup = new CollectorsGroup(
                CollectorsUtils.CollectReaction(msg, reaction => true, async args => {
                    var i = args.Reaction.Emote.Name switch {
                        "1️⃣" => 0,
                        "2️⃣" => 1,
                        "3️⃣" => 2,
                        "4️⃣" => 3,
                        "5️⃣" => 4,
                        "6️⃣" => 5,
                        "7️⃣" => 6,
                        "8️⃣" => 7,
                        "9️⃣" => 8,
                        "🔟"  => 9,
                        "⬅️"  => -2, // All displayed tracks
                        _     => -1  // Other emoji, ignore it
                    };

                    await ProcessAdd(i, tracks, msg);
                }, CollectorFilter.IgnoreBots),
                CollectorsUtils.CollectMessage(_requester, message => message.Channel.Id == _targetChannel.Id, async args => {
                    if (!int.TryParse(args.Message.Content, out var result)) return;
                    if (result > tracks.Count || result <= 0) return;
                    if (await ProcessAdd(result, tracks, msg)) {
                        await args.RemoveReason();
                    }
                })
            );

            var reactions = new IEmote[] {
                new Emoji("1️⃣"),
                new Emoji("2️⃣"),
                new Emoji("3️⃣"),
                new Emoji("4️⃣"),
                new Emoji("5️⃣"),
                new Emoji("6️⃣"),
                new Emoji("7️⃣"),
                new Emoji("8️⃣"),
                new Emoji("9️⃣"),
                new Emoji("🔟")
            };

            OnEnd = async localized => {
                _collectorGroup.DisposeAll();
                _cancellationTokenSource.Cancel();
                _ = msg.DelayedDelete(Constants.StandardTimeSpan);
                await msg.ModifyAsync(properties =>
                    properties.Embed = new EmbedBuilder().WithColor(Color.Orange).WithTitle(Loc.Get("ChainsCommon.Ended"))
                                                         .WithDescription(localized.Get(Loc))
                                                         .Build());
                await msg.RemoveAllReactionsAsync();
            };

            await msg.AddReactionsAsync(reactions.Take(tracks.Count).ToArray(), new RequestOptions {CancelToken = _cancellationTokenSource.Token});
            await msg.AddReactionAsync(new Emoji("⬅️"), new RequestOptions {CancelToken = _cancellationTokenSource.Token});
            SetTimeout(TimeSpan.FromMinutes(1));
        }

        private async Task<bool> ProcessAdd(int i, List<LavalinkTrack> tracks, IUserMessage msg) {
            var authoredLavalinkTracks = new List<LavalinkTrack>();
            if (i == -2)
                authoredLavalinkTracks.AddRange(tracks.Take(10).Select(track => track.AddAuthor(_requester.Username)));
            if (i >= 0 && i <= tracks.Count - 1) authoredLavalinkTracks.Add(tracks[i].AddAuthor(_requester.Username));
            switch (authoredLavalinkTracks.Count) {
                case 0:
                    return false;
                case 1:
                    _player.WriteToQueueHistory(Loc.Get("MusicQueues.Enqueued", _requester.Username, MusicController.EscapeTrack(authoredLavalinkTracks[0].Title)));
                    break;
                default:
                    _player.WriteToQueueHistory(Loc.Get("Music.AddTracks", _requester.Username, authoredLavalinkTracks.Count));
                    break;
            }

            // await _player.EnqueueControlMessageSend(_targetChannel);
            await _player.PlayAsync(authoredLavalinkTracks.First(), true);
            _player.Playlist.AddRange(authoredLavalinkTracks.Skip(1));

            _cancellationTokenSource.Cancel();
            _collectorGroup?.DisposeAll();
            msg.SafeDelete();
            OnEnd = localized => { };
            return true;
        }
    }
}