namespace SvetloeTV.Mailer.Core;

public static class AppLog
{
    private static readonly object Sync = new();
    public static void Info(string area, string message) => Write("INFO", area, message);
    public static void Warn(string area, string message) => Write("WARN", area, message);
    public static void Error(string area, Exception ex) => Write("ERROR", area, ex.ToString());

    private static void Write(string level, string area, string message)
    {
        try
        {
            AppPaths.Ensure();
            lock (Sync)
            {
                var path = Path.Combine(AppPaths.Logs, $"{DateTime.Now:yyyy-MM-dd}.log");
                File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\t{level}\t{area}\t{message.Replace("\r", " ").Replace("\n", " ")}\n");
            }
        }
        catch { }
    }
}
