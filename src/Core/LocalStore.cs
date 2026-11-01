using System.Text.Json;
namespace AnimeReserve;
public sealed class Draft {public RulePlan Plan {get;set;}public uint RegisteredId {get;set;}public string Result {get;set;}="未送信";}
public sealed class Preferences {public string EdcbFolder {get;set;}="";public string DefaultBat {get;set;}="";public List<long> DefaultServices {get;set;}=[];public List<Draft> Drafts {get;set;}=[];}
public static class LocalStore {
 public static Anime ManualAnime(string title,IEnumerable<int> existing){title=(title??"").Trim();if(title.Length==0||title.Length>500)throw new ArgumentException("作品名を1～500文字で入力してください。");int id=checked(Math.Min(0,existing.DefaultIfEmpty(0).Min())-1);return new Anime(id,title,"","","MANUAL","");}
 public static readonly JsonSerializerOptions Json=new(){IncludeFields=true,WriteIndented=true};
 public static Preferences Load(string folder){string file=Path.Combine(folder,"settings.json");if(!File.Exists(file))return new();try{return JsonSerializer.Deserialize<Preferences>(File.ReadAllText(file),Json)??new();}catch(Exception e){throw new IOException("設定ファイルを読み込めません。元のファイルを確認してください。",e);}}
 public static void Save(string folder,Preferences value){Directory.CreateDirectory(folder);string f=Path.Combine(folder,"settings.json"),t=f+".tmp-"+Guid.NewGuid().ToString("N");File.WriteAllText(t,JsonSerializer.Serialize(value,Json));File.Move(t,f,true);}
}
