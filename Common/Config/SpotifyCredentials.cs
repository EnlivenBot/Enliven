namespace Common.Config {
    public class SpotifyCredentials {
        /// <summary>
        /// Spotify client ID for resolving spotify related things. Leave null for disabling spotify integration
        /// Obtain at https://developer.spotify.com/dashboard/
        /// </summary>
        /// <seealso cref="SpotifyClientSecret"/>
        public string? SpotifyClientID { get; set; }

        /// <summary>
        /// Spotify client secret for resolving spotify related things. Leave null for disabling spotify integration
        /// Obtain at https://developer.spotify.com/dashboard/
        /// </summary>
        /// <seealso cref="SpotifyClientID"/>
        public string? SpotifyClientSecret { get; set; }
    }
}