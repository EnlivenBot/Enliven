using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bot.Config.Emoji;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.MessageComponents;
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
        public static IEmote[] NumberReactions = {
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

        public static IEmote AllReaction = new Emoji("⬅️");

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly string _query;
        private readonly IUser _requester;
        private readonly SearchMode _searchMode;
        private readonly IMessageChannel _targetChannel;
        private readonly FinalLavalinkPlayer _player;
        private readonly IMusicController _controller;
        private readonly EnlivenComponentBuilder _componentBuilder;
        private readonly CollectorService _collectorService;
        private CollectorsGroup? _collectorGroup;

        public AdvancedMusicSearchChain(GuildConfig guildConfig,
                                        FinalLavalinkPlayer player,
                                        IMessageChannel targetChannel,
                                        IUser requester,
                                        SearchMode searchMode,
                                        string query,
                                        IMusicController controller,
                                        MessageComponentService messageComponentService,
                                        CollectorService collectorService)
            : base($"{nameof(AdvancedMusicSearchChain)}_{guildConfig.GuildId}_{requester.Id}", guildConfig.Loc) {
            _player = player;
            _targetChannel = targetChannel;
            _requester = requester;
            _searchMode = searchMode;
            _query = query;
            _controller = controller;
            _collectorService = collectorService;
            _componentBuilder = messageComponentService.GetBuilder();
            MainBuilder
                .WithColor(Color.Gold)
                .WithTitle(guildConfig.Loc.Get("Music.SearchResultsTitle"));
        }

        public async Task Start() {
            SetTimeout(Constants.VeryShortTimeSpan);
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
                var stringBuilder = new StringBuilder();
                // 1500 - the maximum number of characters to be within the embed description limit. Taken with a margin
                // 10 - max number of tracks
                for (var i = 0; i < tracks.Count && stringBuilder.Length < 1500 && i < 10; i++) {
                    var track = tracks[i];
                    stringBuilder.AppendLine($"{i + 1}. [{track.Title}]({track.Source})\n");
                }

                MainBuilder.Description += stringBuilder.ToString();

                for (var index = 0; index < tracks.Count && index < 10; index++) {
                    var track = tracks[index];
                    var button = new EnlivenButtonBuilder().WithStyle(ButtonStyle.Secondary)
                        .WithTargetRow(index / 5).WithEmote(NumberReactions[index]).WithLabel(track.Title.SafeSubstring(20, "...") ?? "")
                        .WithCustomId(index.ToString());
                    _componentBuilder.WithButton(button);
                }

                var controlsRow = tracks.Count < 6 ? 1 : 2;
                _componentBuilder.WithButton(new EnlivenButtonBuilder().WithStyle(ButtonStyle.Success).WithEmote(AllReaction).WithLabel(Loc.Get("Common.All")).WithTargetRow(controlsRow).WithCustomId("All"));
                _componentBuilder.WithButton(new EnlivenButtonBuilder().WithStyle(ButtonStyle.Danger).WithEmote(CommonEmoji.LegacyStop).WithLabel(Loc.Get("Common.Stop")).WithTargetRow(controlsRow).WithCustomId("Stop"));
            }

            var msg = await _targetChannel.SendMessageAsync(null, false, MainBuilder.Build(), component: _componentBuilder.Build());
            _componentBuilder.AssociateWithMessage(msg);
            if (!tracks.Any())
                return;

            _componentBuilder.SetCallback(async (s, component, arg3) => {
                if (component.User.Id != _requester.Id) {
                    var embed = CommandHandlerService.GetErrorEmbed(component.User, Loc, Loc.Get("Common.OnlyRequester", component.User.Mention)).Build();
                    _ = component.FollowupAsync(embed: embed, ephemeral: true).DelayedDelete(TimeSpan.FromSeconds(15));
                    return;
                }
                switch (s) {
                    case var _ when int.TryParse(s, out var index):
                        await ProcessAdd(new[] { tracks[index] }, msg);
                        break;
                    case "All":
                        await ProcessAdd(tracks.Take(10), msg);
                        break;
                    case "Stop":
                        End();
                        break;
                }
            });

            _collectorGroup = new CollectorsGroup(
                _collectorService.CollectMessage(_requester, message => message.Channel.Id == _targetChannel.Id, async args => {
                    if (!int.TryParse(args.Message.Content, out var result)) return;
                    if (result > tracks.Count || result <= 0) return;
                    if (await ProcessAdd(new[] { tracks[result - 1] }, msg)) {
                        await args.RemoveReason();
                    }
                })
            );

            OnEnd = async localized => {
                _collectorGroup.DisposeAll();
                _componentBuilder.Dispose();
                _cancellationTokenSource.Cancel();
                _ = msg.DelayedDelete(Constants.StandardTimeSpan);
                await msg.ModifyAsync(properties => {
                    properties.Embed = new EmbedBuilder().WithColor(Color.Orange).WithTitle(Loc.Get("ChainsCommon.Ended"))
                        .WithDescription(localized.Get(Loc))
                        .Build();
                    properties.Components = new ComponentBuilder().Build();
                });
                await msg.RemoveAllReactionsAsync();
            };

            SetTimeout(TimeSpan.FromMinutes(1));
        }

        private async Task<bool> ProcessAdd(IEnumerable<LavalinkTrack> tracks, IUserMessage msg) {
            var authoredLavalinkTracks = tracks.Select(track => track.AddAuthor(_requester.Username)).ToList();
            switch (authoredLavalinkTracks.Count) {
                case 1:
                    _player.WriteToQueueHistory(Loc.Get("MusicQueues.Enqueued", _requester.Username, MusicController.EscapeTrack(authoredLavalinkTracks[0].Title)));
                    break;
                default:
                    _player.WriteToQueueHistory(Loc.Get("Music.AddTracks", _requester.Username, authoredLavalinkTracks.Count));
                    break;
            }

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