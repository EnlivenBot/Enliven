namespace ChatExporter.Exporter
{
    internal class PostambleTemplateContext {
        public ExportContext ExportContext { get; }

        public PostambleTemplateContext(ExportContext exportContext)
        {
            ExportContext = exportContext;
        }
    }
}