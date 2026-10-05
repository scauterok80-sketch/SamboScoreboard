using System;
using System.IO;
using System.Linq;
using Xunit;
using SamboSecretary;

public class FullCycleTests{
    static string TempDb()=>Path.Combine(Path.GetTempPath(),"SamboSecretaryTests",Guid.NewGuid()+".db");

    [Fact]
    public void RoundRobinCategoryRunsFromRegistrationToRanking(){
        var db=new Database(TempDb());
        var cid=db.AddCategory("Спортивное самбо","Мужчины","18+","71 кг","Круговая","Без утешительных встреч","Полностью автоматическая");
        var ids=Enumerable.Range(1,4).Select(i=>db.AddAthlete($"спортсмен{i} имя",team:"Команда "+i,discipline:"Спортивное самбо",age:"18+",weight:"71 кг",categoryId:cid)).ToList();
        foreach(var id in ids)db.SetWeigh(id,70.0,"Допущен");
        var schedule=TournamentEngine.RoundRobin(ids);foreach(var x in schedule)db.AddBout(cid,x.No,x.Stage,x.Red,x.Blue,1);
        foreach(var b in db.Bouts(cid)){
            long winner=Math.Min(b.RedId!.Value,b.BlueId!.Value);bool red=winner==b.RedId;
            var cp=CompetitionRules.ClassificationFor("3:1",4,2,red,"По очкам");
            db.SetBoutResult(b.Id,red?4:2,red?2:4,winner,"По очкам","Арбитр",cp.Code,cp.Red,cp.Blue,240,false,red?1:0,red?0:1,0,red?0:1,red?1:0,0);
        }
        var ranking=CompetitionRules.RankRoundRobin(ids,db.BoutRules(cid));
        Assert.Equal(ids,ranking.Ordered);Assert.Empty(ranking.Unresolved);
        Assert.All(db.BoutRules(cid),b=>Assert.Equal("Завершён",b.Status));
    }

    [Fact]
    public void ChangingWinnerCanReplaceUnplayedFutureParticipant(){
        var db=new Database(TempDb());
        var cid=db.AddCategory("Спортивное самбо","Мужчины","18+","71 кг","Олимпийская","Без утешительных встреч","Полностью автоматическая");
        long a=db.AddAthlete("альфа один",categoryId:cid),b=db.AddAthlete("бета два",categoryId:cid),c=db.AddAthlete("гамма три",categoryId:cid);
        long q=db.AddBout(cid,1,"1/4",a,b,1);long semi=db.AddBout(cid,2,"Полуфинал",a,c,1);
        db.SetBoutResult(q,4,2,a,"По очкам","","3:1",3,1,240,false);
        Assert.False(db.HasCompletedFutureDependency(cid,a,1));
        db.ReplaceFutureParticipant(cid,a,b,1);
        var s=db.Bouts(cid).Single(x=>x.Id==semi);
        Assert.Equal(b,s.RedId);
    }

    [Fact]
    public void ResultPersistsActionQualityAndDuration(){
        var db=new Database(TempDb());
        var cid=db.AddCategory("Спортивное самбо","Мужчины","18+","71 кг","Круговая","Без утешительных встреч","Полностью автоматическая");
        long a=db.AddAthlete("альфа один",categoryId:cid),b=db.AddAthlete("бета два",categoryId:cid);
        long bout=db.AddBout(cid,1,"Круг 1",a,b,1);
        db.SetBoutResult(bout,7,2,a,"По очкам","Судья","3:1",3,1,173,false,1,1,1,0,1,0);
        var x=db.BoutsByRuleId(bout);
        Assert.Equal(173,x.DurationSeconds);Assert.Equal(1,x.Red4);Assert.Equal(1,x.Red2);Assert.Equal(1,x.Red1);Assert.Equal("3:1",x.ResultCode);
    }

    [Fact]
    public void DisqualifiedLoserDoesNotEnterConsolation(){
        var b=new BoutRuleRow(1,1,1,"1/8",1,2,"Завершён",0,0,1,"4:0",4,0,0,false,0,0,0,0,0,0,"Дисквалификация");
        Assert.Empty(CompetitionRules.DirectLossesTo(1,new[]{b},false));
        Assert.True(CompetitionRules.IsDisqualified(2,new[]{b}));
    }
}