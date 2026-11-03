using EpgTimer;
namespace AnimeReserve;
public static class DraftFactory {
 // EDCB's partialReceptionFlag identifies 1seg; service names/types alone do not.
 public static List<long> AllChannels(IEnumerable<EpgServiceInfo> services){var keys=services.GroupBy(Edcb.ServiceKey).Where(g=>g.All(s=>s.partialReceptionFlag==0)).Select(g=>g.Key).Order().ToList();if(keys.Count==0)throw new InvalidOperationException("ワンセグを除いた対象の放送局がありません。EDCBのサービス情報と接続を確認してください。");return keys;}
 public static RulePlan Create(Anime anime,IEnumerable<EpgServiceInfo> services,RecSettingData recording,string batOverride=null){var rec=Edcb.Clone(recording);if(!string.IsNullOrEmpty(batOverride))rec.BatFilePath=batOverride;var plan=new RulePlan(anime.Id,anime.Title,anime.Title,"",AllChannels(services),rec);Edcb.Build(plan);return plan;}
 public static RulePlan WithAllChannels(RulePlan plan,IEnumerable<EpgServiceInfo> services)=>plan with {Services=AllChannels(services)};
 public static List<Draft> CreateMissing(IEnumerable<Anime> anime,IEnumerable<Draft> existing,IEnumerable<EpgServiceInfo> services,RecSettingData recording,string batOverride=null){
  var known=new HashSet<int>(existing.Select(d=>d.Plan.AnimeId));var channels=services.ToArray();var prepared=new List<Draft>();
  foreach(var work in anime)if(known.Add(work.Id))prepared.Add(new Draft {Plan=Create(work,channels,recording,batOverride)});
  return prepared;
 }
}
