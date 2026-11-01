using System.IO.Pipes;
using EpgTimer;
namespace AnimeReserve;
public interface IEdcbTransport {Task<byte[]> Request(uint command,byte[] body,CancellationToken token=default);}
public sealed class LocalPipeTransport : IEdcbTransport {
 readonly string name;readonly TimeSpan wait;public LocalPipeTransport(string name="EpgTimerSrvPipe",TimeSpan? wait=null){this.name=name;this.wait=wait??TimeSpan.FromSeconds(10);}
 public async Task<byte[]> Request(uint command,byte[] body,CancellationToken token=default){
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(wait);
  try {
   using var pipe=new NamedPipeClientStream(".",name,PipeDirection.InOut,PipeOptions.Asynchronous);await pipe.ConnectAsync(timeout.Token);
   byte[] head=new byte[8];BitConverter.GetBytes(command).CopyTo(head,0);BitConverter.GetBytes(body.Length).CopyTo(head,4);await pipe.WriteAsync(head,timeout.Token);await pipe.WriteAsync(body,timeout.Token);
   await pipe.ReadExactlyAsync(head,timeout.Token);uint result=BitConverter.ToUInt32(head),size=BitConverter.ToUInt32(head,4);if(size>64*1024*1024)throw new IOException("EDCBからの応答サイズが異常です。");byte[] reply=new byte[size];await pipe.ReadExactlyAsync(reply,timeout.Token);
   if(result!=1)throw new IOException("EDCBの操作に失敗しました（応答コード "+result+"）。");return reply;
  }catch(OperationCanceledException) when(!token.IsCancellationRequested){throw new IOException("EDCBへの接続がタイムアウトしました。EpgTimerSrvの起動と実行ユーザーを確認してください。予約送信中の場合は登録済みかを確認してから再操作してください。");}
 }
}
public record ProgramMatch(string Title,DateTime Start,uint Seconds,ushort Onid,ushort Tsid,ushort Sid);
public record RulePlan(int AnimeId,string AnimeTitle,string Keyword,string Exclude,List<long> Services,RecSettingData Recording);
public record RegistrationReceipt(uint Id,bool AlreadyExists);
public sealed class Edcb {
 readonly IEdcbTransport transport;readonly SemaphoreSlim gate=new(1);
 public Edcb(IEdcbTransport transport=null){this.transport=transport??new LocalPipeTransport();}
 public static byte[] Pack(object value,bool versioned){var w=new CtrlCmdWriter(new MemoryStream(),versioned?(ushort)5:(ushort)0);if(versioned)w.Write((ushort)5);if(value!=null)w.Write(value);return w.Stream.ToArray();}
 static T Unpack<T>(byte[] data,bool versioned) where T:new(){using var ms=new MemoryStream(data,false);var r=new CtrlCmdReader(ms);if(versioned){ushort v=0;r.Read(ref v);r.Version=v;}object value=new T();r.Read(ref value);return (T)value;}
 public async Task<List<EpgServiceInfo>> Services(CancellationToken t=default)=>Unpack<List<EpgServiceInfo>>(await transport.Request(1021,[],t).ConfigureAwait(false),false);
 public async Task<List<EpgAutoAddData>> Rules(CancellationToken t=default)=>Unpack<List<EpgAutoAddData>>(await transport.Request(2131,Pack(null,true),t).ConfigureAwait(false),true);
 public static long ServiceKey(EpgServiceInfo s)=>((long)s.ONID<<32)|((long)s.TSID<<16)|s.SID;
 public static EpgAutoAddData Build(RulePlan plan){
  string key=(plan.Keyword??"").Trim();if(key.Length==0||key.Length>500||key.Contains('\r')||key.Contains('\n')||key.StartsWith("^!{")||key.StartsWith("C!{")||key.StartsWith("D!{"))throw new ArgumentException("検索ワードを1～500文字で入力してください。");
  if(plan.Services==null||plan.Services.Count==0)throw new ArgumentException("録画対象の放送局を1つ以上選択してください。");
  var recording=Clone(plan.Recording??throw new ArgumentException("録画設定を選択してください。"));
  if(recording.IsNoRec()||recording.GetRecMode()==4)throw new ArgumentException("録画するプリセットを選択してください（無効・視聴モードは使用できません）。");
  if(!string.IsNullOrWhiteSpace(recording.BatFilePath)){if(!Path.IsPathFullyQualified(recording.BatFilePath)||!File.Exists(recording.BatFilePath)||!(recording.BatFilePath.EndsWith(".bat",StringComparison.OrdinalIgnoreCase)||recording.BatFilePath.EndsWith(".cmd",StringComparison.OrdinalIgnoreCase)))throw new ArgumentException("録画後BATは、このPCに存在する.batまたは.cmdを選択してください。");}
  return new EpgAutoAddData {searchInfo=new EpgSearchKeyInfo {andKey=key,notKey=(plan.Exclude??"").Trim(),titleOnlyFlag=1,serviceList=(plan.Services??[]).Distinct().Order().ToList()},recSetting=recording};
 }
 public static T Clone<T>(T value) where T:ICtrlCmdReadWrite,new()=>Unpack<T>(Pack(value,true),true);
 public async Task<List<ProgramMatch>> Preview(RulePlan plan,CancellationToken t=default){var rule=Build(plan);var events=Unpack<List<EpgEventInfo>>(await transport.Request(1025,Pack(new List<EpgSearchKeyInfo>{rule.searchInfo},false),t).ConfigureAwait(false),false);return events.Select(e=>new ProgramMatch(e.ShortInfo?.event_name??"番組名なし",e.start_time,e.durationSec,e.original_network_id,e.transport_stream_id,e.service_id)).OrderBy(e=>e.Start).ToList();}
 static string Keyword(string key)=>(key??"").Trim().Normalize().ToUpperInvariant();
 public static bool Equivalent(EpgAutoAddData first,EpgAutoAddData second){
  var a=Clone(first.searchInfo);var b=Clone(second.searchInfo);a.andKey=Keyword(a.andKey);b.andKey=Keyword(b.andKey);a.serviceList=a.serviceList.Distinct().Order().ToList();b.serviceList=b.serviceList.Distinct().Order().ToList();
  return Pack(a,true).SequenceEqual(Pack(b,true))&&Pack(first.recSetting,true).SequenceEqual(Pack(second.recSetting,true));
 }
 public async Task<uint> Register(RulePlan plan,string data,CancellationToken token=default)=>(await RegisterDetailed(plan,data,token).ConfigureAwait(false)).Id;
 public async Task<RegistrationReceipt> RegisterDetailed(RulePlan plan,string data,CancellationToken token=default){
  var rule=Build(plan);await gate.WaitAsync(token);try{
   Directory.CreateDirectory(data);using var lease=new FileStream(Path.Combine(data,"register.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
   var before=await Rules(token).ConfigureAwait(false);var existing=before.Where(r=>Keyword(r.searchInfo.andKey)==Keyword(rule.searchInfo.andKey)).ToList();
   if(existing.Count==1&&existing[0].dataID>0&&Equivalent(existing[0],rule))return new(existing[0].dataID,true);
   if(existing.Count>0)throw new InvalidOperationException("同じ検索ワードの自動予約が既にありますが、録画設定・BAT・放送局などの条件が異なるか複数あります。EPGTimerのキーワード自動予約で確認してください（ID："+string.Join(", ",existing.Select(r=>r.dataID))+"）。既存条件は変更していません。");
   await transport.Request(2132,Pack(new List<EpgAutoAddData>{rule},true),token).ConfigureAwait(false);
   var after=await Rules(token).ConfigureAwait(false);var found=after.Where(r=>r.dataID>0&&!before.Any(b=>b.dataID==r.dataID)&&Equivalent(r,rule)).ToList();
   if(found.Count!=1)throw new IOException("送信は完了しましたが登録IDと条件を確認できません。EPGTimerのキーワード自動予約を確認してください。再送信はしていません。");return new(found[0].dataID,false);
  }finally{gate.Release();}
 }
}
