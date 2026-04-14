using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace FileWatcherService;

public class FileWatcherWorker : BackgroundService
{
    private readonly ILogger<FileWatcherWorker> _logger;
    private readonly FileWatcherOptions _watcherOptions;
    private readonly IEmailNotifier _emailNotifier;

    public FileWatcherWorker(
        ILogger<FileWatcherWorker> logger,
        IOptions<FileWatcherOptions> watcherOptions,
        IEmailNotifier emailNotifier)
    {
        _logger = logger;
        _watcherOptions = watcherOptions.Value;
        _emailNotifier = emailNotifier;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FileWatcherWorker started. Scheduled run time: {Time} daily.", _watcherOptions.ScheduledRunTime);

        EnsureLocalFoldersExist();

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilNextRun();
            _logger.LogInformation("Next check scheduled at {Time} (in {Hours:F1} hours).",
                DateTime.Now.Add(delay).ToString("MM/dd/yyyy HH:mm"), delay.TotalHours);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            try
            {
                await CheckMissingFilesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error during file check cycle");
            }
        }

        _logger.LogInformation("FileWatcherWorker stopped.");
    }

    private TimeSpan GetDelayUntilNextRun()
    {
        var scheduledTime = TimeSpan.Parse(_watcherOptions.ScheduledRunTime);
        var now = DateTime.Now;
        var nextRun = now.Date.Add(scheduledTime);

        // If scheduled time already passed today, target tomorrow
        if (nextRun <= now)
            nextRun = nextRun.AddDays(1);

        return nextRun - now;
    }

    private async Task CheckMissingFilesAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting file check cycle at {Time}", DateTimeOffset.Now);

        var today = DateTime.Now;
        var missingFiles = new List<string>();

        foreach (var target in _watcherOptions.WatchTargets)
        {
            var baseName = ResolveDatePattern(target.FileNameFormat, today);

            foreach (var ext in target.Extensions)
            {
                var fileName = baseName + ext;
                var fullPath = Path.Combine(target.LocalFolderPath, fileName);

                if (!File.Exists(fullPath))
                {
                    _logger.LogInformation("Missing file: {FileName}", fileName);
                    missingFiles.Add(fileName);
                }
                else
                {
                    _logger.LogDebug("File present: {FileName}", fileName);
                }
            }
        }

        if (missingFiles.Count == 0)
        {
            _logger.LogDebug("All files present.");
            return;
        }

        _logger.LogWarning("{Count} missing file(s) detected. Sending notification.", missingFiles.Count);
        await _emailNotifier.SendMissingFilesReportAsync(missingFiles, cancellationToken);
    }

    // Resolves patterns like "ePic-{yyMMdd}" or "EpicorDetailExport_{MM-dd-yy}" using today's date.
    private static string ResolveDatePattern(string format, DateTime date)
    {
        return Regex.Replace(format, @"\{([^}]+)\}", m => date.ToString(m.Groups[1].Value));
    }

    private void EnsureLocalFoldersExist()
    {
        foreach (var target in _watcherOptions.WatchTargets)
        {
            if (!Directory.Exists(target.LocalFolderPath))
                _logger.LogWarning(
                    "Local folder does not exist: {Folder}. Ensure OneDrive is synced and the path is correct.",
                    target.LocalFolderPath);
            else
                _logger.LogInformation("Monitoring folder: {Folder}", target.LocalFolderPath);
        }
    }
}
