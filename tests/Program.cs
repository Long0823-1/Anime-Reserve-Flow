using AnimeReserve;
using EpgTimer;
using System.Net;
using System.Text;
using System.Text.Json;
int count=0;void Check(bool ok,string title){if(!ok)throw new Exception(title);Console.WriteLine("PASS "+title);count++;}
string root=Path.Combine(Path.GetTempPath(),"AnimeReserveTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
Check(Catalog.Season(1)=="WINTER"&&Catalog.Season(4)=="FALL","quarter mapping");
var mock=new FakeEdcb();var edcb=new Edcb(mock);var rec=new RecSettingData {Priority=3,TuijyuuFlag=1,UseMargineFlag=1,StartMargine=15,EndMargine=20,ServiceMode=17};rec.RecFolderList.Add(new(){RecFolder=root,WritePlugIn="Write_Default.dll",RecNamePlugIn="RecName_Macro.dll?{Title}"});
string bat=Path.Combine(root,"録画後 & encode.bat");File.WriteAllText(bat,"@echo off");rec.BatFilePath=bat;
var plan=new RulePlan(1,"テスト作品","テスト作品", "再放送",[1234,1234],rec);var rule=Edcb.Build(plan);
Check(rule.searchInfo.titleOnlyFlag==1&&rule.searchInfo.regExpFlag==0&&rule.searchInfo.serviceList.Count==1,"title-only keyword and selected channels");
rec.Priority=1;Check(rule.recSetting.Priority==3,"recording setting independent clone");
var decoded=Read<List<EpgAutoAddData>>(Edcb.Pack(new List<EpgAutoAddData>{rule},true),true).Single();
Check(decoded.recSetting.BatFilePath==bat&&decoded.recSetting.RecFolderList[0].RecNamePlugIn.Contains("Macro")&&decoded.recSetting.EndMargine==20&&decoded.recSetting.ServiceMode==17,"protocol preserves BAT, plugins, margin and service mode");
Check((await edcb.Preview(plan)).Single().Title=="テスト作品 第1話","EPG preview protocol");
uint id=await edcb.Register(plan,root);Check(id==1&&mock.AddCalls==1,"auto reservation added and read back verified");
bool duplicate=false;try{await edcb.Register(plan,root);}catch(InvalidOperationException){duplicate=true;}Check(duplicate&&mock.AddCalls==1,"duplicate keyword cannot be submitted twice");
foreach(var bad in new[]{plan with {Keyword=""},plan with {Services=[]},plan with {Keyword="^!{999}test"},plan with {Recording=new RecSettingData {RecMode=5}},plan with {Recording=new RecSettingData {BatFilePath=Path.Combine(root,"missing.bat")}}}){bool fail=false;try{Edcb.Build(bad);}catch(ArgumentException){fail=true;}Check(fail,"invalid draft blocked");}
var pref=new Preferences {EdcbFolder=root,Drafts=[new Draft {Plan=plan,RegisteredId=1}]};LocalStore.Save(root,pref);Check(LocalStore.Load(root).Drafts[0].Plan.Recording.BatFilePath==bat,"draft and BAT survive restart");
var allServices=new[]{new EpgServiceInfo {ONID=1,TSID=2,SID=3},new EpgServiceInfo {ONID=1,TSID=2,SID=4},new EpgServiceInfo {ONID=1,TSID=2,SID=3}};
var quick=DraftFactory.Create(new Anime(40,"簡単追加","","","TV",""),allServices,rec);
Check(quick.Services.Count==2&&quick.Services.Contains(Edcb.ServiceKey(allServices[1])),"quick add includes all EDCB channels without manual selection");
Check(quick.Recording.BatFilePath==bat&&!ReferenceEquals(quick.Recording,rec),"quick add keeps an independent recording preset and BAT snapshot");
var upgraded=DraftFactory.WithAllChannels(plan,allServices);
Check(upgraded.Services.SequenceEqual(quick.Services)&&upgraded.Keyword==plan.Keyword&&upgraded.Recording.BatFilePath==bat&&plan.Services.SequenceEqual(new long[]{1234,1234}),"old unsent draft uses all channels without changing keyword or BAT");
bool noServices=false;try{DraftFactory.Create(new Anime(41,"test","","","TV",""),Array.Empty<EpgServiceInfo>(),rec);}catch(InvalidOperationException){noServices=true;}Check(noServices,"failed service retrieval cannot silently create an empty channel rule");
string ini="[SET]\r\nPresetID=3,\r\n[REC_DEF]\r\nRecMode=1\r\nPriority=2\r\n[REC_DEF3]\r\nSetName=アニメ専用\r\nRecMode=1\r\nBatFilePath="+bat+"\r\nUseMargineFlag=1\r\nStartMargine=25\r\n[REC_DEF_FOLDER3]\r\nCount=1\r\n0="+root+"\r\nWritePlugIn0=Write_Default.dll\r\n";File.WriteAllText(Path.Combine(root,"EpgTimerSrv.ini"),ini,Encoding.Unicode);
var presets=RecordingPresets.Load(root);Check(presets.Count==2&&presets[1].Name=="アニメ専用"&&presets[1].Setting.BatFilePath==bat&&presets[1].Setting.RecFolderList.Count==1&&presets[1].Setting.StartMargine==25,"EDCB recording presets and BAT imported read-only");
var handler=new FakeHttp();var catalog=new Catalog(Path.Combine(root,"Cache"),new HttpClient(handler));var list=await catalog.Fetch(2026,4);Check(list.Items.Single().Title=="テストアニメ"&&!list.Cached,"seasonal GraphQL native title parsed");Check((await catalog.Fetch(2026,4)).Cached&&handler.Calls==1,"catalog cache avoids unnecessary requests");handler.Fail=true;Check((await catalog.Fetch(2026,4,true)).Notice.Length>0,"offline fetch preserves cache with visible notice");
// Exercise real named-pipe framing without altering the running EDCB.
string pipeName="AnimeReserveTests-"+Guid.NewGuid().ToString("N");
var server=Task.Run(async()=>{using var pipe=new System.IO.Pipes.NamedPipeServerStream(pipeName,System.IO.Pipes.PipeDirection.InOut,1,System.IO.Pipes.PipeTransmissionMode.Byte,System.IO.Pipes.PipeOptions.Asynchronous);await pipe.WaitForConnectionAsync();byte[] head=new byte[8];await pipe.ReadExactlyAsync(head);uint size=BitConverter.ToUInt32(head,4);byte[] payload=new byte[size];await pipe.ReadExactlyAsync(payload);if(BitConverter.ToUInt32(head)!=1021||size!=0)throw new Exception("bad request frame");byte[] reply=Edcb.Pack(new List<EpgServiceInfo>{new(){ONID=1,TSID=2,SID=3,service_name="テスト局"}},false);BitConverter.GetBytes(1u).CopyTo(head,0);BitConverter.GetBytes(reply.Length).CopyTo(head,4);await pipe.WriteAsync(head);await pipe.WriteAsync(reply);});
var framed=await new Edcb(new LocalPipeTransport(pipeName)).Services();await server;Check(framed.Single().service_name=="テスト局","real named-pipe request and reply framing");
bool timeout=false;try{await new LocalPipeTransport("AnimeReserveAbsent-"+Guid.NewGuid(),TimeSpan.FromMilliseconds(200)).Request(1021,[]);}catch(IOException){timeout=true;}Check(timeout,"missing EDCB pipe times out without hanging");
var manual=LocalStore.ManualAnime("  放送前の新作アニメ  ",new[]{1,50,-1,-3});Check(manual.Id==-4&&manual.Title=="放送前の新作アニメ","manual works have stable separate IDs and editable native titles");
var zeroServer=new FakeEdcb {NoHits=true};var zeroEdcb=new Edcb(zeroServer);var futurePlan=plan with {AnimeId=manual.Id,AnimeTitle=manual.Title,Keyword=manual.Title};Check((await zeroEdcb.Preview(futurePlan)).Count==0,"not yet broadcast work can have zero EPG matches");
uint futureId=await zeroEdcb.Register(futurePlan,root);Check(futureId==1&&zeroServer.Rules.Single().searchInfo.andKey==manual.Title&&zeroServer.Rules.Single().recSetting.BatFilePath==bat,"zero EPG matches still allow keyword auto-reservation with BAT");
var bulkExisting=new List<Draft>{new(){Plan=plan with {Keyword="カスタム済み"},RegisteredId=99}};
var bulkAnime=new[]{new Anime(1,"既存作品","","","TV",""),new Anime(2,"新作A","","","TV",""),new Anime(3,"新作B","","","TV",""),new Anime(2,"新作A","","","TV","")};
var batch=DraftFactory.CreateMissing(bulkAnime,bulkExisting,allServices,rec,bat);
Check(batch.Count==2&&batch.Select(d=>d.Plan.AnimeId).SequenceEqual(new[]{2,3}),"bulk add skips existing and duplicate IDs");
Check(batch.All(d=>d.Plan.Services.Count==2&&d.Plan.Recording.BatFilePath==bat),"bulk add applies all channels and common BAT to every work");
Check(bulkExisting.Count==1&&bulkExisting[0].RegisteredId==99&&bulkExisting[0].Plan.Keyword=="カスタム済み","bulk add preserves existing custom settings and registered IDs");
batch[0].Plan.Recording.Priority=5;Check(batch[1].Plan.Recording.Priority==rec.Priority,"bulk recording snapshots are independent");
Check(DraftFactory.CreateMissing(bulkAnime,bulkExisting.Concat(batch),allServices,rec,bat).Count==0,"repeating bulk add creates no duplicates");
bool invalidBatch=false;try{DraftFactory.CreateMissing(new[]{new Anime(4,"valid","","","TV",""),new Anime(5,"","","","TV","")},bulkExisting,allServices,rec,bat);}catch(ArgumentException){invalidBatch=true;}Check(invalidBatch&&bulkExisting.Count==1&&bulkExisting[0].RegisteredId==99,"invalid bulk draft cannot partially modify selection");
if(args.Contains("--live-catalog")){var live=await new Catalog(Path.Combine(root,"Live")).Fetch(2026,4);Check(live.Items.Count>0,"actual AniList season fetch");Console.WriteLine("LIVE CATALOG "+live.Items.Count+" works");}
if(args.Contains("--read-edcb")){var local=new Edcb();var channels=await local.Services();var rules=await local.Rules();Check(channels.Count>0,"local EpgTimerSrv read-only pipe connection");Console.WriteLine("EDCB READ ONLY "+channels.Count+" channels / "+rules.Count+" rules");int folderArg=Array.IndexOf(args,"--edcb-folder");if(folderArg>=0){var real=RecordingPresets.Load(args[folderArg+1]);Check(real.Count>0,"actual EDCB recording presets read");}}
Console.WriteLine("ALL PASS "+count+" fixtures "+root);
static T Read<T>(byte[] data,bool versioned) where T:new(){using var ms=new MemoryStream(data);var r=new CtrlCmdReader(ms);if(versioned){ushort v=0;r.Read(ref v);r.Version=v;}object x=new T();r.Read(ref x);return (T)x;}
sealed class FakeEdcb:IEdcbTransport {
 public List<EpgAutoAddData> Rules=[];public int AddCalls;public bool NoHits;
 public Task<byte[]> Request(uint command,byte[] body,CancellationToken token=default){
  if(command==2131)return Task.FromResult(Edcb.Pack(Rules,true));
  if(command==2132){using var ms=new MemoryStream(body);var r=new CtrlCmdReader(ms);ushort v=0;r.Read(ref v);r.Version=v;object items=new List<EpgAutoAddData>();r.Read(ref items);foreach(var rule in (List<EpgAutoAddData>)items){rule.dataID=(uint)Rules.Count+1;Rules.Add(rule);}AddCalls++;return Task.FromResult(Array.Empty<byte>());}
  if(command==1025&&NoHits)return Task.FromResult(Edcb.Pack(new List<EpgEventInfo>(),false));
  if(command==1025)return Task.FromResult(Edcb.Pack(new List<EpgEventInfo>{new(){ShortInfo=new(){event_name="テスト作品 第1話"},start_time=new DateTime(2026,11,1),durationSec=1800}},false));
  throw new Exception("unexpected command "+command);
 }
}
sealed class FakeHttp:HttpMessageHandler {
 public int Calls;public bool Fail;
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken){Calls++;if(Fail)throw new HttpRequestException("offline");return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("""{"data":{"Page":{"pageInfo":{"hasNextPage":false},"media":[{"id":1,"title":{"native":"テストアニメ","romaji":"Test"},"siteUrl":"https://anilist.co/anime/1","format":"TV","status":"RELEASING"}]}}}""")});}
}
