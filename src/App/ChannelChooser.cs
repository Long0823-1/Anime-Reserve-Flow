using EpgTimer;
namespace AnimeReserve.App;
sealed class ChannelChooser:ContentView {
 readonly List<EpgServiceInfo> available;readonly HashSet<long> selected;readonly List<Action> refresh=[];
 public List<long> Selected=>selected.Order().ToList();
 static Label Text(string value,int size=14)=>new(){Text=value,FontSize=size,LineBreakMode=LineBreakMode.WordWrap};
 public ChannelChooser(IEnumerable<EpgServiceInfo> services,IEnumerable<long> initial) {
  available=Channels.Eligible(services);selected=(initial??[]).ToHashSet();
  var stack=new VerticalStackLayout {Spacing=14};var total=Text("");stack.Add(total);refresh.Add(()=>total.Text="選択中："+Channels.Summary(selected));
  foreach(var band in Enum.GetValues<BroadcastBand>()) {
   var group=available.Where(s=>Channels.Band(s.ONID)==band).ToList();if(band==BroadcastBand.Other&&group.Count==0)continue;
   var counter=Text("",16);var checks=new List<(long Key,CheckBox Check)>();bool updating=false;bool expanded=false;var detail=new VerticalStackLayout {Spacing=6,IsVisible=false};
   void Update(){updating=true;foreach(var item in checks)item.Check.IsChecked=selected.Contains(item.Key);updating=false;counter.Text=Channels.Name(band)+"："+group.Count(s=>selected.Contains(Edcb.ServiceKey(s)))+" / "+group.Count+"局";}
   refresh.Add(Update);
   Button Button(string label,Action action)=>new(){Text=label,IsEnabled=group.Count>0,Command=new Command(action)};
   void Select(bool enabled){var next=Channels.SetBand(selected,available,band,enabled);selected.Clear();selected.UnionWith(next);foreach(var f in refresh)f();}
   var all=Button("全選択",()=>Select(true));all.AutomationId="select-"+band;
   var none=Button("全解除",()=>Select(false));none.AutomationId="clear-"+band;
   var toggle=Button("局ごとに選ぶ",()=>{});toggle.Command=new Command(()=>{
    if(!expanded&&checks.Count==0)foreach(var service in group) {
     long key=Edcb.ServiceKey(service);var check=new CheckBox {IsChecked=selected.Contains(key),AutomationId="channel-"+key};
     string name=string.IsNullOrWhiteSpace(service.service_name)?"SID "+service.SID:service.service_name;SemanticProperties.SetDescription(check,name);
     check.CheckedChanged+=(_,e)=>{if(updating)return;if(e.Value)selected.Add(key);else selected.Remove(key);foreach(var f in refresh)f();};checks.Add((key,check));
     var row=new Grid {ColumnDefinitions=new ColumnDefinitionCollection {new(GridLength.Auto),new(GridLength.Star)},ColumnSpacing=8};row.Add(check,0,0);row.Add(Text(name),1,0);detail.Add(row);
    }
    expanded=!expanded;detail.IsVisible=expanded;toggle.Text=expanded?"局一覧を閉じる":"局ごとに選ぶ";
   });
   var buttons=new HorizontalStackLayout {Spacing=8,Children={all,none,toggle}};
   var block=new VerticalStackLayout {Spacing=8,Children={counter,buttons,detail}};stack.Add(new Border {Content=block,Padding=14,Stroke=Color.FromArgb("#DCE4EF"),StrokeShape=new Microsoft.Maui.Controls.Shapes.RoundRectangle {CornerRadius=16}});
  }
  int missing=selected.Count(k=>!available.Any(s=>Edcb.ServiceKey(s)==k));
  if(missing>0)stack.Add(Text("以前選んだ局のうち"+missing+"局は現在取得できません。送信時は取得できた対象局だけを使います。"));
  stack.Add(Text("ワンセグは表示・選択・送信から除外します。0局の区分はEDCBからサービスを取得できていません。",12));Content=stack;foreach(var f in refresh)f();
 }
}
