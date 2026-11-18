using SvetloeTV.Mailer.Core;

namespace SvetloeTV.Mailer;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => AppLog.Error("UI", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => AppLog.Error("Fatal", e.ExceptionObject as Exception ?? new Exception("Unknown fatal error"));

        AppPaths.Ensure();
        using var single = new Mutex(true, "SvetloeTV.Mailer.Singleton", out var first);
        if (!first)
        {
            MessageBox.Show("Программа уже запущена. Проверьте область уведомлений Windows.", "Светлое ТВ — Рассылка", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            using var store = new AppStore();
            store.Initialize();
            var settingsStore = new SettingsStore();
            var settings = settingsStore.Load();

            if (!settings.Configured)
            {
                using var setup = new SetupForm(store, settingsStore, settings);
                if (setup.ShowDialog() != DialogResult.OK) return;
                settings = settingsStore.Load();
            }

            Application.Run(new MainForm(store, settingsStore, settings));
        }
        catch (Exception ex)
        {
            AppLog.Error("Startup", ex);
            MessageBox.Show($"Не удалось запустить программу.\n\n{ex.Message}\n\nПодробности записаны в журнал.", "Светлое ТВ — Рассылка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
