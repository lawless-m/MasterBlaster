namespace MasterBlaster.Logging;

using MasterBlaster.Config;

public class ScreenshotManager
{
    private readonly LoggingConfig _config;

    public ScreenshotManager(LoggingConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Saves a PNG screenshot to the configured screenshot directory.
    /// Returns the full path of the saved file.
    /// </summary>
    public string SaveScreenshot(byte[] pngData, string prefix)
    {
        var dir = Path.GetFullPath(_config.ScreenshotDirectory);
        Directory.CreateDirectory(dir);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
        var fileName = $"{prefix}_{timestamp}.png";
        var filePath = Path.Combine(dir, fileName);

        File.WriteAllBytes(filePath, pngData);
        return filePath;
    }

    /// <summary>
    /// Deletes screenshots older than the configured retention period.
    /// </summary>
    public void CleanOldScreenshots()
    {
        var dir = Path.GetFullPath(_config.ScreenshotDirectory);
        if (!Directory.Exists(dir))
            return;

        var cutoff = DateTime.UtcNow.AddDays(-_config.RetentionDays);
        var files = Directory.GetFiles(dir, "*.png");

        foreach (var file in files)
        {
            var info = new FileInfo(file);
            if (info.CreationTimeUtc < cutoff)
            {
                try
                {
                    info.Delete();
                }
                catch (IOException)
                {
                    // Best-effort cleanup; skip files that are locked.
                }
            }
        }
    }
}
