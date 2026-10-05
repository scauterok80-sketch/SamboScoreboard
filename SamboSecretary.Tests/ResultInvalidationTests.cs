using System;
using System.IO;
using Xunit;
using SamboSecretary;

public class ResultInvalidationTests{
    static string TempDb()=>Path.Combine(Path.GetTempPath(),"SamboSecretaryTests",Guid.NewGuid()+".db");

    [Fact]
    public void EditingBoutResultInvalidatesSavedPlacements(){
        var db=new Database(TempDb());
        var cid=db.AddCategory("Спортивное самбо","Мужчины","18+","71 кг","Олимпийская","Без утешительных встреч","Полностью автоматическая");
        long a=db.AddAthlete("альфа один",categoryId:cid),b=db.AddAthlete("бета два",categoryId:cid);
        long bout=db.AddBout(cid,1,"Финал",a,b,1);
        db.SetBoutResult(bout,4,0,a,"По очкам","","3:0",3,0,240,false);
        db.SavePlacement(cid,a,1,"Победитель");db.SavePlacement(cid,b,2,"Финалист");db.SetCategoryStatus(cid,"Завершена");
        Assert.Equal(2,db.Placements(cid).Count);
        db.SetBoutResult(bout,0,4,b,"По очкам","","3:0",0,3,240,false);
        Assert.Empty(db.Placements(cid));
        Assert.Equal("Проводится",Assert.Single(db.Categories()).Status);
    }
}