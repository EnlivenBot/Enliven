using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Bot.Config.Emoji;
using Bot.Config.Localization.Entries;
using Bot.Config.Localization.Providers;
using Bot.DiscordRelated;
using Bot.DiscordRelated.Commands;
using Bot.Music;
using Bot.Utilities;
using Bot.Utilities.Collector;
using Bot.Utilities.Music;
using Discord;
using Org.BouncyCastle.Utilities.Collections;
using SpotifyAPI.Web;

namespace Bot.Commands.Chains {
    public class FixSpotifyChain : ChainBase {
        private IMessageChannel _channel = null!;
        private string _request = null!;
        private IUser _requester = null!;
        private string? _trackId;
        private IUserMessage? _controlMessage;
        private PaginatedMessage? _paginatedMessage;
        private CollectorsGroup? _collectorController;

        private FixSpotifyChain(string? uid, ILocalizationProvider loc) : base(uid, loc) { }

        public static FixSpotifyChain CreateInstance(IUser requester, IMessageChannel channel, ILocalizationProvider loc, string request) {
            var fixSpotifyChain = new FixSpotifyChain($"{nameof(FixSpotifyChain)}_{requester.Id}", loc) {
                _request = request, _requester = requester, _channel = channel
            };

            return fixSpotifyChain;
        }

        public async Task Start() {
            var isTrack = SpotifyMusicProvider.IsTrack(_request, out var trackId);
            _trackId = trackId;
            MainBuilder.WithColor(Color.Gold).WithTitle(Loc.Get("Chains.FixSpotifyTitle"));
            _controlMessage = await _channel.SendMessageAsync(null, false, MainBuilder.Build());
            SetTimeout(Constants.StandardTimeSpan);
            OnEnd = localized => {
                _paginatedMessage?.Dispose();
                _collectorController?.DisposeAll();
                var controlMessage = _controlMessage;
                _controlMessage = null;
                var propertiesEmbed = new EmbedBuilder().WithColor(Color.Orange).WithTitle(Loc.Get("ChainsCommon.Ended"))
                                                        .WithDescription(localized.Get(Loc)).Build();
                try {
                    controlMessage.ModifyAsync(properties => { properties.Embed = propertiesEmbed; });
                }
                catch (Exception) {
                    controlMessage.Channel.SendMessageAsync(null, false, propertiesEmbed);
                }

                controlMessage.DelayedDelete(Constants.StandardTimeSpan);
            };
            switch (isTrack) {
                case true:
                    await StartWithTrack(new SpotifyTrackData(_trackId!));
                    break;
                case false:
                    await StartWithPlaylist();
                    break;
                case null:
                    OnEnd.Invoke(new EntryLocalized("Chains.FixSpotifyQueryNotRecognised", _request.SafeSubstring(50)!));
                    break;
            }
        }

        private async Task StartWithPlaylist() {
            var spotify = (await SpotifyMusicProvider.SpotifyClient)!;
            try {
                var playlist = await spotify.Playlists.Get(_trackId!);
                var tracks = (await spotify.PaginateAll(playlist!.Tracks!))
                            .Select(track => track.Track as FullTrack)
                            .Where(track => track != null)
                            .Select(track => new SpotifyTrackData(track!.Id, track)).ToList()!;

                var stringBuilder = new StringBuilder();
                for (var index = 0; index < tracks.Count; index++) {
                    var spotifyTrackData = tracks[index];
                    var fullTrack = await spotifyTrackData.GetTrack();
                    stringBuilder.AppendLine($"{index + 1}. [{fullTrack.Name}]({fullTrack.Uri}) *by {fullTrack.Artists.First().Name}*");
                }

                _paginatedMessage = new PaginatedMessage(PaginatedAppearanceOptions.Default, _controlMessage!, Loc);
                _paginatedMessage.SetPages(stringBuilder.ToString(), "{0}", Int32.MaxValue,
                    new List<EmbedFieldBuilder> {
                        new EmbedFieldBuilder {Name = Loc.Get("Chains.FixSpotifyPlaylistChooseName"), Value = Loc.Get("Chains.FixSpotifyPlaylistChooseValue")}
                    });
                await _paginatedMessage.Update();
                ResetTimeout();

                _collectorController?.DisposeAll();
                _collectorController = CollectorsUtils.CollectMessage(_requester, message => true, args => {
                    if (int.TryParse(args.Message.Content, out var index) && index >= 0 && index <= tracks.Count) {
                        args.StopCollect();
                        _paginatedMessage.Dispose();
                        _paginatedMessage = null;
                        #pragma warning disable 4014
                        StartWithTrack(tracks[index.Normalize(1, tracks.Count) - 1]);
                        #pragma warning restore 4014
                    }
                });
            }
            catch (Exception) {
                OnEnd.Invoke(new EntryLocalized("Chains.FixSpotifyNotFound", _request.SafeSubstring(50)!));
            }
        }

        private async Task StartWithTrack(SpotifyTrackData spotifyTrackData) {
            SetTimeout(Constants.StandardTimeSpan);
            var association = await SpotifyMusicProvider.ResolveWithCache(spotifyTrackData, MusicUtils.Cluster);
            if (association == null) {
                OnEnd.Invoke(new EntryLocalized("Chains.FixSpotifyAssociationCreationError"));
                return;
            }
            
            var onPaginatedMessageStop = new EventHandler((sender, args) => End());
            // ReSharper disable once RedundantAssignment
            var fullTrack = await spotifyTrackData.GetTrack();

            UpdateMessage();

            _collectorController?.DisposeAll();
            _collectorController = CollectorsUtils.CollectMessage(_requester, message => _channel.Id == message.Channel.Id, async args => {
                var voteMatch = Regex.Match(args.Message.Content, @"^(\d+) (\+|\-)$");
                if (voteMatch.Success && int.TryParse(voteMatch.Groups[1].Value, out var voteMatchIndex)) {
                    try {
                        association.Associations[voteMatchIndex - 1].AddVote(_requester.Id, voteMatch.Groups[2].Value == "+");
                        association.Save();

                        await args.RemoveReason();
                        _paginatedMessage!.Stop -= onPaginatedMessageStop;

                        UpdateMessage();
                    }
                    catch (Exception) {
                        // ignored
                    }

                    return;
                }

                if (int.TryParse(args.Message.Content, out var index) && index >= 0 && index <= association.Associations.Count) {
                    args.StopCollect();
                    _paginatedMessage!.Stop -= onPaginatedMessageStop;
                    _paginatedMessage.StopAndClear();

                    await StartWithAssociation(association, spotifyTrackData, association.Associations[index.Normalize(1, association.Associations.Count) - 1]);
                    SetTimeout(Constants.StandardTimeSpan);
                    return;
                }

                var addMatch = Regex.Match(args.Message.Content, @"^(new|add) (.*)$");
                if (addMatch.Success) {
                    var searchResults = (await new DefaultMusicProvider(MusicUtils.Cluster, addMatch.Groups[2].Value).Provide())
                                       .Where(track => track != null).ToList();
                    try {
                        var resultTrack = searchResults.Single();

                        var existentEqualsTrack = association.Associations.FirstOrDefault(data => data.Identifier == resultTrack.Identifier);
                        if (existentEqualsTrack != null) {
                            existentEqualsTrack.AddVote(_requester.Id, true);
                            _channel.SendMessageAsync(null, false,
                                         CommandHandler.GetErrorEmbed(_requester, Loc, Loc.Get("Chains.FixSpotifyTrackAlreadyExists")).Build())
                                    .DelayedDelete(Constants.StandardTimeSpan);
                        }
                        else {
                            association.Associations.Add(new SpotifyTrackAssociation.TrackAssociationData(resultTrack.Identifier, _requester.ToLink())
                                {UpvotedUsers = new List<ulong> {_requester.Id}});
                        }

                        association.Save();
                        await args.RemoveReason();
                        _paginatedMessage!.Stop -= onPaginatedMessageStop;
                        UpdateMessage();
                    }
                    catch (Exception) {
                        _channel.SendMessageAsync(null, false, CommandHandler.GetErrorEmbed(_requester, Loc,
                                     Loc.Get("Chains.FixSpotifyNewTrackError", addMatch.Groups[2].Value.SafeSubstring(200, "...")!)).Build())
                                .DelayedDelete(Constants.StandardTimeSpan);
                    }
                }
            });

            void UpdateMessage() {
                SetTimeout(Constants.StandardTimeSpan);
                var fields = association.Associations.Select((data, i) =>
                    new EmbedFieldBuilder {
                        Name = $"{i + 1}. {data.Association.Title.SafeSubstring(200, "...")}",
                        Value = $"[[Source]({data.Association.Source})] *by {data.Author.Data.GetMention(true)}*\n" +
                                $"Score: `{data.Score}` | " +
                                (data.UpvotedUsers.Contains(_requester.Id) ? $"**{data.UpvotedUsers.Count} 👍** | " : $"{data.UpvotedUsers.Count} 👍 | ") +
                                (data.DownvotedUsers.Contains(_requester.Id) ? $"**{data.DownvotedUsers.Count} 👎**" : $"{data.DownvotedUsers.Count} 👎")
                    }).ToList();

                _paginatedMessage ??= new PaginatedMessage(PaginatedAppearanceOptions.Default, _controlMessage!, Loc);
                _paginatedMessage.SetPages(Loc.Get("Chains.FixSpotifyAssociationChoose", $"***{fullTrack.Name}*** - **{fullTrack.Artists.First().Name}**"), fields, Int32.MaxValue);
                _paginatedMessage.Stop += onPaginatedMessageStop;
            }
        }

        private async Task StartWithAssociation(SpotifyTrackAssociation association, SpotifyTrackData trackData,
                                                SpotifyTrackAssociation.TrackAssociationData data) {
            SetTimeout(Constants.StandardTimeSpan);
            var fullTrack = await trackData.GetTrack();
            UpdateBuilder();
            _controlMessage = await _channel.SendMessageAsync(null, false, MainBuilder.Build());
            var emojiCancellation = new CancellationTokenSource();
            await _controlMessage.AddReactionsAsync(new IEmote[] {
                CommonEmoji.LegacyReverse, CommonEmoji.ThumbsUp, CommonEmoji.ThumbsDown,
                // TODO Warning implementation
                // CommonEmoji.Warning,
                CommonEmoji.LegacyStop
            }, new RequestOptions {CancelToken = emojiCancellation.Token});

            _collectorController?.DisposeAll();
            _collectorController = CollectorsUtils.CollectReaction(_requester, reaction => true, async args => {
                if (args.Reaction.Emote.Equals(CommonEmoji.LegacyReverse)) {
                    emojiCancellation.Cancel();
                    try {
                        await _controlMessage.RemoveAllReactionsAsync();
                    }
                    catch (Exception) {
                        // ignored
                    }

                    await StartWithTrack(trackData);
                }
                else if (args.Reaction.Emote.Equals(CommonEmoji.ThumbsUp)) {
                    data.AddVote(_requester.Id, true);
                    association.Save();
                    UpdateBuilder();
                    await _controlMessage.ModifyAsync(properties => { properties.Embed = MainBuilder.Build(); });
                }
                else if (args.Reaction.Emote.Equals(CommonEmoji.ThumbsDown)) {
                    data.AddVote(_requester.Id, false);
                    association.Save();
                    UpdateBuilder();
                    await _controlMessage.ModifyAsync(properties => { properties.Embed = MainBuilder.Build(); });
                }
                else if (args.Reaction.Emote.Equals(CommonEmoji.Warning)) {
                    // TODO Warning implementation
                }
                else if (args.Reaction.Emote.Equals(CommonEmoji.LegacyStop)) {
                    End();
                }
                SetTimeout(Constants.StandardTimeSpan);
            });

            void UpdateBuilder() {
                MainBuilder.WithDescription($"{fullTrack.Name} - {fullTrack.Artists[0].Name}\n\n" +
                                            $"[{data.Association.Title.SafeSubstring(200)}]({data.Association.Source}) *by {data.Author.Data.GetMention(true)}*\n" +
                                            $"Score: `{data.Score}` | " +
                                            (data.UpvotedUsers.Contains(_requester.Id)
                                                ? $"**{data.UpvotedUsers.Count} 👍** | "
                                                : $"{data.UpvotedUsers.Count} 👍 | ") +
                                            (data.DownvotedUsers.Contains(_requester.Id)
                                                ? $"**{data.DownvotedUsers.Count} 👎** | "
                                                : $"{data.DownvotedUsers.Count} 👎"));
            }
        }

        // public override void Update() {
        //     try {
        //         _controlMessage?.ModifyAsync(properties => {
        //             properties.Embed = MainBuilder.Build();
        //         });
        //     }
        //     catch (Exception) {
        //         // ignored
        //     }
        // }
    }
}