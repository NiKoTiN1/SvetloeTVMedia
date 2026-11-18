namespace SvetloeTV.Mailer.Core;

public sealed class EngineStatusEventArgs : EventArgs
{
    public UiHealth Health { get; init; }
    public string Text { get; init; } = "";
}

public sealed class MailerEngine : IDisposable
{
    private readonly AppStore _store; private readonly SettingsStore _settingsStore; private AppSettings _settings; private readonly CancellationTokenSource _cts=new(); private Task? _loop; private DateTime _lastBackupUtc=DateTime.MinValue; private bool _busy;
    public event EventHandler<EngineStatusEventArgs>? StatusChanged; public event EventHandler? DataChanged;
    public MailerEngine(AppStore store,SettingsStore settingsStore,AppSettings settings){_store=store;_settingsStore=settingsStore;_settings=settings;}
    public void StartLoop(){_loop??=Task.Run(LoopAsync);}
    public void ReloadSettings()=>_settings=_settingsStore.Load();
    public void StartCampaign(){_store.SetMeta("RunState","Running");_store.AddEvent("State","","Рассылка запущена");Signal(UiHealth.Running,"Рассылка запущена");}
    public void PauseCampaign(string reason="Остановлено оператором"){_store.SetMeta("RunState","Paused");_store.AddEvent("State","",reason);Signal(UiHealth.Paused,reason);}
    public string RunState=>_store.GetMeta("RunState")??"Paused";

    private async Task LoopAsync()
    {
        await Task.Delay(1500,_cts.Token);
        while(!_cts.IsCancellationRequested)
        {
            try{await TickAsync(_cts.Token);}catch(OperationCanceledException){}catch(Exception ex){AppLog.Error("Engine",ex);_store.AddEvent("Error","",ex.Message);Signal(UiHealth.Error,"Внутренняя ошибка. Рассылка приостановлена.");_store.SetMeta("RunState","Paused");}
            await Task.Delay(3000,_cts.Token);
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        if(_busy)return;_busy=true;try
        {
            if((DateTime.UtcNow-_lastBackupUtc)>TimeSpan.FromHours(6)){try{_store.Backup();_lastBackupUtc=DateTime.UtcNow;}catch(Exception ex){AppLog.Error("Backup",ex);}}
            await MaybeSendDailyReport(ct);
            if(RunState!="Running"){Signal(UiHealth.Paused,"Рассылка на паузе");return;}
            var stats=_store.Stats();if(stats.Remaining==0){_store.SetMeta("RunState","Completed");Signal(UiHealth.Completed,"Рассылка завершена");return;}
            var now=DateTime.Now;if(now.Hour<_settings.WorkStartHour||now.Hour>=_settings.WorkEndHour){Signal(UiHealth.Waiting,$"Вне рабочего времени. Продолжим после {_settings.WorkStartHour:00}:00");return;}
            if(stats.SentToday>=_settings.DailyLimit){Signal(UiHealth.Waiting,"Дневной лимит достигнут. Продолжим завтра.");return;}
            if(DateTime.TryParse(_store.GetMeta("CooldownUntilUtc"),out var cool)&&cool>DateTime.UtcNow){Signal(UiHealth.Cooldown,$"Пауза сервера до {cool.ToLocalTime():HH:mm}");return;}
            var last=_store.LastSendUtc();if(last.HasValue&&DateTime.UtcNow-last.Value<TimeSpan.FromSeconds(Math.Max(20,_settings.IntervalSeconds))){Signal(UiHealth.Running,"Работаем. Ожидание следующей отправки.");return;}

            var stage=_store.GetMeta("SafetyStage")??"Control";
            if(stage=="Control"){await SendControlAsync(ct);return;}
            var r=_store.NextRecipient(DateTime.UtcNow);if(r==null){Signal(UiHealth.Waiting,"Нет адресов, готовых к отправке сейчас.");return;}
            await SendRecipientAsync(r,stage,ct);
        }
        finally{_busy=false;DataChanged?.Invoke(this,EventArgs.Empty);}
    }

    private async Task SendControlAsync(CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(_settings.ControlEmail)){PauseCampaign("Не указан контрольный e-mail");Signal(UiHealth.Error,"Нужна настройка контрольного e-mail");return;}
        Signal(UiHealth.Running,"Проверяем SMTP и отправляем контрольное письмо...");var o=await Sender().SendAsync(_settings.ControlEmail,"[КОНТРОЛЬ] "+_settings.Subject,_settings.Body,File.Exists(AppPaths.Presentation)?AppPaths.Presentation:null,ct);
        if(o.Kind==SendOutcomeKind.Sent){_store.AddEvent("Control",_settings.ControlEmail,"Контрольное письмо отправлено");_store.SetMeta("SafetyStage","Sample5");_store.SetMeta("StageAttempts","0");_store.SetMeta("StagePermanentFailures","0");Signal(UiHealth.Running,"Контроль SMTP пройден. Начинаем безопасный старт.");}
        else if(o.Kind==SendOutcomeKind.TemporaryFailure){ApplyCooldown(o,"Контрольный SMTP временно недоступен");}
        else{PauseCampaign("Контроль SMTP не пройден");_store.AddEvent("Error",_settings.ControlEmail,o.Message);Signal(UiHealth.Error,"SMTP не прошёл проверку. Нужен администратор.");}
    }

    private async Task SendRecipientAsync(Recipient r,string stage,CancellationToken ct)
    {
        Signal(UiHealth.Running,$"Отправка: {r.Email}");var o=await Sender().SendAsync(r.Email,_settings.Subject,Personalize(_settings.Body,r),File.Exists(AppPaths.Presentation)?AppPaths.Presentation:null,ct);
        switch(o.Kind)
        {
            case SendOutcomeKind.Sent:_store.MarkSent(r);AdvanceSafety(stage,false);_store.SetMeta("ThrottleLevel","0");break;
            case SendOutcomeKind.PermanentFailure:_store.MarkSuppressed(r,o.Message);AdvanceSafety(stage,true);break;
            case SendOutcomeKind.TemporaryFailure:
                var retry=r.Attempts>=2?DateTime.UtcNow.AddHours(24):DateTime.UtcNow.AddHours(1);_store.MarkRetry(r,o.Message,retry);if(o.SmtpCode is >=400 and <500)ApplyCooldown(o,"Почтовый сервер ограничил отправку");break;
            case SendOutcomeKind.AuthFailure:PauseCampaign("Ошибка авторизации SMTP");_store.AddEvent("Error",r.Email,o.Message);Signal(UiHealth.Error,"Почта отклонила авторизацию. Нужен администратор.");break;
            default:PauseCampaign("Ошибка конфигурации");_store.AddEvent("Error",r.Email,o.Message);Signal(UiHealth.Error,"Ошибка настроек. Нужен администратор.");break;
        }
    }

    private void AdvanceSafety(string stage,bool permanentFailure)
    {
        if(stage is not ("Sample5" or "Sample20"))return;var attempts=int.TryParse(_store.GetMeta("StageAttempts"),out var a)?a:0;var fails=int.TryParse(_store.GetMeta("StagePermanentFailures"),out var f)?f:0;attempts++;if(permanentFailure)fails++;_store.SetMeta("StageAttempts",attempts.ToString());_store.SetMeta("StagePermanentFailures",fails.ToString());var target=stage=="Sample5"?5:20;
        if(attempts<target)return;if(fails>=Math.Max(2,(int)Math.Ceiling(target*0.30))){PauseCampaign("Высокий процент недействительных адресов на проверочной партии");Signal(UiHealth.Error,"Слишком много недействительных адресов. Нужен администратор.");return;}
        _store.SetMeta("SafetyStage",stage=="Sample5"?"Sample20":"Normal");_store.SetMeta("StageAttempts","0");_store.SetMeta("StagePermanentFailures","0");_store.AddEvent("Safety","",stage=="Sample5"?"Проверка 5 завершена автоматически":"Проверка 20 завершена автоматически; обычный режим разрешён");
    }

    private void ApplyCooldown(SendOutcome o,string text)
    {
        var level=int.TryParse(_store.GetMeta("ThrottleLevel"),out var l)?Math.Min(l+1,5):1;_store.SetMeta("ThrottleLevel",level.ToString());var mins=Math.Min(360,15*(int)Math.Pow(2,level-1));var until=DateTime.UtcNow.AddMinutes(mins);_store.SetMeta("CooldownUntilUtc",until.ToString("O"));_store.AddEvent("Cooldown","",$"{text}: {mins} мин. {o.Message}");Signal(UiHealth.Cooldown,$"{text}. Продолжим автоматически через {mins} мин.");
    }
    private SmtpSender Sender()=>new(_settings);
    private static string Personalize(string body,Recipient r)=>body.Replace("{{organization}}",r.Organization??"").Replace("{{contact}}",r.ContactName??"");

    private async Task MaybeSendDailyReport(CancellationToken ct)
    {
        if(!_settings.SendDailyReport||string.IsNullOrWhiteSpace(_settings.ControlEmail))return;var now=DateTime.Now;if(now.Hour<_settings.DailyReportHour||(now.Hour==_settings.DailyReportHour&&now.Minute<_settings.DailyReportMinute))return;var key="DailyReportDate";if(_store.GetMeta(key)==now.ToString("yyyy-MM-dd"))return;var s=_store.Stats();var body=$"Ежедневный отчёт «Светлое ТВ — Рассылка»\r\n\r\nОтправлено сегодня: {s.SentToday}\r\nОшибок сегодня: {s.ErrorsToday}\r\nВсего отправлено: {s.Sent}\r\nИсключено: {s.Suppressed}\r\nОсталось: {s.Remaining}\r\nСтатус: {RunState}";var o=await Sender().SendAsync(_settings.ControlEmail,"Светлое ТВ — ежедневный отчёт",body,null,ct);if(o.Kind==SendOutcomeKind.Sent)_store.SetMeta(key,now.ToString("yyyy-MM-dd"));
    }
    private void Signal(UiHealth h,string t)=>StatusChanged?.Invoke(this,new EngineStatusEventArgs{Health=h,Text=t});
    public void Dispose(){_cts.Cancel();try{_loop?.Wait(1500);}catch{} _cts.Dispose();}
}
