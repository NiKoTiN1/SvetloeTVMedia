using SvetloeTV.Mailer.Core;

namespace SvetloeTV.Mailer;

public sealed class PinDialog : Form
{
    private readonly TextBox _pin = new() { UseSystemPasswordChar = true, Width = 180, MaxLength = 12 };
    public string Pin => _pin.Text;
    public PinDialog(string title="PIN администратора")
    {
        Text=title;StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ClientSize=new Size(360,150);Font=new Font("Segoe UI",10);
        var label=new Label{Text="Введите PIN администратора:",AutoSize=true,Location=new Point(28,25)};_pin.Location=new Point(30,55);
        var ok=new Button{Text="ОК",DialogResult=DialogResult.OK,Location=new Point(205,100),Size=new Size(65,30)};var cancel=new Button{Text="Отмена",DialogResult=DialogResult.Cancel,Location=new Point(275,100),Size=new Size(70,30)};
        Controls.AddRange(new Control[]{label,_pin,ok,cancel});AcceptButton=ok;CancelButton=cancel;Shown+=(_,_)=>_pin.Focus();
    }
}
