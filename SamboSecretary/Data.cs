using Microsoft.Data.Sqlite;
using ClosedXML.Excel;

namespace SamboSecretary;

public record Tournament(long Id,string Name,string Place,string StartDate,string EndDate,int Mats,string ChiefReferee,string ChiefSecretary);
public record CategoryRow(long Id,string Discipline,string Gender,string AgeGroup,string WeightCategory,string System,string Repechage,string DrawMode,bool DrawApproved,string Status);
public record Athlete(long Id,string FullName,string BirthDate,string Gender,string Region,string Organization,string Team,string Coach,string Rank,string Discipline,string AgeGroup,string WeightCategory,double? DeclaredWeight,double? ActualWeight,string Status,long? CategoryId);
public record JudgeRow(long Id,string Name,string Region,string Category,string Role);
public record BoutRow(long Id,long CategoryId,int BoutNo,string Stage,long? RedId,string RedName,long? BlueId,string BlueName,int Mat,string Status,int? RedScore,int? BlueScore,long? WinnerId,string WinnerName,string Reason,string Judges);

public sealed class Database {
    public string FileName { get; }
    public Database(string file){ FileName=file; Directory.CreateDirectory(Path.GetDirectoryName(file)!); Init(); }

    SqliteConnection Open(){ var c=new SqliteConnection($"Data Source={FileName}"); c.Open(); return c; }
    void Exec(SqliteConnection c,string sql){ using var q=c.CreateCommand(); q.CommandText=sql; q.ExecuteNonQuery(); }

    void Init(){
        using var c=Open();
        Exec(c,@"CREATE TABLE IF NOT EXISTS tournament(
            id INTEGER PRIMARY KEY CHECK(id=1),name TEXT,place TEXT,start_date TEXT,end_date TEXT,mats INTEGER DEFAULT 1,chief_referee TEXT,chief_secretary TEXT);
        INSERT OR IGNORE INTO tournament(id,name,place,start_date,end_date,mats,chief_referee,chief_secretary) VALUES(1,'','','','',1,'','');

        CREATE TABLE IF NOT EXISTS categories(
            id INTEGER PRIMARY KEY AUTOINCREMENT,discipline TEXT NOT NULL,gender TEXT,age_group TEXT,weight_category TEXT NOT NULL,
            system TEXT DEFAULT '',repechage TEXT DEFAULT '',draw_mode TEXT DEFAULT '',draw_approved INTEGER DEFAULT 0,status TEXT DEFAULT 'Подготовка');

        CREATE TABLE IF NOT EXISTS athletes(
            id INTEGER PRIMARY KEY AUTOINCREMENT,full_name TEXT NOT NULL,birth_date TEXT,gender TEXT,region TEXT,organization TEXT,team TEXT,coach TEXT,rank TEXT,
            discipline TEXT,age_group TEXT,weight_category TEXT,declared_weight REAL,actual_weight REAL,status TEXT DEFAULT 'Заявлен',category_id INTEGER);

        CREATE TABLE IF NOT EXISTS judges(
            id INTEGER PRIMARY KEY AUTOINCREMENT,name TEXT NOT NULL,region TEXT,category TEXT,role TEXT);

        CREATE TABLE IF NOT EXISTS draw_positions(
            id INTEGER PRIMARY KEY AUTOINCREMENT,category_id INTEGER NOT NULL,position INTEGER NOT NULL,athlete_id INTEGER,group_name TEXT DEFAULT '');

        CREATE TABLE IF NOT EXISTS bouts(
            id INTEGER PRIMARY KEY AUTOINCREMENT,category_id INTEGER NOT NULL,bout_no INTEGER NOT NULL,stage TEXT,
            red_id INTEGER,blue_id INTEGER,mat INTEGER DEFAULT 1,status TEXT DEFAULT 'Ожидает',
            red_score INTEGER,blue_score INTEGER,winner_id INTEGER,reason TEXT,judges TEXT DEFAULT '');

        CREATE TABLE IF NOT EXISTS audit(
            id INTEGER PRIMARY KEY AUTOINCREMENT,ts TEXT NOT NULL,action TEXT NOT NULL);");
        EnsureAthleteColumns(c);
    }

    void EnsureAthleteColumns(SqliteConnection c){
        var required=new Dictionary<string,string>{
            ["birth_date"]="TEXT",["region"]="TEXT",["organization"]="TEXT",["coach"]="TEXT",["rank"]="TEXT",
            ["declared_weight"]="REAL",["category_id"]="INTEGER"
        };
        var existing=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using(var q=c.CreateCommand()){ q.CommandText="PRAGMA table_info(athletes)"; using var r=q.ExecuteReader(); while(r.Read()) existing.Add(r.GetString(1)); }
        foreach(var kv in required) if(!existing.Contains(kv.Key)) Exec(c,$"ALTER TABLE athletes ADD COLUMN {kv.Key} {kv.Value}");
    }

    public void Audit(string s){
        using var c=Open(); using var q=c.CreateCommand();
        q.CommandText="INSERT INTO audit(ts,action) VALUES($t,$a)";
        q.Parameters.AddWithValue("$t",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        q.Parameters.AddWithValue("$a",s); q.ExecuteNonQuery();
    }

    public List<(string Time,string Action)> AuditRows(){
        using var c=Open(); using var q=c.CreateCommand(); q.CommandText="SELECT ts,action FROM audit ORDER BY id DESC LIMIT 1000";
        using var r=q.ExecuteReader(); var x=new List<(string,string)>(); while(r.Read()) x.Add((r.GetString(0),r.GetString(1))); return x;
    }

    public Tournament GetTournament(){
        using var c=Open(); using var q=c.CreateCommand(); q.CommandText="SELECT id,name,place,start_date,end_date,mats,chief_referee,chief_secretary FROM tournament WHERE id=1";
        using var r=q.ExecuteReader(); r.Read(); return new(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetInt32(5),r.GetString(6),r.GetString(7));
    }
    public void SaveTournament(string name,string place,string start,string end,int mats,string chiefReferee,string chiefSecretary){
        using var c=Open(); using var q=c.CreateCommand();
        q.CommandText=@"UPDATE tournament SET name=$n,place=$p,start_date=$s,end_date=$e,mats=$m,chief_referee=$cr,chief_secretary=$cs WHERE id=1";
        q.Parameters.AddWithValue("$n",name); q.Parameters.AddWithValue("$p",place); q.Parameters.AddWithValue("$s",start); q.Parameters.AddWithValue("$e",end);
        q.Parameters.AddWithValue("$m",mats); q.Parameters.AddWithValue("$cr",chiefReferee); q.Parameters.AddWithValue("$cs",chiefSecretary); q.ExecuteNonQuery();
        Audit("Сохранены данные турнира");
    }

    public long AddCategory(string discipline,string gender,string age,string weight,string system,string repechage,string drawMode){
        using var c=Open(); using var q=c.CreateCommand();
        q.CommandText=@"INSERT INTO categories(discipline,gender,age_group,weight_category,system,repechage,draw_mode) VALUES($d,$g,$a,$w,$s,$r,$m);SELECT last_insert_rowid();";
        q.Parameters.AddWithValue("$d",discipline);q.Parameters.AddWithValue("$g",gender);q.Parameters.AddWithValue("$a",age);q.Parameters.AddWithValue("$w",weight);
        q.Parameters.AddWithValue("$s",system);q.Parameters.AddWithValue("$r",repechage);q.Parameters.AddWithValue("$m",drawMode);
        var id=(long)q.ExecuteScalar()!; Audit($"Добавлена категория {discipline}, {gender}, {age}, {weight}"); return id;
    }
    public void DeleteCategory(long id){ using var c=Open(); using var q=c.CreateCommand(); q.CommandText="DELETE FROM categories WHERE id=$id"; q.Parameters.AddWithValue("$id",id); q.ExecuteNonQuery(); Audit($"Удалена категория {id}"); }
    public void UpdateCategorySettings(long id,string system,string repechage,string drawMode){
        using var c=Open(); using var q=c.CreateCommand(); q.CommandText="UPDATE categories SET system=$s,repechage=$r,draw_mode=$m WHERE id=$id";
        q.Parameters.AddWithValue("$s",system);q.Parameters.AddWithValue("$r",repechage);q.Parameters.AddWithValue("$m",drawMode);q.Parameters.AddWithValue("$id",id);q.ExecuteNonQuery();
    }
    public void ApproveDraw(long id,bool approved){ using var c=Open();using var q=c.CreateCommand();q.CommandText="UPDATE categories SET draw_approved=$a,status=$s WHERE id=$id";q.Parameters.AddWithValue("$a",approved?1:0);q.Parameters.AddWithValue("$s",approved?"Жеребьёвка утверждена":"Подготовка");q.Parameters.AddWithValue("$id",id);q.ExecuteNonQuery();Audit($"{(approved?"Утверждена":"Разблокирована")} жеребьёвка категории {id}");}
    public List<CategoryRow> Categories(){
        using var c=Open();using var q=c.CreateCommand();q.CommandText="SELECT id,discipline,gender,age_group,weight_category,system,repechage,draw_mode,draw_approved,status FROM categories ORDER BY discipline,gender,age_group,weight_category";
        using var r=q.ExecuteReader();var x=new List<CategoryRow>();while(r.Read())x.Add(new(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5),r.GetString(6),r.GetString(7),r.GetInt32(8)!=0,r.GetString(9)));return x;
    }

    public long AddAthlete(string fullName,string birth="",string gender="",string region="",string organization="",string team="",string coach="",string rank="",string discipline="Спортивное самбо",string age="",string weight="",double? declaredWeight=null,long? categoryId=null){
        using var c=Open();using var q=c.CreateCommand();
        q.CommandText=@"INSERT INTO athletes(full_name,birth_date,gender,region,organization,team,coach,rank,discipline,age_group,weight_category,declared_weight,category_id)
                        VALUES($n,$b,$g,$r,$o,$t,$c,$rk,$d,$a,$w,$dw,$cid);SELECT last_insert_rowid();";
        q.Parameters.AddWithValue("$n",NameNormalizer.Normalize(fullName));q.Parameters.AddWithValue("$b",birth);q.Parameters.AddWithValue("$g",gender);q.Parameters.AddWithValue("$r",region);
        q.Parameters.AddWithValue("$o",organization);q.Parameters.AddWithValue("$t",team);q.Parameters.AddWithValue("$c",coach);q.Parameters.AddWithValue("$rk",rank);
        q.Parameters.AddWithValue("$d",discipline);q.Parameters.AddWithValue("$a",age);q.Parameters.AddWithValue("$w",weight);q.Parameters.AddWithValue("$dw",(object?)declaredWeight??DBNull.Value);q.Parameters.AddWithValue("$cid",(object?)categoryId??DBNull.Value);
        var id=(long)q.ExecuteScalar()!;Audit("Добавлен спортсмен "+NameNormalizer.Normalize(fullName));return id;
    }

    public void UpdateAthleteStatus(long id,string status){using var c=Open();using var q=c.CreateCommand();q.CommandText="UPDATE athletes SET status=$s WHERE id=$id";q.Parameters.AddWithValue("$s",status);q.Parameters.AddWithValue("$id",id);q.ExecuteNonQuery();Audit($"Статус спортсмена {id}: {status}");}
    public void SetWeigh(long id,double kg,string status="Допущен"){using var c=Open();using var q=c.CreateCommand();q.CommandText="UPDATE athletes SET actual_weight=$w,status=$s WHERE id=$id";q.Parameters.AddWithValue("$w",kg);q.Parameters.AddWithValue("$s",status);q.Parameters.AddWithValue("$id",id);q.ExecuteNonQuery();Audit($"Взвешивание {id}: {kg} кг, {status}");}
    public void AssignCategory(long id,long? categoryId){using var c=Open();using var q=c.CreateCommand();q.CommandText="UPDATE athletes SET category_id=$c WHERE id=$id";q.Parameters.AddWithValue("$c",(object?)categoryId??DBNull.Value);q.Parameters.AddWithValue("$id",id);q.ExecuteNonQuery();}
    public List<Athlete> Athletes(long? categoryId=null){
        using var c=Open();using var q=c.CreateCommand();
        q.CommandText=@"SELECT id,full_name,COALESCE(birth_date,''),COALESCE(gender,''),COALESCE(region,''),COALESCE(organization,''),COALESCE(team,''),COALESCE(coach,''),COALESCE(rank,''),
                       COALESCE(discipline,''),COALESCE(age_group,''),COALESCE(weight_category,''),declared_weight,actual_weight,COALESCE(status,''),category_id
                       FROM athletes "+(categoryId.HasValue?"WHERE category_id=$cid ":"")+"ORDER BY full_name";
        if(categoryId.HasValue)q.Parameters.AddWithValue("$cid",categoryId.Value);
        using var r=q.ExecuteReader();var x=new List<Athlete>();
        while(r.Read())x.Add(new(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5),r.GetString(6),r.GetString(7),r.GetString(8),r.GetString(9),r.GetString(10),r.GetString(11),r.IsDBNull(12)?null:r.GetDouble(12),r.IsDBNull(13)?null:r.GetDouble(13),r.GetString(14),r.IsDBNull(15)?null:r.GetInt64(15)));
        return x;
    }

    public long AddJudge(string name,string region,string category,string role){
        using var c=Open();using var q=c.CreateCommand();q.CommandText="INSERT INTO judges(name,region,category,role) VALUES($n,$r,$c,$o);SELECT last_insert_rowid();";
        q.Parameters.AddWithValue("$n",NameNormalizer.Normalize(name));q.Parameters.AddWithValue("$r",region);q.Parameters.AddWithValue("$c",category);q.Parameters.AddWithValue("$o",role);
        var id=(long)q.ExecuteScalar()!;Audit("Добавлен судья "+NameNormalizer.Normalize(name));return id;
    }
    public List<JudgeRow> Judges(){using var c=Open();using var q=c.CreateCommand();q.CommandText="SELECT id,name,COALESCE(region,''),COALESCE(category,''),COALESCE(role,'') FROM judges ORDER BY name";using var r=q.ExecuteReader();var x=new List<JudgeRow>();while(r.Read())x.Add(new(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4)));return x;}

    public void ClearDrawPositions(long categoryId){using var c=Open();using var q=c.CreateCommand();q.CommandText="DELETE FROM draw_positions WHERE category_id=$c";q.Parameters.AddWithValue("$c",categoryId);q.ExecuteNonQuery();}
    public void AddDrawPosition(long categoryId,int position,long? athleteId,string groupName=""){using var c=Open();using var q=c.CreateCommand();q.CommandText="INSERT INTO draw_positions(category_id,position,athlete_id,group_name) VALUES($c,$p,$a,$g)";q.Parameters.AddWithValue("$c",categoryId);q.Parameters.AddWithValue("$p",position);q.Parameters.AddWithValue("$a",(object?)athleteId??DBNull.Value);q.Parameters.AddWithValue("$g",groupName);q.ExecuteNonQuery();}
    public List<(int Position,long? AthleteId,string Athlete,string Group)> DrawPositions(long categoryId){
        using var c=Open();using var q=c.CreateCommand();q.CommandText=@"SELECT d.position,d.athlete_id,COALESCE(a.full_name,''),COALESCE(d.group_name,'') FROM draw_positions d LEFT JOIN athletes a ON a.id=d.athlete_id WHERE d.category_id=$c ORDER BY d.position";q.Parameters.AddWithValue("$c",categoryId);
        using var r=q.ExecuteReader();var x=new List<(int,long?,string,string)>();while(r.Read())x.Add((r.GetInt32(0),r.IsDBNull(1)?null:r.GetInt64(1),r.GetString(2),r.GetString(3)));return x;
    }

    public void ClearBouts(long categoryId){using var c=Open();using var q=c.CreateCommand();q.CommandText="DELETE FROM bouts WHERE category_id=$c";q.Parameters.AddWithValue("$c",categoryId);q.ExecuteNonQuery();}
    public long AddBout(long categoryId,int no,string stage,long? red,long? blue,int mat=1){
        using var c=Open();using var q=c.CreateCommand();q.CommandText=@"INSERT INTO bouts(category_id,bout_no,stage,red_id,blue_id,mat,status) VALUES($c,$n,$s,$r,$b,$m,'Ожидает');SELECT last_insert_rowid();";
        q.Parameters.AddWithValue("$c",categoryId);q.Parameters.AddWithValue("$n",no);q.Parameters.AddWithValue("$s",stage);q.Parameters.AddWithValue("$r",(object?)red??DBNull.Value);q.Parameters.AddWithValue("$b",(object?)blue??DBNull.Value);q.Parameters.AddWithValue("$m",mat);return (long)q.ExecuteScalar()!;
    }
    public void SetBoutStatus(long boutId,string status,int mat){
        using var c=Open();using var q=c.CreateCommand();q.CommandText="UPDATE bouts SET status=$s,mat=$m WHERE id=$id";q.Parameters.AddWithValue("$s",status);q.Parameters.AddWithValue("$m",mat);q.Parameters.AddWithValue("$id",boutId);q.ExecuteNonQuery();Audit($"Поединок {boutId}: статус {status}, ковёр {mat}");
    }
    public void SetBoutResult(long boutId,int redScore,int blueScore,long winnerId,string reason,string judges){
        using var c=Open();using var q=c.CreateCommand();q.CommandText="UPDATE bouts SET red_score=$rs,blue_score=$bs,winner_id=$w,reason=$r,judges=$j,status='Завершён' WHERE id=$id";
        q.Parameters.AddWithValue("$rs",redScore);q.Parameters.AddWithValue("$bs",blueScore);q.Parameters.AddWithValue("$w",winnerId);q.Parameters.AddWithValue("$r",reason);q.Parameters.AddWithValue("$j",judges);q.Parameters.AddWithValue("$id",boutId);q.ExecuteNonQuery();Audit($"Внесён результат поединка {boutId}");
    }
    public List<BoutRow> Bouts(long categoryId){
        using var c=Open();using var q=c.CreateCommand();q.CommandText=@"SELECT b.id,b.category_id,b.bout_no,COALESCE(b.stage,''),b.red_id,COALESCE(r.full_name,''),b.blue_id,COALESCE(bl.full_name,''),b.mat,COALESCE(b.status,''),b.red_score,b.blue_score,b.winner_id,COALESCE(w.full_name,''),COALESCE(b.reason,''),COALESCE(b.judges,'')
            FROM bouts b LEFT JOIN athletes r ON r.id=b.red_id LEFT JOIN athletes bl ON bl.id=b.blue_id LEFT JOIN athletes w ON w.id=b.winner_id WHERE b.category_id=$c ORDER BY b.bout_no";
        q.Parameters.AddWithValue("$c",categoryId);using var r=q.ExecuteReader();var x=new List<BoutRow>();
        while(r.Read())x.Add(new(r.GetInt64(0),r.GetInt64(1),r.GetInt32(2),r.GetString(3),r.IsDBNull(4)?null:r.GetInt64(4),r.GetString(5),r.IsDBNull(6)?null:r.GetInt64(6),r.GetString(7),r.GetInt32(8),r.GetString(9),r.IsDBNull(10)?null:r.GetInt32(10),r.IsDBNull(11)?null:r.GetInt32(11),r.IsDBNull(12)?null:r.GetInt64(12),r.GetString(13),r.GetString(14),r.GetString(15)));return x;
    }
}

public static class ExcelImporter {
    public static (string[] Headers,List<Dictionary<string,string>> Rows) Read(string path){
        using var wb=new XLWorkbook(path);var ws=wb.Worksheets.First();var used=ws.RangeUsed();if(used==null)return([],[]);
        var h=used.FirstRow().Cells().Select(c=>c.GetString().Trim()).ToArray();
        var rows=new List<Dictionary<string,string>>();
        foreach(var row in used.RowsUsed().Skip(1)){
            var d=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            for(int i=0;i<h.Length;i++) if(h[i]!="") d[h[i]]=row.Cell(i+1).GetFormattedString();
            rows.Add(d);
        }
        return(h,rows);
    }
    public static string GuessHeader(IEnumerable<string> headers,params string[] keys){
        return headers.FirstOrDefault(h=>keys.Any(k=>h.Contains(k,StringComparison.OrdinalIgnoreCase)))??"";
    }
    public static string Value(Dictionary<string,string> row,string? header)=>string.IsNullOrWhiteSpace(header)?"":(row.TryGetValue(header,out var v)?v:"");
    public static void CreateTemplate(string path){
        using var wb=new XLWorkbook();var ws=wb.AddWorksheet("Заявка");
        string[] headers={"Фамилия","Имя","Отчество","Дата рождения","Пол","Регион","Организация","Команда","Тренер","Разряд","Дисциплина","Возрастная группа","Весовая категория","Заявленный вес"};
        for(int i=0;i<headers.Length;i++){ws.Cell(1,i+1).Value=headers[i];ws.Cell(1,i+1).Style.Font.Bold=true;}
        ws.SheetView.FreezeRows(1);ws.Columns().AdjustToContents();wb.SaveAs(path);
    }
}