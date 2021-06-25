using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Bot.Config.Emoji;
using Bot.DiscordRelated;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.MessageComponents;
using Bot.Music.Spotify;
using Bot.Utilities.Collector;
using Common;
using Common.Config;
using Common.Localization.Entries;
using Common.Localization.Providers;
using Common.Music.Controller;
using Common.Music.Resolvers;
using Discord;
using SpotifyAPI.Web;

namespace Bot.Commands.Chains {
    public class FixSpotifyChain : ChainBase {
        private IMessageChannel _channel = null!;
        private SpotifyUrl _request = null!;
        private IUser _requester = null!;
        private IUserMessage? _controlMessage;
        private PaginatedMessage? _paginatedMessage;
        private CollectorsGroup? _collectorController;
        private IMusicController _musicController = null!;
        private IUserDataProvider _userDataProvider = null!;
        private ISpotifyAssociationProvider _spotifyAssociationProvider = null!;
        private ISpotifyAssociationCreator _spotifyAssociationCreator = null!;
        private SpotifyMusicResolver _resolver = null!;
        private SpotifyClientResolver _spotifyClientResolver = null!;
        private MessageComponentService _messageComponentService = null!;

        private FixSpotifyChain(string? uid, ILocalizationProvider loc) : base(uid, loc) { }

        public static FixSpotifyChain CreateInstance(IUser requester, IMessageChannel channel, ILocalizationProvider loc, string request,
                                                     IMusicController musicController, IUserDataProvider userDataProvider, 
                                                     ISpotifyAssociationProvider spotifyAssociationProvider, ISpotifyAssociationCreator spotifyAssociationCreator, 
                                                     SpotifyMusicResolver resolver, SpotifyClientResolver spotifyClientResolver,
                                                     MessageComponentService service) {
            var fixSpotifyChain = new FixSpotifyChain($"{nameof(FixSpotifyChain)}_{requester.Id}", loc) {
                _request = new SpotifyUrl(request), _requester = requester, _channel = channel,
                _musicController = musicController, _userDataProvider = userDataProvider, 
                _spotifyAssociationProvider = spotifyAssociationProvider, _spotifyAssociationCreator = spotifyAssociationCreator, 
                _resolver = resolver, _spotifyClientResolver = spotifyClientResolver, _messageComponentService = service
            };

            return fixSpotifyChain;
        }

        public async Task Start() {
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

                _ = controlMessage.DelayedDelete(Constants.StandardTimeSpan);
            };
            switch (_request.Type) {
                case SpotifyUrl.SpotifyUrlType.Album:
                case SpotifyUrl.SpotifyUrlType.Playlist:
                    await StartWithPlaylist();
                    break;
                case SpotifyUrl.SpotifyUrlType.Track:
                    await StartWithTrack(new SpotifyTrackWrapper(_request.Id));
                    break;
                case SpotifyUrl.SpotifyUrlType.Unknown:
                    OnEnd.Invoke(new EntryLocalized("Chains.FixSpotifyQueryNotRecognised", _request.Request.SafeSubstring(50)!));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private async Task StartWithPlaylist() {
            var spotify = (await _spotifyClientResolver.GetSpotify())!;
            try {
                var playlist = await spotify.Playlists.Get(_request.Id);
                var tracks = (await spotify.PaginateAll(playlist!.Tracks!))
                            .Select(track => track.Track as FullTrack)
                            .Where(track => track != null)
                            .Select(track => new SpotifyTrackWrapper(track!.Id, track)).ToList()!;

                var stringBuilder = new StringBuilder();
                for (var index = 0; index < tracks.Count; index++) {
                    var spotifyTrackData = tracks[index];
                    var fullTrack = await spotifyTrackData.GetFullTrack(spotify);
                    stringBuilder.AppendLine($"{index + 1}. [{fullTrack.Name}]({fullTrack.Uri}) *by {fullTrack.Artists.First().Name}*");
                }

                _paginatedMessage = new PaginatedMessage(PaginatedAppearanceOptions.Default, _controlMessage!, Loc, _messageComponentService);
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
                OnEnd.Invoke(new EntryLocalized("Chains.FixSpotifyNotFound", _request.Request.SafeSubstring(50)!));
            }
        }

        private async Task StartWithTrack(SpotifyTrackWrapper spotifyTrackWrapper) {
            SetTimeout(Constants.StandardTimeSpan);
            var association = await _spotifyAssociationCreator.ResolveAssociation(spotifyTrackWrapper, _musicController.Cluster);
            if (association == null) {
                OnEnd.Invoke(new EntryLocalized("Chains.FixSpotifyAssociationCreationError"));
                return;
            }

            IDisposable? paginatedMessageDispose = null;
            // ReSharper disable once RedundantAssignment
            var fullTrack = await spotifyTrackWrapper.GetFullTrack(await _spotifyClientResolver.GetSpotify());

            UpdateMessage();

            _collectorController?.DisposeAll();
            _collectorController = CollectorsUtils.CollectMessage(_requester, message => _channel.Id == message.Channel.Id, async args => {
                var voteMatch = Regex.Match(args.Message.Content, @"^(\d+) (\+|\-)$");
                if (voteMatch.Success && int.TryParse(voteMatch.Groups[1].Value, out var voteMatchIndex)) {
                    try {
                        association.Associations[voteMatchIndex - 1].AddVote(_requester.Id, voteMatch.Groups[2].Value == "+");
                        association.Save();

                        await args.RemoveReason();
                        paginatedMessageDispose?.Dispose();

                        UpdateMessage();
                    }
                    catch (Exception) {
                        // ignored
                    }

                    return;
                }

                if (int.TryParse(args.Message.Content, out var index) && index >= 0 && index <= association.Associations.Count) {
                    args.StopCollect();
                    paginatedMessageDispose?.Dispose();
                    _paginatedMessage?.Dispose();

                    await StartWithAssociation(association, spotifyTrackWrapper, association.Associations[index.Normalize(1, association.Associations.Count) - 1]);
                    SetTimeout(Constants.StandardTimeSpan);
                    return;
                }

                var addMatch = Regex.Match(args.Message.Content, @"^(new|add) (.*)$");
                if (addMatch.Success) {
                    var searchResults = (await (await new LavalinkMusicResolver().Resolve(_musicController.Cluster, addMatch.Groups[2].Value)).Resolve())
                                       .Where(track => track != null).ToList();
                    try {
                        var resultTrack = searchResults.Single();

                        var existentEqualsTrack = association.Associations.FirstOrDefault(data => data.Identifier == resultTrack.Identifier);
                        if (existentEqualsTrack != null) {
                            existentEqualsTrack.AddVote(_requester.Id, true);
                            var embed = CommandHandlerService.GetErrorEmbed(_requester, Loc, Loc.Get("Chains.FixSpotifyTrackAlreadyExists")).Build();
                            _ = _channel.SendMessageAsync(null, false, embed).DelayedDelete(Constants.StandardTimeSpan);
                        }
                        else {
                            association.Associations.Add(new SpotifyAssociation.TrackAssociationData(resultTrack.Identifier, _requester.ToLink())
                                {UpvotedUsers = new List<ulong> {_requester.Id}});
                        }

                        association.Save();
                        await args.RemoveReason();
                        paginatedMessageDispose?.Dispose();
                        UpdateMessage();
                    }
                    catch (Exception) {
                        var embed = CommandHandlerService.GetErrorEmbed(_requester, Loc,
                            Loc.Get("Chains.FixSpotifyNewTrackError", addMatch.Groups[2].Value.SafeSubstring(200, "...")!)).Build();
                        _ = _channel.SendMessageAsync(null, false, embed).DelayedDelete(Constants.StandardTimeSpan);
                    }
                }
            });

            void UpdateMessage() {
                SetTimeout(Constants.StandardTimeSpan);
                var fields = association.Associations.Select((data, i) =>
                    new EmbedFieldBuilder {
                        Name = $"{i + 1}. {data.Association.Title.SafeSubstring(200, "...")}",
                        Value = $"[[Source]({data.Association.Source})] *by {data.Author.GetData(_userDataProvider).GetMentionWithUsername()}*\n" +
                                $"Score: `{data.Score}` | " +
                                (data.UpvotedUsers.Contains(_requester.Id) ? $"**{data.UpvotedUsers.Count} 👍** | " : $"{data.UpvotedUsers.Count} 👍 | ") +
                                (data.DownvotedUsers.Contains(_requester.Id) ? $"**{data.DownvotedUsers.Count} 👎**" : $"{data.DownvotedUsers.Count} 👎")
                    }).ToList();

                _paginatedMessage ??= new PaginatedMessage(PaginatedAppearanceOptions.Default, _controlMessage!, Loc, _messageComponentService);
                _paginatedMessage.SetPages(Loc.Get("Chains.FixSpotifyAssociationChoose", $"***{fullTrack.Name}*** - **{fullTrack.Artists.First().Name}**"),
                    fields, Int32.MaxValue);
                _paginatedMessage.Disposed.Subscribe(base1 => End());
            }
        }

        private async Task StartWithAssociation(SpotifyAssociation association, SpotifyTrackWrapper trackWrapper,
                                                SpotifyAssociation.TrackAssociationData data) {
            SetTimeout(Constants.StandardTimeSpan);
            var fullTrack = await trackWrapper.GetFullTrack((await _spotifyClientResolver.GetSpotify())!);
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

                    await StartWithTrack(trackWrapper);
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
                                            $"[{data.Association.Title.SafeSubstring(200)}]({data.Association.Source}) *by {data.Author.GetData(_userDataProvider).GetMentionWithUsername()}*\n" +
                                            $"Score: `{data.Score}` | " +
                                            (data.UpvotedUsers.Contains(_requester.Id)
                                                ? $"**{data.UpvotedUsers.Count} 👍** | "
                                                : $"{data.UpvotedUsers.Count} 👍 | ") +
                                            (data.DownvotedUsers.Contains(_requester.Id)
                                                ? $"**{data.DownvotedUsers.Count} 👎** | "
                                                : $"{data.DownvotedUsers.Count} 👎"));
            }
        }
    }
}