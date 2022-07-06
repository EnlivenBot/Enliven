using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Common;
using Common.Music;
using Common.Music.Resolvers;
using Lavalink4NET.Cluster;
using Lavalink4NET.Player;
using Newtonsoft.Json.Linq;

namespace Bot.Music.Deezer {
    public class DeezerMusicResolver : IMusicResolver {
        private static readonly Regex DeezerAppStateRegex = new(@"<script>window\.__DZR_APP_STATE__ = (.*)<\/script>");
        private static readonly Regex DeezerLinkRegex = new(@"(?:deezer\.com\/\D*\/(?:album|track|playlist)|deezer\.page\.link\/\w*)");
        private static readonly HttpClient HttpClient = new();
        
        private readonly LavalinkMusicResolver _lavalinkMusicResolver;
        public DeezerMusicResolver(LavalinkMusicResolver lavalinkMusicResolver) {
            _lavalinkMusicResolver = lavalinkMusicResolver;
        }
        
        public Task<MusicResolveResult> Resolve(LavalinkCluster cluster, string query) {
            var musicResolveResult = new MusicResolveResult(() => IsDeezerTrack(query), () => ResolveTracks(cluster, query));
            return Task.FromResult(musicResolveResult);

        }
        private Task<bool> IsDeezerTrack(string query) {
            return Task.FromResult(DeezerLinkRegex.IsMatch(query));
        }
        
        private async Task<List<LavalinkTrack>> ResolveTracks(LavalinkCluster lavalinkCluster, string query) {
            try {
                var pageContent = await HttpClient.GetStringAsync(query);
                var deezerAppStateJson = DeezerAppStateRegex.Match(pageContent).Groups[1].Value;
                var state = JObject.Parse(deezerAppStateJson);
            
                var trackDatas = state.ContainsKey("SONGS")
                    ? state["SONGS"]!["data"]!.ToArray()
                    : new[] { state["DATA"] };

                return await trackDatas
                    .Select(token => new { Title = token!.Value<string>("SNG_TITLE"), Artist = token!.Value<string>("ART_NAME") })
                    .Select(arg => _lavalinkMusicResolver.Resolve(lavalinkCluster, $"{arg.Title} {arg.Artist}").PipeAsync(result => result.Resolve()))
                    .Pipe(Task.WhenAll)
                    .PipeAsync(lists => lists.Select(list => list.FirstOrDefault()))
                    .PipeAsync(list => list!.Where(track => track != null).ToList());
            }
            catch (Exception) {
                throw new TrackNotFoundException(false);
            }
        }

        public Task OnException(LavalinkCluster cluster, string query, Exception e) {
            return Task.CompletedTask;
        }
    }
}