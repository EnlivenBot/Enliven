namespace ChatExporter.Exporter {
    internal class ExportContext {
        public ExportContext(bool willBeRenderedToImage) {
            WillBeRenderedToImage = willBeRenderedToImage;
        }
        
        public bool WillBeRenderedToImage { get; }
        
        public virtual string AdditionalStyles { get; } = "";
    }
}