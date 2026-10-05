using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Xunit;
using SamboSecretary;

public class MigrationTests{
    static string TempDb()=>Path.Combine(Path.GetTempPath(),"SamboSecretaryTests",Guid.NewGuid()+".db");

    [Fact]
    public void OldDatabaseIsUpgradedWithoutLosingCoreData(){
        var file=TempDb();Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        using(var c=new SqliteConnection($"Data Source={file}")){
            c.Open();using var q=c.CreateCommand();
            q.CommandText=@"
CREATE TABLE tournament(id INTEGER PRIMARY KEY,name TEXT,place TEXT,start_date TEXT,end_date TEXT,mats INTEGER,chief_referee TEXT,chief_secretary TEXT,team_scheme TEXT);
INSERT INTO tournament VALUES(1,'Старый турнир','Москва','2026-01-01','2026-01-02',2,'ГС','ГСЕК','7,5,3,1');
CREATE TABLE athletes(id INTEGER PRIMARY KEY AUTOINCREMENT,full_name TEXT,birth_date TEXT,gender TEXT,region TEXT,organization TEXT,team TEXT,coach TEXT,rank TEXT,discipline TEXT,age_group TEXT,weight_category TEXT,declared_weight REAL,actual_weight REAL,status TEXT,category_id INTEGER);
INSERT INTO athletes(full_name,status) VALUES('ИВАНОВ Иван Иванович','Заявлен');
CREATE TABLE judges(id INTEGER PRIMARY KEY AUTOINCREMENT,name TEXT,region TEXT,category TEXT,role TEXT);
INSERT INTO judges(name,region,category,role) VALUES('ПЕТРОВ Петр Петрович','Москва','ВК','Арбитр');";
            q.ExecuteNonQuery();
        }
        var db=new Database(file);
        var t=db.GetTournament();
        Assert.Equal("Старый турнир",t.Name);Assert.Equal("",t.ProtocolHeader);Assert.Equal("",t.ProtocolFooter);
        var a=Assert.Single(db.Athletes());Assert.Equal("ИВАНОВ Иван Иванович",a.FullName);Assert.Equal("",a.StatusReason);
        var j=Assert.Single(db.Judges());Assert.Equal("ПЕТРОВ Петр Петрович",j.Name);Assert.Equal("",j.Notes);
        db.SaveTournament(t.Name,t.Place,t.StartDate,t.EndDate,t.Mats,t.ChiefReferee,t.ChiefSecretary,t.TeamScheme,"Новая шапка","Новый подвал");
        db.UpdateJudge(j.Id,j.Name,j.Region,j.Category,j.Role,"примечание");
        Assert.Equal("Новая шапка",db.GetTournament().ProtocolHeader);Assert.Equal("примечание",Assert.Single(db.Judges()).Notes);
    }
}