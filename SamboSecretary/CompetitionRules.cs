namespace SamboSecretary;

public record ClassificationResult(string Code,int Red,int Blue,bool Clean);
public record RankMetric(long AthleteId,int ClassPoints,int Wins,int MutualWins,int MutualClass,int CleanWins,int CleanWinSeconds,int NonCleanFourZero,int Win30,int Win31,int Win20,int LossClass,int CleanLosses,int CleanLossSeconds,int Tech4,int Tech2,int Tech1,int TechPoints,int TechDiff);
public record RankingResult(List<long> Ordered,List<List<long>> Unresolved,Dictionary<long,RankMetric> Metrics);

public static class CompetitionRules{
    public static ClassificationResult ClassificationFor(string resultCode,int redScore,int blueScore,bool redWon,string reason){
        string code=resultCode?.Trim()??"";
        bool clean=reason is "Чистая победа" or "Болевой приём" or "Удушающий приём" or "Явное преимущество" or "Нокаут" or "Два нокдауна" or "Потеря сознания при удушающем";
        if(string.IsNullOrWhiteSpace(code)){
            if(clean||reason.Contains("Дисквали",StringComparison.OrdinalIgnoreCase)||reason.Contains("Снятие врачом",StringComparison.OrdinalIgnoreCase)||reason.Contains("Неявка",StringComparison.OrdinalIgnoreCase)||reason.Contains("Техничес",StringComparison.OrdinalIgnoreCase)) code="4:0";
            else{
                int winner=redWon?redScore:blueScore, loser=redWon?blueScore:redScore;
                int diff=Math.Abs(redScore-blueScore);
                if(diff==0) code="2:0";
                else code=loser>0?"3:1":"3:0";
            }
        }
        (int w,int l)=code switch{"4:0"=>(4,0),"3:1"=>(3,1),"3:0"=>(3,0),"2:0"=>(2,0),"0:0"=>(0,0),_=>(0,0)};
        return redWon?new(code,w,l,clean):new(code,l,w,clean);
    }

    public static RankingResult RankRoundRobin(IReadOnlyCollection<long> athletes,IReadOnlyCollection<BoutRuleRow> allBouts){
        var bouts=allBouts.Where(b=>b.Status=="Завершён"&&b.RedId.HasValue&&b.BlueId.HasValue&&b.WinnerId.HasValue).ToList();
        var metrics=athletes.ToDictionary(id=>id,id=>BuildMetric(id,athletes,bouts));
        var ordered=new List<long>();
        var unresolved=new List<List<long>>();
        foreach(var totalGroup in athletes.GroupBy(id=>metrics[id].ClassPoints).OrderByDescending(g=>g.Key)){
            var ids=totalGroup.ToList();
            if(ids.Count==1){ordered.Add(ids[0]);continue;}
            if(ids.Count==2){
                var h=bouts.FirstOrDefault(b=>(b.RedId==ids[0]&&b.BlueId==ids[1])||(b.RedId==ids[1]&&b.BlueId==ids[0]));
                if(h?.WinnerId is long w){ordered.Add(w);ordered.Add(ids.First(x=>x!=w));continue;}
            }
            var groupMetrics=ids.ToDictionary(id=>id,id=>BuildMetric(id,ids,bouts));
            var sorted=ids.OrderByDescending(id=>groupMetrics[id].MutualWins)
                .ThenByDescending(id=>groupMetrics[id].MutualClass)
                .ThenByDescending(id=>groupMetrics[id].CleanWins)
                .ThenBy(id=>groupMetrics[id].CleanWinSeconds)
                .ThenByDescending(id=>groupMetrics[id].NonCleanFourZero)
                .ThenByDescending(id=>groupMetrics[id].Win30)
                .ThenByDescending(id=>groupMetrics[id].Win31)
                .ThenByDescending(id=>groupMetrics[id].Win20)
                .ThenByDescending(id=>groupMetrics[id].LossClass)
                .ThenBy(id=>groupMetrics[id].CleanLosses)
                .ThenByDescending(id=>groupMetrics[id].CleanLossSeconds)
                .ThenByDescending(id=>groupMetrics[id].Tech4)
                .ThenByDescending(id=>groupMetrics[id].Tech2)
                .ThenByDescending(id=>groupMetrics[id].Tech1)
                .ThenByDescending(id=>groupMetrics[id].TechPoints)
                .ThenByDescending(id=>groupMetrics[id].TechDiff)
                .ThenByDescending(id=>metrics[id].Wins)
                .ThenByDescending(id=>metrics[id].CleanWins)
                .ThenBy(id=>metrics[id].CleanWinSeconds)
                .ThenByDescending(id=>metrics[id].Win30)
                .ThenByDescending(id=>metrics[id].Win31)
                .ThenByDescending(id=>metrics[id].Win20)
                .ToList();
            int i=0;
            while(i<sorted.Count){
                var key=TieKey(sorted[i],groupMetrics,metrics);
                var tied=new List<long>{sorted[i]};int j=i+1;
                while(j<sorted.Count&&TieKey(sorted[j],groupMetrics,metrics)==key){tied.Add(sorted[j]);j++;}
                if(tied.Count==2){
                    var h=bouts.FirstOrDefault(b=>(b.RedId==tied[0]&&b.BlueId==tied[1])||(b.RedId==tied[1]&&b.BlueId==tied[0]));
                    if(h?.WinnerId is long w){ordered.Add(w);ordered.Add(tied.First(x=>x!=w));}
                    else{ordered.AddRange(tied);unresolved.Add(tied);}
                }else{
                    ordered.AddRange(tied);
                    if(tied.Count>1)unresolved.Add(tied);
                }
                i=j;
            }
        }
        return new(ordered,unresolved,metrics);
    }

    static string TieKey(long id,Dictionary<long,RankMetric> group,Dictionary<long,RankMetric> all){
        var g=group[id];var a=all[id];
        return string.Join("|",g.MutualWins,g.MutualClass,g.CleanWins,g.CleanWinSeconds,g.NonCleanFourZero,g.Win30,g.Win31,g.Win20,g.LossClass,g.CleanLosses,g.CleanLossSeconds,g.Tech4,g.Tech2,g.Tech1,g.TechPoints,g.TechDiff,a.Wins,a.CleanWins,a.CleanWinSeconds,a.Win30,a.Win31,a.Win20,a.Tech4,a.Tech2,a.Tech1);
    }

    static RankMetric BuildMetric(long id,IReadOnlyCollection<long> comparisonSet,List<BoutRuleRow> bouts){
        int classPoints=0,wins=0,mutualWins=0,mutualClass=0,cleanWins=0,cleanWinSeconds=0,nonClean40=0,win30=0,win31=0,win20=0,lossClass=0,cleanLosses=0,cleanLossSeconds=0,tech4=0,tech2=0,tech1=0,tech=0,diff=0;
        foreach(var b in bouts.Where(b=>b.RedId==id||b.BlueId==id)){
            bool red=b.RedId==id;long opponent=red?b.BlueId!.Value:b.RedId!.Value;int ownClass=red?b.RedClass:b.BlueClass,oppClass=red?b.BlueClass:b.RedClass;int ownScore=red?b.RedScore:b.BlueScore,oppScore=red?b.BlueScore:b.RedScore;bool won=b.WinnerId==id;
            classPoints+=ownClass;tech+=ownScore;diff+=ownScore-oppScore;
            tech4+=red?b.Red4:b.Blue4;tech2+=red?b.Red2:b.Blue2;tech1+=red?b.Red1:b.Blue1;if(won)wins++;
            bool mutual=comparisonSet.Contains(opponent);
            if(mutual){mutualClass+=ownClass;if(won)mutualWins++;}
            string code=b.ResultCode;
            if(won){
                if(code=="4:0"){
                    if(b.IsClean){cleanWins++;cleanWinSeconds+=b.DurationSeconds;}else if(b.DurationSeconds==0)nonClean40++;
                }else if(code=="3:0")win30++;else if(code=="3:1")win31++;else if(code=="2:0")win20++;
            }else{
                lossClass+=ownClass;
                if(code=="4:0"&&b.IsClean){cleanLosses++;cleanLossSeconds+=b.DurationSeconds;}
            }
        }
        return new(id,classPoints,wins,mutualWins,mutualClass,cleanWins,cleanWinSeconds,nonClean40,win30,win31,win20,lossClass,cleanLosses,cleanLossSeconds,tech4,tech2,tech1,tech,diff);
    }


    public static Dictionary<long,int> ConsolationLossDepth(IEnumerable<BoutRuleRow> bouts){
        var completed=bouts.Where(b=>b.Status=="Завершён"&&b.WinnerId.HasValue&&b.RedId.HasValue&&b.BlueId.HasValue&&b.Stage.StartsWith("Утешение",StringComparison.OrdinalIgnoreCase)).ToList();
        var maxStep=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        foreach(var b in completed){
            var m=System.Text.RegularExpressions.Regex.Match(b.Stage,@"^(Утешение\s+(?:Ф|ПФ)-[^/]+\s*/).*?шаг\s*(\d+)",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if(!m.Success)continue;var prefix=m.Groups[1].Value.Trim();int step=int.Parse(m.Groups[2].Value);
            if(!maxStep.TryGetValue(prefix,out var old)||step>old)maxStep[prefix]=step;
        }
        var result=new Dictionary<long,int>();
        foreach(var b in completed){
            long loser=b.RedId==b.WinnerId?b.BlueId!.Value:b.RedId!.Value;int depth;
            if(b.Stage.StartsWith("Утешение группа",StringComparison.OrdinalIgnoreCase))depth=1;
            else{
                var m=System.Text.RegularExpressions.Regex.Match(b.Stage,@"^(Утешение\s+(?:Ф|ПФ)-[^/]+\s*/).*?шаг\s*(\d+)",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if(!m.Success)continue;var prefix=m.Groups[1].Value.Trim();int step=int.Parse(m.Groups[2].Value);int max=maxStep[prefix];
                bool semiSource=prefix.Contains("ПФ-",StringComparison.OrdinalIgnoreCase);
                depth=(max-step)+1+(semiSource?1:0);
            }
            if(!result.TryGetValue(loser,out var oldDepth)||depth<oldDepth)result[loser]=depth;
        }
        return result;
    }

    public static int StageOrder(string stage){
        if(stage.Contains("1/32"))return 1;if(stage.Contains("1/16"))return 2;if(stage.Contains("1/8"))return 3;if(stage.Contains("1/4"))return 4;if(stage.Contains("Полуфинал"))return 5;if(stage=="Финал")return 6;
        return 0;
    }

    public static bool CanContinueAfterLoss(BoutRuleRow b,long loserId){
        if(b.WinnerId==loserId)return true;
        var r=b.Reason??"";
        return !r.Contains("Дисквали",StringComparison.OrdinalIgnoreCase)
            && !r.Contains("Снятие врачом",StringComparison.OrdinalIgnoreCase)
            && !r.Contains("снят с соревнований",StringComparison.OrdinalIgnoreCase)
            && !r.Contains("травм",StringComparison.OrdinalIgnoreCase)
            && !r.Contains("Нокаут",StringComparison.OrdinalIgnoreCase)
            && !r.Contains("Два нокдауна",StringComparison.OrdinalIgnoreCase)
            && !r.Contains("Потеря сознания",StringComparison.OrdinalIgnoreCase);
    }
    public static bool IsDisqualified(long athleteId,IEnumerable<BoutRuleRow> bouts){
        return bouts.Any(b=>b.Status=="Завершён"&&(b.RedId==athleteId||b.BlueId==athleteId)&&b.WinnerId!=athleteId&&(b.Reason??"").Contains("Дисквали",StringComparison.OrdinalIgnoreCase));
    }
    public static List<long> DirectLossesTo(long winner,IEnumerable<BoutRuleRow> mainBouts,bool includeSemifinal=true){
        return mainBouts.Where(b=>b.Status=="Завершён"&&b.WinnerId==winner&&StageOrder(b.Stage)>0&&(includeSemifinal||!b.Stage.Contains("Полуфинал")))
            .OrderBy(b=>StageOrder(b.Stage))
            .Select(b=>(Bout:b,Loser:b.RedId==winner?b.BlueId!.Value:b.RedId!.Value))
            .Where(x=>CanContinueAfterLoss(x.Bout,x.Loser))
            .Select(x=>x.Loser).ToList();
    }
}