using EpgTimer;
namespace AnimeReserve;
public enum BroadcastBand { Terrestrial, BS, CS, SKY, Other }
public static class Channels {
 public static BroadcastBand Band(ushort onid)=>onid is >=0x7880 and <=0x7fe8?BroadcastBand.Terrestrial:onid is 4 or 11?BroadcastBand.BS:onid is 6 or 7 or 12?BroadcastBand.CS:onid is 1 or 3 or 10?BroadcastBand.SKY:BroadcastBand.Other;
 public static string Name(BroadcastBand band)=>band switch {BroadcastBand.Terrestrial=>"地デジ",BroadcastBand.BS=>"BS",BroadcastBand.CS=>"CS",BroadcastBand.SKY=>"SKY",_=>"その他"};
 public static List<EpgServiceInfo> Eligible(IEnumerable<EpgServiceInfo> services)=>services.GroupBy(Edcb.ServiceKey).Where(g=>g.All(s=>s.partialReceptionFlag==0)).Select(g=>g.First()).OrderBy(s=>Band(s.ONID)).ThenBy(s=>s.service_name).ThenBy(Edcb.ServiceKey).ToList();
 public static List<long> Initial(Preferences preferences,IEnumerable<EpgServiceInfo> services)=>preferences.DefaultServices.Count>0?preferences.DefaultServices.Distinct().Order().ToList():Eligible(services).Where(s=>Band(s.ONID)==BroadcastBand.Terrestrial).Select(Edcb.ServiceKey).Order().ToList();
 public static List<long> Resolve(IEnumerable<long> selected,IEnumerable<EpgServiceInfo> services) {
  var available=Eligible(services).Select(Edcb.ServiceKey).ToHashSet();var result=(selected??[]).Where(available.Contains).Distinct().Order().ToList();
  if(result.Count==0)throw new InvalidOperationException("録画対象の放送局が未選択、または選択した局をEDCBから取得できません。地デジ・BS・CS・SKYの対象局を1局以上選んでください（ワンセグは対象外）。");return result;
 }
 public static List<long> SetBand(IEnumerable<long> selected,IEnumerable<EpgServiceInfo> services,BroadcastBand band,bool enabled) {
  var result=selected.ToHashSet();var group=Eligible(services).Where(s=>Band(s.ONID)==band).Select(Edcb.ServiceKey);if(enabled)result.UnionWith(group);else result.ExceptWith(group);return result.Order().ToList();
 }
 public static string Summary(IEnumerable<long> selected) {
  var groups=(selected??[]).Distinct().GroupBy(k=>Band((ushort)(k>>32))).OrderBy(g=>g.Key).Select(g=>Name(g.Key)+" "+g.Count()+"局").ToList();return groups.Count==0?"未選択（登録には1局以上必要）":string.Join(" / ",groups);
 }
}
