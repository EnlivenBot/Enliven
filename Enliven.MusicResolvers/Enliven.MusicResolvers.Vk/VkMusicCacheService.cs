namespace Enliven.MusicResolvers.Vk;

public class VkMusicCacheService {
    private readonly TimeSpan _expireTime;
    private readonly string? _extension;
    private readonly string _path;
    private Dictionary<string, CacheEntry> _cacheEntries = new();

    public VkMusicCacheService(TimeSpan expireTime, string path, string? extension) {
        _path = path;
        _extension = extension ?? "tmp";
        _expireTime = expireTime;

        // Resetting cache folder
        if (Directory.Exists(path)) Directory.Delete(path, true);
        Directory.CreateDirectory(path);
    }

    public bool TryAccess(string key, out string path) {
        path = string.Empty;
        if (_cacheEntries.TryGetValue(key, out var entry) && !entry.IsExpired) {
            entry.Reset(_expireTime);
            path = entry.Path;
            return true;
        }

        return false;
    }

    public void Put(string key) {
        if (_cacheEntries.TryGetValue(key, out var entry) && !entry.IsExpired) {
            entry.Path = GetFilePathForKey(key);
            entry.Reset(_expireTime);
        }

        _cacheEntries[key] = new CacheEntry(_expireTime, GetFilePathForKey(key));
    }

    public string GetFilePathForKey(string key) {
        return Path.Combine(_path, $"{key}.{_extension}");
    }

    private sealed class CacheEntry {
        private PeriodicTimer? _periodicTimer;

        public CacheEntry(TimeSpan expireTime, string path) {
            Path = path;
            Reset(expireTime);
        }

        public string Path { get; set; }

        public bool IsExpired { get; private set; }

        public async void Reset(TimeSpan expireTime) {
            _periodicTimer?.Dispose();
            _periodicTimer = new PeriodicTimer(expireTime);
            if (await _periodicTimer.WaitForNextTickAsync()) {
                _periodicTimer.Dispose();
                try {
                    IsExpired = true;
                    File.Delete(Path);
                }
                catch (Exception) {
                    // ignored
                }
            }
        }
    }
}