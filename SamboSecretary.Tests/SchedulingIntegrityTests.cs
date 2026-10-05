using System;
using System.IO;
using Xunit;
using SamboSecretary;

public class SchedulingIntegrityTests{
    static string TempDb()=>Path.Combine(Path.GetTempPath(),"SamboSecretaryTests",Guid.NewGuid()+".db");

    [Fact]
    public void EventRejectsSeventhMat(){
        var db=new Database(TempDb());
        Assert.Throws<ArgumentOutOfRangeException>(()=>db.SaveTournament("T","","","",7,"",""));
    }

    [Fact]
    public void BoutRejectsSeventhMatAndDuplicateVisibleNumber(){
        var db=new Database(TempDb());
        var cid=db.AddCategory("Спортивное самбо","Мужчины","18+","71 кг","Круговая","","");
        long a=db.AddAthlete("альфа один",categoryId:cid);
        long b=db.AddAthlete("бета два",categoryId:cid);
        long c=db.AddAthlete("гамма три",categoryId:cid);
        long d=db.AddAthlete("дельта четыре",categoryId:cid);
        long first=db.AddBout(cid,1,"Круг 1",a,b,1);
        long second=db.AddBout(cid,2,"Круг 1",c,d,1);
        Assert.Throws<ArgumentOutOfRangeException>(()=>db.SetBoutStatus(first,"Готов",7));
        db.SetBoutStatus(first,"Готов",1,"10:00",100);
        Assert.Throws<InvalidOperationException>(()=>db.SetBoutStatus(second,"Готов",1,"10:10",100));
    }
}