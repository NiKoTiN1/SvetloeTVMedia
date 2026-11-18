using SvetloeTV.Mailer.Core;

namespace SvetloeTV.Mailer;

public sealed class SetupForm : Form
{
    private readonly AppStore _store; private readonly SettingsStore _settingsStore; private AppSettings _settings; private readonly bool _firstRun;
    private readonly TextBox host=new(){Width=200}, port=new(){Width=80}, user=new(){Width=260}, pass=new(){Width=260,UseSystemPasswordChar=true}, fromEmail=new(){Width=260}, fromName=new(){Width=260}, controlEmail=new(){Width=260};
    private readonly CheckBox ssl=new(){Text="SSL/TLS",AutoSize=true};
    private readonly TextBox subject=new(){Width=640}, body=new(){Width=640,Height=125,Multiline=true,ScrollBars=ScrollBars.Vertical};
    private readonly NumericUpDown interval=new(){Minimum=20,Maximum=3600,Value=90,Width=90}, daily=new(){Minimum=10,Maximum=5000,Value=200,Width=90}, startHour=new(){Minimum=0,Maximum=23,Value=9,Width=70}, endHour=new(){Minimum=1,Maximum=24,Value=18,Width=70};
    private readonly TextBox pdfPath=new(){Width=510,ReadOnly=true}, recipientsPath=new(){Width=510,ReadOnly=true}, historyPath=new(){Width=510,ReadOnly=true}, pin=new(){Width=160,UseSystemPasswordChar=true,MaxLength=12}, pin2=new(){Width=160,UseSystemPasswordChar=true,MaxLength=12};
    private readonly CheckBox autoStart=new(){Text="Запускать вместе с Windows",AutoSize=true}, dailyReport=new(){Text="Отправлять ежедневный отчёт на контрольный e-mail",AutoSize=true};
    private readonly Label info=new(){AutoSize=false,Width=680,Height=42,ForeColor=Color.DimGray};

    public SetupForm(AppStore store,SettingsStore settingsStore,AppSettings settings,bool firstRun=true)
    {
        _store=store;_settingsStore=settingsStore;_settings=settings;_firstRun=firstRun;Text=firstRun?"Светлое ТВ — первичная настройка":"Светлое ТВ — настройки";StartPosition=FormStartPosition.CenterParent;ClientSize=new Size(760,760);MinimumSize=new Size(760,760);Font=new Font("Segoe UI",9.5f);AutoScroll=true;
        Build();LoadValues();
    }

    private void Build()
    {
        int y=18;Controls.Add(Title("Почта (SMTP)",20,y));y+=38;
        AddRow("Сервер",host,ref y);AddRow("Порт",port,ref y);ssl.Location=new Point(575,y-34);Controls.Add(ssl);
        AddRow("Логин",user,ref y);AddRow("Пароль приложения",pass,ref y);AddRow("E-mail отправителя",fromEmail,ref y);AddRow("Имя отправителя",fromName,ref y);AddRow("Контрольный e-mail",controlEmail,ref y);
        var test=new Button{Text="Проверить SMTP",Location=new Point(575,y-36),Size=new Size(140,30)};test.Click+=async(_,_)=>await TestSmtp();Controls.Add(test);
        Controls.Add(Title("Письмо",20,y));y+=38;AddRow("Тема",subject,ref y);body.Location=new Point(160,y);Controls.Add(new Label{Text="Текст",Location=new Point(20,y+3),AutoSize=true});Controls.Add(body);y+=138;
        AddFileRow("Презентация PDF",pdfPath,"PDF (*.pdf)|*.pdf",ref y);
        Controls.Add(Title("База адресов",20,y));y+=38;AddFileRow("Получатели CSV",recipientsPath,"CSV (*.csv;*.txt)|*.csv;*.txt|Все файлы|*.*",ref y);AddFileRow("История CSV (необяз.)",historyPath,"CSV (*.csv;*.txt)|*.csv;*.txt|Все файлы|*.*",ref y);
        Controls.Add(Title("Безопасность и скорость",20,y));y+=38;
        AddInline("Интервал, сек.",interval,20,y);AddInline("Лимит в сутки",daily,245,y);AddInline("Работа с",startHour,470,y);var to=new Label{Text="до",Location=new Point(650,y+5),AutoSize=true};endHour.Location=new Point(680,y);Controls.AddRange(new Control[]{to,endHour});y+=42;
        autoStart.Location=new Point(20,y);dailyReport.Location=new Point(245,y);Controls.AddRange(new Control[]{autoStart,dailyReport});y+=38;
        Controls.Add(new Label{Text=_firstRun?"PIN администратора":"Новый PIN (оставьте пустым, чтобы не менять)",Location=new Point(20,y+4),AutoSize=true});pin.Location=new Point(245,y);Controls.Add(pin);Controls.Add(new Label{Text="Повтор",Location=new Point(420,y+4),AutoSize=true});pin2.Location=new Point(490,y);Controls.Add(pin2);y+=48;
        info.Location=new Point(20,y);Controls.Add(info);y+=55;
        var save=new Button{Text=_firstRun?"Сохранить и открыть программу":"Сохранить",Location=new Point(450,y),Size=new Size(190,38)};var cancel=new Button{Text="Отмена",Location=new Point(645,y),Size=new Size(90,38),DialogResult=DialogResult.Cancel};save.Click+=(_,_)=>SaveAll();Controls.AddRange(new Control[]{save,cancel});AcceptButton=save;CancelButton=cancel;
    }
    private static Label Title(string t,int x,int y)=>new(){Text=t,Location=new Point(x,y),AutoSize=true,Font=new Font("Segoe UI Semibold",12)};
    private void AddRow(string label,Control c,ref int y){Controls.Add(new Label{Text=label,Location=new Point(20,y+5),AutoSize=true});c.Location=new Point(160,y);Controls.Add(c);y+=38;}
    private void AddInline(string label,Control c,int x,int y){Controls.Add(new Label{Text=label,Location=new Point(x,y+5),AutoSize=true});c.Location=new Point(x+115,y);Controls.Add(c);}
    private void AddFileRow(string label,TextBox box,string filter,ref int y){Controls.Add(new Label{Text=label,Location=new Point(20,y+5),AutoSize=true});box.Location=new Point(160,y);Controls.Add(box);var b=new Button{Text="Выбрать...",Location=new Point(680,y-1),Size=new Size(70,28)};b.Click+=(_,_)=>{using var d=new OpenFileDialog{Filter=filter};if(d.ShowDialog(this)==DialogResult.OK)box.Text=d.FileName;};Controls.Add(b);y+=38;}
    private void LoadValues(){host.Text=_settings.SmtpHost;port.Text=_settings.SmtpPort.ToString();ssl.Checked=_settings.SmtpSsl;user.Text=_settings.SmtpUser;fromEmail.Text=_settings.FromEmail;fromName.Text=_settings.FromName;controlEmail.Text=_settings.ControlEmail;subject.Text=_settings.Subject;body.Text=_settings.Body;interval.Value=Math.Clamp(_settings.IntervalSeconds,(int)interval.Minimum,(int)interval.Maximum);daily.Value=Math.Clamp(_settings.DailyLimit,(int)daily.Minimum,(int)daily.Maximum);startHour.Value=Math.Clamp(_settings.WorkStartHour,0,23);endHour.Value=Math.Clamp(_settings.WorkEndHour,1,24);autoStart.Checked=_settings.AutoStartWithWindows;dailyReport.Checked=_settings.SendDailyReport;if(File.Exists(AppPaths.Presentation))pdfPath.Text=AppPaths.Presentation;}
    private bool FillSettings(bool includeSecrets=true)
    {
        if(string.IsNullOrWhiteSpace(host.Text)||!int.TryParse(port.Text,out var p)||p<1||p>65535||string.IsNullOrWhiteSpace(user.Text)||string.IsNullOrWhiteSpace(fromEmail.Text)||string.IsNullOrWhiteSpace(controlEmail.Text)){MessageBox.Show("Заполните сервер, порт, логин, e-mail отправителя и контрольный e-mail.",Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);return false;}
        _settings.SmtpHost=host.Text.Trim();_settings.SmtpPort=p;_settings.SmtpSsl=ssl.Checked;_settings.SmtpUser=user.Text.Trim();_settings.FromEmail=fromEmail.Text.Trim();_settings.FromName=fromName.Text.Trim();_settings.ControlEmail=controlEmail.Text.Trim();_settings.Subject=subject.Text.Trim();_settings.Body=body.Text;_settings.IntervalSeconds=(int)interval.Value;_settings.DailyLimit=(int)daily.Value;_settings.WorkStartHour=(int)startHour.Value;_settings.WorkEndHour=(int)endHour.Value;_settings.AutoStartWithWindows=autoStart.Checked;_settings.SendDailyReport=dailyReport.Checked;
        if(includeSecrets&&!string.IsNullOrWhiteSpace(pass.Text))SettingsStore.SetPassword(_settings,pass.Text);
        return true;
    }
    private async Task TestSmtp()
    {
        if(!FillSettings())return;if(string.IsNullOrWhiteSpace(pass.Text)&&string.IsNullOrWhiteSpace(_settings.SmtpPasswordProtected)){MessageBox.Show("Введите пароль приложения.",Text);return;}Cursor=Cursors.WaitCursor;info.Text="Проверяем SMTP...";try{var s=new SmtpSender(_settings);var attach=!string.IsNullOrWhiteSpace(pdfPath.Text)&&File.Exists(pdfPath.Text)?pdfPath.Text:null;var o=await s.SendAsync(_settings.ControlEmail,"[ПРОВЕРКА SMTP] "+_settings.Subject,"Проверка настроек SMTP программы «Светлое ТВ — Рассылка».",attach,CancellationToken.None);info.Text=o.Kind==SendOutcomeKind.Sent?"SMTP работает. Контрольное письмо отправлено.":$"Ошибка SMTP: {o.Message}";info.ForeColor=o.Kind==SendOutcomeKind.Sent?Color.DarkGreen:Color.Firebrick;}finally{Cursor=Cursors.Default;}}
    private void SaveAll()
    {
        if(!FillSettings())return;if(string.IsNullOrWhiteSpace(_settings.SmtpPasswordProtected)){MessageBox.Show("Введите пароль приложения SMTP.",Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
        if(_firstRun){if(pin.Text.Length<4||pin.Text!=pin2.Text){MessageBox.Show("Задайте одинаковый PIN администратора длиной не менее 4 символов.",Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}SettingsStore.SetPin(_settings,pin.Text);}else if(!string.IsNullOrEmpty(pin.Text)){if(pin.Text.Length<4||pin.Text!=pin2.Text){MessageBox.Show("Новый PIN и повтор не совпадают или слишком короткие.",Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}SettingsStore.SetPin(_settings,pin.Text);}
        try
        {
            if(!string.IsNullOrWhiteSpace(pdfPath.Text)&&File.Exists(pdfPath.Text)&&!Path.GetFullPath(pdfPath.Text).Equals(Path.GetFullPath(AppPaths.Presentation),StringComparison.OrdinalIgnoreCase))File.Copy(pdfPath.Text,AppPaths.Presentation,true);
            if(!string.IsNullOrWhiteSpace(recipientsPath.Text))_store.ImportRecipients(recipientsPath.Text);if(!string.IsNullOrWhiteSpace(historyPath.Text))_store.ImportHistory(historyPath.Text);
            var stats=_store.Stats();if(stats.Total==0){MessageBox.Show("Импортируйте CSV с получателями.",Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}if(!File.Exists(AppPaths.Presentation)){MessageBox.Show("Выберите PDF-презентацию.",Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
            _settings.Configured=true;_settingsStore.Save(_settings);_store.ResetSafety();_store.Backup();DialogResult=DialogResult.OK;Close();
        }
        catch(Exception ex){AppLog.Error("Setup.Save",ex);MessageBox.Show("Не удалось сохранить настройки: "+ex.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Error);}
    }
}
