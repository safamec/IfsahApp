namespace IfsahApp.Config;

public static class AppSettings
{
    private static readonly IConfigurationRoot _configuration;

    static AppSettings()
    {
        _configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory) // important when running from bin/
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
    }

    public static string BaseAttachmentPath
    {
        get
        {
            var path = _configuration.GetSection("AttachmentSettings")["BasePath"];

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException("BasePath is not configured in appsettings.json under AttachmentSettings.");
            }

            return path;
        }
    }
}
