namespace SvetloeTV.Mailer.Core;

public enum RecipientStatus { Pending, Sent, Suppressed, Retry }
public enum SendOutcomeKind { Sent, PermanentFailure, TemporaryFailure, AuthFailure, ConfigFailure }
public enum UiHealth { Ready, Running, Paused, Waiting, Cooldown, Error, Completed, SetupRequired }

public sealed class Recipient
{
    public long Id { get; set; }
    public string Email { get; set; } = "";
    public string Organization { get; set; } = "";
    public string ContactName { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public int Attempts { get; set; }
}

public sealed class DashboardStats
{
    public int Total { get; set; }
    public int Sent { get; set; }
    public int Suppressed { get; set; }
    public int Remaining { get; set; }
    public int ErrorsToday { get; set; }
    public int SentToday { get; set; }
}

public sealed record SendOutcome(SendOutcomeKind Kind, string Message, int? SmtpCode = null);
public sealed record EventRow(DateTime LocalTime, string Type, string Email, string Message);

public sealed class AppSettings
{
    public bool Configured { get; set; }
    public string SmtpHost { get; set; } = "smtp.mail.ru";
    public int SmtpPort { get; set; } = 465;
    public bool SmtpSsl { get; set; } = true;
    public string SmtpUser { get; set; } = "";
    public string SmtpPasswordProtected { get; set; } = "";
    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "Светлое ТВ";
    public string ControlEmail { get; set; } = "";
    public string Subject { get; set; } = "Телеканал «Светлое ТВ» — предложение для оператора";
    public string Body { get; set; } = "Добрый день!\r\n\r\nНаправляем предложение телеканала «Светлое ТВ». Подробная информация находится в приложенной презентации.\r\n\r\nС уважением,\r\nкоманда «Светлое ТВ».";
    public int IntervalSeconds { get; set; } = 90;
    public int DailyLimit { get; set; } = 200;
    public int WorkStartHour { get; set; } = 9;
    public int WorkEndHour { get; set; } = 18;
    public bool AutoStartWithWindows { get; set; } = true;
    public bool ResumeAfterRestart { get; set; } = true;
    public bool SendDailyReport { get; set; } = true;
    public int DailyReportHour { get; set; } = 18;
    public int DailyReportMinute { get; set; } = 15;
    public string AdminPinSalt { get; set; } = "";
    public string AdminPinHash { get; set; } = "";
}
