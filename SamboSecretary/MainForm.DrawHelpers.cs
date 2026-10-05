namespace SamboSecretary;

public sealed partial class MainForm{
    List<Athlete> SeparateMixedOrder(List<Athlete> athletes,int aSize,int bSize){
        var rng=new Random();var a=new List<Athlete>();var b=new List<Athlete>();
        var groups=athletes.OrderBy(_=>rng.Next()).GroupBy(x=>string.IsNullOrWhiteSpace(x.Team)?$"__{x.Id}":x.Team,StringComparer.OrdinalIgnoreCase).OrderByDescending(g=>g.Count()).ToList();
        foreach(var g in groups){
            foreach(var athlete in g.OrderBy(_=>rng.Next())){
                int inA=a.Count(x=>!string.IsNullOrWhiteSpace(athlete.Team)&&string.Equals(x.Team,athlete.Team,StringComparison.OrdinalIgnoreCase));
                int inB=b.Count(x=>!string.IsNullOrWhiteSpace(athlete.Team)&&string.Equals(x.Team,athlete.Team,StringComparison.OrdinalIgnoreCase));
                if(a.Count>=aSize)b.Add(athlete);
                else if(b.Count>=bSize)a.Add(athlete);
                else if(inA<inB)a.Add(athlete);
                else if(inB<inA)b.Add(athlete);
                else if(a.Count/(double)aSize<=b.Count/(double)bSize)a.Add(athlete);else b.Add(athlete);
            }
        }
        return a.Concat(b).ToList();
    }

    List<long?> BuildSeparatedOlympicSlots(List<Athlete> athletes,int size,Dictionary<long,int>? fixedPos){
        var rng=new Random();var slots=Enumerable.Repeat<long?>(null,size).ToList();var fixedIndexes=new HashSet<int>();
        fixedPos??=new Dictionary<long,int>();
        foreach(var kv in fixedPos){slots[kv.Value-1]=kv.Key;fixedIndexes.Add(kv.Value-1);}
        var rest=athletes.Where(a=>!fixedPos.ContainsKey(a.Id)).OrderBy(_=>rng.Next()).ToList();
        var emptyPairs=Enumerable.Range(0,size/2).Where(p=>slots[p*2]==null&&slots[p*2+1]==null).OrderBy(_=>rng.Next()).ToList();
        if(rest.Count<emptyPairs.Count)throw new InvalidOperationException("Фиксированный посев создаёт пустую пару BYE–BYE. Измените позиции сеяных спортсменов.");
        int k=0;
        foreach(var p in emptyPairs){
            int left=p*2,right=left+1;
            int target=rng.Next(2)==0?left:right;slots[target]=rest[k++].Id;
        }
        var free=Enumerable.Range(0,size).Where(i=>slots[i]==null).OrderBy(_=>rng.Next()).ToList();
        for(int i=0;k<rest.Count;i++,k++)slots[free[i]]=rest[k].Id;
        var teams=athletes.ToDictionary(a=>a.Id,a=>a.Team??"");
        int Conflicts(){
            int n=0;for(int i=0;i<size;i+=2){
                if(!slots[i].HasValue||!slots[i+1].HasValue)continue;
                var t1=teams[slots[i]!.Value];var t2=teams[slots[i+1]!.Value];
                if(!string.IsNullOrWhiteSpace(t1)&&string.Equals(t1,t2,StringComparison.OrdinalIgnoreCase))n++;
            }return n;
        }
        int best=Conflicts();
        var movable=Enumerable.Range(0,size).Where(i=>slots[i].HasValue&&!fixedIndexes.Contains(i)).ToList();
        bool improved=true;int guard=0;
        while(improved&&best>0&&guard++<100){
            improved=false;
            foreach(var i in movable)foreach(var j in movable.Where(x=>x>i)){
                (slots[i],slots[j])=(slots[j],slots[i]);int now=Conflicts();
                if(now<best){best=now;improved=true;goto NextRound;}
                (slots[i],slots[j])=(slots[j],slots[i]);
            }
            NextRound:;
        }
        return slots;
    }
}