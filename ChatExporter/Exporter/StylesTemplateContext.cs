namespace ChatExporter.Exporter {
    internal class StylesTemplateContext {
        public StylesTemplateContext(ExportContext context, string themeName, string title) {
            Context = context;
            ThemeName = themeName;
            Title = title;
        }
        public ExportContext Context { get; }
        public string ThemeName { get; }
        public string Title { get; }
    }
}