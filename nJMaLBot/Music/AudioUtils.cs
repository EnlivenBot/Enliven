using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Audio;

namespace Bot.Music
{
    internal static class AudioUtils
    {
        private static readonly ConcurrentDictionary<ulong, AudioInfo> ConnectedChannels =
            new ConcurrentDictionary<ulong, AudioInfo>();

        public static async Task JoinAudio(IVoiceChannel target)
        {
            if (ConnectedChannels.TryGetValue(target.Id, out var client))
            {
                return;
            }

            if (ConnectedChannels.IsEmpty)
                Program.Client.UserVoiceStateUpdated += UserVoiceStateUpdated;

            ConnectedChannels.TryAdd(target.Id, new AudioInfo(await target.ConnectAsync(), target.Id));
        }

        private static async Task UserVoiceStateUpdated(Discord.WebSocket.SocketUser arg1,
            Discord.WebSocket.SocketVoiceState arg2, Discord.WebSocket.SocketVoiceState arg3)
        {
            var (_, value) = ConnectedChannels.FirstOrDefault(x =>
                x.Value.IsAlone
                    ? arg3.VoiceChannel.Id == x.Key
                    : arg2.VoiceChannel.Id == x.Key && arg3.VoiceChannel.Id != x.Key);

            value.IsAlone = !value.IsAlone;
        }

        public static async Task LeaveAudio(ulong channelId)
        {
            if (ConnectedChannels.TryRemove(channelId, out var client))
            {
                await client.Client.StopAsync();
                //await Log(LogSeverity.Info, $"Disconnected from voice on {guild.Name}.");
                if (ConnectedChannels.IsEmpty)
                    Program.Client.UserVoiceStateUpdated -= UserVoiceStateUpdated;
            }
        }
    }
}