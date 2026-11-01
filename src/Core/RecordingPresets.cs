using System.Runtime.InteropServices;
using System.Text;
using EpgTimer;
namespace AnimeReserve;
public record RecordingPreset(string Name,RecSettingData Setting);
public static class RecordingPresets {
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern uint GetPrivateProfileString(string section,string key,string fallback,StringBuilder result,uint size,string file);
 public static List<RecordingPreset> Load(string folder){
  string file=Path.GetFullPath(Path.Combine(folder,"EpgTimerSrv.ini"));if(!File.Exists(file))throw new FileNotFoundException("EpgTimerSrv.iniがあるEDCBフォルダーを選択してください。",file);
  string Read(string s,string k,string d=""){var b=new StringBuilder(32768);GetPrivateProfileString(s,k,d,b,32768,file);return b.ToString();}
  int Num(string s,string k,int d=0)=>int.TryParse(Read(s,k,d.ToString()),out int n)?n:d;
  List<RecordingPreset> result=[];foreach(var id in ("0,"+Read("SET","PresetID")).Split(',').Where(x=>uint.TryParse(x,out _)).Select(uint.Parse).Distinct()){
   string n=id==0?"":id.ToString(),s="REC_DEF"+n;byte mode=(byte)Num(s,"RecMode",1);if(mode/5%2!=0)mode=(byte)(5+(Num(s,"NoRecMode",1)+4)%5);
   var r=new RecSettingData {RecMode=mode,Priority=(byte)Num(s,"Priority",2),TuijyuuFlag=(byte)Num(s,"TuijyuuFlag",1),ServiceMode=(uint)Num(s,"ServiceMode"),PittariFlag=(byte)Num(s,"PittariFlag"),BatFilePath=Read(s,"BatFilePath"),SuspendMode=(byte)Num(s,"SuspendMode"),RebootFlag=(byte)Num(s,"RebootFlag"),UseMargineFlag=(byte)Num(s,"UseMargineFlag"),StartMargine=Num(s,"StartMargine"),EndMargine=Num(s,"EndMargine"),ContinueRecFlag=(byte)Num(s,"ContinueRec"),PartialRecFlag=(byte)Num(s,"PartialRec"),TunerID=(uint)Num(s,"TunerID")};
   foreach(var pair in new[]{("REC_DEF_FOLDER"+n,r.RecFolderList),("REC_DEF_FOLDER_1SEG"+n,r.PartialRecFolder)})for(int i=0;i<Math.Clamp(Num(pair.Item1,"Count"),0,100);i++)pair.Item2.Add(new RecFileSetInfo {RecFolder=Read(pair.Item1,i.ToString()),WritePlugIn=Read(pair.Item1,"WritePlugIn"+i,"Write_Default.dll"),RecNamePlugIn=Read(pair.Item1,"RecNamePlugIn"+i)});
   result.Add(new(id==0?"デフォルト":Read(s,"SetName","プリセット "+id),r));
  }return result;
 }
}
