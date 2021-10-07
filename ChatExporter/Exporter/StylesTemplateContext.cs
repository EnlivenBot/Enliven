using System.Runtime.CompilerServices;

namespace ChatExporter.Exporter
{
    internal class StylesTemplateContext
    {
        public ExportContext Context { get; }
        public string ThemeName { get; }
        public string Title { get; }

        public StylesTemplateContext(ExportContext context, string themeName, string title) {
            Context = context;
            ThemeName = themeName;
            Title = title;
        }
    }
}