using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using PuppeteerSharp;
using PuppeteerSharp.BrowserData;

namespace ChatExporter {
    public sealed class HtmlRendererService : IDisposable, IAsyncDisposable {
        private readonly Task<InstalledBrowser>? _browserDownloadingTask;
        private readonly SemaphoreSlim _semaphoreSlim = new(1);
        private IBrowser? _browser;
        public HtmlRendererService(ILogger logger) {
            logger.Info("Starting Chrome downloading");
            _browserDownloadingTask = new BrowserFetcher().DownloadAsync(BrowserTag.Stable);
            _browserDownloadingTask.ContinueWith(task => {
                logger.Fatal(task.Exception?.Flatten(), "Chrome downloaded failed");
            }, TaskContinuationOptions.OnlyOnFaulted);
            _browserDownloadingTask.ContinueWith(task => {
                logger.Info("Chrome downloaded");
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        public ValueTask DisposeAsync() {
            return _browser?.DisposeAsync() ?? new ValueTask();
        }

        public void Dispose() {
            _browser?.Dispose();
        }

        private async Task<IBrowser> InitializeBrowser() {
            if (_browserDownloadingTask == null) {
                throw new InvalidOperationException("HtmlRendererService start initialization only after OnPostDiscordStart call (after starting Discord client)");
            }

            await _semaphoreSlim.WaitAsync();
            try {
                await _browserDownloadingTask;
                return _browser ??= await Puppeteer.LaunchAsync(new LaunchOptions {
                    Headless = true
                });
            }
            finally {
                _semaphoreSlim.Release();
            }
        }

        public async Task RenderHtml(string html, string path, int? pageWidth = null) {
            var browser = await InitializeBrowser();

            await using var page = await browser.NewPageAsync();
            await page.SetContentAsync(html);
            await page.SetViewportAsync(new ViewPortOptions() { Width = pageWidth ?? 512, Height = 1 });
            await page.ScreenshotAsync(path, new ScreenshotOptions() { FullPage = true });
        }

        public async Task<MemoryStream> RenderHtmlToStream(string html, int? pageWidth = null) {
            var tempFileName = Path.GetRandomFileName();
            try {
                await RenderHtml(html, tempFileName, pageWidth);
                return new MemoryStream(await File.ReadAllBytesAsync(tempFileName));
            }
            finally {
                File.Delete(tempFileName);
            }
        }
    }
}