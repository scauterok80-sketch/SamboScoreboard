namespace SamboSecretary;

public sealed class RankingOverrideDialog:Form{
    readonly DataGridView grid=new(){Dock=DockStyle.Fill,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells};
    public List<long> OrderedIds{get;private set;}=new();
    public RankingOverrideDialog(IEnumerable<Athlete> athletes,Dictionary<long,RankMetric> metrics){
        Text="Решение ГСК при полном равенстве";Width=1100;Height=520;StartPosition=FormStartPosition.CenterParent;
        var info=new Label{Dock=DockStyle.Top,Height=60,Padding=new Padding(10),Text="Автоматические критерии не разделили спортсменов. Укажите порядок 1..N только внутри этой спорной группы. Решение будет записано в журнал."};
        grid.Columns.Add("id","ID");grid.Columns["id"]!.Visible=false;grid.Columns.Add("name","Спортсмен");grid.Columns["name"]!.ReadOnly=true;
        grid.Columns.Add("cp","Классиф. очки");grid.Columns["cp"]!.ReadOnly=true;grid.Columns.Add("wins","Победы");grid.Columns["wins"]!.ReadOnly=true;
        grid.Columns.Add("mw","Победы во взаимных");grid.Columns["mw"]!.ReadOnly=true;grid.Columns.Add("mc","Очки во взаимных");grid.Columns["mc"]!.ReadOnly=true;
        grid.Columns.Add("clean","Чистые 4:0");grid.Columns["clean"]!.ReadOnly=true;grid.Columns.Add("w30","3:0");grid.Columns["w30"]!.ReadOnly=true;
        grid.Columns.Add("w31","3:1");grid.Columns["w31"]!.ReadOnly=true;grid.Columns.Add("w20","2:0");grid.Columns["w20"]!.ReadOnly=true;
        grid.Columns.Add("tech","Тех. разница");grid.Columns["tech"]!.ReadOnly=true;grid.Columns.Add("order","Порядок 1..N");
        foreach(var a in athletes){
            var m=metrics[a.Id];grid.Rows.Add(a.Id,a.FullName,m.ClassPoints,m.Wins,m.MutualWins,m.MutualClass,m.CleanWins,m.Win30,m.Win31,m.Win20,m.TechDiff,"");
        }
        var bottom=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=48,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(8)};
        var ok=new Button{Text="Зафиксировать решение",AutoSize=true};var cancel=new Button{Text="Отмена",DialogResult=DialogResult.Cancel,AutoSize=true};
        ok.Click+=(s,e)=>AcceptOrder();bottom.Controls.Add(cancel);bottom.Controls.Add(ok);Controls.Add(grid);Controls.Add(info);Controls.Add(bottom);CancelButton=cancel;
    }
    void AcceptOrder(){
        var list=new List<(long Id,int Order)>();var used=new HashSet<int>();int n=grid.Rows.Count;
        foreach(DataGridViewRow r in grid.Rows){
            if(!int.TryParse(Convert.ToString(r.Cells["order"].Value),out var p)||p<1||p>n||!used.Add(p)){MessageBox.Show($"Введите уникальные значения от 1 до {n}.");return;}
            list.Add((Convert.ToInt64(r.Cells["id"].Value),p));
        }
        OrderedIds=list.OrderBy(x=>x.Order).Select(x=>x.Id).ToList();DialogResult=DialogResult.OK;Close();
    }
}