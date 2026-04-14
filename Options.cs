namespace FileWatcherService;

public class FileWatcherOptions
{
    public string LocalFolderPath { get; set; } = string.Empty;
    public string ScheduledRunTime { get; set; } = "04:00"; // 24-hour format HH:mm
    public List<WatchTarget> WatchTargets { get; set; } = new();
}

public class WatchTarget
{
    public string LibraryName { get; set; } = string.Empty;
    public string LocalFolderPath { get; set; } = string.Empty;

    // Use date format tokens in curly braces, e.g. "ePic-{yyMMdd}" or "EpicorDetailExport_{MM-dd-yy}"
    public string FileNameFormat { get; set; } = string.Empty;

    public List<string> Extensions { get; set; } = new();
}

public class SharePointOptions
{
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string SiteUrl { get; set; } = string.Empty;
}

public class EmailOptions
{
    public string SenderAddress { get; set; } = string.Empty;
    public List<string> Recipients { get; set; } = new();
}
