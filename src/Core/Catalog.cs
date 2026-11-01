using System.Net.Http.Json;
using System.Text.Json;
namespace AnimeReserve;
public record Anime(int Id,string Title,string Romaji,string Url,string Format,string Status);
public record CatalogResult(List<Anime> Items,DateTimeOffset Updated,bool Cached,string Notice);
public sealed class Catalog {
 readonly HttpClient http;readonly string data;
 public Catalog(string data,HttpClient http=null){this.data=data;this.http=http??new HttpClient {Timeout=TimeSpan.FromSeconds(30)};this.http.DefaultRequestHeaders.UserAgent.ParseAdd("AnimeReserveFlow/0.1.3");}
 public static string Season(int quarter)=>quarter switch {1=>"WINTER",2=>"SPRING",3=>"SUMMER",4=>"FALL",_=>throw new ArgumentException("季節は1～4です。")};
 public async Task<CatalogResult> Fetch(int year,int quarter,bool force=false,CancellationToken token=default){
  if(year<2000||year>2100)throw new ArgumentException("年は2000～2100を指定してください。");string season=Season(quarter);Directory.CreateDirectory(data);string file=Path.Combine(data,$"catalog-{year}-{quarter}.json");
  var cached=File.Exists(file)?JsonSerializer.Deserialize<CatalogResult>(await File.ReadAllTextAsync(file,token)):null;
  if(!force&&cached!=null&&DateTimeOffset.UtcNow-cached.Updated<TimeSpan.FromHours(6))return cached with {Cached=true};
  try {
   List<Anime> items=[];for(int page=1;page<=20;page++){
    using var response=await http.PostAsJsonAsync("https://graphql.anilist.co",new {query="query($year:Int!,$season:MediaSeason!,$page:Int!){Page(page:$page,perPage:50){pageInfo{hasNextPage}media(type:ANIME,seasonYear:$year,season:$season,format_in:[TV,TV_SHORT],sort:POPULARITY_DESC){id title{native romaji}siteUrl format status}}}",variables=new {year,season,page}},token);
    if((int)response.StatusCode==429)throw new IOException("作品情報APIの取得上限です。時間を置いて再取得してください。");response.EnsureSuccessStatusCode();using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
    if(json.RootElement.TryGetProperty("errors",out _))throw new IOException("作品情報APIがエラーを返しました。");var p=json.RootElement.GetProperty("data").GetProperty("Page");
    foreach(var m in p.GetProperty("media").EnumerateArray()){var t=m.GetProperty("title");string native=t.GetProperty("native").GetString(),romaji=t.GetProperty("romaji").GetString();items.Add(new(m.GetProperty("id").GetInt32(),native??romaji??"名称未登録",romaji??"",m.GetProperty("siteUrl").GetString(),m.GetProperty("format").GetString(),m.GetProperty("status").GetString()));}
    if(!p.GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean())break;if(page==20)throw new IOException("作品一覧が大きすぎます。");await Task.Delay(2200,token);
   }
   var result=new CatalogResult(items.DistinctBy(x=>x.Id).ToList(),DateTimeOffset.UtcNow,false,"");string temp=file+".tmp-"+Guid.NewGuid().ToString("N");await File.WriteAllTextAsync(temp,JsonSerializer.Serialize(result),token);File.Move(temp,file,true);return result;
  }catch(Exception e) when(cached!=null&&e is not OperationCanceledException){return cached with {Cached=true,Notice="取得できないため保存済み一覧を表示："+e.Message};}
 }
}
