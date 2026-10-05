using ClosedXML.Excel;
using Xunit;
using SamboSecretary;

public class DataTests{
    static string TempDb()=>Path.Combine(Path.GetTempPath(),"SamboSecretaryTests",Guid.NewGuid()+".db");

    [Fact]
    public void TournamentCanBeSavedAndRead(){
        var db=new Database(TempDb());
        db.SaveTournament("Кубок","Москва","2026-10-10","2026-10-11",3,"ИВАНОВ Иван","ПЕТРОВ Петр");
        var t=db.GetTournament();
        Assert.Equal("Кубок",t.Name);Assert.Equal("Москва",t.Place);Assert.Equal(3,t.Mats);
    }

    [Fact]
    public void CategoryAndAthleteAreStored(){
        var db=new Database(TempDb());
        var cid=db.AddCategory("Спортивное самбо","Мужчины","18+","71 кг","Круговая","По положению","Полностью автоматическая");
        var aid=db.AddAthlete("петров пЕТР пЕТРОВИЧ","01.01.2000","Мужской","Москва","СШОР","Москва","Иванов","КМС","Спортивное самбо","18+","71 кг",70.5,cid);
        var a=db.Athletes(cid).Single();
        Assert.Equal(aid,a.Id);Assert.Equal("ПЕТРОВ Петр Петрович",a.FullName);Assert.Equal(cid,a.CategoryId);
    }

    [Fact]
    public void WeighInChangesStatus(){
        var db=new Database(TempDb());var id=db.AddAthlete("Иванов Иван");
        db.SetWeigh(id,68.4,"Допущен");var a=db.Athletes().Single();
        Assert.Equal(68.4,a.ActualWeight);Assert.Equal("Допущен",a.Status);
    }

    [Fact]
    public void DrawPositionsPreserveBye(){
        var db=new Database(TempDb());var cid=db.AddCategory("Спортивное самбо","Мужчины","18+","71 кг","Олимпийская","От финалистов","Полностью автоматическая");
        var id=db.AddAthlete("Иванов Иван");
        db.AddDrawPosition(cid,1,id,"Сетка");db.AddDrawPosition(cid,2,null,"Сетка");
        var p=db.DrawPositions(cid);
        Assert.Equal(id,p[0].AthleteId);Assert.Null(p[1].AthleteId);
    }

    [Fact]
    public void ExcelImporterReadsHeadersAndRows(){
        var file=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".xlsx");
        using(var wb=new XLWorkbook()){var ws=wb.AddWorksheet("Заявка");ws.Cell(1,1).Value="Фамилия";ws.Cell(1,2).Value="Имя";ws.Cell(2,1).Value="петров";ws.Cell(2,2).Value="ПЕТР";wb.SaveAs(file);}
        var x=ExcelImporter.Read(file);
        Assert.Contains("Фамилия",x.Headers);Assert.Single(x.Rows);Assert.Equal("петров",ExcelImporter.Value(x.Rows[0],"Фамилия"));
        File.Delete(file);
    }
}