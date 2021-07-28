using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Common;
using NLog;
using PuppeteerSharp;

namespace ChatExporter {
    public class HtmlRendererService : IService {
        private ILogger Logger = LogManager.GetCurrentClassLogger();
        private Task<RevisionInfo>? _browserDownloadingTask;
        private Browser? _browser;
        private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1);

        public Task OnPostDiscordStartInitialize() {
            Logger.Info("Starting Chrome downloading");
            _browserDownloadingTask = new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultChromiumRevision);
            _browserDownloadingTask.ContinueWith(task => {
                Logger.Fatal("Chrome downloaded failed");
            }, TaskContinuationOptions.OnlyOnFaulted);
            _browserDownloadingTask.ContinueWith(task => {
                Logger.Info("Chrome downloaded");
            }, TaskContinuationOptions.OnlyOnRanToCompletion);

            return Task.CompletedTask;
        }

        public Task OnShutdown(bool isDiscordStarted) {
            return _browser?.CloseAsync() ?? Task.CompletedTask;
        }

        private async Task<Browser> InitializeBrowser() {
            if (_browserDownloadingTask == null) {
                throw new Exception("HtmlRendererService start initialization only after OnPostDiscordStartInitialize call (after starting Discord client)");
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
            await page.SetViewportAsync(new ViewPortOptions() {Width = pageWidth ?? 512, Height = 1});
            await page.ScreenshotAsync(path, new ScreenshotOptions() {FullPage = true});
        }

        public async Task<MemoryStream> RenderHtmlToStream(string html, int? pageWidth = null) {
            var tempFileName = Path.GetTempFileName();
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