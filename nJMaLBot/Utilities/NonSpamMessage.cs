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
        
        private Task? _modifyAsync;
        private bool _modifyQueued;
        public EmbedBuilder EmbedBuilder { get; set; }
        public IUserMessage? Message { get; private set; }

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

        public DateTime LastCheck;

        public async Task Update() {
            try {
                if (Message == null)
                    await ForceResend();

                //Not thread safe method cuz in this case, thread safety is a waste of time
                if (_modifyAsync?.IsCompleted ?? true) {
                    UpdateInternal();
                    if (_modifyAsync != null) await _modifyAsync;
                }
                else {
                    if (_modifyQueued)
                        return;
                    try {
                        _modifyQueued = true;
                        await _modifyAsync;
                        UpdateInternal();
                    }
                    finally {
                        _modifyQueued = false;
                    }
                }

                void UpdateInternal() {
                    _modifyAsync = Task.Run(async () => {
                        try {
                            if (await NeedResend()) {
                                await ForceResend();
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
                    });
                }
            }
            catch {
                // ignored
            }
        }

        private async Task<bool> NeedResend() {
            try {
                if (DateTime.Now - LastCheck > TimeSpan.FromSeconds(20)) {
                    return (await _currentChannel!.GetMessagesAsync(3).FlattenAsync()).FirstOrDefault(message => message.Id == Message!.Id) == null;
                }

                return false;
            }
            catch (Exception) {
                return true;
            }
        }

        private SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1);
        private Timer? _deletionTimer;
        private IMessageChannel? _currentChannel;

        public async Task ForceResend() {
            if (_semaphoreSlim.CurrentCount == 0) {
                // If message now sending - wait for sending end
                // And release back
                await _semaphoreSlim.WaitAsync(1);
                if (Message != null) {
                    _semaphoreSlim.Release();
                    return;
                }
            }
            else {
                await _semaphoreSlim.WaitAsync();
            }

            try {
                Message?.SafeDelete();
                foreach (var channel in TargetChannels) {
                    try {
                        var sendMessage = channel.SendMessageAsync(null, false, AssemblyEmbed().Build());
                        _modifyAsync = sendMessage;
                        Message = await sendMessage;
                        _currentChannel = channel;
                        LastCheck = DateTime.Now;
                        break;
                    }
                    catch (Exception) {
                        // ignored
                    }
                }
            }
            finally {
                _semaphoreSlim.Release();
            }
        }
    }
}