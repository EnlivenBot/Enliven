using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Bot.DiscordRelated.Criteria;
using Bot.Utilities;
using Common;
using Discord;
using Tyrrrz.Extensions;

namespace Bot.DiscordRelated {
    public class NonSpamMessageController {
        public static readonly Regex UserMentionRegex = new Regex(@"(?<!<@)!?(\d+)(?=>)");

        private readonly SingleTask<IUserMessage?> _resendTask;
        private readonly SingleTask _updateTask;
        private IMessageChannel? _currentChannel;

        private Timer? _deletionTimer;

        private TimeChecker _lastSendTimeChecker = new TimeChecker(TimeSpan.FromSeconds(20));

        public NonSpamMessageController(IMessageChannel channel, string title, Color embedColor = default) {
            TargetChannels.Add(channel);
            Title = title;
            EmbedBuilder = new EmbedBuilder().WithColor(embedColor).WithTitle(title);
            _updateTask = new SingleTask(async () => {
                try {
                    if (Message == null) {
                        await Resend();
                    }
                    else {
                        try {
                            if (await _lastSendTimeChecker.ToCriteria().AddCriterion(new EnsureLastMessage(_currentChannel!, Message).Invert()).JudgeAsync()) {
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
            }) {CanBeDirty = true, BetweenExecutionsDelay = TimeSpan.FromSeconds(1)};
            _resendTask = new SingleTask<IUserMessage?>(async () => {
                try {
                    Message?.SafeDelete();
                    Message = null;
                    foreach (var messageChannel in TargetChannels) {
                        try {
                            var sendMessage = messageChannel.SendMessageAsync(null, false, AssemblyEmbed().Build());
                            _currentChannel = messageChannel;
                            _lastSendTimeChecker.Update();
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
            });
        }

        public EmbedBuilder EmbedBuilder { get; set; }
        public IUserMessage? Message { get; private set; }

        public bool ResetTimeoutOnUpdate { get; set; }
        public TimeSpan? Timeout { get; private set; }

        public string Title { get; set; }
        private Dictionary<string, int> Entries { get; set; } = new Dictionary<string, int>();

        private List<IMessageChannel> TargetChannels { get; set; } = new List<IMessageChannel>();
        private HashSet<string> Mentions { get; set; } = new HashSet<string>();

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

        public Task Update() {
            return _updateTask.Execute();
        }

        public Task<IUserMessage?> Resend() {
            return _resendTask.Execute();
        }
    }
}