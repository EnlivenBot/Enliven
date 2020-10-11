using Bot.Music;
using Discord;

namespace Bot.DiscordRelated.Music {
    public class PlayerShutdownParameters {
        #region Parameters

        public bool NeedSave { get; set; } = true;
        public bool LeaveMessageUnchanged { get; set; }

        #endregion

        #region Storage

        public IUserMessage? LastControlMessage { get; set; } = null!;
        public StoredPlaylist? StoredPlaylist { get; set; }

        #endregion
    }
}