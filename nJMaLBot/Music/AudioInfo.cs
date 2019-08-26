using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Timers;
using Bot.Utilities;
using Discord;
using Discord.Audio;

namespace Bot.Music
{
    class AudioInfo
    {
        private bool _isAlone;

        public AudioInfo(IAudioClient client, ulong audioChannelId) {
            Client = client;
            AudioChannelId = audioChannelId;
        }

        public ulong AudioChannelId { get; }

        public IAudioClient Client { get; }

        public bool IsPlaying { get; set; }
        public bool IsPaused  { get; set; }
        public AudioRepeatMode RepeatMode { get; set; } = AudioRepeatMode.NoRepeat;
        public IMessage ControlMessage { get; set; } = null;
        public List<string> MusicQueue { get; set; } = new List<string>();
        public int NowPlayingId { get; set; } = -1;

        private Timer AloneTimer { get; set; }

        public bool IsAlone {
            get => _isAlone;
            set {
                _isAlone = value;
                if (value) {
                    AloneTimer = new Timer(600000);
                    AloneTimer.Elapsed += (sender, args) => AudioUtils.LeaveAudio(AudioChannelId);
                    AloneTimer.Start();
                }
                else {
                    AloneTimer.Stop();}
            }
        }

        public bool PrintMessage(ulong FromChannel) {
            ControlMessage?.DeleteAsync();
            if (ChannelUtils.IsChannelAssigned((Program.Client.GetChannel(AudioChannelId) as IGuildChannel).GuildId, ChannelUtils.ChannelFunction.Music, out var channelId)) {

                //((ITextChannel)Program.Client.GetChannel(channelId)).SendMessageAsync()
            }
            else {

            }
            //(Program.Client.GetChannel(AudioChannelId) as IGuildChannel).GuildId
            return false;

        }

        public void UpdateMessage() {

        }
    }

    public enum AudioRepeatMode
    {
        NoRepeat,
        RepeatAll,
        RepeatCurrent
    }
}
