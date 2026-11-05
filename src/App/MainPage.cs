using System.Diagnostics;
using EpgTimer;
namespace AnimeReserve.App;
public sealed class MainPage:ContentPage {
 readonly string data;readonly Preferences settings;readonly Edcb edcb=new();readonly Catalog catalog;
 readonly ContentView body=new();readonly Label note=new(){TextColor=Color.FromArgb("#526578")};
 Entry year=new(){WidthRequest=100,Keyboard=Keyboard.Numeric};Picker quarter=new(){WidthRequest=160,ItemsSource=new[]{"冬（1～3月）","春（4～6月）","夏（7～9月）","秋（10～12月）"}};
 Entry filter=new(){Placeholder="作品名で絞り込み"};List<Anime> anime=[];List<EpgServiceInfo> services=[];List<RecordingPreset> presets=[];bool busy;string section="season";
 static Label Text(string s,int size=15)=>new(){Text=s,FontSize=size,LineBreakMode=LineBreakMode.WordWrap};
 Button Btn(string label,Func<Task> action){var b=new Button {Text=label};b.Clicked+=async(_,_)=>{if(busy)return;busy=true;b.IsEnabled=false;try{await action();}catch(Exception e){note.Text=e.Message;await DisplayAlert("確認してください",e.Message,"閉じる");}finally{busy=false;b.IsEnabled=true;}};return b;}
 static VerticalStackLayout Stack(params View[] views){var s=new VerticalStackLayout {Spacing=12};foreach(var v in views)s.Add(v);return s;}
 static HorizontalStackLayout Row(params View[] views){var s=new HorizontalStackLayout {Spacing=12};foreach(var v in views)s.Add(v);return s;}
 static Border Card(View v)=>new(){Content=v,Padding=20,BackgroundColor=Colors.White,Stroke=Colors.Transparent,StrokeShape=new Microsoft.Maui.Controls.Shapes.RoundRectangle {CornerRadius=24}};
 async Task SaveAsync()=>await Task.Run(()=>LocalStore.Save(data,settings));
 public MainPage(){
  var arguments=Environment.GetCommandLineArgs();int dataIndex=Array.IndexOf(arguments,"--data");data=dataIndex>=0&&dataIndex+1<arguments.Length?Path.GetFullPath(arguments[dataIndex+1]):arguments.Contains("--qa")?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Anime-Reserve-Flow","QA"):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Anime-Reserve-Flow");settings=LocalStore.Load(data);catalog=new Catalog(Path.Combine(data,"Cache"));
  if(settings.EdcbFolder.Length==0)try{settings.EdcbFolder=Path.GetDirectoryName(Process.GetProcessesByName("EpgTimerSrv").FirstOrDefault()?.MainModule?.FileName)??"";}catch{}
  Title="Anime Reserve Flow";Padding=28;year.Text=DateTime.Now.Year.ToString();quarter.SelectedIndex=(DateTime.Now.Month-1)/3;
  var grid=new Grid {RowDefinitions=new RowDefinitionCollection {new(GridLength.Auto),new(GridLength.Auto),new(GridLength.Star),new(GridLength.Auto)},RowSpacing=14};
  grid.Add(Stack(Text("今季のアニメを、毎話の録画へ。",30),Text("放送局を選ぶ → 作品を追加 → まとめて登録。地デジ・BS・CS・SKY別に選べます。",14)),0,0);
  grid.Add(Row(Btn("季節のアニメ",()=>{section="season";Show();return Task.CompletedTask;}),Btn("選択した作品",()=>{section="drafts";Show();return Task.CompletedTask;}),Btn("共通設定",()=>{section="settings";Show();return Task.CompletedTask;}),Btn("選択した作品を登録",Send)),0,1);
  grid.Add(body,0,2);grid.Add(Stack(note,Row(Btn("AniList（作品情報）",()=>Launcher.Default.OpenAsync("https://anilist.co").ContinueWith(_=>{})),Btn("Ko-fiで開発を支援",()=>Launcher.Default.OpenAsync("https://ko-fi.com/long0823").ContinueWith(_=>{})))),0,3);Content=grid;
  Loaded+=async(_,_)=>{if(anime.Count>0)return;try{await Fetch(false);}catch(Exception e){note.Text=e.Message;Show();}};Show();
 }
 async Task Fetch(bool force){if(!int.TryParse(year.Text,out int y))throw new ArgumentException("年を入力してください。");note.Text="作品一覧を取得しています…";var result=await catalog.Fetch(y,quarter.SelectedIndex+1,force);anime=result.Items;note.Text=$"{y}年 {quarter.SelectedItem}：{anime.Count}作品 / {(result.Cached?"保存済み":"取得完了")} {result.Updated.ToLocalTime():MM/dd HH:mm} "+result.Notice;Show();}
 void Show(){if(section=="season")ShowSeason();else if(section=="drafts")ShowDrafts();else ShowSettings();}
 void ShowSeason(){
  string previousYear=year.Text??DateTime.Now.Year.ToString(),previousFilter=filter.Text??"";int previousQuarter=quarter.SelectedIndex;
  // Fresh controls prevent reusing WinUI handlers disposed when another section was shown.
  year=new Entry {Text=previousYear,WidthRequest=100,Keyboard=Keyboard.Numeric};quarter=new Picker {WidthRequest=160,ItemsSource=new[]{"冬（1～3月）","春（4～6月）","夏（7～9月）","秋（10～12月）"},SelectedIndex=previousQuarter};filter=new Entry {Text=previousFilter,Placeholder="作品名で絞り込み",WidthRequest=320};filter.Completed+=(_,_)=>ShowSeason();
  var header=Card(Stack(Row(year,quarter,Btn("一覧を取得",()=>Fetch(true)),Btn("一覧になくても手入力で追加",AddManual)),Row(filter,Btn("絞り込み",()=>{ShowSeason();return Task.CompletedTask;}),Btn("全"+anime.Count+"作品を追加",AddAll)),Text("TV・短編TVアニメの放送開始クールを表示します。全件追加は絞り込みに関係なく、取得したクール全体が対象です。",13)));
  var rows=anime.Where(a=>a.Title.Contains(filter.Text??"",StringComparison.OrdinalIgnoreCase)||a.Romaji.Contains(filter.Text??"",StringComparison.OrdinalIgnoreCase)).ToList();
  int created=0;var list=new CollectionView {Header=header,ItemsSource=rows,SelectionMode=SelectionMode.None,ItemSizingStrategy=ItemSizingStrategy.MeasureFirstItem,ItemTemplate=new DataTemplate(()=>{created++;return new SeasonCell(this);}),EmptyView=Text("一致する作品がありません。「一覧になくても手入力で追加」から登録できます。")};DisplayList(list,"season",rows.Count,()=>created);
 }
 void DisplayList(CollectionView list,string name,int items,Func<int> created){
  if(Environment.GetCommandLineArgs().Contains("--ui-metrics")){var timer=Stopwatch.StartNew();list.Loaded+=async(_,_)=>{try{double loaded=timer.Elapsed.TotalMilliseconds;await Task.Delay(700);if(body.Content!=list)return;Directory.CreateDirectory(data);var value=new {view=name,items,createdCells=created(),loadedMs=Math.Round(loaded,1)};await File.AppendAllTextAsync(Path.Combine(data,"ui-metrics.jsonl"),System.Text.Json.JsonSerializer.Serialize(value)+Environment.NewLine);}catch{}};}
  body.Content=list;
 }
 sealed class SeasonCell:ContentView {
  readonly MainPage owner;readonly Label title=Text("",20),romaji=Text("",12);readonly Button add;
  public SeasonCell(MainPage owner){this.owner=owner;title.MaxLines=2;romaji.MaxLines=1;add=owner.Btn("＋ 追加",async()=>{if(BindingContext is Anime a){await owner.QuickAdd(a);if(BindingContext is Anime current&&current.Id==a.Id)add!.Text="追加済み";}});Content=Card(Stack(title,romaji,Row(add,owner.Btn("設定を変更",async()=>{if(BindingContext is Anime a)await owner.Edit(a,owner.settings.Drafts.FirstOrDefault(d=>d.Plan.AnimeId==a.Id));}))));HeightRequest=170;Margin=new Thickness(0,6);BindingContextChanged+=(_,_)=>{if(BindingContext is Anime a){title.Text=a.Title;romaji.Text=a.Romaji;add.Text=owner.settings.Drafts.Any(d=>d.Plan.AnimeId==a.Id)?"追加済み":"＋ 追加";}};}
 }
 void ShowDrafts(){
  var header=Stack(Text("放送局は作品ごとの選択を使います。「設定を変更」で地デジ・BS・CS・SKYの対象局や検索ワード・BATを調整できます。",14),Btn("作品名を手入力で追加",AddManual));
  int created=0;var list=new CollectionView {Header=header,ItemsSource=settings.Drafts.ToList(),SelectionMode=SelectionMode.None,ItemSizingStrategy=ItemSizingStrategy.MeasureFirstItem,ItemTemplate=new DataTemplate(()=>{created++;return new DraftCell(this);}),EmptyView=Text("作品はまだ選択されていません。季節のアニメから追加してください。")};DisplayList(list,"drafts",settings.Drafts.Count,()=>created);
 }
 sealed class DraftCell:ContentView {
  readonly Label title=Text("",21),keyword=Text(""),bat=Text(""),status=Text("");
  public DraftCell(MainPage owner){title.MaxLines=2;keyword.MaxLines=1;bat.MaxLines=1;status.MaxLines=3;Content=Card(Stack(title,keyword,bat,status,Row(owner.Btn("結果の詳細",async()=>{if(BindingContext is Draft d)await owner.DisplayAlert("登録結果",d.Plan.AnimeTitle+"\n"+(d.RegisteredId>0?"EDCB ID："+d.RegisteredId+"\n":"")+d.Result,"閉じる");}),owner.Btn("設定を変更",async()=>{if(BindingContext is Draft d)await owner.Edit(new Anime(d.Plan.AnimeId,d.Plan.AnimeTitle,"","","",""),d);}),owner.Btn("一覧から外す",async()=>{if(BindingContext is Draft d&&await owner.DisplayAlert("選択から外す",d.Plan.AnimeTitle+"をこの一覧から外します。EPGTimer側の予約は削除しません。","外す","戻る")){owner.settings.Drafts.Remove(d);await owner.SaveAsync();owner.Show();}}))));HeightRequest=290;Margin=new Thickness(0,6);BindingContextChanged+=(_,_)=>{if(BindingContext is Draft d){title.Text=d.Plan.AnimeTitle;keyword.Text="検索："+d.Plan.Keyword+" / "+Channels.Summary(d.Plan.Services);bat.Text="録画後BAT："+(d.Plan.Recording.BatFilePath.Length==0?"なし":d.Plan.Recording.BatFilePath);status.Text=d.RegisteredId>0?"登録済み ID "+d.RegisteredId+" · "+d.Result:d.Result;}};}
 }
 async Task AddManual(){var name=await DisplayPromptAsync("作品を手入力で追加","アニメ一覧・番組表にない作品も、検索条件として登録できます。作品名を入力してください。",accept:"追加",cancel:"戻る",maxLength:500,initialValue:filter.Text??"");if(name==null)return;var work=LocalStore.ManualAnime(name,settings.Drafts.Select(d=>d.Plan.AnimeId));await QuickAdd(work);Show();}
 async Task QuickAdd(Anime a){if(settings.Drafts.Any(d=>d.Plan.AnimeId==a.Id)){note.Text="この作品は追加済みです。";return;}if(services.Count==0||presets.Count==0)await Connect();var preset=presets[Math.Clamp(settings.DefaultPresetIndex,0,presets.Count-1)];var plan=DraftFactory.Create(a,services,preset.Setting,settings.DefaultBat,settings.SelectedServices??[]);settings.Drafts.Add(new Draft {Plan=plan});await SaveAsync();note.Text=settings.Drafts.Count(d=>d.RegisteredId==0)+"作品を選択中。「選択した作品を登録」でまとめて登録できます。";}
 async Task AddAll(){
  if(anime.Count==0){note.Text="先に年・季節を選び、一覧を取得してください。";return;}
  if(anime.All(a=>settings.Drafts.Any(d=>d.Plan.AnimeId==a.Id))){note.Text="このリストはすべて追加済みです。";return;}
  if(services.Count==0||presets.Count==0)await Connect();
  var preset=presets[Math.Clamp(settings.DefaultPresetIndex,0,presets.Count-1)];var added=DraftFactory.CreateMissing(anime,settings.Drafts,services,preset.Setting,settings.DefaultBat,settings.SelectedServices??[]);
  settings.Drafts.AddRange(added);try{await SaveAsync();}catch{foreach(var draft in added)settings.Drafts.Remove(draft);throw;}
  ShowSeason();note.Text=added.Count+"作品を追加しました（追加済み "+(anime.Select(a=>a.Id).Distinct().Count()-added.Count)+"作品はそのまま）。「選択した作品を登録」でまとめて登録できます。";
 }
 async Task Connect(){note.Text="EDCBに接続しています…";services=await edcb.Services();presets=await Task.Run(()=>RecordingPresets.Load(settings.EdcbFolder));settings.SelectedServices??=Channels.Initial(settings,services);await SaveAsync();note.Text=$"EDCB接続OK：{services.Count}局 / 録画プリセット{presets.Count}件";}
 void ShowSettings(){
  var channels=new ChannelChooser(services,settings.SelectedServices??[]);
  var commonPreset=new Picker {Title="共通の録画プリセット",ItemsSource=presets.Count>0?presets.Select(p=>p.Name).ToList():new List<string>{"デフォルト（接続後に読み込み）"},SelectedIndex=presets.Count>0?Math.Clamp(settings.DefaultPresetIndex,0,presets.Count-1):0};
  var folder=new Entry {Text=settings.EdcbFolder,Placeholder="EpgTimerSrv.iniのあるフォルダー"};var bat=new Entry {Text=settings.DefaultBat,Placeholder="共通の録画後BAT（空欄ならプリセットの設定）"};
  body.Content=new ScrollView {Content=Card(Stack(Text("同じPCのEPGTimerに接続",23),Text("EpgTimerSrvを起動してください。設定ファイルは読み取るだけで変更しません。"),folder,Btn("EDCBのEXEから選ぶ",async()=>{var f=await FilePicker.Default.PickAsync();if(f!=null){folder.Text=Path.GetDirectoryName(f.FullPath);settings.EdcbFolder=folder.Text;await SaveAsync();}}),Text("全作品で使う録画プリセット"),commonPreset,bat,Row(Btn("共通BATを選ぶ",async()=>{var f=await FilePicker.Default.PickAsync();if(f!=null)bat.Text=f.FullPath;}),Btn("プリセットのBATに戻す",()=>{bat.Text="";return Task.CompletedTask;})),Btn("保存して接続を確認",async()=>{settings.EdcbFolder=folder.Text??"";settings.DefaultBat=bat.Text??"";if(services.Count>0)settings.SelectedServices=channels.Selected;if(presets.Count>0)settings.DefaultPresetIndex=Math.Max(0,commonPreset.SelectedIndex);await SaveAsync();await Connect();ShowSettingsAfterConnect();}),Text("共通BAT・放送局は今後追加する作品の初期値です。追加済み作品の放送局は下のボタンで明示的に反映できます。",13),Text("共通の対象放送局",22),Text("接続すると取得できた局を表示します。初回は地デジだけを選択します。BS・CS・SKYは必要な区分の全選択、または局ごとのチェックで追加してください。",13),channels,Btn("放送局の選択を保存",async()=>{if(services.Count==0)throw new InvalidOperationException("先にEDCBへ接続してください。");settings.SelectedServices=channels.Selected;await SaveAsync();note.Text="共通の放送局を保存しました："+Channels.Summary(channels.Selected);}),Btn("この放送局を未送信作品にも適用",async()=>{var chosen=Channels.Resolve(channels.Selected,services);var pending=settings.Drafts.Where(d=>d.RegisteredId==0).ToList();if(!await DisplayAlert("未送信作品の放送局を変更",pending.Count+"作品へ適用します。録画プリセット・BAT・登録済み予約は変更しません。\n"+Channels.Summary(chosen),"適用する","戻る"))return;foreach(var draft in pending){draft.Plan=draft.Plan with {Services=chosen.ToList()};draft.Result="未送信（放送局を変更）";}settings.SelectedServices=chosen;await SaveAsync();note.Text=pending.Count+"作品の対象放送局を変更しました。";})))};
 }
 void ShowSettingsAfterConnect(){ShowSettings();}
 async Task Edit(Anime a,Draft? draft){
  if(draft?.RegisteredId>0){await DisplayAlert("登録済みです","変更・削除はEPGTimerの自動予約一覧から行ってください。","閉じる");return;}
  if(services.Count==0||presets.Count==0)await Connect();
  var word=new Entry {Text=draft?.Plan.Keyword??a.Title};var exclude=new Entry {Text=draft?.Plan.Exclude??"",Placeholder="除外ワード（例：再放送）"};var bat=new Entry {Text=draft?.Plan.Recording.BatFilePath??settings.DefaultBat};
  var channels=new ChannelChooser(services,draft?.Plan.Services??settings.SelectedServices??[]);
  int initialPreset=Math.Clamp(settings.DefaultPresetIndex,0,presets.Count-1);
  var picker=new Picker {Title="EDCBの録画プリセット",ItemsSource=presets.Select(p=>p.Name).ToList(),SelectedIndex=draft==null?initialPreset:-1};
  bool keepDraft=draft!=null;if(draft==null&&bat.Text.Length==0)bat.Text=presets[initialPreset].Setting.BatFilePath;
  picker.SelectedIndexChanged+=(_,_)=>{if(picker.SelectedIndex>=0){keepDraft=false;bat.Text=presets[picker.SelectedIndex].Setting.BatFilePath;}};
  RulePlan Plan(){var rec=keepDraft?Edcb.Clone(draft!.Plan.Recording):Edcb.Clone(presets[picker.SelectedIndex<0?0:picker.SelectedIndex].Setting);rec.BatFilePath=bat.Text??"";return new(a.Id,a.Title,word.Text??"",exclude.Text??"",Channels.Resolve(channels.Selected,services),rec);}
  var page=new ContentPage {Title="作品の録画設定",Padding=24};
  // Buttons on this page use their own guard so the parent action can finish opening it.
  bool editingBusy=false;
  Button Action(string title,Func<Task> f){var b=new Button {Text=title};b.Clicked+=async(_,_)=>{if(editingBusy)return;editingBusy=true;b.IsEnabled=false;try{await f();}catch(Exception e){await page.DisplayAlert("確認してください",e.Message,"閉じる");}finally{editingBusy=false;b.IsEnabled=true;}};return b;}
  async Task SaveDraft(){var p=Plan();Edcb.Build(p);if(draft==null)settings.Drafts.Add(new Draft {Plan=p});else{draft.Plan=p;draft.Result="未送信";}await SaveAsync();await Navigation.PopModalAsync();section="drafts";Show();}
  page.Content=new ScrollView {Content=Stack(Text(a.Title,25),Text("番組表に未掲載でも保存・登録できます。照合は任意です。",14),Text("検索ワード（番組名のみ・通常検索）"),word,Text("空白区切りはAND検索です。EPG表記に合わせて編集してください。",12),exclude,Text("録画プリセット"),picker,Text("録画後BAT（この作品専用・空欄なら実行なし）"),bat,Row(Action("BATを選ぶ",async()=>{var f=await FilePicker.Default.PickAsync();if(f!=null)bat.Text=f.FullPath;}),Action("BATなし",()=>{bat.Text="";return Task.CompletedTask;})),Text("対象放送局",20),channels,Action("現在の番組表で照合（省略可）",async()=>{var hits=await edcb.Preview(Plan());if(hits.Count==0){if(await page.DisplayAlert("番組表の一致は0件です","未放送・番組表未取得の可能性があります。0件でも検索条件を保存し、EPGTimerに自動予約を登録できます。後日条件に一致すると予約されます。","0件でも条件を保存","戻る"))await SaveDraft();}else await page.DisplayAlert("番組表の照合結果",string.Join("\n",hits.Take(20).Select(h=>$"{h.Start:MM/dd HH:mm} {h.Title}"))+($"\n全{hits.Count}件"),"閉じる");}),Action("保存する",SaveDraft),Action("戻る",async()=>await Navigation.PopModalAsync()))};await Navigation.PushModalAsync(new NavigationPage(page));
 }
 async Task Send(){
  var pending=settings.Drafts.Where(d=>d.RegisteredId==0).ToList();if(pending.Count==0){note.Text="未送信の作品はありません。";return;}
  if(services.Count==0||presets.Count==0)await Connect();
  services=await edcb.Services(); // Re-read availability; never broaden a saved selection.
  if(!await DisplayAlert("EPGTimerへ自動予約を登録",string.Join("\n",pending.Select(d=>d.Plan.Keyword+"（"+Channels.Summary(d.Plan.Services)+"）"))+"\n\n作品ごとに選択した放送局だけを対象に、指定の録画後BATでキーワード自動予約を追加します。番組表にない作品も登録できます。同条件の既存予約は照合し、条件が違う作品は理由を表示して後続を処理します。", "登録する","戻る"))return;
  var result=await RegistrationBatch.Run(edcb,settings,services,data,SaveAsync,(index,total,draft)=>note.Text=$"自動予約を確認・送信中：{index}/{total} {draft.Plan.AnimeTitle}");
  section="drafts";Show();note.Text=result.Message;
  await DisplayAlert(result.Failed>0?"一部の作品を登録できませんでした":"登録結果",result.Message,"閉じる");
 }
}
