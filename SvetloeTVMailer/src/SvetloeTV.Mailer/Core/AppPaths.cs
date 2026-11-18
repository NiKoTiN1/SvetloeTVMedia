namespace SvetloeTV.Mailer.Core;

public static class AppPaths
{
    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SvetloeTVMailer");
    public static string Data => Path.Combine(Root, "Data");
    public static string Logs => Path.Combine(Root, "Logs");
    public static string Backups => Path.Combine(Root, "Backups");
    public static string Assets => Path.Combine(Root, "Assets");
    public static string Db => Path.Combine(Data, "mailer.db");
    public static string Settings => Path.Combine(Data, "settings.json");
    public static string Presentation => Path.Combine(Assets, "presentation.pdf");

    public static void Ensure()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Data);
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Backups);
        Directory.CreateDirectory(Assets);
    }
}
