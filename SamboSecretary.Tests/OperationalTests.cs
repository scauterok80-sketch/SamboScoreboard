using System;
using System.IO;
using Xunit;
using SamboSecretary;

public class OperationalTests{
    static string TempDb()=>Path.Combine(Path.GetTempPath(),"SamboSecretaryTests",Guid.NewGuid()+".db");

    [Theory]
    [InlineData("71 кг",71.1,true)]
    [InlineData("71 кг",70.9,false)]
    [InlineData("+98 кг",110,false)]
    public void WeightMismatchIsDetectedWithoutChangingCategory(string category,double actual,bool expected){
        Assert.Equal(expected,WeightRules.IsOverweight(category,actual,out _));
    }

    [Fact]
    public void JudgeCanBeAssignedToCategoryAndMat(){
        var db=new Database(TempDb());
        var cid=db.AddCategory("Спортивное самбо","Мужчины","18+","71 кг","Олимпийская","От финалистов","Полностью автоматическая");
        var jid=db.AddJudge("иванов иван иванович","Москва","ВК","Арбитр");
        db.AssignJudge(cid,jid,2,"Арбитр");
        var x=Assert.Single(db.JudgeAssignments(cid));
        Assert.Equal(2,x.Mat);Assert.Equal("ИВАНОВ Иван Иванович",x.JudgeName);
    }

    [Fact]
    public void PlacementIsPersisted(){
        var db=new Database(TempDb());
        var cid=db.AddCategory("Спортивное самбо","Мужчины","18+","71 кг","Круговая","Без утешительных встреч","Полностью автоматическая");
        var aid=db.AddAthlete("петров петр петрович",categoryId:cid);
        db.SavePlacement(cid,aid,1,"Тест");
        var p=Assert.Single(db.Placements(cid));
        Assert.Equal(1,p.Place);Assert.Equal("ПЕТРОВ Петр Петрович",p.Athlete);
    }

    [Fact]
    public void AmbiguousImportedNameIsRejected(){
        Assert.False(NameNormalizer.TryNormalizeImported(out _,out var error,"Петров"));
        Assert.NotEmpty(error);
        Assert.True(NameNormalizer.TryNormalizeImported(out var normalized,out _,"петров","ПЕТР","петрович"));
        Assert.Equal("ПЕТРОВ Петр Петрович",normalized);
    }
}