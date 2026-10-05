namespace SamboSecretary;

public sealed class CategoryDialog:Form{
    public ComboBox Discipline=new(){DropDownStyle=ComboBoxStyle.DropDownList};
    public ComboBox Gender=new(){DropDownStyle=ComboBoxStyle.DropDownList};
    public TextBox Age=new();
    public TextBox Weight=new();
    public ComboBox SystemBox=new(){DropDownStyle=ComboBoxStyle.DropDownList};
    public ComboBox Repechage=new(){DropDownStyle=ComboBoxStyle.DropDownList};
    public ComboBox DrawModeBox=new(){DropDownStyle=ComboBoxStyle.DropDownList};
    public CategoryDialog(){
        Text="Категория";Width=470;Height=430;StartPosition=FormStartPosition.CenterParent;
        Discipline.Items.AddRange(["Спортивное самбо","Боевое самбо"]);
        Gender.Items.AddRange(["Мужчины","Женщины","Юноши","Девушки"]);
        SystemBox.Items.AddRange(["Авто","Круговая","Смешанная","Олимпийская"]);
        Repechage.Items.AddRange(["По положению","От финалистов","От полуфиналистов","Без утешительных встреч"]);
        DrawModeBox.Items.AddRange(["Ручная","Посев + автоматическая","Полностью автоматическая"]);
        Discipline.SelectedIndex=0;Gender.SelectedIndex=0;SystemBox.SelectedIndex=0;Repechage.SelectedIndex=0;DrawModeBox.SelectedIndex=2;
        var p=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,Padding=new Padding(14),AutoSize=true};
        AddRow(p,"Дисциплина",Discipline);AddRow(p,"Пол / группа",Gender);AddRow(p,"Возрастная группа",Age);AddRow(p,"Весовая категория",Weight);
        AddRow(p,"Система",SystemBox);AddRow(p,"Утешительные",Repechage);AddRow(p,"Жеребьёвка",DrawModeBox);
        var ok=new Button{Text="Сохранить",DialogResult=DialogResult.OK,AutoSize=true};var cancel=new Button{Text="Отмена",DialogResult=DialogResult.Cancel,AutoSize=true};
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,AutoSize=true};buttons.Controls.Add(cancel);buttons.Controls.Add(ok);
        p.Controls.Add(buttons,0,p.RowCount);p.SetColumnSpan(buttons,2);Controls.Add(p);AcceptButton=ok;CancelButton=cancel;
    }
    static void AddRow(TableLayoutPanel p,string label,Control c){int r=p.RowCount++;p.RowStyles.Add(new RowStyle(SizeType.AutoSize));p.Controls.Add(new Label{Text=label,AutoSize=true,Margin=new Padding(3,9,12,3)},0,r);c.Dock=DockStyle.Fill;p.Controls.Add(c,1,r);}
}

public sealed class AthleteDialog:Form{
    public readonly TextBox FullName=new(),Birth=new(),Region=new(),Organization=new(),Team=new(),Coach=new(),Rank=new(),Age=new(),Weight=new(),DeclaredWeight=new();
    public readonly ComboBox Gender=new(){DropDownStyle=ComboBoxStyle.DropDownList};
    public readonly ComboBox Discipline=new(){DropDownStyle=ComboBoxStyle.DropDownList};
    public AthleteDialog(){
        Text="Спортсмен";Width=520;Height=620;StartPosition=FormStartPosition.CenterParent;
        Gender.Items.AddRange(["","Мужской","Женский"]);Gender.SelectedIndex=0;
        Discipline.Items.AddRange(["Спортивное самбо","Боевое самбо"]);Discipline.SelectedIndex=0;
        var p=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,Padding=new Padding(14),AutoScroll=true};
        Add(p,"ФИО",FullName);Add(p,"Дата рождения",Birth);Add(p,"Пол",Gender);Add(p,"Регион / город",Region);Add(p,"Организация / спортшкола",Organization);
        Add(p,"Команда",Team);Add(p,"Тренер",Coach);Add(p,"Разряд / звание",Rank);Add(p,"Дисциплина",Discipline);Add(p,"Возрастная группа",Age);Add(p,"Весовая категория",Weight);Add(p,"Заявленный вес",DeclaredWeight);
        var ok=new Button{Text="Сохранить",DialogResult=DialogResult.OK,AutoSize=true};var cancel=new Button{Text="Отмена",DialogResult=DialogResult.Cancel,AutoSize=true};
        var b=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,AutoSize=true};b.Controls.Add(cancel);b.Controls.Add(ok);p.Controls.Add(b,0,p.RowCount);p.SetColumnSpan(b,2);
        Controls.Add(p);AcceptButton=ok;CancelButton=cancel;
    }
    static void Add(TableLayoutPanel p,string label,Control c){int r=p.RowCount++;p.RowStyles.Add(new RowStyle(SizeType.AutoSize));p.Controls.Add(new Label{Text=label,AutoSize=true,Margin=new Padding(3,9,12,3)},0,r);c.Dock=DockStyle.Fill;p.Controls.Add(c,1,r);}
}

public sealed class JudgeDialog:Form{
    public readonly TextBox NameBox=new(),Region=new(),Category=new();
    public readonly ComboBox Role=new(){DropDownStyle=ComboBoxStyle.DropDownList};
    public JudgeDialog(){
        Text="Судья";Width=460;Height=330;StartPosition=FormStartPosition.CenterParent;
        Role.Items.AddRange(["Главный судья","Главный секретарь","Руководитель ковра","Арбитр","Боковой судья","Секретарь ковра","Судья при участниках","Судья на взвешивании"]);
        Role.SelectedIndex=3;
        var p=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,Padding=new Padding(14)};
        Add(p,"ФИО",NameBox);Add(p,"Регион",Region);Add(p,"Судейская категория",Category);Add(p,"Роль",Role);
        var ok=new Button{Text="Сохранить",DialogResult=DialogResult.OK,AutoSize=true};var cancel=new Button{Text="Отмена",DialogResult=DialogResult.Cancel,AutoSize=true};
        var b=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,AutoSize=true};b.Controls.Add(cancel);b.Controls.Add(ok);p.Controls.Add(b,0,p.RowCount);p.SetColumnSpan(b,2);Controls.Add(p);AcceptButton=ok;CancelButton=cancel;
    }
    static void Add(TableLayoutPanel p,string label,Control c){int r=p.RowCount++;p.RowStyles.Add(new RowStyle(SizeType.AutoSize));p.Controls.Add(new Label{Text=label,AutoSize=true,Margin=new Padding(3,9,12,3)},0,r);c.Dock=DockStyle.Fill;p.Controls.Add(c,1,r);}
}

public sealed class ImportMappingDialog:Form{
    readonly Dictionary<string,ComboBox> boxes=new();
    public Dictionary<string,string> Mapping=>boxes.ToDictionary(x=>x.Key,x=>x.Value.SelectedItem?.ToString()??"");
    public ImportMappingDialog(string[] headers,List<Dictionary<string,string>> rows){
        Text="Импорт Excel — сопоставление колонок";Width=900;Height=720;StartPosition=FormStartPosition.CenterParent;
        var split=new SplitContainer{Dock=DockStyle.Fill,Orientation=Orientation.Horizontal,SplitterDistance=370};
        var p=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,Padding=new Padding(10),AutoScroll=true};
        var fields=new (string Label,string[] Keys)[]{
            ("ФИО",new[]{"фио","ф.и.о","спортсмен"}),("Фамилия",new[]{"фамил"}),("Имя",new[]{"имя"}),("Отчество",new[]{"отчеств"}),
            ("Дата рождения",new[]{"рожд"}),("Пол",new[]{"пол"}),("Регион",new[]{"регион","город"}),("Организация",new[]{"организац","школ"}),
            ("Команда",new[]{"команд"}),("Тренер",new[]{"тренер"}),("Разряд",new[]{"разряд","звание"}),("Дисциплина",new[]{"дисцип"}),
            ("Возрастная группа",new[]{"возраст"}),("Весовая категория",new[]{"категор","весов"}),("Заявленный вес",new[]{"заявлен","вес"})
        };
        int i=0;
        foreach(var f in fields){
            var cb=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=240};cb.Items.Add("");cb.Items.AddRange(headers);
            var guess=ExcelImporter.GuessHeader(headers,f.Keys);if(guess!="")cb.SelectedItem=guess;else cb.SelectedIndex=0;boxes[f.Label]=cb;
            int col=(i%2)*2,row=i/2;p.Controls.Add(new Label{Text=f.Label,AutoSize=true,Margin=new Padding(3,8,6,3)},col,row);p.Controls.Add(cb,col+1,row);i++;
        }
        split.Panel1.Controls.Add(p);
        var grid=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells};
        grid.Columns.Clear();
        foreach(var h in headers) grid.Columns.Add(h,h);
        foreach(var row in rows.Take(20)){int n=grid.Rows.Add();foreach(DataGridViewColumn c in grid.Columns)grid.Rows[n].Cells[c.Index].Value=row.TryGetValue(c.Name,out var v)?v:"";}
        split.Panel2.Controls.Add(grid);
        var bottom=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=48,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(8)};
        var ok=new Button{Text="Импортировать",DialogResult=DialogResult.OK,AutoSize=true};var cancel=new Button{Text="Отмена",DialogResult=DialogResult.Cancel,AutoSize=true};bottom.Controls.Add(cancel);bottom.Controls.Add(ok);
        Controls.Add(split);Controls.Add(bottom);AcceptButton=ok;CancelButton=cancel;
    }
}

public sealed class ResultDialog:Form{
    public readonly NumericUpDown RedScore=new(){Minimum=0,Maximum=999},BlueScore=new(){Minimum=0,Maximum=999};
    public readonly ComboBox Winner=new(){DropDownStyle=ComboBoxStyle.DropDownList};
    public readonly ComboBox Reason=new(){DropDownStyle=ComboBoxStyle.DropDownList};
    public readonly TextBox Judges=new();
    public ResultDialog(string red,string blue,string defaultJudges){
        Text="Результат поединка";Width=520;Height=360;StartPosition=FormStartPosition.CenterParent;
        Winner.Items.AddRange([red,blue]);Winner.SelectedIndex=0;
        Reason.Items.AddRange(["По очкам","Чистая победа","Болевой приём","Явное преимущество","Техническая победа","По замечаниям","Снятие врачом / травма","Неявка","Дисквалификация"]);
        Reason.SelectedIndex=0;Judges.Text=defaultJudges;
        var p=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,Padding=new Padding(14)};
        Add(p,$"Баллы: {red}",RedScore);Add(p,$"Баллы: {blue}",BlueScore);Add(p,"Победитель",Winner);Add(p,"Причина победы",Reason);Add(p,"Судьи",Judges);
        var ok=new Button{Text="Подтвердить результат",DialogResult=DialogResult.OK,AutoSize=true};var cancel=new Button{Text="Отмена",DialogResult=DialogResult.Cancel,AutoSize=true};
        var b=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,AutoSize=true};b.Controls.Add(cancel);b.Controls.Add(ok);p.Controls.Add(b,0,p.RowCount);p.SetColumnSpan(b,2);Controls.Add(p);AcceptButton=ok;CancelButton=cancel;
    }
    static void Add(TableLayoutPanel p,string label,Control c){int r=p.RowCount++;p.RowStyles.Add(new RowStyle(SizeType.AutoSize));p.Controls.Add(new Label{Text=label,AutoSize=true,Margin=new Padding(3,9,12,3)},0,r);c.Dock=DockStyle.Fill;p.Controls.Add(c,1,r);}
}

static class TupleExt{
    public static IEnumerable<string> Skip(this (string,string,string,string) t,int n)=>new[]{t.Item1,t.Item2,t.Item3,t.Item4}.Skip(n);
    public static IEnumerable<string> Skip(this (string,string) t,int n)=>new[]{t.Item1,t.Item2}.Skip(n);
    public static IEnumerable<string> Skip(this (string,string,string) t,int n)=>new[]{t.Item1,t.Item2,t.Item3}.Skip(n);
}