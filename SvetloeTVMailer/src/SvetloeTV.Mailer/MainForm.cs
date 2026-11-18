using System.Diagnostics;
using SvetloeTV.Mailer.Core;

namespace SvetloeTV.Mailer;

public sealed class MainForm : Form
{
    private readonly AppStore _store;private readonly SettingsStore _settingsStore;private AppSettings _settings;private readonly MailerEngine _engine;private readonly System.Windows.Forms.Timer _timer=new(){Interval=2000};private readonly NotifyIcon _tray;private bool _realExit;
    private readonly Panel statusPanel=new(){Height=92,Dock=DockStyle.Top,Padding=new Padding(22)};private readonly Label statusTitle=new(){AutoSize=true,Font=new Font("Segoe UI Semibold",18)};private readonly Label statusText=new(){AutoSize=true,Font=new Font("Segoe UI",10)};
    private readonly Label totalV=StatValue(),sentV=StatValue(),remainingV=StatValue(),suppressedV=StatValue(),todayV=StatValue(),errorsV=StatValue();private readonly ProgressBar progress=new(){Height=16,Dock=DockStyle.Top};private readonly DataGridView grid=new(){Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Color.White,BorderStyle=BorderStyle.FixedSingle};
    private readonly Button start=new(){Text="▶ НАЧАТЬ РАССЫЛКУ",Height=44,Width=210},pause=new(){Text="⏸ ПАУЗА",Height=44,Width=120},settings=new(){Text="⚙ Настройки",Height=36,Width=120},backup=new(){Text="Резервная копия",Height=36,Width=145},logs=new(){Text="Журнал",Height=36,Width=100};

    public MainForm(AppStore store,SettingsStore settingsStore,AppSettings settingsModel)
    {
        _store=store;_settingsStore=settingsStore;_settings=settingsModel;Text="Светлое ТВ — Рассылка";StartPosition=FormStartPosition.CenterScreen;MinimumSize=new Size(880,620);ClientSize=new Size(980,680);Font=new Font("Segoe UI",10);
        Build();_engine=new MailerEngine(store,settingsStore,settingsModel);_engine.StatusChanged+=Engine_StatusChanged;_engine.DataChanged+=(_,_)=>BeginInvoke(new Action(RefreshData));_engine.StartLoop();
        _timer.Tick+=(_,_)=>RefreshData();_timer.Start();_tray=BuildTray();FormClosing+=OnClosing;Resize+=(_,_)=>{if(WindowState==FormWindowState.Minimized)HideToTray();};Shown+=(_,_)=>{RefreshData();if(Environment.GetCommandLineArgs().Any(a=>a.Equals("--autostart",StringComparison.OrdinalIgnoreCase)))BeginInvoke(new Action(HideToTray));};
    }
    private void Build()
    {
        statusPanel.Controls.Add(statusTitle);statusPanel.Controls.Add(statusText);statusTitle.Location=new Point(22,16);statusText.Location=new Point(24,55);Controls.Add(statusPanel);Controls.Add(progress);
        var stats=new TableLayoutPanel{Dock=DockStyle.Top,Height=122,ColumnCount=6,RowCount=2,Padding=new Padding(18,12,18,10)};for(int i=0;i<6;i++)stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,16.66f));var labels=new[]{"Всего","Отправлено","Осталось","Исключено","Сегодня","Ошибок сегодня"};var values=new[]{totalV,sentV,remainingV,suppressedV,todayV,errorsV};for(int i=0;i<6;i++){stats.Controls.Add(new Label{Text=labels[i],Dock=DockStyle.Fill,TextAlign=ContentAlignment.BottomCenter,ForeColor=Color.DimGray},i,0);values[i].Dock=DockStyle.Fill;values[i].TextAlign=ContentAlignment.TopCenter;stats.Controls.Add(values[i],i,1);}Controls.Add(stats);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Top,Height=64,Padding=new Padding(18,8,18,8),FlowDirection=FlowDirection.LeftToRight};actions.Controls.AddRange(new Control[]{start,pause,settings,backup,logs});Controls.Add(actions);
        start.Click+=(_,_)=>_engine.StartCampaign();pause.Click+=(_,_)=>_engine.PauseCampaign();settings.Click+=(_,_)=>OpenSettings();backup.Click+=(_,_)=>{try{_store.Backup();MessageBox.Show("Резервная копия создана.",Text);}catch(Exception ex){MessageBox.Show(ex.Message,Text);}};logs.Click+=(_,_)=>Process.Start(new ProcessStartInfo{FileName=AppPaths.Logs,UseShellExecute=true});
        var caption=new Label{Text="Последние события",Dock=DockStyle.Top,Height=34,Padding=new Padding(18,8,0,0),Font=new Font("Segoe UI Semibold",10)};Controls.Add(caption);grid.Columns.Add("time","Время");grid.Columns.Add("type","Событие");grid.Columns.Add("email","E-mail");grid.Columns.Add("message","Описание");grid.Columns[0].FillWeight=18;grid.Columns[1].FillWeight=18;grid.Columns[2].FillWeight=28;grid.Columns[3].FillWeight=70;Controls.Add(grid);
    }
    private static Label StatValue()=>new(){Text="0",Font=new Font("Segoe UI Semibold",18),AutoSize=false};
    private NotifyIcon BuildTray(){var menu=new ContextMenuStrip();menu.Items.Add("Открыть",null,(_,_)=>ShowFromTray());menu.Items.Add("Начать",null,(_,_)=>_engine.StartCampaign());menu.Items.Add("Пауза",null,(_,_)=>_engine.PauseCampaign());menu.Items.Add(new ToolStripSeparator());menu.Items.Add("Выход (PIN)",null,(_,_)=>ExitWithPin());var n=new NotifyIcon{Text="Светлое ТВ — Рассылка",Visible=true,Icon=SystemIcons.Application,ContextMenuStrip=menu};n.DoubleClick+=(_,_)=>ShowFromTray();return n;}
    private void RefreshData()
    {
        if(IsDisposed)return;var s=_store.Stats();totalV.Text=s.Total.ToString("N0");sentV.Text=s.Sent.ToString("N0");remainingV.Text=s.Remaining.ToString("N0");suppressedV.Text=s.Suppressed.ToString("N0");todayV.Text=s.SentToday.ToString("N0");errorsV.Text=s.ErrorsToday.ToString("N0");progress.Maximum=Math.Max(1,s.Total);progress.Value=Math.Min(progress.Maximum,s.Sent+s.Suppressed);start.Enabled=_engine.RunState!="Running"&&s.Remaining>0;pause.Enabled=_engine.RunState=="Running";
        grid.Rows.Clear();foreach(var e in _store.RecentEvents(40))grid.Rows.Add(e.LocalTime.ToString("dd.MM HH:mm:ss"),HumanType(e.Type),e.Email,e.Message);
        if(_engine.RunState=="Completed")SetStatus(UiHealth.Completed,"Рассылка завершена","Все доступные адреса обработаны.");else if(_engine.RunState!="Running")SetStatus(UiHealth.Paused,"Рассылка на паузе","Нажмите «Начать рассылку», когда нужно продолжить.");
    }
    private static string HumanType(string t)=>t switch{"Sent"=>"Отправлено","Suppressed"=>"Исключено","Retry"=>"Повтор позже","Cooldown"=>"Пауза сервера","Control"=>"Контроль","Safety"=>"Проверка","State"=>"Статус","Error"=>"Ошибка","Import"=>"Импорт","History"=>"История","Backup"=>"Копия",_=>t};
    private void Engine_StatusChanged(object? sender,EngineStatusEventArgs e){if(IsDisposed)return;BeginInvoke(new Action(()=>SetStatus(e.Health,TitleFor(e.Health),e.Text)));}
    private static string TitleFor(UiHealth h)=>h switch{UiHealth.Running=>"● РАБОТАЕТ",UiHealth.Cooldown=>"● ВРЕМЕННАЯ ПАУЗА",UiHealth.Waiting=>"● ОЖИДАНИЕ",UiHealth.Error=>"● НУЖЕН АДМИНИСТРАТОР",UiHealth.Completed=>"● ЗАВЕРШЕНО",_=>"● НА ПАУЗЕ"};
    private void SetStatus(UiHealth h,string title,string text){statusTitle.Text=title;statusText.Text=text;statusPanel.BackColor=h switch{UiHealth.Running=>Color.FromArgb(226,244,232),UiHealth.Cooldown or UiHealth.Waiting=>Color.FromArgb(255,246,214),UiHealth.Error=>Color.FromArgb(255,228,228),UiHealth.Completed=>Color.FromArgb(225,237,255),_=>Color.FromArgb(238,240,243)};statusTitle.ForeColor=h switch{UiHealth.Running=>Color.DarkGreen,UiHealth.Error=>Color.Firebrick,UiHealth.Cooldown or UiHealth.Waiting=>Color.DarkGoldenrod,_=>Color.FromArgb(45,55,65)};}
    private void OpenSettings(){using var p=new PinDialog();if(p.ShowDialog(this)!=DialogResult.OK)return;if(!SettingsStore.VerifyPin(_settings,p.Pin)){MessageBox.Show("Неверный PIN.",Text,MessageBoxButtons.OK,MessageBoxIcon.Error);return;}using var f=new SetupForm(_store,_settingsStore,_settings,false);if(f.ShowDialog(this)==DialogResult.OK){_settings=_settingsStore.Load();_engine.ReloadSettings();RefreshData();}}
    private void ExitWithPin(){using var p=new PinDialog("Завершение программы");if(p.ShowDialog(this)!=DialogResult.OK)return;if(!SettingsStore.VerifyPin(_settings,p.Pin)){MessageBox.Show("Неверный PIN.",Text);return;}if(MessageBox.Show("Завершить программу? Если рассылка запущена, после следующего запуска она продолжится автоматически.",Text,MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;_realExit=true;Close();}
    private void OnClosing(object? sender,FormClosingEventArgs e){if(!_realExit&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;HideToTray();return;}_timer.Stop();_tray.Visible=false;_engine.Dispose();}
    private void HideToTray(){Hide();_tray.ShowBalloonTip(1500,"Светлое ТВ — Рассылка","Программа продолжает работать в фоне.",ToolTipIcon.Info);}
    private void ShowFromTray(){Show();WindowState=FormWindowState.Normal;Activate();}
}
