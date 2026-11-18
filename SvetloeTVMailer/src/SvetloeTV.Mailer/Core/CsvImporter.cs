using System.Net.Mail;
using System.Text;

namespace SvetloeTV.Mailer.Core;

public static class CsvImporter
{
    public static List<Dictionary<string,string>> Read(string path)
    {
        var lines=File.ReadAllLines(path,DetectEncoding(path));if(lines.Length==0)return new();var delim=GuessDelimiter(lines[0]);var headers=ParseLine(lines[0],delim).Select(Norm).ToArray();var result=new List<Dictionary<string,string>>();
        for(int i=1;i<lines.Length;i++){if(string.IsNullOrWhiteSpace(lines[i]))continue;var vals=ParseLine(lines[i],delim);var d=new Dictionary<string,string>();for(int j=0;j<headers.Length;j++)d[headers[j]]=j<vals.Count?vals[j].Trim():"";result.Add(d);}return result;
    }
    public static string FindEmail(Dictionary<string,string> row)
    {
        foreach(var k in new[]{"email","e-mail","почта","элпочта","электроннаяпочта","mail"})if(row.TryGetValue(Norm(k),out var v)&&IsEmail(v))return v.Trim();
        foreach(var v in row.Values)if(IsEmail(v))return v.Trim();return "";
    }
    public static string Get(Dictionary<string,string> row,params string[] names){foreach(var n in names)if(row.TryGetValue(Norm(n),out var v)&&!string.IsNullOrWhiteSpace(v))return v.Trim();return "";}
    private static bool IsEmail(string? s){try{if(string.IsNullOrWhiteSpace(s))return false;var m=new MailAddress(s.Trim());return m.Address.Equals(s.Trim(),StringComparison.OrdinalIgnoreCase);}catch{return false;}}
    private static char GuessDelimiter(string s){var counts=new Dictionary<char,int>{{';',s.Count(c=>c==';')},{',',s.Count(c=>c==',')},{'\t',s.Count(c=>c=='\t')}};return counts.OrderByDescending(x=>x.Value).First().Key;}
    private static List<string> ParseLine(string s,char d){var r=new List<string>();var b=new StringBuilder();bool q=false;for(int i=0;i<s.Length;i++){var c=s[i];if(c=='"'){if(q&&i+1<s.Length&&s[i+1]=='"'){b.Append('"');i++;}else q=!q;}else if(c==d&&!q){r.Add(b.ToString());b.Clear();}else b.Append(c);}r.Add(b.ToString());return r;}
    private static string Norm(string s)=>new string(s.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    private static Encoding DetectEncoding(string path){var bytes=File.ReadAllBytes(path).Take(4).ToArray();if(bytes.Length>=3&&bytes[0]==0xEF&&bytes[1]==0xBB&&bytes[2]==0xBF)return Encoding.UTF8;try{return Encoding.GetEncoding(1251);}catch{return Encoding.UTF8;}}
}
