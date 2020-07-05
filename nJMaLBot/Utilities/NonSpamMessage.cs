using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Tyrrrz.Extensions;

namespace Bot.Utilities {
    public class NonSpamMessageController {
        public static readonly Regex UserMentionRegex = new Regex(@"(?<!<@)!?(\d+)(?=>)");

        public EmbedBuilder EmbedBuilder { get; set; }
        public IUserMessage? Message { get; private set; }

        public bool ResetTimeoutOnUpdate { get; set; }
        public TimeSpan? Timeout { get; private set; }

        public string Title { get; set; }
        private Dictionary<string, int> Entries { get; set; } = new Dictionary<string, int>();

        private List<IMessageChannel> TargetChannels { get; set; } = new List<IMessageChannel>();
        private HashSet<string> Mentions { get; set; } = new HashSet<string>();

        public NonSpamMessageController(IMessageChannel channel, string title, Color embedColor = default) {
            TargetChannels.Add(channel);
            Title = title;
            EmbedBuilder = new EmbedBuilder().WithColor(embedColor).WithTitle(title);
        }

        public NonSpamMessageController AddChannel(IMessageChannel channel) {
            TargetChannels.Add(channel);
            return this;
        }

        public NonSpamMessageController AddEntry(string entry) {
            var matchCollection = UserMentionRegex.Matches(entry);
            Mentions.AddRange(matchCollection.Select(match => $"<@{match.Value}>"));
            if (Entries.TryGetValue(entry, out var count)) {
                Entries[entry] = count + 1;
            }
            else {
                Entries[entry] = 1;
            }

            return this;
        }

        public NonSpamMessageController UpdateTimeout(TimeSpan? timeout = null) {
            Timeout = timeout;
            _deletionTimer?.Dispose();
            if (timeout != null) {
                _deletionTimer = new Timer(state => {
                    Entries.Clear();
                    Mentions.Clear();
                    AssemblyEmbed();
                    Message.SafeDelete();
                    Message = null;
                }, null, timeout.Value, TimeSpan.FromMilliseconds(0));
            }

            return this;
        }

        public EmbedBuilder AssemblyEmbed() {
            EmbedBuilder = new EmbedBuilder();
            EmbedBuilder.WithTitle(Title);
            EmbedBuilder.Description = string.Join("\n", Entries.Select(pair => pair.Key + (pair.Value == 1 ? "" : $" ({pair.Value}x)")));
            return EmbedBuilder;
        }

        public DateTime LastSendTime;
        private readonly object _updateLock = new object();
        private TaskCompletionSource<bool>? _updateTaskSource;

        public Task Update() {
            if (ResetTimeoutOnUpdate && Timeout != null) UpdateTimeout(Timeout);
            lock (_updateLock) {
                if (_updateTaskSource != null) return _updateTaskSource.Task;
                _updateTaskSource = new TaskCompletionSource<bool>();
                _ = Task.Run(async () => {
                    await UpdateInternal();
                    _updateTaskSource!.SetResult(true);
                    _updateTaskSource = null;
                });

                return _updateTaskSource.Task;
            }
        }

        private async Task UpdateInternal() {
            try {
                if (Message == null) {
                    await Resend();
                }
                else {
                    try {
                        var needResend = await Utilities.Try(async () => {
                            if (DateTime.Now - LastSendTime > TimeSpan.FromSeconds(20)) {
                                return (await _currentChannel!.GetMessagesAsync(3).FlattenAsync()).FirstOrDefault(message => message.Id == Message!.Id) == null;
                            }

                            return false;
                        }, () => Task.FromResult(true));

                        if (needResend) {
                            await Resend();
                        }
                        else {
                            await Message!.ModifyAsync(properties => {
                                properties.Embed = AssemblyEmbed().Build();
                                properties.Content = string.Join(", ", Mentions);
                            });
                        }
                    }
                    catch (Exception) {
                        // ignored
                    }
                }
            }
            catch {
                // ignored
            }
        }

        private Timer? _deletionTimer;
        private IMessageChannel? _currentChannel;

        private readonly object _sendLock = new object();
        private TaskCompletionSource<IUserMessage?>? _sendTaskSource;

        public Task<IUserMessage?> Resend() {
            lock (_sendLock) {
                if (_sendTaskSource != null) return _sendTaskSource.Task;
                _sendTaskSource = new TaskCompletionSource<IUserMessage?>();
                _ = Task.Run(async () => {
                    _sendTaskSource.SetResult(await ResendInternal());
                    _sendTaskSource = null;
                });

                return _sendTaskSource.Task;
            }
        }

        private async Task<IUserMessage?> ResendInternal() {
            try {
                Message?.SafeDelete();
                Message = null;
                foreach (var channel in TargetChannels) {
                    try {
                        var sendMessage = channel.SendMessageAsync(null, false, AssemblyEmbed().Build());
                        _currentChannel = channel;
                        LastSendTime = DateTime.Now;
                        UpdateTimeout(Timeout);
                        Message = await sendMessage;
                        return await sendMessage;
                    }
                    catch {
                        // ignored
                    }
                }
            }
            catch {
                // ignored
            }

            return null;
        }
    }
}