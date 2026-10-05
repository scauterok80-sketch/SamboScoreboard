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

    [Theory]
    [InlineData("+98 кг",98.0,true)]
    [InlineData("+98 кг",98.1,false)]
    [InlineData("свыше 80 кг",79.9,true)]
    [InlineData("свыше 80 кг",81.0,false)]
    public void OpenEndedWeightMismatchIsDetected(string category,double actual,bool expected){
        Assert.Equal(expected,WeightRules.IsMismatch(category,actual,out _));
    }

    [Fact]
    public void EligibilityRejectsUnweighedOrMismatchedAthlete(){
        var cat=new CategoryRow(1,"Спортивное самбо","Мужчины","18+","71 кг","Круговая","","",false,"Подготовка");
        var a=new Athlete(1,"ИВАНОВ Иван Иванович","","Мужской","","","","","","Спортивное самбо","18+","71 кг",70,null,"Допущен",1);
        Assert.Contains(EligibilityRules.ValidateForDraw(cat,new[]{a}),x=>x.Contains("взвешивания"));
        var b=a with{ActualWeight=72};
        Assert.Contains(EligibilityRules.ValidateForDraw(cat,new[]{b}),x=>x.Contains("превышает"));
    }

    [Fact]
    public void DuplicateIdentityBlocksDraw(){
        var cat=new CategoryRow(1,"Спортивное самбо","Мужчины","18+","71 кг","Круговая","","",false,"Подготовка");
        var a=new Athlete(1,"ИВАНОВ Иван Иванович","2000-01-01","Мужской","","","","","","Спортивное самбо","18+","71 кг",70,70,"Допущен",1);
        var b=a with{Id=2};
        Assert.Contains(EligibilityRules.ValidateForDraw(cat,new[]{a,b}),x=>x.StartsWith("Дубликат"));
    }

    [Fact]
    public void ApprovedDrawBlocksEligibilityMutation(){
        var db=new Database(TempDb());
        var cid=db.AddCategory("Спортивное самбо","Мужчины","18+","71 кг","Круговая","Без утешительных встреч","Полностью автоматическая");
        var aid=db.AddAthlete("иванов иван иванович",discipline:"Спортивное самбо",age:"18+",weight:"71 кг",categoryId:cid);
        db.SetWeigh(aid,70,"Допущен");db.ApproveDraw(cid,true);
        Assert.Throws<InvalidOperationException>(()=>db.SetWeigh(aid,70.2,"Допущен"));
        Assert.Throws<InvalidOperationException>(()=>db.UpdateAthleteStatus(aid,"Не допущен"));
    }

    [Fact]
    public void CategoryWithDataCannotBeDeleted(){
        var db=new Database(TempDb());
        var cid=db.AddCategory("Спортивное самбо","Мужчины","18+","71 кг","Круговая","","");
        db.AddAthlete("иванов иван",categoryId:cid);
        Assert.Throws<InvalidOperationException>(()=>db.DeleteCategory(cid));
    }

    [Fact]
    public void DisplayNumberAndScheduleDoNotChangeBracketOrder(){
        var db=new Database(TempDb());
        var cid=db.AddCategory("Спортивное самбо","Мужчины","18+","71 кг","Олимпийская","Без утешительных встреч","Полностью автоматическая");
        long a=db.AddAthlete("альфа один",categoryId:cid),b=db.AddAthlete("бета два",categoryId:cid);
        long id=db.AddBout(cid,7,"1/4",a,b,1);
        db.SetBoutStatus(id,"Готов",2,"14:35",101);
        var x=Assert.Single(db.Bouts(cid));
        Assert.Equal(7,x.BoutNo);Assert.Equal(101,x.DisplayNo);Assert.Equal("14:35",x.ScheduledTime);Assert.Equal(2,x.Mat);
    }

    [Fact]
    public void DatabaseHandlesRealistic250ParticipantEvent(){
        var db=new Database(TempDb());int total=0;
        for(int ci=0;ci<10;ci++){
            string weight=(50+ci*5)+" кг";
            var cid=db.AddCategory("Спортивное самбо",ci%2==0?"Мужчины":"Женщины","18+",weight,"Олимпийская","От финалистов","Полностью автоматическая");
            for(int i=0;i<25;i++){
                string sex=ci%2==0?"Мужской":"Женский";
                var id=db.AddAthlete($"спортсмен{ci}_{i} имя отчество",birth:$"200{i%10}-01-01",gender:sex,region:"Регион "+(i%8),team:"Команда "+(i%12),discipline:"Спортивное самбо",age:"18+",weight:weight,categoryId:cid);
                db.SetWeigh(id,49+ci*5,"Допущен");total++;
            }
        }
        Assert.Equal(250,total);Assert.Equal(250,db.Athletes().Count);Assert.Equal(10,db.Categories().Count);
    }

    [Fact]
    public void AdmissionReasonPersists(){
        var db=new Database(TempDb());var id=db.AddAthlete("петров петр петрович");
        db.UpdateAthleteStatus(id,"Не допущен","нет медицинского допуска");
        var a=Assert.Single(db.Athletes());
        Assert.Equal("Не допущен",a.Status);Assert.Equal("нет медицинского допуска",a.StatusReason);
    }
}
