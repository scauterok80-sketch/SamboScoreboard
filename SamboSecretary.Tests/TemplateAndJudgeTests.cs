using System;
using System.IO;
using Xunit;
using SamboSecretary;

public class TemplateAndJudgeTests{
    static string TempDb()=>Path.Combine(Path.GetTempPath(),"SamboSecretaryTests",Guid.NewGuid()+".db");

    [Fact]
    public void ProtocolTemplateTextPersists(){
        var db=new Database(TempDb());
        db.SaveTournament("Кубок","Москва","2026-10-10","2026-10-11",3,"Судья","Секретарь","7,5,3,1","Федерация региона","Подпись / печать");
        var t=db.GetTournament();
        Assert.Equal("Федерация региона",t.ProtocolHeader);
        Assert.Equal("Подпись / печать",t.ProtocolFooter);
    }

    [Fact]
    public void JudgeNotesCanBeStoredAndEdited(){
        var db=new Database(TempDb());
        var id=db.AddJudge("иванов иван иванович","Москва","ВК","Арбитр","резерв");
        var j=Assert.Single(db.Judges());
        Assert.Equal("резерв",j.Notes);
        db.UpdateJudge(id,j.Name,j.Region,j.Category,"Руководитель ковра","ковёр 1");
        j=Assert.Single(db.Judges());
        Assert.Equal("Руководитель ковра",j.Role);
        Assert.Equal("ковёр 1",j.Notes);
    }
}