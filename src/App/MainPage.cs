using System.Diagnostics;
using EpgTimer;
namespace AnimeReserve.App;
public sealed class MainPage:ContentPage {
 readonly string data;readonly Preferences settings;readonly Edcb edcb=new();readonly Catalog catalog;
 readonly VerticalStackLayout body=new(){Spacing=16};readonly Label note=new(){TextColor=Color.FromArgb("#526578")};
 Entry year=new(){WidthRequest=100,Keyboard=Keyboard.Numeric};Picker quarter=new(){WidthRequest=160,ItemsSource=new[]{"冬（1～3月）","春（4～6月）","夏（7～9月）","秋（10～12月）"}};
 Entry filter=new(){Placeholder="作品名で絞り込み"};List<Anime> anime=[];List<EpgServiceInfo> services=[];List<RecordingPreset> presets=[];bool busy;string section="season";
 static Label Text(string s,int size=15)=>new(){Text=s,FontSize=size,LineBreakMode=LineBreakMode.WordWrap};
 Button Btn(string label,Func<Task> action){var b=new Button {Text=label};b.Clicked+=async(_,_)=>{if(busy)return;busy=true;b.IsEnabled=false;try{await action();}catch(Exception e){note.Text=e.Message;await DisplayAlert("確認してください",e.Message,"閉じる");}finally{busy=false;b.IsEnabled=true;}};return b;}
 static VerticalStackLayout Stack(params View[] views){var s=new VerticalStackLayout {Spacing=12};foreach(var v in views)s.Add(v);return s;}
 static HorizontalStackLayout Row(params View[] views){var s=new HorizontalStackLayout {Spacing=12};foreach(var v in views)s.Add(v);return s;}
 static Border Card(View v)=>new(){Content=v,Padding=20,BackgroundColor=Colors.White,Stroke=Colors.Transparent,StrokeShape=new Microsoft.Maui.Controls.Shapes.RoundRectangle {CornerRadius=24}};
 void Save()=>LocalStore.Save(data,settings);
 public MainPage(){
  data=Environment.GetCommandLineArgs().Contains("--qa")?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Anime-Reserve-Flow","QA"):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Anime-Reserve-Flow");settings=LocalStore.Load(data);catalog=new Catalog(Path.Combine(data,"Cache"));
  if(settings.EdcbFolder.Length==0)try{settings.EdcbFolder=Path.GetDirectoryName(Process.GetProcessesByName("EpgTimerSrv").FirstOrDefault()?.MainModule?.FileName)??"";}catch{}
  Title="Anime Reserve Flow";Padding=28;year.Text=DateTime.Now.Year.ToString();quarter.SelectedIndex=(DateTime.Now.Month-1)/3;
  var grid=new Grid {RowDefinitions=new RowDefinitionCollection {new(GridLength.Auto),new(GridLength.Auto),new(GridLength.Star),new(GridLength.Auto)},RowSpacing=14};
  grid.Add(Stack(Text("今季のアニメを、毎話の録画へ。",30),Text("作品を選ぶ → 検索ワード・放送局・録画後BATを確認 → EPGTimerへ登録",14)),0,0);
  grid.Add(Row(Btn("季節のアニメ",()=>{section="season";Show();return Task.CompletedTask;}),Btn("選択した作品",()=>{section="drafts";Show();return Task.CompletedTask;}),Btn("接続・録画設定",()=>{section="settings";Show();return Task.CompletedTask;})),0,1);
  grid.Add(new ScrollView {Content=body},0,2);grid.Add(Stack(note,Row(Btn("AniList（作品情報）",()=>Launcher.Default.OpenAsync("https://anilist.co").ContinueWith(_=>{})),Btn("Ko-fiで開発を支援",()=>Launcher.Default.OpenAsync("https://ko-fi.com/long0823").ContinueWith(_=>{})))),0,3);Content=grid;
  Loaded+=async(_,_)=>{if(anime.Count>0)return;try{await Fetch(false);}catch(Exception e){note.Text=e.Message;Show();}};Show();
 }
 async Task Fetch(bool force){if(!int.TryParse(year.Text,out int y))throw new ArgumentException("年を入力してください。");note.Text="作品一覧を取得しています…";var result=await catalog.Fetch(y,quarter.SelectedIndex+1,force);anime=result.Items;note.Text=$"{y}年 {quarter.SelectedItem}：{anime.Count}作品 / {(result.Cached?"保存済み":"取得完了")} {result.Updated.ToLocalTime():MM/dd HH:mm} "+result.Notice;Show();}
 void Show(){body.Clear();if(section=="season")ShowSeason();else if(section=="drafts")ShowDrafts();else ShowSettings();}
 void ShowSeason(){
  string previousYear=year.Text??DateTime.Now.Year.ToString(),previousFilter=filter.Text??"";int previousQuarter=quarter.SelectedIndex;
  // Fresh controls prevent reusing WinUI handlers disposed when another section was shown.
  year=new Entry {Text=previousYear,WidthRequest=100,Keyboard=Keyboard.Numeric};quarter=new Picker {WidthRequest=160,ItemsSource=new[]{"冬（1～3月）","春（4～6月）","夏（7～9月）","秋（10～12月）"},SelectedIndex=previousQuarter};filter=new Entry {Text=previousFilter,Placeholder="作品名で絞り込み",WidthRequest=320};filter.Completed+=(_,_)=>ShowSeason();
  body.Clear();body.Add(Card(Stack(Row(year,quarter,Btn("一覧を取得",()=>Fetch(true)),Btn("一覧になくても手入力で追加",AddManual)),Row(filter,Btn("絞り込み",()=>{ShowSeason();return Task.CompletedTask;})),Text("TV・短編TVアニメの放送開始クールを表示します。地域の放送日時はEDCBの番組表で確認します。",13))));
  var rows=anime.Where(a=>a.Title.Contains(filter.Text??"",StringComparison.OrdinalIgnoreCase)||a.Romaji.Contains(filter.Text??"",StringComparison.OrdinalIgnoreCase)).ToList();
  foreach(var a in rows){var existing=settings.Drafts.FirstOrDefault(d=>d.Plan.AnimeId==a.Id);body.Add(Card(Stack(Text(a.Title,20),Text(a.Romaji,12),Row(Btn(existing==null?"録画したい作品に追加":"選択内容を確認",()=>Edit(a,existing)),Btn("作品情報を開く",async()=>{if(Uri.TryCreate(a.Url,UriKind.Absolute,out var u)&&u.Scheme=="https"&&u.Host=="anilist.co")await Launcher.Default.OpenAsync(u);})))));}
  if(rows.Count==0)body.Add(Text("一致する作品がありません。「一覧になくても手入力で追加」から登録できます。"));
 }
 void ShowDrafts(){
  body.Add(Text("番組表に一致がなくても条件を登録できます。検索ワードと対象局を確認してください。",14));body.Add(Btn("作品名を手入力で追加",AddManual));
  foreach(var d in settings.Drafts.ToList()){var a=new Anime(d.Plan.AnimeId,d.Plan.AnimeTitle,"","","","");body.Add(Card(Stack(Text(d.Plan.AnimeTitle,21),Text("検索："+d.Plan.Keyword+" / 放送局 "+d.Plan.Services.Count+"局"),Text("録画後BAT："+(d.Plan.Recording.BatFilePath.Length==0?"なし":d.Plan.Recording.BatFilePath)),Text(d.RegisteredId>0?"登録済み ID "+d.RegisteredId:d.Result),Row(Btn("設定を確認",()=>Edit(a,d)),Btn("一覧から外す",async()=>{if(await DisplayAlert("選択から外す",d.Plan.AnimeTitle+"をこの一覧から外します。EPGTimer側の予約は削除しません。","外す","戻る")){settings.Drafts.Remove(d);Save();Show();}})))));}
  body.Add(Btn("未送信の作品をEPGTimerへ登録",Send));
 }
 async Task AddManual(){var name=await DisplayPromptAsync("作品を手入力で追加","アニメ一覧・番組表にない作品も、検索条件として登録できます。作品名を入力してください。",accept:"設定へ",cancel:"戻る",maxLength:500,initialValue:filter.Text??"");if(name==null)return;var work=LocalStore.ManualAnime(name,settings.Drafts.Select(d=>d.Plan.AnimeId));await Edit(work,null);}
 async Task Connect(){services=await edcb.Services();presets=RecordingPresets.Load(settings.EdcbFolder);var rules=await edcb.Rules();Save();note.Text=$"EDCB接続OK：{services.Count}局 / 録画プリセット{presets.Count}件 / 自動予約{rules.Count}件";}
 void ShowSettings(){
  var folder=new Entry {Text=settings.EdcbFolder,Placeholder="EpgTimerSrv.iniのあるフォルダー"};var bat=new Entry {Text=settings.DefaultBat,Placeholder="共通の録画後BAT（空欄ならプリセットの設定）"};
  body.Add(Card(Stack(Text("同じPCのEPGTimerに接続",23),Text("EpgTimerSrvを起動してください。設定ファイルは読み取るだけで変更しません。"),folder,Btn("EDCBのEXEから選ぶ",async()=>{var f=await FilePicker.Default.PickAsync();if(f!=null){folder.Text=Path.GetDirectoryName(f.FullPath);settings.EdcbFolder=folder.Text;Save();}}),bat,Row(Btn("共通BATを選ぶ",async()=>{var f=await FilePicker.Default.PickAsync();if(f!=null)bat.Text=f.FullPath;}),Btn("共通BATを解除",()=>{bat.Text="";return Task.CompletedTask;})),Btn("保存して接続を確認",async()=>{settings.EdcbFolder=folder.Text??"";settings.DefaultBat=bat.Text??"";Save();await Connect();}),Text("共通BATは今後追加する作品の初期値です。選択済み作品や登録済み自動予約の設定は変わりません。",13))));
 }
 async Task Edit(Anime a,Draft? draft){
  if(draft?.RegisteredId>0){await DisplayAlert("登録済みです","変更・削除はEPGTimerの自動予約一覧から行ってください。","閉じる");return;}
  if(services.Count==0||presets.Count==0)await Connect();
  var word=new Entry {Text=draft?.Plan.Keyword??a.Title};var exclude=new Entry {Text=draft?.Plan.Exclude??"",Placeholder="除外ワード（例：再放送）"};var bat=new Entry {Text=draft?.Plan.Recording.BatFilePath??settings.DefaultBat};
  var picker=new Picker {Title="EDCBの録画プリセット",ItemsSource=presets.Select(p=>p.Name).ToList(),SelectedIndex=draft==null?0:-1};
  bool keepDraft=draft!=null;if(draft==null&&bat.Text.Length==0)bat.Text=presets[0].Setting.BatFilePath;
  picker.SelectedIndexChanged+=(_,_)=>{if(picker.SelectedIndex>=0){keepDraft=false;bat.Text=presets[picker.SelectedIndex].Setting.BatFilePath;}};
  var boxes=new List<(EpgServiceInfo Service,CheckBox Box)>();var channels=new VerticalStackLayout {Spacing=2};foreach(var service in services.Where(s=>s.service_type is 1 or 165).OrderBy(s=>s.ONID).ThenBy(s=>s.service_name)){
   var b=new CheckBox {IsChecked=(draft?.Plan.Services??settings.DefaultServices).Contains(Edcb.ServiceKey(service))};boxes.Add((service,b));channels.Add(Row(b,Text(service.service_name)));}
  RulePlan Plan(){var rec=keepDraft?Edcb.Clone(draft!.Plan.Recording):Edcb.Clone(presets[picker.SelectedIndex<0?0:picker.SelectedIndex].Setting);rec.BatFilePath=bat.Text??"";return new(a.Id,a.Title,word.Text??"",exclude.Text??"",boxes.Where(b=>b.Box.IsChecked).Select(b=>Edcb.ServiceKey(b.Service)).ToList(),rec);}
  var page=new ContentPage {Title="作品の録画設定",Padding=24};
  // Buttons on this page use their own guard so the parent action can finish opening it.
  bool editingBusy=false;
  Button Action(string title,Func<Task> f){var b=new Button {Text=title};b.Clicked+=async(_,_)=>{if(editingBusy)return;editingBusy=true;b.IsEnabled=false;try{await f();}catch(Exception e){await page.DisplayAlert("確認してください",e.Message,"閉じる");}finally{editingBusy=false;b.IsEnabled=true;}};return b;}
  async Task SaveDraft(){var p=Plan();Edcb.Build(p);if(draft==null)settings.Drafts.Add(new Draft {Plan=p});else{draft.Plan=p;draft.Result="未送信";}settings.DefaultServices=p.Services.ToList();Save();await Navigation.PopModalAsync();section="drafts";Show();}
  page.Content=new ScrollView {Content=Stack(Text(a.Title,25),Text("番組表に未掲載でも保存・登録できます。照合は任意です。",14),Text("検索ワード（番組名のみ・通常検索）"),word,Text("空白区切りはAND検索です。EPG表記に合わせて編集してください。",12),exclude,Text("録画プリセット"),picker,Text("録画後BAT（この作品専用・空欄なら実行なし）"),bat,Row(Action("BATを選ぶ",async()=>{var f=await FilePicker.Default.PickAsync();if(f!=null)bat.Text=f.FullPath;}),Action("BATなし",()=>{bat.Text="";return Task.CompletedTask;})),Text("録画する放送局を選択",18),new ScrollView {Content=channels,HeightRequest=240},Action("現在の番組表で照合（省略可）",async()=>{var hits=await edcb.Preview(Plan());if(hits.Count==0){if(await page.DisplayAlert("番組表の一致は0件です","未放送・番組表未取得の可能性があります。0件でも検索条件を保存し、EPGTimerに自動予約を登録できます。後日条件に一致すると予約されます。","0件でも条件を保存","戻る"))await SaveDraft();}else await page.DisplayAlert("番組表の照合結果",string.Join("\n",hits.Take(20).Select(h=>$"{h.Start:MM/dd HH:mm} {h.Title}"))+($"\n全{hits.Count}件"),"閉じる");}),Action("番組表に一致がなくても条件を保存",SaveDraft),Action("戻る",async()=>await Navigation.PopModalAsync()))};await Navigation.PushModalAsync(new NavigationPage(page));
 }
 async Task Send(){var pending=settings.Drafts.Where(d=>d.RegisteredId==0).ToList();if(pending.Count==0){note.Text="未送信の作品はありません。";return;}foreach(var d in pending)Edcb.Build(d.Plan);
  if(!await DisplayAlert("EPGTimerへ自動予約を登録",string.Join("\n",pending.Select(d=>d.Plan.Keyword))+"\n\n選択した放送局・録画後BATでキーワード自動予約を追加します。番組表にない作品も、後日条件に一致すると予約されます。", "登録する","戻る"))return;
  foreach(var d in pending){try{d.RegisteredId=await edcb.Register(d.Plan,data);d.Result="登録完了";Save();}catch(Exception e){d.Result=e.Message;Save();note.Text=d.Plan.AnimeTitle+"："+e.Message;break;}}
  Show();note.Text="登録結果を各作品で確認してください。録画後BATはEDCBが録画終了後に実行します。";
 }
}
