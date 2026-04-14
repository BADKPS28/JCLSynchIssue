using Azure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Graph;

namespace FileWatcherService;

public interface ISharePointDownloader
{
    Task<bool> FileExistsOnSharePointAsync(string fileName, string libraryName, CancellationToken cancellationToken);
    Task<bool> DownloadFileAsync(string fileName, string libraryName, string localDestinationPath, CancellationToken cancellationToken);
}

public class SharePointDownloader : ISharePointDownloader
{
    private readonly SharePointOptions _options;
    private readonly ILogger<SharePointDownloader> _logger;
    private readonly GraphServiceClient _graphClient;

    // Cache site ID and drive IDs to avoid repeated Graph API calls each cycle
    private string? _siteId;
    private readonly Dictionary<string, string> _driveIdCache = new(StringComparer.OrdinalIgnoreCase);

    public SharePointDownloader(IOptions<SharePointOptions> options, ILogger<SharePointDownloader> logger)
    {
        _options = options.Value;
        _logger = logger;
        _graphClient = CreateGraphClient();
    }

    private GraphServiceClient CreateGraphClient()
    {
        var credential = new ClientSecretCredential(
            _options.TenantId,
            _options.ClientId,
            _options.ClientSecret);

        return new GraphServiceClient(credential);
    }

    public async Task<bool> FileExistsOnSharePointAsync(string fileName, string libraryName, CancellationToken cancellationToken)
    {
        try
        {
            var driveId = await GetDriveIdAsync(libraryName, cancellationToken);
            if (driveId is null)
                return false;

            var driveItem = await _graphClient.Drives[driveId]
                .Root
                .ItemWithPath(fileName)
                .GetAsync(cancellationToken: cancellationToken);

            return driveItem?.Id is not null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check SharePoint existence for {FileName} in library '{Library}'", fileName, libraryName);
            return false;
        }
    }

    public async Task<bool> DownloadFileAsync(string fileName, string libraryName, string localDestinationPath, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Downloading {FileName} from library '{Library}'...", fileName, libraryName);

            var driveId = await GetDriveIdAsync(libraryName, cancellationToken);
            if (driveId is null)
                return false;

            // Get the file item directly by name from the library root
            var driveItem = await _graphClient.Drives[driveId]
                .Root
                .ItemWithPath(fileName)
                .GetAsync(cancellationToken: cancellationToken);

            if (driveItem?.Id is null)
            {
                _logger.LogWarning("File not found on SharePoint: {FileName} in library '{Library}'", fileName, libraryName);
                return false;
            }

            // Download the file content
            var contentStream = await _graphClient.Drives[driveId]
                .Items[driveItem.Id]
                .Content
                .GetAsync(cancellationToken: cancellationToken);

            if (contentStream is null)
            {
                _logger.LogError("Empty content stream for file: {FileName}", fileName);
                return false;
            }

            var fullLocalPath = Path.Combine(localDestinationPath, fileName);
            using var fileStream = File.Create(fullLocalPath);
            await contentStream.CopyToAsync(fileStream, cancellationToken);

            _logger.LogInformation("Successfully downloaded {FileName} to {Path}", fileName, fullLocalPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download {FileName} from library '{Library}'", fileName, libraryName);
            return false;
        }
    }

    private async Task<string?> GetDriveIdAsync(string libraryName, CancellationToken cancellationToken)
    {
        if (_driveIdCache.TryGetValue(libraryName, out var cachedId))
            return cachedId;

        try
        {
            // Resolve site ID once
            if (_siteId is null)
            {
                var siteHostname = new Uri(_options.SiteUrl).Host;
                var sitePath = new Uri(_options.SiteUrl).AbsolutePath;

                var site = await _graphClient.Sites[$"{siteHostname}:{sitePath}"]
                    .GetAsync(cancellationToken: cancellationToken);

                if (site?.Id is null)
                {
                    _logger.LogError("Could not resolve SharePoint site: {SiteUrl}", _options.SiteUrl);
                    return null;
                }

                _siteId = site.Id;
            }

            // Find the drive by library name
            var drives = await _graphClient.Sites[_siteId].Drives
                .GetAsync(cancellationToken: cancellationToken);

            var drive = drives?.Value?.FirstOrDefault(d =>
                string.Equals(d.Name, libraryName, StringComparison.OrdinalIgnoreCase));

            if (drive?.Id is null)
            {
                _logger.LogError("Could not find document library: {LibraryName}", libraryName);
                return null;
            }

            _driveIdCache[libraryName] = drive.Id;
            return drive.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve drive ID for library '{LibraryName}'", libraryName);
            return null;
        }
    }
}
