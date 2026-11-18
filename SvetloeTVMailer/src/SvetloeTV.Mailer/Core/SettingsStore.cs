using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace SvetloeTV.Mailer.Core;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public AppSettings Load()
    {
        AppPaths.Ensure();
        if (!File.Exists(AppPaths.Settings)) return new AppSettings();
        try { return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.Settings), JsonOptions) ?? new AppSettings(); }
        catch (Exception ex) { AppLog.Error("Settings.Load", ex); return new AppSettings(); }
    }

    public void Save(AppSettings settings)
    {
        AppPaths.Ensure();
        var tmp = AppPaths.Settings + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(tmp, AppPaths.Settings, true);
        ApplyAutoStart(settings.AutoStartWithWindows);
    }

    public static void SetPassword(AppSettings s, string password) => s.SmtpPasswordProtected = Dpapi.Protect(password);
    public static string GetPassword(AppSettings s) => string.IsNullOrWhiteSpace(s.SmtpPasswordProtected) ? "" : Dpapi.Unprotect(s.SmtpPasswordProtected);

    public static void SetPin(AppSettings s, string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        s.AdminPinSalt = Convert.ToBase64String(salt);
        s.AdminPinHash = Convert.ToBase64String(HashPin(pin, salt));
    }

    public static bool VerifyPin(AppSettings s, string pin)
    {
        if (string.IsNullOrWhiteSpace(s.AdminPinHash) || string.IsNullOrWhiteSpace(s.AdminPinSalt)) return false;
        try
        {
            var salt = Convert.FromBase64String(s.AdminPinSalt);
            return CryptographicOperations.FixedTimeEquals(HashPin(pin, salt), Convert.FromBase64String(s.AdminPinHash));
        }
        catch { return false; }
    }

    private static byte[] HashPin(string pin, byte[] salt)
    {
        using var pbkdf = new Rfc2898DeriveBytes(pin, salt, 120_000, HashAlgorithmName.SHA256);
        return pbkdf.GetBytes(32);
    }

    private static void ApplyAutoStart(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;
            if (enabled) key.SetValue("SvetloeTVMailer", $""{Application.ExecutablePath}" --autostart");
            else key.DeleteValue("SvetloeTVMailer", false);
        }
        catch (Exception ex) { AppLog.Error("AutoStart", ex); }
    }

    private static class Dpapi
    {
        [StructLayout(LayoutKind.Sequential)] private struct DATA_BLOB { public int cbData; public IntPtr pbData; }
        [DllImport("Crypt32.dll", SetLastError = true, CharSet = CharSet.Auto)] private static extern bool CryptProtectData(ref DATA_BLOB pDataIn, string? szDataDescr, IntPtr pOptionalEntropy, IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, ref DATA_BLOB pDataOut);
        [DllImport("Crypt32.dll", SetLastError = true, CharSet = CharSet.Auto)] private static extern bool CryptUnprotectData(ref DATA_BLOB pDataIn, IntPtr ppszDataDescr, IntPtr pOptionalEntropy, IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, ref DATA_BLOB pDataOut);
        [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr hMem);
        private const int CRYPTPROTECT_UI_FORBIDDEN = 0x1;

        public static string Protect(string text) => Convert.ToBase64String(ProtectBytes(Encoding.UTF8.GetBytes(text)));
        public static string Unprotect(string text) => Encoding.UTF8.GetString(UnprotectBytes(Convert.FromBase64String(text)));

        private static byte[] ProtectBytes(byte[] input)
        {
            var inputBlob = ToBlob(input); var output = new DATA_BLOB();
            try
            {
                if (!CryptProtectData(ref inputBlob, "SvetloeTVMailer SMTP", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, ref output)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                return FromBlob(output);
            }
            finally { FreeBlob(inputBlob); if (output.pbData != IntPtr.Zero) LocalFree(output.pbData); }
        }

        private static byte[] UnprotectBytes(byte[] input)
        {
            var inputBlob = ToBlob(input); var output = new DATA_BLOB();
            try
            {
                if (!CryptUnprotectData(ref inputBlob, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, ref output)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                return FromBlob(output);
            }
            finally { FreeBlob(inputBlob); if (output.pbData != IntPtr.Zero) LocalFree(output.pbData); }
        }

        private static DATA_BLOB ToBlob(byte[] bytes) { var p = Marshal.AllocHGlobal(bytes.Length); Marshal.Copy(bytes, 0, p, bytes.Length); return new DATA_BLOB { cbData = bytes.Length, pbData = p }; }
        private static byte[] FromBlob(DATA_BLOB b) { var bytes = new byte[b.cbData]; Marshal.Copy(b.pbData, bytes, 0, b.cbData); return bytes; }
        private static void FreeBlob(DATA_BLOB b) { if (b.pbData != IntPtr.Zero) Marshal.FreeHGlobal(b.pbData); }
    }
}
