using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bot.Utilities.Collector;
using Bot.Utilities.Emoji;
using Discord;
using Tyrrrz.Extensions;

namespace Bot.Utilities {
    public class PaginatedMessage {
        private readonly EmbedBuilder _embedBuilder = new EmbedBuilder();
        private readonly SingleTask<IUserMessage?> _resendTask;
        private readonly SingleTask _updateTask;
        public readonly PaginatedAppearanceOptions Options;
        private CollectorController _collectorController;
        private bool _jumpEnabled;
        private TaskCompletionSource<bool> _stopTask = new TaskCompletionSource<bool>();

        private Timer _timeoutTimer;

        public IUserMessage? Message;

        public PaginatedMessage(PaginatedAppearanceOptions options, IUserMessage message, MessagePage? errorPage = null) : this(options, message.Channel,
            errorPage) {
            Channel = message.Channel;
            Message = message;
            SetupReactions();
            if (Message.Author.Id != Program.Client.CurrentUser.Id) throw new ArgumentException($"{nameof(message)} must be from the current user");
        }

        public PaginatedMessage(PaginatedAppearanceOptions options, IMessageChannel channel, MessagePage? errorPage = null) {
            Channel = channel;
            Options = options;

            ErrorPage = errorPage ?? new MessagePage("Loading...");

            Pages.CollectionChanged += (sender, args) => Update();

            _timeoutTimer = new Timer(state => {
                _timeoutTimer.Change(-1, -1);
                StopAndClear();
            });

            _updateTask = new SingleTask(async () => {
                UpdateTimeout(true);
                try {
                    if (Message == null) {
                        await Resend();
                    }
                    else {
                        try {
                            await Message!.ModifyAsync(properties => {
                                #pragma warning disable 618
                                UpdateEmbed();
                                #pragma warning restore 618

                                properties.Embed = _embedBuilder.Build();
                                properties.Content = null;
                            });
                        }
                        catch (Exception) {
                            // ignored
                        }
                    }
                }
                catch {
                    // ignored
                }
                finally {
                    UpdateTimeout();
                }
            }) {CanBeDirty = true};

            _resendTask = new SingleTask<IUserMessage?>(async () => {
                UpdateTimeout(true);
                try {
                    Message?.SafeDelete();
                    Message = null;
                    #pragma warning disable 618
                    UpdateEmbed();
                    #pragma warning restore 618
                    Message = await Channel.SendMessageAsync(null, false, _embedBuilder.Build());
                    SetupReactions();
                    return Message;
                }
                catch {
                    // ignored
                }
                finally {
                    UpdateTimeout();
                }

                return null;
            });
        }

        [Obsolete("Internal method, do not use")]
        private void UpdateEmbed() {
            try {
                UpdateInternal(Pages[PageNumber], true);
            }
            catch (Exception) {
                UpdateInternal(ErrorPage, false);
            }

            void UpdateInternal(MessagePage messagePage, bool withFooter) {
                _embedBuilder.Fields.Clear();
                _embedBuilder.Fields = messagePage.Fields;
                _embedBuilder.Description = messagePage.Description;
                _embedBuilder.Footer = Footer ?? new EmbedFooterBuilder();
                if (withFooter) {
                    _embedBuilder.Footer =
                        _embedBuilder.Footer.WithText(Options.FooterFormat.Format(PageNumber + 1, Pages.Count) +
                                                      (string.IsNullOrWhiteSpace(Footer?.Text) ? "" : $" | {Footer.Text}"));
                }
            }
        }

        private void SetupReactions() {
            try {
                Message?.RemoveAllReactionsAsync();
            }
            catch (Exception) {
                // Ignored
            }

            _collectorController = CollectorsUtils.CollectReaction(Message, reaction => true, args => {
                if (args.Reaction.Emote.Equals(Options.Back)) {
                    PageNumber--;
                }
                else if (args.Reaction.Emote.Equals(Options.Next)) {
                    PageNumber++;
                }
                else if (args.Reaction.Emote.Equals(Options.First)) {
                    PageNumber = 0;
                }
                else if (args.Reaction.Emote.Equals(Options.Last)) {
                    PageNumber = Pages.Count - 1;
                }
                else if (args.Reaction.Emote.Equals(Options.Stop)) {
                    StopAndClear();
                    return;
                }
                else if (Options.DisplayInformationIcon && args.Reaction.Emote.Equals(Options.Info)) {
                    try {
                        Channel.SendMessageAsync(Options.InformationText).DelayedDelete(Options.InfoTimeout);
                    }
                    catch (Exception) {
                        // ignored
                    }
                }
                else if (_jumpEnabled && args.Reaction.Emote.Equals(Options.Jump)) {
                    CollectorsUtils.CollectMessage(args.Reaction.Channel, message => message.Author.Id == args.Reaction.UserId, async eventArgs => {
                        eventArgs.StopCollect();
                        await eventArgs.RemoveReason();
                        if (int.TryParse(eventArgs.Message.Content, out var result)) {
                            PageNumber = result - 1;
                            CoercePageNumber();
                            Update(false);
                        }
                    });
                }
                else {
                    return;
                }

                args.RemoveReason();
                CoercePageNumber();
                Update();
            }, CollectorFilter.IgnoreBots);
            _ = Task.Run(async () => {
                await Message.AddReactionAsync(Options.First);
                await Message.AddReactionAsync(Options.Back);
                await Message.AddReactionAsync(Options.Next);
                await Message.AddReactionAsync(Options.Last);

                var manageMessages = (Channel is IGuildChannel guildChannel)
                    ? (await guildChannel.GetUserAsync(Program.Client.CurrentUser.Id)).GetPermissions(guildChannel).ManageMessages
                    : false;

                _jumpEnabled = Options.JumpDisplayOptions == JumpDisplayOptions.Always
                            || (Options.JumpDisplayOptions == JumpDisplayOptions.WithManageMessages && manageMessages);
                if (_jumpEnabled) await Message.AddReactionAsync(Options.Jump);

                await Message.AddReactionAsync(Options.Stop);

                if (Options.DisplayInformationIcon)
                    await Message.AddReactionAsync(Options.Info);
            });
        }

        public IMessageChannel Channel { get; set; }

        public ObservableCollection<MessagePage> Pages { get; private set; } = new ObservableCollection<MessagePage>();

        public MessagePage ErrorPage { get; set; }

        public int PageNumber { get; set; }

        public string Title {
            get => _embedBuilder.Title;
            set => _embedBuilder.Title = value;
        }

        public string Url {
            get => _embedBuilder.Url;
            set => _embedBuilder.Url = value;
        }

        public string ThumbnailUrl {
            get => _embedBuilder.ThumbnailUrl;
            set => _embedBuilder.ThumbnailUrl = value;
        }

        public string ImageUrl {
            get => _embedBuilder.ImageUrl;
            set => _embedBuilder.ImageUrl = value;
        }

        public DateTimeOffset? Timestamp {
            get => _embedBuilder.Timestamp;
            set => _embedBuilder.Timestamp = value;
        }

        public Color? Color {
            get => _embedBuilder.Color;
            set => _embedBuilder.Color = value;
        }

        public EmbedAuthorBuilder Author {
            get => _embedBuilder.Author;
            set => _embedBuilder.Author = value;
        }

        public EmbedFooterBuilder? Footer { get; set; }

        public void UpdateTimeout(bool pauseTimer = false) {
            _timeoutTimer.Change(pauseTimer ? TimeSpan.FromMilliseconds(-1) : Options.Timeout ?? TimeSpan.FromMilliseconds(-1),
                TimeSpan.FromMilliseconds(-1));
        }

        public event EventHandler Stop;

        public Task Wait() {
            return _stopTask.Task;
        }

        public void StopAndClear() {
            _collectorController?.Dispose();
            Message?.DeleteAsync();
            OnStop();
        }

        public void CoercePageNumber() {
            PageNumber = PageNumber.Normalize(0, Pages.Count - 1);
        }

        public void SetDefaultPage() {
            PageNumber = -1;
        }

        /// <summary>
        /// Accepts a string that is broken for description in embed'e and formats
        /// </summary>
        /// <param name="content">Source string</param>
        /// <param name="format">Format string, every page will be formatted with this string. {0} - For content {1} - For page number </param>
        /// <param name="lineLimit">Max lines at one page</param>
        public void SetPages(string content, string format = "", int lineLimit = int.MaxValue) {
            Pages.Clear();
            var pageContentLenght = 2048 - format.Length;
            var lines = content.Split("\n");
            var page = new MessagePage();
            var currentLinesCount = 0;
            foreach (var line in lines) {
                if (page.Description.Length + line.Length > pageContentLenght || currentLinesCount > lineLimit) {
                    FinishCurrentPage();
                    currentLinesCount = 0;
                }

                page.Description += "\n" + line;
                currentLinesCount++;
            }

            FinishCurrentPage();

            void FinishCurrentPage() {
                page.Description = format.Format(page.Description, Pages.Count + 1);
                Pages.Add(page!);
                page = new MessagePage();
            }

            CoercePageNumber();
        }

        public void SetPages(IEnumerable<MessagePage> pages) {
            Pages.Clear();

            Pages.AddRange(pages);

            CoercePageNumber();
        }

        public void SetPages(string description, IEnumerable<EmbedFieldBuilder> fields, int? fieldsLimit) {
            Pages.Clear();

            fieldsLimit = (fieldsLimit ?? EmbedBuilder.MaxFieldCount).Normalize(0, EmbedBuilder.MaxFieldCount);
            _embedBuilder.Fields.Clear();
            _embedBuilder.Description = null;
            _embedBuilder.Footer = Footer;
            var lengthLimit = EmbedBuilder.MaxEmbedLength - _embedBuilder.Length - 10;


            var pagesData = fields.GroupContiguous((list, builder) =>
                list.Count <= fieldsLimit &&
                list.Sum(fieldBuilder => fieldBuilder.Name.Length + fieldBuilder.Value.ToString()!.Length) +
                builder.Name.Length + builder.Value.ToString()!.Length < lengthLimit - description.Length);

            Pages.AddRange(pagesData.Select(list => new MessagePage(description, list)));

            CoercePageNumber();
        }

        public Task Update(bool resendIfNeeded = true) {
            if (!resendIfNeeded && Message == null && !_updateTask.IsExecuting) {
                return Task.CompletedTask;
            }

            return _updateTask.Execute();
        }

        public Task Resend() {
            return _resendTask.Execute();
        }

        protected virtual void OnStop() {
            Stop?.Invoke(this, EventArgs.Empty);
            var oldTaskCompletionSource = _stopTask;
            _stopTask = new TaskCompletionSource<bool>();
            oldTaskCompletionSource.SetResult(true);
        }

        public class MessagePage {
            public MessagePage() { }

            public MessagePage(string description) {
                Description = description;
            }

            public MessagePage(string description, IEnumerable<EmbedFieldBuilder> fieldBuilders) {
                Description = description;
                Fields = fieldBuilders.ToList();
            }

            public string Description { get; set; } = "";
            public List<EmbedFieldBuilder> Fields { get; set; } = new List<EmbedFieldBuilder>();
        }
    }

    public class PaginatedAppearanceOptions {
        public static PaginatedAppearanceOptions Default = new PaginatedAppearanceOptions();
        public IEmote Back = CommonEmoji.LegacyReverse;
        public bool DisplayInformationIcon;
        public IEmote First = CommonEmoji.LegacyTrackPrevious;


        public string FooterFormat = "{0}/{1}";
        public IEmote Info = CommonEmoji.Help;
        public string InformationText = "This is a paginator. React with the respective icons to change page.";
        public TimeSpan InfoTimeout = TimeSpan.FromSeconds(30);
        public IEmote Jump = CommonEmoji.InputNumbers;

        public JumpDisplayOptions JumpDisplayOptions = JumpDisplayOptions.WithManageMessages;
        public IEmote Last = CommonEmoji.LegacyTrackNext;
        public IEmote Next = CommonEmoji.LegacyPlay;
        public IEmote Stop = CommonEmoji.LegacyStop;

        public TimeSpan? Timeout = null;
        public PaginatedAppearanceOptions() { }

        public PaginatedAppearanceOptions(string informationText) {
            InformationText = informationText;
            DisplayInformationIcon = true;
        }
    }


    public enum JumpDisplayOptions {
        Never,
        WithManageMessages,
        Always
    }
}