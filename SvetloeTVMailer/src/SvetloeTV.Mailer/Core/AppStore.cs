using Microsoft.Data.Sqlite;
using System.Globalization;

namespace SvetloeTV.Mailer.Core;

public sealed class AppStore : IDisposable
{
    private readonly string _cs = $"Data Source={AppPaths.Db};Cache=Shared";
    public void Initialize()
    {
        AppPaths.Ensure();
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
PRAGMA journal_mode=WAL;
CREATE TABLE IF NOT EXISTS recipients(
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 email TEXT NOT NULL UNIQUE COLLATE NOCASE,
 organization TEXT NOT NULL DEFAULT '',
 contact_name TEXT NOT NULL DEFAULT '',
 status TEXT NOT NULL DEFAULT 'Pending',
 attempts INTEGER NOT NULL DEFAULT 0,
 last_error TEXT,
 last_attempt_utc TEXT,
 retry_at_utc TEXT,
 sent_utc TEXT
);
CREATE INDEX IF NOT EXISTS ix_recipients_status_retry ON recipients(status,retry_at_utc);
CREATE TABLE IF NOT EXISTS events(
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 utc TEXT NOT NULL,
 type TEXT NOT NULL,
 email TEXT NOT NULL DEFAULT '',
 message TEXT NOT NULL DEFAULT ''
);
CREATE INDEX IF NOT EXISTS ix_events_utc ON events(utc);
CREATE TABLE IF NOT EXISTS meta(key TEXT PRIMARY KEY, value TEXT NOT NULL);
";
        cmd.ExecuteNonQuery();
        EnsureMeta("RunState", "Paused"); EnsureMeta("SafetyStage", "Control"); EnsureMeta("StageAttempts", "0"); EnsureMeta("StagePermanentFailures", "0"); EnsureMeta("CooldownUntilUtc", ""); EnsureMeta("ThrottleLevel", "0");
    }

    private SqliteConnection Open() { var c = new SqliteConnection(_cs); c.Open(); return c; }
    private void EnsureMeta(string key, string value) { if (GetMeta(key) == null) SetMeta(key, value); }

    public string? GetMeta(string key)
    {
        using var c = Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT value FROM meta WHERE key=$k"; cmd.Parameters.AddWithValue("$k", key); return cmd.ExecuteScalar()?.ToString();
    }
    public void SetMeta(string key, string value)
    {
        using var c = Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "INSERT INTO meta(key,value) VALUES($k,$v) ON CONFLICT(key) DO UPDATE SET value=$v"; cmd.Parameters.AddWithValue("$k", key); cmd.Parameters.AddWithValue("$v", value); cmd.ExecuteNonQuery();
    }

    public int ImportRecipients(string path)
    {
        var rows = CsvImporter.Read(path); int added = 0;
        using var c = Open(); using var tx = c.BeginTransaction();
        foreach (var row in rows)
        {
            var email = CsvImporter.FindEmail(row);
            if (string.IsNullOrWhiteSpace(email)) continue;
            using var cmd = c.CreateCommand(); cmd.Transaction = tx;
            cmd.CommandText = "INSERT OR IGNORE INTO recipients(email,organization,contact_name) VALUES($e,$o,$n)";
            cmd.Parameters.AddWithValue("$e", email.Trim()); cmd.Parameters.AddWithValue("$o", CsvImporter.Get(row, "organization", "организация", "company", "оператор")); cmd.Parameters.AddWithValue("$n", CsvImporter.Get(row, "contactname", "contact", "контакт", "фио"));
            added += cmd.ExecuteNonQuery();
        }
        tx.Commit(); AddEvent("Import", "", $"Импортировано новых адресов: {added}"); return added;
    }

    public int ImportHistory(string path)
    {
        var rows = CsvImporter.Read(path); int changed = 0;
        using var c = Open(); using var tx = c.BeginTransaction();
        foreach (var row in rows)
        {
            var email = CsvImporter.FindEmail(row); if (string.IsNullOrWhiteSpace(email)) continue;
            var statusRaw = CsvImporter.Get(row, "status", "статус", "result", "результат").ToLowerInvariant();
            var status = statusRaw.Contains("suppress") || statusRaw.Contains("bounce") || statusRaw.Contains("недейств") ? "Suppressed" : "Sent";
            using var cmd = c.CreateCommand(); cmd.Transaction = tx;
            cmd.CommandText = @"INSERT INTO recipients(email,status,sent_utc) VALUES($e,$s,$u)
ON CONFLICT(email) DO UPDATE SET status=$s, sent_utc=CASE WHEN $s='Sent' THEN COALESCE(recipients.sent_utc,$u) ELSE recipients.sent_utc END";
            cmd.Parameters.AddWithValue("$e", email.Trim()); cmd.Parameters.AddWithValue("$s", status); cmd.Parameters.AddWithValue("$u", DateTime.UtcNow.ToString("O")); changed += cmd.ExecuteNonQuery();
        }
        tx.Commit(); AddEvent("History", "", $"Импорт истории: {changed}"); return changed;
    }

    public Recipient? NextRecipient(DateTime utcNow)
    {
        using var c = Open(); using var cmd = c.CreateCommand();
        cmd.CommandText = @"SELECT id,email,organization,contact_name,status,attempts FROM recipients
WHERE status='Pending' OR (status='Retry' AND (retry_at_utc IS NULL OR retry_at_utc='' OR retry_at_utc <= $now)) ORDER BY id LIMIT 1";
        cmd.Parameters.AddWithValue("$now", utcNow.ToString("O")); using var r = cmd.ExecuteReader(); if (!r.Read()) return null;
        return new Recipient { Id=r.GetInt64(0), Email=r.GetString(1), Organization=r.GetString(2), ContactName=r.GetString(3), Status=r.GetString(4), Attempts=r.GetInt32(5) };
    }

    public void MarkSent(Recipient r)
    {
        using var c = Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "UPDATE recipients SET status='Sent', attempts=attempts+1,last_attempt_utc=$u,sent_utc=$u,last_error=NULL,retry_at_utc=NULL WHERE id=$id"; cmd.Parameters.AddWithValue("$u",DateTime.UtcNow.ToString("O")); cmd.Parameters.AddWithValue("$id",r.Id); cmd.ExecuteNonQuery(); AddEvent("Sent", r.Email, "Письмо отправлено");
    }
    public void MarkSuppressed(Recipient r,string error)
    {
        using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="UPDATE recipients SET status='Suppressed',attempts=attempts+1,last_attempt_utc=$u,last_error=$e,retry_at_utc=NULL WHERE id=$id";cmd.Parameters.AddWithValue("$u",DateTime.UtcNow.ToString("O"));cmd.Parameters.AddWithValue("$e",error);cmd.Parameters.AddWithValue("$id",r.Id);cmd.ExecuteNonQuery();AddEvent("Suppressed",r.Email,error);
    }
    public void MarkRetry(Recipient r,string error,DateTime retryUtc)
    {
        using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="UPDATE recipients SET status='Retry',attempts=attempts+1,last_attempt_utc=$u,last_error=$e,retry_at_utc=$r WHERE id=$id";cmd.Parameters.AddWithValue("$u",DateTime.UtcNow.ToString("O"));cmd.Parameters.AddWithValue("$e",error);cmd.Parameters.AddWithValue("$r",retryUtc.ToString("O"));cmd.Parameters.AddWithValue("$id",r.Id);cmd.ExecuteNonQuery();AddEvent("Retry",r.Email,error);
    }

    public DashboardStats Stats()
    {
        using var c=Open();
        int Count(string where="") { using var cmd=c.CreateCommand();cmd.CommandText="SELECT COUNT(*) FROM recipients "+where;return Convert.ToInt32(cmd.ExecuteScalar()); }
        var localStart=DateTime.Today.ToUniversalTime().ToString("O");
        using var sCmd=c.CreateCommand();sCmd.CommandText="SELECT COUNT(*) FROM events WHERE type='Sent' AND utc >= $u";sCmd.Parameters.AddWithValue("$u",localStart);var sentToday=Convert.ToInt32(sCmd.ExecuteScalar());
        using var eCmd=c.CreateCommand();eCmd.CommandText="SELECT COUNT(*) FROM events WHERE type IN ('Retry','Suppressed','Error') AND utc >= $u";eCmd.Parameters.AddWithValue("$u",localStart);var errors=Convert.ToInt32(eCmd.ExecuteScalar());
        return new DashboardStats { Total=Count(), Sent=Count("WHERE status='Sent'"), Suppressed=Count("WHERE status='Suppressed'"), Remaining=Count("WHERE status IN ('Pending','Retry')"), SentToday=sentToday, ErrorsToday=errors };
    }

    public List<EventRow> RecentEvents(int take=50)
    {
        using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="SELECT utc,type,email,message FROM events ORDER BY id DESC LIMIT $n";cmd.Parameters.AddWithValue("$n",take);using var r=cmd.ExecuteReader();var list=new List<EventRow>();while(r.Read()){var utc=DateTime.Parse(r.GetString(0),null,DateTimeStyles.RoundtripKind);list.Add(new EventRow(utc.ToLocalTime(),r.GetString(1),r.GetString(2),r.GetString(3)));}return list;
    }
    public void AddEvent(string type,string email,string message)
    {
        using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="INSERT INTO events(utc,type,email,message) VALUES($u,$t,$e,$m)";cmd.Parameters.AddWithValue("$u",DateTime.UtcNow.ToString("O"));cmd.Parameters.AddWithValue("$t",type);cmd.Parameters.AddWithValue("$e",email??"");cmd.Parameters.AddWithValue("$m",message??"");cmd.ExecuteNonQuery();
    }
    public DateTime? LastSendUtc()
    {
        using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="SELECT MAX(sent_utc) FROM recipients WHERE sent_utc IS NOT NULL";var x=cmd.ExecuteScalar()?.ToString();return DateTime.TryParse(x,null,DateTimeStyles.RoundtripKind,out var d)?d:null;
    }
    public void Backup()
    {
        AppPaths.Ensure();var target=Path.Combine(AppPaths.Backups,$"mailer_{DateTime.Now:yyyyMMdd_HHmmss}.db");using var source=Open();using var dest=new SqliteConnection($"Data Source={target}");dest.Open();source.BackupDatabase(dest);AddEvent("Backup","",Path.GetFileName(target));
        var files=new DirectoryInfo(AppPaths.Backups).GetFiles("mailer_*.db").OrderByDescending(f=>f.CreationTimeUtc).Skip(20);foreach(var f in files){try{f.Delete();}catch{}}
    }
    public void ResetSafety() { SetMeta("SafetyStage","Control");SetMeta("StageAttempts","0");SetMeta("StagePermanentFailures","0");SetMeta("ThrottleLevel","0");SetMeta("CooldownUntilUtc",""); }
    public void Dispose() { }
}
