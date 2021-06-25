using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bot.DiscordRelated.MessageComponents;
using Bot.Utilities.Collector;
using Common;
using Common.Localization.Providers;
using Common.Utils;
using Discord;
using Discord.WebSocket;
using Tyrrrz.Extensions;

#pragma warning disable 4014

namespace Bot.DiscordRelated {
    public partial class PaginatedMessage : DisposableBase {
        private readonly EmbedBuilder _embedBuilder = new EmbedBuilder();
        private readonly SingleTask<IUserMessage?> _resendTask;
        private readonly SingleTask _updateTask;
        public readonly PaginatedAppearanceOptions Options;
        private bool _jumpEnabled;
        private bool _isCollectionUpdating;

        private readonly Timer _timeoutTimer;

        public IUserMessage? Message;
        public ILocalizationProvider Loc;
        private EnlivenComponentManager _enlivenComponentManager = null!;
        private MessageComponentService _messageComponentService;

        public PaginatedMessage(PaginatedAppearanceOptions options, IUserMessage message, ILocalizationProvider loc, MessageComponentService messageComponentService, MessagePage? errorPage = null)
            : this(options, message.Channel, loc, messageComponentService, errorPage) {
            Channel = message.Channel;
            Message = message;
            if (Message.Author.Id != Program.Client.CurrentUser.Id) throw new ArgumentException($"{nameof(message)} must be from the current user");
        }

        public PaginatedMessage(PaginatedAppearanceOptions options, IMessageChannel channel, ILocalizationProvider loc, MessageComponentService messageComponentService, MessagePage? errorPage = null) {
            _messageComponentService = messageComponentService;
            Loc = loc;
            Channel = channel;
            Options = options;

            ErrorPage = errorPage ?? new MessagePage("Loading...");
            _ = InitializeComponentManager();

            Pages.CollectionChanged += (sender, args) => Update();
            _timeoutTimer = new Timer(state => { Dispose(); });
            _updateTask = new SingleTask(UpdateTaskAction) {CanBeDirty = true, BetweenExecutionsDelay = TimeSpan.FromSeconds(1.5)};
            _resendTask = new SingleTask<IUserMessage?>(ResendTaskAction) {BetweenExecutionsDelay = TimeSpan.FromSeconds(10)};

            _isCollectionUpdating = false;
        }

        private async Task InitializeComponentManager() {
            _enlivenComponentManager = new EnlivenComponentManager(_messageComponentService);
            _enlivenComponentManager.SetCallback(OnButtonPress);
            
            var builder = new EnlivenButtonBuilder().WithStyle(ButtonStyle.Secondary).WithTargetRow(0);
            _enlivenComponentManager.WithButton(builder.Clone().WithEmote(Options.First).WithCustomId("First"));
            _enlivenComponentManager.WithButton(builder.Clone().WithEmote(Options.Back).WithCustomId("Back"));
            _enlivenComponentManager.WithButton(builder.Clone().WithEmote(Options.Next).WithCustomId("Next"));
            _enlivenComponentManager.WithButton(builder.Clone().WithEmote(Options.Last).WithCustomId("Last"));
            builder.WithTargetRow(1);
            if (Options.StopEnabled) _enlivenComponentManager.WithButton(builder.Clone().WithEmote(Options.Stop).WithCustomId("Stop").WithStyle(ButtonStyle.Danger));
            if (Options.DisplayInformationIcon) _enlivenComponentManager.WithButton(builder.Clone().WithEmote(Options.Info).WithCustomId("Info").WithStyle(ButtonStyle.Primary));

            _jumpEnabled = Options.JumpDisplayOptions == JumpDisplayOptions.Always ||
                           Options.JumpDisplayOptions == JumpDisplayOptions.WithManageMessages &&
                           Channel is IGuildChannel guildChannel &&
                           (await guildChannel.GetUserAsync(Program.Client.CurrentUser.Id)).GetPermissions(guildChannel).ManageMessages;
            if (_jumpEnabled) _enlivenComponentManager.WithButton(builder.Clone().WithEmote(Options.Jump).WithCustomId("Jump").WithPriority(0));
        }

        private void OnButtonPress(string s, SocketMessageComponent component, EnlivenButtonBuilder arg3) {
            switch (s) {
                case "Back":
                    PageNumber--;
                    break;
                case "Next":
                    PageNumber++;
                    break;
                case "First":
                    PageNumber = 0;
                    break;
                case "Last":
                    PageNumber = Pages.Count - 1;
                    break;
                case "Stop":
                    Dispose();
                    return;
                case "Info":
                    _ = component.FollowupAsync(Options.InformationText).DelayedDelete(Options.InfoTimeout);
                    break;
                case "Jump":
                    CollectorsUtils.CollectMessage(component.Channel, message => message.Author.Id == component.User.Id, async eventArgs => {
                        eventArgs.StopCollect();
                        if (!int.TryParse(eventArgs.Message.Content, out var result)) return;
                        await eventArgs.RemoveReason();
                        PageNumber = result - 1;
                        CoercePageNumber();
                        Update(false);
                    });
                    break;
                default:
                    return;
            }

            CoercePageNumber();
            _updateTask.Execute(true, TimeSpan.FromSeconds(1));
        }

        private async Task UpdateTaskAction() {
            if (IsDisposed) return;
            UpdateTimeout(true);
            try {
                if (Message == null) {
                    await Resend();
                }
                else {
                    await Message!.ModifyAsync(properties => {
                        properties.Components = _enlivenComponentManager.Build();
                        properties.Embed = GenerateEmbed();
                        properties.Content = null;
                    });
                    _enlivenComponentManager.AssociateWithMessage(Message);
                }
            }
            catch {
                // ignored
            }
            finally {
                UpdateTimeout();
            }
        }
        
        private async Task<IUserMessage?> ResendTaskAction() {
            UpdateTimeout(true);
            try {
                Message?.SafeDelete();
                Message = null;
                Message = await Channel.SendMessageAsync(null, false, GenerateEmbed(), component: _enlivenComponentManager.Build());
                _enlivenComponentManager.AssociateWithMessage(Message);
                return Message;
            }
            catch {
                // ignored
            }
            finally {
                UpdateTimeout();
            }

            return null;
        }

        private Embed GenerateEmbed() {
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
                    var footerText = Options.FooterFormat.Get(Loc, PageNumber + 1, Pages.Count);
                    if (!string.IsNullOrWhiteSpace(Footer?.Text)) footerText += $" | {Footer.Text}";
                    _embedBuilder.Footer = _embedBuilder.Footer.WithText(footerText);
                }
            }

            return _embedBuilder.Build();
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
        /// <param name="fields">Persistent fields rendered for any page</param>
        public void SetPages(string content, string format = "{0}", int lineLimit = int.MaxValue, IEnumerable<EmbedFieldBuilder>? fields = null) {
            PagesRecreating(() => {
                var pageContentLength = Constants.MaxFieldLength - format.Length;
                var fieldsList = fields?.ToList();
                if (fieldsList != null) {
                    pageContentLength -= fieldsList.Sum(builder => builder.Name.Length + builder.Value.ToString()!.Length);
                }

                var lines = content.Split("\n");
                var page = new MessagePage {Fields = fieldsList?.ToList() ?? new List<EmbedFieldBuilder>()};
                var currentLinesCount = 0;
                foreach (var line in lines) {
                    if (page.Description.Length + line.Length > pageContentLength || currentLinesCount > lineLimit) {
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
            });
        }

        public void SetPages(IEnumerable<MessagePage> pages) {
            PagesRecreating(() => Pages.AddRange(pages));
        }

        public void SetPages(string description, IEnumerable<EmbedFieldBuilder> fields, int? fieldsLimit) {
            PagesRecreating(() => {
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
            });
        }

        private void PagesRecreating(Action action) {
            _isCollectionUpdating = true;
            Pages.Clear();
            try {
                action();
            }
            finally {
                CoercePageNumber();
                _isCollectionUpdating = false;
                Update();
            }
        }


        public Task Update(bool resendIfNeeded = true) {
            if (_isCollectionUpdating) {
                return Task.CompletedTask;
            }

            if (!resendIfNeeded && Message == null && !_updateTask.IsExecuting) {
                return Task.CompletedTask;
            }

            var manyPages = Pages.Count > 1;
            _enlivenComponentManager.GetButton("Jump")?.WithDisabled(!manyPages);
            _enlivenComponentManager.GetButton("Back")?.WithDisabled(!manyPages);
            _enlivenComponentManager.GetButton("Next")?.WithDisabled(!manyPages);
            _enlivenComponentManager.GetButton("First")?.WithDisabled(!manyPages);
            _enlivenComponentManager.GetButton("Last")?.WithDisabled(!manyPages);
            return _updateTask.Execute();
        }

        public Task Resend() {
            return _resendTask.Execute();
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

        protected override void DisposeInternal() {
            base.DisposeInternal();
            _timeoutTimer.Dispose();
            _resendTask.Dispose();
            _updateTask.Dispose();
            Message?.SafeDelete();
            _enlivenComponentManager.Dispose();
        }
    }
}