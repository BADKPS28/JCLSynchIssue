using FileWatcherService;

IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "FileWatcherService";
    })
    .ConfigureServices((context, services) =>
    {
        services.Configure<FileWatcherOptions>(
            context.Configuration.GetSection("FileWatcher"));
        services.Configure<SharePointOptions>(
            context.Configuration.GetSection("SharePoint"));
        services.Configure<EmailOptions>(
            context.Configuration.GetSection("Email"));

        services.AddSingleton<ISharePointDownloader, SharePointDownloader>();
        services.AddSingleton<IEmailNotifier, EmailNotifier>();
        services.AddHostedService<FileWatcherWorker>();
    })
    .ConfigureLogging((context, logging) =>
    {
        logging.AddEventLog();
    })
    .Build();

await host.RunAsync();
