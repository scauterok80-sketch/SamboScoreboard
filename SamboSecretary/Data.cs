using Microsoft.Data.Sqlite;
using ClosedXML.Excel;

namespace SamboSecretary;

public record Tournament(long Id,string Name,string Place,string StartDate,string EndDate,int Mats,string ChiefReferee,string ChiefSecretary,string TeamScheme,string ProtocolHeader="",string ProtocolFooter="");
public record CategoryRow(long Id,string Discipline,string Gender,string AgeGroup,string WeightCategory,string System,string Repechage,string DrawMode,bool DrawApproved,string Status);
public record Athlete(long Id,string FullName,string BirthDate,string Gender,string Region,string Organization,string Team,string Coach,string Rank,string Discipline,string AgeGroup,string WeightCategory,double? DeclaredWeight,double? ActualWeight,string Status,long? CategoryId,string StatusReason="");
public record JudgeRow(long Id,string Name,string Region,string Category,string Role,string Notes="");
public record JudgeAssignmentRow(long Id,long CategoryId,long JudgeId,string JudgeName,int Mat,string Role);
public record BoutRow(long Id,long CategoryId,int BoutNo,int DisplayNo,string Stage,long? RedId,string RedName,long? BlueId,string BlueName,int Mat,string ScheduledTime,string Status,int? RedScore,int? BlueScore,long? WinnerId,string WinnerName,string Reason,string Judges);
public record BoutRuleRow(long Id,long CategoryId,int BoutNo,string Stage,long? RedId,long? BlueId,string Status,int RedScore,int BlueScore,long? WinnerId,string ResultCode,int RedClass,int BlueClass,int DurationSeconds,bool IsClean,int Red4,int Red2,int Red1,int Blue4,int Blue2,int Blue1,string Reason);
public record PlacementRow(long AthleteId,string Athlete,string Team,string Region,int Place,string Source);

public sealed class Database {
    public string FileName { get; }
    public Database(string file){ FileName=file; Directory.CreateDirectory(Path.GetDirectoryName(file)!); Init(); }

    SqliteConnection Open(){ var c=new SqliteConnection($"Data Source={FileName}"); c.Open(); return c; }
    void Exec(SqliteConnection c,string sql){ using var q=c.CreateCommand(); q.CommandText=sql; q.ExecuteNonQuery(); }

    void Init(){
        using var c=Open();
        Exec(c,@"CREATE TABLE IF NOT EXISTS tournament(
            id INTEGER PRIMARY KEY CHECK(id=1),name TEXT,place TEXT,start_date TEXT,end_date TEXT,mats INTEGER DEFAULT 1,chief_referee TEXT,chief_secretary TEXT,team_scheme TEXT DEFAULT '7,5,3,1',protocol_header TEXT DEFAULT '',protocol_footer TEXT DEFAULT '');
        INSERT OR IGNORE INTO tournament(id,name,place,start_date,end_date,mats,chief_referee,chief_secretary,team_scheme,protocol_header,protocol_footer) VALUES(1,'','','','',1,'','','7,5,3,1','','');

        CREATE TABLE IF NOT EXISTS categories(
            id INTEGER PRIMARY KEY AUTOINCREMENT,discipline TEXT NOT NULL,gender TEXT,age_group TEXT,weight_category TEXT NOT NULL,
            system TEXT DEFAULT '',repechage TEXT DEFAULT '',draw_mode TEXT DEFAULT '',draw_approved INTEGER DEFAULT 0,status TEXT DEFAULT 'Подготовка');

        CREATE TABLE IF NOT EXISTS athletes(
            id INTEGER PRIMARY KEY AUTOINCREMENT,full_name TEXT NOT NULL,birth_date TEXT,gender TEXT,region TEXT,organization TEXT,team TEXT,coach TEXT,rank TEXT,
            discipline TEXT,age_group TEXT,weight_category TEXT,declared_weight REAL,actual_weight REAL,status TEXT DEFAULT 'Заявлен',category_id INTEGER);

        CREATE TABLE IF NOT EXISTS judges(
            id INTEGER PRIMARY KEY AUTOINCREMENT,name TEXT NOT NULL,region TEXT,category TEXT,role TEXT,notes TEXT DEFAULT '');

        CREATE TABLE IF NOT EXISTS judge_assignments(
            id INTEGER PRIMARY KEY AUTOINCREMENT,category_id INTEGER NOT NULL,judge_id INTEGER NOT NULL,mat INTEGER DEFAULT 1,role TEXT DEFAULT '',
            UNIQUE(category_id,judge_id,mat,role));

        CREATE TABLE IF NOT EXISTS draw_positions(
            id INTEGER PRIMARY KEY AUTOINCREMENT,category_id INTEGER NOT NULL,position INTEGER NOT NULL,athlete_id INTEGER,group_name TEXT DEFAULT '');

        CREATE TABLE IF NOT EXISTS bouts(
            id INTEGER PRIMARY KEY AUTOINCREMENT,category_id INTEGER NOT NULL,bout_no INTEGER NOT NULL,stage TEXT,
            red_id INTEGER,blue_id INTEGER,mat INTEGER DEFAULT 1,status TEXT DEFAULT 'Ожидает',
            red_score INTEGER,blue_score INTEGER,winner_id INTEGER,reason TEXT,judges TEXT DEFAULT '');

        CREATE TABLE IF NOT EXISTS placements(
            id INTEGER PRIMARY KEY AUTOINCREMENT,category_id INTEGER NOT NULL,athlete_id INTEGER NOT NULL,place INTEGER NOT NULL,source TEXT DEFAULT '');

        CREATE TABLE IF NOT EXISTS audit(
            id INTEGER PRIMARY KEY AUTOINCREMENT,ts TEXT NOT NULL,action TEXT NOT NULL);");
        EnsureTournamentColumns(c);
        EnsureAthleteColumns(c);
        EnsureJudgeColumns(c);
        EnsureBoutColumns(c);
    }

    void EnsureTournamentColumns(SqliteConnection c){
        var existing=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using(var q=c.CreateCommand()){q.CommandText="PRAGMA table_info(tournament)";using var r=q.ExecuteReader();while(r.Read())existing.Add(r.GetString(1));}
        if(!existing.Contains("team_scheme"))Exec(c,"ALTER TABLE tournament ADD COLUMN team_scheme TEXT DEFAULT '7,5,3,1'");
        if(!existing.Contains("protocol_header"))Exec(c,"ALTER TABLE tournament ADD COLUMN protocol_header TEXT DEFAULT ''");
        if(!existing.Contains("protocol_footer"))Exec(c,"ALTER TABLE tournament ADD COLUMN protocol_footer TEXT DEFAULT ''");
    }
    void EnsureAthleteColumns(SqliteConnection c){
        var required=new Dictionary<string,string>{
            ["birth_date"]="TEXT",["region"]="TEXT",["organization"]="TEXT",["coach"]="TEXT",["rank"]="TEXT",
            ["declared_weight"]="REAL",["category_id"]="INTEGER",["status_reason"]="TEXT DEFAULT ''"
        };
        var existing=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using(var q=c.CreateCommand()){ q.CommandText="PRAGMA table_info(athletes)"; using var r=q.ExecuteReader(); while(r.Read()) existing.Add(r.GetString(1)); }
        foreach(var kv in required) if(!existing.Contains(kv.Key)) Exec(c,$"ALTER TABLE athletes ADD COLUMN {kv.Key} {kv.Value}");
    }
    void EnsureJudgeColumns(SqliteConnection c){
        var existing=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using(var q=c.CreateCommand()){q.CommandText="PRAGMA table_info(judges)";using var r=q.ExecuteReader();while(r.Read())existing.Add(r.GetString(1));}
        if(!existing.Contains("notes"))Exec(c,"ALTER TABLE judges ADD COLUMN notes TEXT DEFAULT ''");
    }

    void EnsureBoutColumns(SqliteConnection c){
        var required=new Dictionary<string,string>{
            ["result_code"]="TEXT DEFAULT ''",["red_class"]="INTEGER DEFAULT 0",["blue_class"]="INTEGER DEFAULT 0",
            ["duration_seconds"]="INTEGER DEFAULT 0",["is_clean"]="INTEGER DEFAULT 0",
            ["red_4"]="INTEGER DEFAULT 0",["red_2"]="INTEGER DEFAULT 0",["red_1"]="INTEGER DEFAULT 0",["blue_4"]="INTEGER DEFAULT 0",["blue_2"]="INTEGER DEFAULT 0",["blue_1"]="INTEGER DEFAULT 0",
            ["display_no"]="INTEGER DEFAULT 0",["scheduled_time"]="TEXT DEFAULT ''"
        };
        var existing=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using(var q=c.CreateCommand()){q.CommandText="PRAGMA table_info(bouts)";using var r=q.ExecuteReader();while(r.Read())existing.Add(r.GetString(1));}
        foreach(var kv in required)if(!existing.Contains(kv.Key))Exec(c,$"ALTER TABLE bouts ADD COLUMN {kv.Key} {kv.Value}");
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
        using var c=Open(); using var q=c.CreateCommand(); q.CommandText="SELECT id,name,place,start_date,end_date,mats,chief_referee,chief_secretary,COALESCE(team_scheme,'7,5,3,1'),COALESCE(protocol_header,''),COALESCE(protocol_footer,'') FROM tournament WHERE id=1";
        using var r=q.ExecuteReader(); r.Read(); return new(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetInt32(5),r.GetString(6),r.GetString(7),r.GetString(8),r.GetString(9),r.GetString(10));
    }
    public void ResetForNewTournament(){
        using var c=Open();using var tx=c.BeginTransaction();
        foreach(var table in new[]{"judge_assignments","placements","bouts","draw_positions","judges","athletes","categories","audit"}){
            using var q=c.CreateCommand();q.Transaction=tx;q.CommandText=$"DELETE FROM {table}";q.ExecuteNonQuery();
        }
        using(var q=c.CreateCommand()){q.Transaction=tx;q.CommandText="UPDATE tournament SET name='',place='',start_date='',end_date='',mats=1,chief_referee='',chief_secretary='',team_scheme='7,5,3,1',protocol_header='',protocol_footer='' WHERE id=1";q.ExecuteNonQuery();}
        tx.Commit();Audit("Создан новый турнир");
    }
    public void SaveTournament(string name,string place,string start,string end,int mats,string chiefReferee,string chiefSecretary,string teamScheme="7,5,3,1",string protocolHeader="",string protocolFooter=""){
        if(mats<1||mats>6)throw new ArgumentOutOfRangeException(nameof(mats),"Количество ковров должно быть от 1 до 6.");
        using var c=Open(); using var q=c.CreateCommand();
        q.CommandText=@"UPDATE tournament SET name=$n,place=$p,start_date=$s,end_date=$e,mats=$m,chief_referee=$cr,chief_secretary=$cs,team_scheme=$ts,protocol_header=$ph,protocol_footer=$pf WHERE id=1";
        q.Parameters.AddWithValue("$n",name); q.Parameters.AddWithValue("$p",place); q.Parameters.AddWithValue("$s",start); q.Parameters.AddWithValue("$e",end);
        q.Parameters.AddWithValue("$m",mats); q.Parameters.AddWithValue("$cr",chiefReferee); q.Parameters.AddWithValue("$cs",chiefSecretary);q.Parameters.AddWithValue("$ts",string.IsNullOrWhiteSpace(teamScheme)?"7,5,3,1":teamScheme);q.Parameters.AddWithValue("$ph",protocolHeader??"");q.Parameters.AddWithValue("$pf",protocolFooter??""); q.ExecuteNonQuery();
        Audit("Сохранены данные турнира");
    }

    public long AddCategory(string discipline,string gender,string age,string weight,string system,string repechage,string drawMode){
        using var c=Open(); using var q=c.CreateCommand();
        q.CommandText=@"INSERT INTO categories(discipline,gender,age_group,weight_category,system,repechage,draw_mode) VALUES($d,$g,$a,$w,$s,$r,$m);SELECT last_insert_rowid();";
        q.Parameters.AddWithValue("$d",discipline);q.Parameters.AddWithValue("$g",gender);q.Parameters.AddWithValue("$a",age);q.Parameters.AddWithValue("$w",weight);
        q.Parameters.AddWithValue("$s",system);q.Parameters.AddWithValue("$r",repechage);q.Parameters.AddWithValue("$m",drawMode);
        var id=(long)q.ExecuteScalar()!; Audit($"Добавлена категория {discipline}, {gender}, {age}, {weight}"); return id;
    }
    public void UpdateCategory(long id,string discipline,string gender,string age,string weight,string system,string repechage,string drawMode){
        using var c=Open();using(var chk=c.CreateCommand()){chk.CommandText="SELECT COUNT(*) FROM bouts WHERE category_id=$id AND status='Завершён'";chk.Parameters.AddWithValue("$id",id);if(Convert.ToInt32(chk.ExecuteScalar())>0)throw new InvalidOperationException("Нельзя менять параметры категории после проведения поединков.");}
        using var q=c.CreateCommand();q.CommandText=@"UPDATE categories SET discipline=$d,gender=$g,age_group=$a,weight_category=$w,system=$s,repechage=$r,draw_mode=$m WHERE id=$id";
        q.Parameters.AddWithValue("$d",discipline);q.Parameters.AddWithValue("$g",gender);q.Parameters.AddWithValue("$a",age);q.Parameters.AddWithValue("$w",weight);q.Parameters.AddWithValue("$s",system);q.Parameters.AddWithValue("$r",repechage);q.Parameters.AddWithValue("$m",drawMode);q.Parameters.AddWithValue("$id",id);q.ExecuteNonQuery();Audit($"Изменена категория {id}");
    }
    public void DeleteCategory(long id){
        using var c=Open();
        foreach(var sql in new[]{"SELECT COUNT(*) FROM athletes WHERE category_id=$id","SELECT COUNT(*) FROM draw_positions WHERE category_id=$id","SELECT COUNT(*) FROM bouts WHERE category_id=$id","SELECT COUNT(*) FROM placements WHERE category_id=$id","SELECT COUNT(*) FROM judge_assignments WHERE category_id=$id"}){
            using var chk=c.CreateCommand();chk.CommandText=sql;chk.Parameters.AddWithValue("$id",id);
            if(Convert.ToInt32(chk.ExecuteScalar())>0)throw new InvalidOperationException("Категория содержит участников, жеребьёвку, поединки, результаты или назначения судей. Сначала удалите/перенесите связанные данные.");
        }
        using var q=c.CreateCommand();q.CommandText="DELETE FROM categories WHERE id=$id";q.Parameters.AddWithValue("$id",id);q.ExecuteNonQuery();Audit($"Удалена категория {id}");
    }
    public void UpdateCategorySettings(long id,string system,string repechage,string drawMode){
        using var c=Open(); using var q=c.CreateCommand(); q.CommandText="UPDATE categories SET system=$s,repechage=$r,draw_mode=$m WHERE id=$id";
        q.Parameters.AddWithValue("$s",system);q.Parameters.AddWithValue("$r",repechage);q.Parameters.AddWithValue("$m",drawMode);q.Parameters.AddWithValue("$id",id);q.ExecuteNonQuery();
    }
    public void ApproveDraw(long id,bool approved){
        using var c=Open();
        if(!approved){using var chk=c.CreateCommand();chk.CommandText="SELECT COUNT(*) FROM bouts WHERE category_id=$id AND status='Завершён'";chk.Parameters.AddWithValue("$id",id);if(Convert.ToInt32(chk.ExecuteScalar())>0)throw new InvalidOperationException("Нельзя разблокировать жеребьёвку после проведения поединков.");}
        using var q=c.CreateCommand();q.CommandText="UPDATE categories SET draw_approved=$a,status=$s WHERE id=$id";q.Parameters.AddWithValue("$a",approved?1:0);q.Parameters.AddWithValue("$s",approved?"Жеребьёвка утверждена":"Подготовка");q.Parameters.AddWithValue("$id",id);q.ExecuteNonQuery();Audit($"{(approved?"Утверждена":"Разблокирована")} жеребьёвка категории {id}");
    }
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

    bool CategoryApproved(SqliteConnection c,long? categoryId){
        if(!categoryId.HasValue)return false;using var q=c.CreateCommand();q.CommandText="SELECT COALESCE(draw_approved,0) FROM categories WHERE id=$id";q.Parameters.AddWithValue("$id",categoryId.Value);return Convert.ToInt32(q.ExecuteScalar()??0)!=0;
    }
    (long? CategoryId,string Discipline,string Gender,string Age,string Weight) AthleteCompetitionFields(SqliteConnection c,long id){
        using var q=c.CreateCommand();q.CommandText="SELECT category_id,COALESCE(discipline,''),COALESCE(gender,''),COALESCE(age_group,''),COALESCE(weight_category,'') FROM athletes WHERE id=$id";q.Parameters.AddWithValue("$id",id);using var r=q.ExecuteReader();if(!r.Read())throw new InvalidOperationException("Спортсмен не найден");
        return(r.IsDBNull(0)?null:r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4));
    }
    void EnsureEligibilityUnlocked(SqliteConnection c,long athleteId){
        var old=AthleteCompetitionFields(c,athleteId);if(CategoryApproved(c,old.CategoryId))throw new InvalidOperationException("Жеребьёвка категории утверждена. Сначала разблокируйте её, затем меняйте допуск или взвешивание.");
        if(old.CategoryId.HasValue){using var q=c.CreateCommand();q.CommandText="SELECT COUNT(*) FROM bouts WHERE category_id=$c AND status='Завершён'";q.Parameters.AddWithValue("$c",old.CategoryId.Value);if(Convert.ToInt32(q.ExecuteScalar())>0)throw new InvalidOperationException("В категории уже есть проведённые поединки. Допуск и данные взвешивания нельзя менять задним числом.");}
    }

    public void UpdateAthlete(long id,string fullName,string birth,string gender,string region,string organization,string team,string coach,string rank,string discipline,string age,string weight,double? declaredWeight,long? categoryId){
        using var c=Open();var old=AthleteCompetitionFields(c,id);
        bool criticalChanged=old.CategoryId!=categoryId||!string.Equals(old.Discipline,discipline,StringComparison.OrdinalIgnoreCase)||!string.Equals(old.Gender,gender,StringComparison.OrdinalIgnoreCase)||!string.Equals(old.Age,age,StringComparison.OrdinalIgnoreCase)||!string.Equals(old.Weight,weight,StringComparison.OrdinalIgnoreCase);
        if(criticalChanged&&CategoryApproved(c,old.CategoryId))throw new InvalidOperationException("Нельзя менять соревновательные данные спортсмена после утверждения жеребьёвки. Сначала разблокируйте категорию.");
        if(old.CategoryId!=categoryId){using var chk=c.CreateCommand();chk.CommandText="SELECT COUNT(*) FROM bouts WHERE red_id=$id OR blue_id=$id OR winner_id=$id";chk.Parameters.AddWithValue("$id",id);if(Convert.ToInt32(chk.ExecuteScalar())>0)throw new InvalidOperationException("Нельзя перенести спортсмена в другую категорию: он уже включён в поединки.");}
        if(categoryId.HasValue&&categoryId!=old.CategoryId&&CategoryApproved(c,categoryId))throw new InvalidOperationException("Нельзя добавить спортсмена в категорию с утверждённой жеребьёвкой.");
        using var q=c.CreateCommand();q.CommandText=@"UPDATE athletes SET full_name=$n,birth_date=$b,gender=$g,region=$r,organization=$o,team=$t,coach=$c,rank=$rk,discipline=$d,age_group=$a,weight_category=$w,declared_weight=$dw,category_id=$cid WHERE id=$id";
        q.Parameters.AddWithValue("$n",NameNormalizer.Normalize(fullName));q.Parameters.AddWithValue("$b",birth);q.Parameters.AddWithValue("$g",gender);q.Parameters.AddWithValue("$r",region);q.Parameters.AddWithValue("$o",organization);q.Parameters.AddWithValue("$t",team);q.Parameters.AddWithValue("$c",coach);q.Parameters.AddWithValue("$rk",rank);q.Parameters.AddWithValue("$d",discipline);q.Parameters.AddWithValue("$a",age);q.Parameters.AddWithValue("$w",weight);q.Parameters.AddWithValue("$dw",(object?)declaredWeight??DBNull.Value);q.Parameters.AddWithValue("$cid",(object?)categoryId??DBNull.Value);q.Parameters.AddWithValue("$id",id);q.ExecuteNonQuery();Audit($"Изменена карточка спортсмена {id}");
    }
    public bool DeleteAthleteIfUnused(long id){
        using var c=Open();using(var q=c.CreateCommand()){q.CommandText="SELECT COUNT(*) FROM bouts WHERE red_id=$id OR blue_id=$id OR winner_id=$id";q.Parameters.AddWithValue("$id",id);if(Convert.ToInt32(q.ExecuteScalar())>0)return false;}
        using(var q=c.CreateCommand()){q.CommandText="DELETE FROM athletes WHERE id=$id";q.Parameters.AddWithValue("$id",id);q.ExecuteNonQuery();}Audit($"Удалён спортсмен {id}");return true;
    }
    public void UpdateAthleteStatus(long id,string status,string reason=""){using var c=Open();EnsureEligibilityUnlocked(c,id);using var q=c.CreateCommand();q.CommandText="UPDATE athletes SET status=$s,status_reason=$r WHERE id=$id";q.Parameters.AddWithValue("$s",status);q.Parameters.AddWithValue("$r",reason??"");q.Parameters.AddWithValue("$id",id);q.ExecuteNonQuery();Audit($"Статус спортсмена {id}: {status}"+(string.IsNullOrWhiteSpace(reason)?"":$"; причина: {reason}"));}
    public void SetOperationalAthleteStatus(long id,string status){
        using var c=Open();using var q=c.CreateCommand();q.CommandText="UPDATE athletes SET status=$s,status_reason=$r WHERE id=$id";q.Parameters.AddWithValue("$s",status);q.Parameters.AddWithValue("$r",status);q.Parameters.AddWithValue("$id",id);q.ExecuteNonQuery();Audit($"Соревновательный статус спортсмена {id}: {status}");
    }
    public void SetWeigh(long id,double kg,string status="Допущен",string reason=""){using var c=Open();EnsureEligibilityUnlocked(c,id);using var q=c.CreateCommand();q.CommandText="UPDATE athletes SET actual_weight=$w,status=$s,status_reason=$r WHERE id=$id";q.Parameters.AddWithValue("$w",kg);q.Parameters.AddWithValue("$s",status);q.Parameters.AddWithValue("$r",reason??"");q.Parameters.AddWithValue("$id",id);q.ExecuteNonQuery();Audit($"Взвешивание {id}: {kg} кг, {status}"+(string.IsNullOrWhiteSpace(reason)?"":$"; причина: {reason}"));}
    public void AssignCategory(long id,long? categoryId){
        using var c=Open();var old=AthleteCompetitionFields(c,id);
        if(old.CategoryId==categoryId)return;
        if(CategoryApproved(c,old.CategoryId)||CategoryApproved(c,categoryId))throw new InvalidOperationException("Нельзя переносить спортсмена из/в категорию с утверждённой жеребьёвкой.");
        using(var chk=c.CreateCommand()){chk.CommandText="SELECT COUNT(*) FROM bouts WHERE red_id=$id OR blue_id=$id OR winner_id=$id";chk.Parameters.AddWithValue("$id",id);if(Convert.ToInt32(chk.ExecuteScalar())>0)throw new InvalidOperationException("Нельзя перенести спортсмена: он уже включён в поединки.");}
        using var q=c.CreateCommand();q.CommandText="UPDATE athletes SET category_id=$c WHERE id=$id";q.Parameters.AddWithValue("$c",(object?)categoryId??DBNull.Value);q.Parameters.AddWithValue("$id",id);q.ExecuteNonQuery();Audit($"Спортсмен {id} перенесён в категорию {categoryId?.ToString()??"без категории"}");
    }
    public List<Athlete> Athletes(long? categoryId=null){
        using var c=Open();using var q=c.CreateCommand();
        q.CommandText=@"SELECT id,full_name,COALESCE(birth_date,''),COALESCE(gender,''),COALESCE(region,''),COALESCE(organization,''),COALESCE(team,''),COALESCE(coach,''),COALESCE(rank,''),
                       COALESCE(discipline,''),COALESCE(age_group,''),COALESCE(weight_category,''),declared_weight,actual_weight,COALESCE(status,''),category_id,COALESCE(status_reason,'')
                       FROM athletes "+(categoryId.HasValue?"WHERE category_id=$cid ":"")+"ORDER BY full_name";
        if(categoryId.HasValue)q.Parameters.AddWithValue("$cid",categoryId.Value);
        using var r=q.ExecuteReader();var x=new List<Athlete>();
        while(r.Read())x.Add(new(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5),r.GetString(6),r.GetString(7),r.GetString(8),r.GetString(9),r.GetString(10),r.GetString(11),r.IsDBNull(12)?null:r.GetDouble(12),r.IsDBNull(13)?null:r.GetDouble(13),r.GetString(14),r.IsDBNull(15)?null:r.GetInt64(15),r.GetString(16)));
        return x;
    }

    public long AddJudge(string name,string region,string category,string role,string notes=""){
        using var c=Open();using var q=c.CreateCommand();q.CommandText="INSERT INTO judges(name,region,category,role,notes) VALUES($n,$r,$c,$o,$notes);SELECT last_insert_rowid();";
        q.Parameters.AddWithValue("$n",NameNormalizer.Normalize(name));q.Parameters.AddWithValue("$r",region);q.Parameters.AddWithValue("$c",category);q.Parameters.AddWithValue("$o",role);q.Parameters.AddWithValue("$notes",notes??"");
        var id=(long)q.ExecuteScalar()!;Audit("Добавлен судья "+NameNormalizer.Normalize(name));return id;
    }
    public void UpdateJudge(long id,string name,string region,string category,string role,string notes){
        using var c=Open();using var q=c.CreateCommand();q.CommandText="UPDATE judges SET name=$n,region=$r,category=$c,role=$o,notes=$notes WHERE id=$id";
        q.Parameters.AddWithValue("$n",NameNormalizer.Normalize(name));q.Parameters.AddWithValue("$r",region);q.Parameters.AddWithValue("$c",category);q.Parameters.AddWithValue("$o",role);q.Parameters.AddWithValue("$notes",notes??"");q.Parameters.AddWithValue("$id",id);q.ExecuteNonQuery();Audit($"Изменена карточка судьи {id}");
    }
    public bool DeleteJudge(long id){
        using var c=Open();using var tx=c.BeginTransaction();
        using(var q=c.CreateCommand()){q.Transaction=tx;q.CommandText="DELETE FROM judge_assignments WHERE judge_id=$id";q.Parameters.AddWithValue("$id",id);q.ExecuteNonQuery();}
        int n;using(var q=c.CreateCommand()){q.Transaction=tx;q.CommandText="DELETE FROM judges WHERE id=$id";q.Parameters.AddWithValue("$id",id);n=q.ExecuteNonQuery();}
        tx.Commit();if(n>0)Audit($"Удалён судья {id}");return n>0;
    }
    public List<JudgeRow> Judges(){using var c=Open();using var q=c.CreateCommand();q.CommandText="SELECT id,name,COALESCE(region,''),COALESCE(category,''),COALESCE(role,''),COALESCE(notes,'') FROM judges ORDER BY name";using var r=q.ExecuteReader();var x=new List<JudgeRow>();while(r.Read())x.Add(new(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5)));return x;}
    public void AssignJudge(long categoryId,long judgeId,int mat,string role){
        if(mat<1||mat>6)throw new ArgumentOutOfRangeException(nameof(mat),"Номер ковра должен быть от 1 до 6.");
        using var c=Open();using var q=c.CreateCommand();q.CommandText="INSERT OR IGNORE INTO judge_assignments(category_id,judge_id,mat,role) VALUES($c,$j,$m,$r)";
        q.Parameters.AddWithValue("$c",categoryId);q.Parameters.AddWithValue("$j",judgeId);q.Parameters.AddWithValue("$m",mat);q.Parameters.AddWithValue("$r",role);q.ExecuteNonQuery();Audit($"Назначен судья {judgeId} в категорию {categoryId}, ковёр {mat}, роль {role}");
    }
    public void RemoveJudgeAssignment(long id){using var c=Open();using var q=c.CreateCommand();q.CommandText="DELETE FROM judge_assignments WHERE id=$id";q.Parameters.AddWithValue("$id",id);q.ExecuteNonQuery();Audit($"Удалено судейское назначение {id}");}
    public List<JudgeAssignmentRow> JudgeAssignments(long? categoryId=null){
        using var c=Open();using var q=c.CreateCommand();q.CommandText=@"SELECT ja.id,ja.category_id,ja.judge_id,j.name,ja.mat,COALESCE(ja.role,j.role,'') FROM judge_assignments ja JOIN judges j ON j.id=ja.judge_id "+(categoryId.HasValue?"WHERE ja.category_id=$c ":"")+"ORDER BY ja.category_id,ja.mat,ja.role,j.name";
        if(categoryId.HasValue)q.Parameters.AddWithValue("$c",categoryId.Value);using var r=q.ExecuteReader();var x=new List<JudgeAssignmentRow>();while(r.Read())x.Add(new(r.GetInt64(0),r.GetInt64(1),r.GetInt64(2),r.GetString(3),r.GetInt32(4),r.GetString(5)));return x;
    }

    public void ClearDrawPositions(long categoryId){using var c=Open();using var q=c.CreateCommand();q.CommandText="DELETE FROM draw_positions WHERE category_id=$c";q.Parameters.AddWithValue("$c",categoryId);q.ExecuteNonQuery();}
    public void AddDrawPosition(long categoryId,int position,long? athleteId,string groupName=""){using var c=Open();using var q=c.CreateCommand();q.CommandText="INSERT INTO draw_positions(category_id,position,athlete_id,group_name) VALUES($c,$p,$a,$g)";q.Parameters.AddWithValue("$c",categoryId);q.Parameters.AddWithValue("$p",position);q.Parameters.AddWithValue("$a",(object?)athleteId??DBNull.Value);q.Parameters.AddWithValue("$g",groupName);q.ExecuteNonQuery();}
    public List<(int Position,long? AthleteId,string Athlete,string Group)> DrawPositions(long categoryId){
        using var c=Open();using var q=c.CreateCommand();q.CommandText=@"SELECT d.position,d.athlete_id,COALESCE(a.full_name,''),COALESCE(d.group_name,'') FROM draw_positions d LEFT JOIN athletes a ON a.id=d.athlete_id WHERE d.category_id=$c ORDER BY d.position";q.Parameters.AddWithValue("$c",categoryId);
        using var r=q.ExecuteReader();var x=new List<(int,long?,string,string)>();while(r.Read())x.Add((r.GetInt32(0),r.IsDBNull(1)?null:r.GetInt64(1),r.GetString(2),r.GetString(3)));return x;
    }

    public void ClearBouts(long categoryId){
        using var c=Open();using(var chk=c.CreateCommand()){chk.CommandText="SELECT COUNT(*) FROM bouts WHERE category_id=$c AND status='Завершён'";chk.Parameters.AddWithValue("$c",categoryId);if(Convert.ToInt32(chk.ExecuteScalar())>0)throw new InvalidOperationException("Нельзя удалить или пережеребьевать сетку: в категории уже есть проведённые поединки. Используйте исправление результата с журналированием.");}
        using var q=c.CreateCommand();q.CommandText="DELETE FROM bouts WHERE category_id=$c";q.Parameters.AddWithValue("$c",categoryId);q.ExecuteNonQuery();
    }
    public long AddBout(long categoryId,int no,string stage,long? red,long? blue,int mat=1){
        if(mat<1||mat>6)throw new ArgumentOutOfRangeException(nameof(mat),"Номер ковра должен быть от 1 до 6.");
        using var c=Open();using var q=c.CreateCommand();q.CommandText=@"INSERT INTO bouts(category_id,bout_no,display_no,stage,red_id,blue_id,mat,status,scheduled_time) VALUES($c,$n,$n,$s,$r,$b,$m,'Ожидает','');SELECT last_insert_rowid();";
        q.Parameters.AddWithValue("$c",categoryId);q.Parameters.AddWithValue("$n",no);q.Parameters.AddWithValue("$s",stage);q.Parameters.AddWithValue("$r",(object?)red??DBNull.Value);q.Parameters.AddWithValue("$b",(object?)blue??DBNull.Value);q.Parameters.AddWithValue("$m",mat);return (long)q.ExecuteScalar()!;
    }
    public void SetBoutStatus(long boutId,string status,int mat,string scheduledTime="",int? displayNo=null){
        if(mat<1||mat>6)throw new ArgumentOutOfRangeException(nameof(mat),"Номер ковра должен быть от 1 до 6.");
        using var c=Open();
        if(status=="Завершён")throw new InvalidOperationException("Статус «Завершён» устанавливается только после внесения результата поединка.");
        if(status=="Идёт"){using var chk=c.CreateCommand();chk.CommandText="SELECT COUNT(*) FROM bouts WHERE id<>$id AND mat=$m AND status='Идёт'";chk.Parameters.AddWithValue("$id",boutId);chk.Parameters.AddWithValue("$m",mat);if(Convert.ToInt32(chk.ExecuteScalar())>0)throw new InvalidOperationException($"На ковре {mat} уже есть поединок со статусом «Идёт».");}
        if(displayNo.HasValue){
            using var chk=c.CreateCommand();chk.CommandText=@"SELECT COUNT(*) FROM bouts WHERE id<>$id AND category_id=(SELECT category_id FROM bouts WHERE id=$id) AND COALESCE(NULLIF(display_no,0),bout_no)=$dn";
            chk.Parameters.AddWithValue("$id",boutId);chk.Parameters.AddWithValue("$dn",displayNo.Value);
            if(Convert.ToInt32(chk.ExecuteScalar())>0)throw new InvalidOperationException($"Номер поединка {displayNo.Value} уже используется в этой категории.");
        }
        using var q=c.CreateCommand();q.CommandText="UPDATE bouts SET status=$s,mat=$m,scheduled_time=$t,display_no=COALESCE($dn,display_no) WHERE id=$id";
        q.Parameters.AddWithValue("$s",status);q.Parameters.AddWithValue("$m",mat);q.Parameters.AddWithValue("$t",scheduledTime??"");q.Parameters.AddWithValue("$dn",(object?)displayNo??DBNull.Value);q.Parameters.AddWithValue("$id",boutId);q.ExecuteNonQuery();Audit($"Поединок {boutId}: видимый № {displayNo?.ToString()??"без изменения"}, статус {status}, ковёр {mat}, время {scheduledTime}");
    }
    public void SetBoutResult(long boutId,int redScore,int blueScore,long winnerId,string reason,string judges,string resultCode,int redClass,int blueClass,int durationSeconds,bool isClean,int red4=0,int red2=0,int red1=0,int blue4=0,int blue2=0,int blue1=0){
        using var c=Open();
        string old="";long categoryId=0;
        using(var oldq=c.CreateCommand()){oldq.CommandText="SELECT category_id,COALESCE(red_score,''),COALESCE(blue_score,''),COALESCE(winner_id,''),COALESCE(reason,''),COALESCE(result_code,'') FROM bouts WHERE id=$id";oldq.Parameters.AddWithValue("$id",boutId);using var r=oldq.ExecuteReader();if(r.Read()){categoryId=r.GetInt64(0);old=$"{r.GetValue(1)}:{r.GetValue(2)} winner={r.GetValue(3)} {r.GetValue(4)} {r.GetValue(5)}";}}
        using var q=c.CreateCommand();q.CommandText=@"UPDATE bouts SET red_score=$rs,blue_score=$bs,winner_id=$w,reason=$r,judges=$j,status='Завершён',
            result_code=$rc,red_class=$rcl,blue_class=$bcl,duration_seconds=$dur,is_clean=$clean,red_4=$r4,red_2=$r2,red_1=$r1,blue_4=$b4,blue_2=$b2,blue_1=$b1 WHERE id=$id";
        q.Parameters.AddWithValue("$rs",redScore);q.Parameters.AddWithValue("$bs",blueScore);q.Parameters.AddWithValue("$w",winnerId);q.Parameters.AddWithValue("$r",reason);q.Parameters.AddWithValue("$j",judges);
        q.Parameters.AddWithValue("$rc",resultCode);q.Parameters.AddWithValue("$rcl",redClass);q.Parameters.AddWithValue("$bcl",blueClass);q.Parameters.AddWithValue("$dur",durationSeconds);q.Parameters.AddWithValue("$clean",isClean?1:0);
        q.Parameters.AddWithValue("$r4",red4);q.Parameters.AddWithValue("$r2",red2);q.Parameters.AddWithValue("$r1",red1);q.Parameters.AddWithValue("$b4",blue4);q.Parameters.AddWithValue("$b2",blue2);q.Parameters.AddWithValue("$b1",blue1);q.Parameters.AddWithValue("$id",boutId);q.ExecuteNonQuery();
        if(categoryId>0){
            using(var d=c.CreateCommand()){d.CommandText="DELETE FROM placements WHERE category_id=$c";d.Parameters.AddWithValue("$c",categoryId);d.ExecuteNonQuery();}
            using(var st=c.CreateCommand()){st.CommandText="UPDATE categories SET status='Проводится' WHERE id=$c";st.Parameters.AddWithValue("$c",categoryId);st.ExecuteNonQuery();}
        }
        Audit($"Результат поединка {boutId}: было [{old}], стало [{redScore}:{blueScore} winner={winnerId} {reason} {resultCode}]");
    }
    public void SetBoutResult(long boutId,int redScore,int blueScore,long winnerId,string reason,string judges){
        var b=BoutsByRuleId(boutId);bool red=b.RedId==winnerId;var cp=CompetitionRules.ClassificationFor(resultCode:"",redScore,blueScore,red,reason);
        SetBoutResult(boutId,redScore,blueScore,winnerId,reason,judges,cp.Code,cp.Red,cp.Blue,0,cp.Clean);
    }
    public List<BoutRow> Bouts(long categoryId){
        using var c=Open();using var q=c.CreateCommand();q.CommandText=@"SELECT b.id,b.category_id,b.bout_no,COALESCE(NULLIF(b.display_no,0),b.bout_no),COALESCE(b.stage,''),b.red_id,COALESCE(r.full_name,''),b.blue_id,COALESCE(bl.full_name,''),b.mat,COALESCE(b.scheduled_time,''),COALESCE(b.status,''),b.red_score,b.blue_score,b.winner_id,COALESCE(w.full_name,''),COALESCE(b.reason,''),COALESCE(b.judges,'')
            FROM bouts b LEFT JOIN athletes r ON r.id=b.red_id LEFT JOIN athletes bl ON bl.id=b.blue_id LEFT JOIN athletes w ON w.id=b.winner_id WHERE b.category_id=$c ORDER BY b.bout_no";
        q.Parameters.AddWithValue("$c",categoryId);using var r=q.ExecuteReader();var x=new List<BoutRow>();
        while(r.Read())x.Add(new(r.GetInt64(0),r.GetInt64(1),r.GetInt32(2),r.GetInt32(3),r.GetString(4),r.IsDBNull(5)?null:r.GetInt64(5),r.GetString(6),r.IsDBNull(7)?null:r.GetInt64(7),r.GetString(8),r.GetInt32(9),r.GetString(10),r.GetString(11),r.IsDBNull(12)?null:r.GetInt32(12),r.IsDBNull(13)?null:r.GetInt32(13),r.IsDBNull(14)?null:r.GetInt64(14),r.GetString(15),r.GetString(16),r.GetString(17)));return x;
    }
    public BoutRuleRow BoutsByRuleId(long boutId){
        using var c=Open();using var q=c.CreateCommand();q.CommandText=@"SELECT id,category_id,bout_no,COALESCE(stage,''),red_id,blue_id,COALESCE(status,''),COALESCE(red_score,0),COALESCE(blue_score,0),winner_id,COALESCE(result_code,''),COALESCE(red_class,0),COALESCE(blue_class,0),COALESCE(duration_seconds,0),COALESCE(is_clean,0),COALESCE(red_4,0),COALESCE(red_2,0),COALESCE(red_1,0),COALESCE(blue_4,0),COALESCE(blue_2,0),COALESCE(blue_1,0),COALESCE(reason,'') FROM bouts WHERE id=$id";q.Parameters.AddWithValue("$id",boutId);using var r=q.ExecuteReader();if(!r.Read())throw new InvalidOperationException("Поединок не найден");return new(r.GetInt64(0),r.GetInt64(1),r.GetInt32(2),r.GetString(3),r.IsDBNull(4)?null:r.GetInt64(4),r.IsDBNull(5)?null:r.GetInt64(5),r.GetString(6),r.GetInt32(7),r.GetInt32(8),r.IsDBNull(9)?null:r.GetInt64(9),r.GetString(10),r.GetInt32(11),r.GetInt32(12),r.GetInt32(13),r.GetInt32(14)!=0,r.GetInt32(15),r.GetInt32(16),r.GetInt32(17),r.GetInt32(18),r.GetInt32(19),r.GetInt32(20),r.GetString(21));
    }
    public List<BoutRuleRow> BoutRules(long categoryId){
        using var c=Open();using var q=c.CreateCommand();q.CommandText=@"SELECT id,category_id,bout_no,COALESCE(stage,''),red_id,blue_id,COALESCE(status,''),COALESCE(red_score,0),COALESCE(blue_score,0),winner_id,COALESCE(result_code,''),COALESCE(red_class,0),COALESCE(blue_class,0),COALESCE(duration_seconds,0),COALESCE(is_clean,0),COALESCE(red_4,0),COALESCE(red_2,0),COALESCE(red_1,0),COALESCE(blue_4,0),COALESCE(blue_2,0),COALESCE(blue_1,0),COALESCE(reason,'') FROM bouts WHERE category_id=$c ORDER BY bout_no";q.Parameters.AddWithValue("$c",categoryId);using var r=q.ExecuteReader();var x=new List<BoutRuleRow>();while(r.Read())x.Add(new(r.GetInt64(0),r.GetInt64(1),r.GetInt32(2),r.GetString(3),r.IsDBNull(4)?null:r.GetInt64(4),r.IsDBNull(5)?null:r.GetInt64(5),r.GetString(6),r.GetInt32(7),r.GetInt32(8),r.IsDBNull(9)?null:r.GetInt64(9),r.GetString(10),r.GetInt32(11),r.GetInt32(12),r.GetInt32(13),r.GetInt32(14)!=0,r.GetInt32(15),r.GetInt32(16),r.GetInt32(17),r.GetInt32(18),r.GetInt32(19),r.GetInt32(20),r.GetString(21)));return x;
    }
    public void UpdateBoutParticipants(long boutId,long? red,long? blue){using var c=Open();using var q=c.CreateCommand();q.CommandText="UPDATE bouts SET red_id=$r,blue_id=$b WHERE id=$id AND status<>'Завершён'";q.Parameters.AddWithValue("$r",(object?)red??DBNull.Value);q.Parameters.AddWithValue("$b",(object?)blue??DBNull.Value);q.Parameters.AddWithValue("$id",boutId);q.ExecuteNonQuery();}
    public void ReplaceFutureParticipant(long categoryId,long oldId,long newId,int afterBoutNo){
        using var c=Open();using var q=c.CreateCommand();q.CommandText=@"UPDATE bouts SET red_id=CASE WHEN red_id=$old THEN $new ELSE red_id END,blue_id=CASE WHEN blue_id=$old THEN $new ELSE blue_id END WHERE category_id=$c AND bout_no>$n AND status<>'Завершён' AND (red_id=$old OR blue_id=$old)";
        q.Parameters.AddWithValue("$old",oldId);q.Parameters.AddWithValue("$new",newId);q.Parameters.AddWithValue("$c",categoryId);q.Parameters.AddWithValue("$n",afterBoutNo);q.ExecuteNonQuery();
    }
    public bool HasCompletedFutureDependency(long categoryId,long athleteId,int afterBoutNo){
        using var c=Open();using var q=c.CreateCommand();q.CommandText="SELECT COUNT(*) FROM bouts WHERE category_id=$c AND bout_no>$n AND status='Завершён' AND (red_id=$a OR blue_id=$a)";q.Parameters.AddWithValue("$c",categoryId);q.Parameters.AddWithValue("$n",afterBoutNo);q.Parameters.AddWithValue("$a",athleteId);return Convert.ToInt32(q.ExecuteScalar())>0;
    }
    public void SetCategoryStatus(long categoryId,string status){
        using var c=Open();using var q=c.CreateCommand();q.CommandText="UPDATE categories SET status=$s WHERE id=$id";q.Parameters.AddWithValue("$s",status);q.Parameters.AddWithValue("$id",categoryId);q.ExecuteNonQuery();Audit($"Статус категории {categoryId}: {status}");
    }
    public void ClearPlacements(long categoryId){using var c=Open();using var q=c.CreateCommand();q.CommandText="DELETE FROM placements WHERE category_id=$c";q.Parameters.AddWithValue("$c",categoryId);q.ExecuteNonQuery();}
    public void SavePlacement(long categoryId,long athleteId,int place,string source){using var c=Open();using var q=c.CreateCommand();q.CommandText="INSERT INTO placements(category_id,athlete_id,place,source) VALUES($c,$a,$p,$s)";q.Parameters.AddWithValue("$c",categoryId);q.Parameters.AddWithValue("$a",athleteId);q.Parameters.AddWithValue("$p",place);q.Parameters.AddWithValue("$s",source);q.ExecuteNonQuery();}
    public List<PlacementRow> Placements(long categoryId){using var c=Open();using var q=c.CreateCommand();q.CommandText=@"SELECT p.athlete_id,a.full_name,COALESCE(a.team,''),COALESCE(a.region,''),p.place,COALESCE(p.source,'') FROM placements p JOIN athletes a ON a.id=p.athlete_id WHERE p.category_id=$c ORDER BY p.place,a.full_name";q.Parameters.AddWithValue("$c",categoryId);using var r=q.ExecuteReader();var x=new List<PlacementRow>();while(r.Read())x.Add(new(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetInt32(4),r.GetString(5)));return x;}
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