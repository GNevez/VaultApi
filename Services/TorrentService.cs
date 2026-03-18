using System.Collections.Concurrent;
using MonoTorrent;
using MonoTorrent.Client;
using vaultApi.DTOs;

namespace vaultApi.Services;

public class TorrentService : ITorrentService, IAsyncDisposable
{
    private readonly ClientEngine _engine;
    private readonly string _downloadPath;
    private readonly ConcurrentDictionary<string, TorrentManager> _managers = new();
    private readonly ConcurrentDictionary<string, string> _titles = new();
    private readonly ILogger<TorrentService> _logger;
    private bool _engineStarted;

    public TorrentService(IConfiguration configuration, ILogger<TorrentService> logger)
    {
        _logger = logger;
        _downloadPath = configuration.GetValue<string>("Torrent:DownloadPath")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "VaultDownloads");

        Directory.CreateDirectory(_downloadPath);

        var cacheDir = Path.Combine(_downloadPath, ".cache");
        Directory.CreateDirectory(cacheDir);

        var settings = new EngineSettingsBuilder
        {
            AllowPortForwarding = true,
            AutoSaveLoadDhtCache = true,
            AutoSaveLoadFastResume = true,
            AutoSaveLoadMagnetLinkMetadata = true,
            CacheDirectory = cacheDir,
            ListenEndPoints = new Dictionary<string, System.Net.IPEndPoint>
            {
                { "ipv4", new System.Net.IPEndPoint(System.Net.IPAddress.Any, 55123) }
            },
            MaximumConnections = 150,
            MaximumHalfOpenConnections = 8,
            DhtEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Any, 55123),
        };

        _engine = new ClientEngine(settings.ToSettings());
        _logger.LogInformation("TorrentService created. Download path: {Path}, Cache: {Cache}", _downloadPath, cacheDir);
    }

    private async Task EnsureEngineStartedAsync()
    {
        if (_engineStarted) return;

        _logger.LogInformation("Starting torrent engine and bootstrapping DHT...");

        // Start the engine (this initializes DHT, port forwarding, etc.)
        await _engine.StartAllAsync();
        _logger.LogInformation("Engine StartAllAsync completed. DHT state: {State}, DHT nodes: {Nodes}",
            _engine.Dht.State, _engine.Dht.NodeCount);

        // Wait briefly for DHT to bootstrap
        var dhtWait = 0;
        while (_engine.Dht.NodeCount == 0 && dhtWait < 10)
        {
            await Task.Delay(1000);
            dhtWait++;
            _logger.LogInformation("Waiting for DHT bootstrap... Nodes: {Nodes}, State: {State}",
                _engine.Dht.NodeCount, _engine.Dht.State);
        }

        _engineStarted = true;
        _logger.LogInformation("Torrent engine ready. DHT nodes: {Count}, DHT state: {State}",
            _engine.Dht.NodeCount, _engine.Dht.State);
    }

    public async Task<DownloadStatusDto> StartDownloadAsync(string magnetUri, string? title = null)
    {
        await EnsureEngineStartedAsync();

        _logger.LogInformation("Starting download for magnet: {Title}", title ?? magnetUri.Substring(0, Math.Min(80, magnetUri.Length)));

        var magnet = MagnetLink.Parse(magnetUri);
        var infoHashStr = magnet.InfoHashes.V1?.ToHex() ?? magnet.InfoHashes.V2?.ToHex() ?? Guid.NewGuid().ToString();

        _logger.LogInformation("Parsed magnet link. InfoHash: {Hash}, Trackers: {TrackerCount}",
            infoHashStr, magnet.AnnounceUrls?.Count ?? 0);

        if (_managers.TryGetValue(infoHashStr, out var existing))
        {
            _logger.LogInformation("Torrent already tracked, returning existing. State: {State}", existing.State);
            return MapToDto(infoHashStr, existing);
        }

        var manager = await _engine.AddAsync(magnet, _downloadPath);

        // Log tracker announces
        manager.PeerConnected += (o, e) =>
        {
            _logger.LogInformation("[{Hash}] Peer connected: {Peer}",
                infoHashStr.Substring(0, 8), e.Peer.Uri);
        };
        manager.PeerDisconnected += (o, e) =>
        {
            _logger.LogDebug("[{Hash}] Peer disconnected: {Peer}",
                infoHashStr.Substring(0, 8), e.Peer.Uri);
        };

        _managers[infoHashStr] = manager;
        if (!string.IsNullOrEmpty(title))
            _titles[infoHashStr] = title;

        await manager.StartAsync();

        _logger.LogInformation("[{Hash}] Download started. State: {State}, Peers: {Peers}",
            infoHashStr.Substring(0, 8), manager.State, manager.Peers.Available);

        // Start background monitor for this torrent's metadata
        _ = MonitorMetadataAsync(infoHashStr, manager);

        return MapToDto(infoHashStr, manager);
    }

    private async Task MonitorMetadataAsync(string id, TorrentManager manager)
    {
        var shortHash = id.Substring(0, 8);
        var startTime = DateTime.UtcNow;

        while (manager.State == TorrentState.Metadata || manager.State == TorrentState.Starting)
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation("[{Hash}] Waiting for metadata... State: {State}, DHT nodes: {Dht}, Peers: {Seeds}S/{Leeches}P, Elapsed: {Elapsed:mm\\:ss}",
                shortHash, manager.State, _engine.Dht.NodeCount, manager.Peers.Seeds, manager.Peers.Leechs, elapsed);

            if (elapsed.TotalMinutes > 5)
            {
                _logger.LogWarning("[{Hash}] Metadata fetch taking over 5 minutes. DHT nodes: {Dht}", shortHash, _engine.Dht.NodeCount);
            }

            await Task.Delay(5000);
        }

        _logger.LogInformation("[{Hash}] Metadata resolved! State: {State}, Name: {Name}, Size: {Size}",
            shortHash, manager.State, manager.Torrent?.Name ?? "?", manager.Torrent?.Size ?? 0);
    }

    public Task<List<DownloadStatusDto>> GetAllDownloadsAsync()
    {
        var list = _managers.Select(kv => MapToDto(kv.Key, kv.Value)).ToList();
        return Task.FromResult(list);
    }

    public Task<DownloadStatusDto?> GetDownloadAsync(string id)
    {
        if (_managers.TryGetValue(id, out var manager))
            return Task.FromResult<DownloadStatusDto?>(MapToDto(id, manager));
        return Task.FromResult<DownloadStatusDto?>(null);
    }

    public async Task PauseAsync(string id)
    {
        if (_managers.TryGetValue(id, out var manager))
        {
            _logger.LogInformation("[{Hash}] Pausing download. State: {State}", id.Substring(0, 8), manager.State);
            await manager.PauseAsync();
        }
    }

    public async Task ResumeAsync(string id)
    {
        if (_managers.TryGetValue(id, out var manager))
        {
            _logger.LogInformation("[{Hash}] Resuming download. State: {State}", id.Substring(0, 8), manager.State);
            await manager.StartAsync();
        }
    }

    public async Task CancelAsync(string id)
    {
        if (_managers.TryGetValue(id, out var manager))
        {
            _logger.LogInformation("[{Hash}] Cancelling download. State: {State}", id.Substring(0, 8), manager.State);
            await manager.StopAsync();
            await _engine.RemoveAsync(manager);
            _managers.TryRemove(id, out _);
            _titles.TryRemove(id, out _);
            _logger.LogInformation("[{Hash}] Download cancelled and removed", id.Substring(0, 8));
        }
    }

    private DownloadStatusDto MapToDto(string id, TorrentManager manager)
    {
        var title = _titles.GetValueOrDefault(id) ?? manager.Torrent?.Name ?? "Unknown";

        return new DownloadStatusDto
        {
            Id = id,
            Title = title,
            MagnetUri = manager.MagnetLink?.ToV1String() ?? "",
            State = manager.State.ToString(),
            Progress = manager.Progress,
            DownloadSpeed = manager.Monitor.DownloadRate,
            UploadSpeed = manager.Monitor.UploadRate,
            TotalSize = manager.Torrent?.Size ?? 0,
            DownloadedBytes = (long)(manager.Progress / 100.0 * (manager.Torrent?.Size ?? 0)),
            Seeds = manager.Peers.Seeds,
            Peers = manager.Peers.Leechs,
            SavePath = manager.SavePath
        };
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var manager in _managers.Values)
        {
            await manager.StopAsync();
        }
        await _engine.StopAllAsync();
    }
}
