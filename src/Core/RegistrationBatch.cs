using EpgTimer;
namespace AnimeReserve;
public record RegistrationSummary(int Added,int Existing,int Failed){
 public string Message=>$"登録結果：新規 {Added}作品 / 既存条件と一致 {Existing}作品 / 失敗 {Failed}作品。"+(Failed>0?"失敗した作品の理由を一覧で確認してください。後続の作品も処理しました。":"EPGTimerの「キーワード自動予約」で確認できます。番組表に一致する番組がない場合、録画予約の一覧にはまだ表示されません。");
}
public static class RegistrationBatch {
 public static async Task<RegistrationSummary> Run(Edcb edcb,Preferences preferences,IEnumerable<EpgServiceInfo> services,string data,Func<Task> save,Action<int,int,Draft> progress,CancellationToken token=default){
  var pending=preferences.Drafts.Where(d=>d.RegisteredId==0).ToList();var channels=services.ToList();int added=0,existing=0,failed=0;
  for(int i=0;i<pending.Count;i++) {
   token.ThrowIfCancellationRequested();var draft=pending[i];progress?.Invoke(i+1,pending.Count,draft);
   try {
    draft.Plan=DraftFactory.WithAllChannels(draft.Plan,channels);
    var result=await edcb.RegisterDetailed(draft.Plan,data,token);draft.RegisteredId=result.Id;draft.Result=result.AlreadyExists?"既存の自動予約と条件一致（確認済み）":"登録完了（EDCBから読戻し確認済み）";
    if(result.AlreadyExists)existing++;else added++;
   }catch(OperationCanceledException){throw;}
   catch(Exception e){draft.Result="登録失敗："+e.Message;failed++;}
   // Do not continue sending if the confirmed IDs cannot be saved.
   await save();
  }
  return new(added,existing,failed);
 }
}
