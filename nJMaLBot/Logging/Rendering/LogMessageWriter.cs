using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DiscordChatExporter.Domain.Discord.Models;
using DiscordChatExporter.Domain.Exporting;
using DiscordChatExporter.Domain.Exporting.Writers;
using DiscordChatExporter.Domain.Exporting.Writers.MarkdownVisitors;
using Scriban;
using Scriban.Runtime;

namespace Bot.Logging.Rendering {
    public class LogMessageWriter : MessageWriter {
        private readonly TextWriter _writer;
        private readonly string _themeName;
        private readonly List<Message> _messageGroupBuffer = new List<Message>();

        private readonly Template _preambleTemplate;
        private readonly Template _messageGroupTemplate;
        private readonly Template _postambleTemplate;

        private long _messageCount;

        public LogMessageWriter(Stream stream, ExportContext context, string themeName)
            : base(stream, context)
        {
            _writer = new StreamWriter(stream);
            _themeName = themeName;
            
            _preambleTemplate = Template.Parse("<!DOCTYPE html>\n<html lang=\"en\">\n\n<head>\n    {{~ # Metadata ~}}\n    <title>{{ Context.Guild.Name | html.escape }} - {{ Context.Channel.Name | html.escape }}</title>\n    <meta charset=\"utf-8\">\n    <meta name=\"viewport\" content=\"width=device-width\">\n\n    {{~ # Styles ~}}\n    <style>\n  .removed {\n    opacity: 0.2;\n    background: darkred;\n}\n      {{ CoreStyleSheet }}\n    </style>\n    <style>\n        {{ ThemeStyleSheet }}\n    </style>\n\n    {{~ # Syntax highlighting ~}}\n    <link rel=\"stylesheet\" href=\"https://cdnjs.cloudflare.com/ajax/libs/highlight.js/9.15.6/styles/{{HighlightJsStyleName}}.min.css\">\n    <script src=\"https://cdnjs.cloudflare.com/ajax/libs/highlight.js/9.15.6/highlight.min.js\"></script>\n    <script>\n        document.addEventListener('DOMContentLoaded', () => {\n            document.querySelectorAll('.pre--multiline').forEach(block => hljs.highlightBlock(block));\n        });\n    </script>\n\n    {{~ # Local scripts ~}}\n    <script>\n        function scrollToMessage(event, id) {\n            var element = document.getElementById('message-' + id);\n\n            if (element) {\n                event.preventDefault();\n\n                element.classList.add('chatlog__message--highlighted');\n\n                window.scrollTo({\n                    top: element.getBoundingClientRect().top - document.body.getBoundingClientRect().top - (window.innerHeight / 2),\n                    behavior: 'smooth'\n                });\n\n                window.setTimeout(function() {\n                    element.classList.remove('chatlog__message--highlighted');\n                }, 2000);\n            }\n        }\n\n        function showSpoiler(event, element) {\n            if (element && element.classList.contains('spoiler--hidden')) {\n                event.preventDefault();\n                element.classList.remove('spoiler--hidden');\n            }\n        }\n    </script>\n</head>\n<body>\n\n\n<div class=\"chatlog\">\n\n");
            _messageGroupTemplate = Template.Parse(HtmlMessageWriterResources.GetMessageGroupTemplateCode());
            _postambleTemplate = Template.Parse("</div>\n\n</body>\n</html>");
        }

        private MessageGroup GetCurrentMessageGroup()
        {
            var firstMessage = _messageGroupBuffer.First();
            return new MessageGroup(firstMessage.Author, firstMessage.Timestamp, _messageGroupBuffer);
        }

        private TemplateContext CreateTemplateContext(IReadOnlyDictionary<string, object>? constants = null)
        {
            // Template context
            var templateContext = new TemplateContext
            {
                MemberRenamer = m => m.Name,
                MemberFilter = m => true,
                LoopLimit = int.MaxValue,
                StrictVariables = true
            };

            // Model
            var scriptObject = new ScriptObject();

            // Constants
            scriptObject.SetValue("Context", Context, true);
            scriptObject.SetValue("CoreStyleSheet", HtmlMessageWriterResources.GetCoreStyleSheetCode(), true);
            scriptObject.SetValue("ThemeStyleSheet", HtmlMessageWriterResources.GetThemeStyleSheetCode(_themeName), true);
            scriptObject.SetValue("HighlightJsStyleName", $"solarized-{_themeName.ToLowerInvariant()}", true);

            // Additional constants
            if (constants != null)
            {
                foreach (var (member, value) in constants)
                    scriptObject.SetValue(member, value, true);
            }

            // Functions
            scriptObject.Import("FormatDate",
                new Func<DateTimeOffset, string>(d => d.ToLocalTime().ToString(Context.DateFormat, CultureInfo.InvariantCulture)));

            scriptObject.Import("FormatColorRgb",
                new Func<Color?, string?>(c => c != null ? $"rgb({c?.R}, {c?.G}, {c?.B})" : null));

            scriptObject.Import("TryGetUserColor",
                new Func<User, Color?>(Context.TryGetUserColor));

            scriptObject.Import("TryGetUserNick",
                new Func<User, string?>(u => Context.TryGetUserMember(u)?.Nick));

            scriptObject.Import("FormatMarkdown",
                new Func<string?, string>(m => FormatMarkdown(m)));

            scriptObject.Import("FormatEmbedMarkdown",
                new Func<string?, string>(m => FormatMarkdown(m, false)));

            // Push model
            templateContext.PushGlobal(scriptObject);

            // Push output
            templateContext.PushOutput(new TextWriterOutput(_writer));

            return templateContext;
        }

        private string FormatMarkdown(string? markdown, bool isJumboAllowed = true) =>
            HtmlMarkdownVisitor.Format(Context, markdown ?? "", isJumboAllowed);

        private async Task RenderCurrentMessageGroupAsync()
        {
            var templateContext = CreateTemplateContext(new Dictionary<string, object>
            {
                ["MessageGroup"] = GetCurrentMessageGroup()
            });

            await templateContext.EvaluateAsync(_messageGroupTemplate.Page);
        }

        public override async Task WritePreambleAsync()
        {
            var templateContext = CreateTemplateContext();
            await templateContext.EvaluateAsync(_preambleTemplate.Page);
        }

        public override async Task WriteMessageAsync(Message message)
        {
            // We dont display message groups in log image
            // If message group is empty buffer the given message
            if (!_messageGroupBuffer.Any())
            {
                _messageGroupBuffer.Add(message);
            }
            // Otherwise, flush the group and render messages
            else
            {
                await RenderCurrentMessageGroupAsync();

                _messageGroupBuffer.Clear();
                _messageGroupBuffer.Add(message);
            }

            // Increment message count
            _messageCount++;
            await _writer.FlushAsync();
        }

        public override async Task WritePostambleAsync()
        {
            // Flush current message group
            if (_messageGroupBuffer.Any())
                await RenderCurrentMessageGroupAsync();

            var templateContext = CreateTemplateContext(new Dictionary<string, object>
            {
                ["MessageCount"] = _messageCount
            });

            await templateContext.EvaluateAsync(_postambleTemplate.Page);
            await _writer.FlushAsync();
        }

        public override async ValueTask DisposeAsync()
        {
            await _writer.DisposeAsync();
            await base.DisposeAsync();
        }
    }
}