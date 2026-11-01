using EpgTimer;
namespace AnimeReserve;
public static class DraftFactory {
 public static List<long> AllChannels(IEnumerable<EpgServiceInfo> services){var keys=services.Select(Edcb.ServiceKey).Distinct().Order().ToList();if(keys.Count==0)throw new InvalidOperationException("EDCBから放送局を取得できませんでした。接続を確認してください。");return keys;}
 public static RulePlan Create(Anime anime,IEnumerable<EpgServiceInfo> services,RecSettingData recording,string batOverride=null){var rec=Edcb.Clone(recording);if(!string.IsNullOrEmpty(batOverride))rec.BatFilePath=batOverride;var plan=new RulePlan(anime.Id,anime.Title,anime.Title,"",AllChannels(services),rec);Edcb.Build(plan);return plan;}
 public static RulePlan WithAllChannels(RulePlan plan,IEnumerable<EpgServiceInfo> services)=>plan with {Services=AllChannels(services)};
 public static List<Draft> CreateMissing(IEnumerable<Anime> anime,IEnumerable<Draft> existing,IEnumerable<EpgServiceInfo> services,RecSettingData recording,string batOverride=null){
  var known=new HashSet<int>(existing.Select(d=>d.Plan.AnimeId));var channels=services.ToArray();var prepared=new List<Draft>();
  foreach(var work in anime)if(known.Add(work.Id))prepared.Add(new Draft {Plan=Create(work,channels,recording,batOverride)});
  return prepared;
 }
}
