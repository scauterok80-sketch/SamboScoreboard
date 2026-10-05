namespace SamboSecretary;

public sealed class DrawPositionDialog:Form{
    readonly DataGridView grid=new(){Dock=DockStyle.Fill,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
    readonly int size; readonly bool requireAll;
    public Dictionary<long,int> Positions { get; private set; }=new();
    public DrawPositionDialog(IEnumerable<Athlete> athletes,int bracketSize,bool manual){
        size=bracketSize;requireAll=manual;Text=manual?"Ручная жеребьёвка":"Посев спортсменов";Width=700;Height=600;StartPosition=FormStartPosition.CenterParent;
        grid.Columns.Add("id","ID");grid.Columns["id"]!.Visible=false;grid.Columns.Add("name","Спортсмен");grid.Columns["name"]!.ReadOnly=true;grid.Columns.Add("pos",$"Позиция 1–{size}");
        foreach(var a in athletes)grid.Rows.Add(a.Id,a.FullName,"");
        var info=new Label{Dock=DockStyle.Top,Height=48,Padding=new Padding(10),Text=manual?"Укажите уникальную позицию в сетке для каждого спортсмена.":"Укажите позиции только для сеяных спортсменов. Остальные позиции программа заполнит автоматически."};
        var bottom=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=48,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(8)};
        var ok=new Button{Text="Применить",AutoSize=true};var cancel=new Button{Text="Отмена",DialogResult=DialogResult.Cancel,AutoSize=true};
        ok.Click+=(s,e)=>ValidateAndClose();bottom.Controls.Add(cancel);bottom.Controls.Add(ok);Controls.Add(grid);Controls.Add(info);Controls.Add(bottom);CancelButton=cancel;
    }
    void ValidateAndClose(){
        var result=new Dictionary<long,int>();var used=new HashSet<int>();
        foreach(DataGridViewRow row in grid.Rows){
            long id=Convert.ToInt64(row.Cells["id"].Value);var raw=Convert.ToString(row.Cells["pos"].Value)?.Trim()??"";
            if(raw==""){if(requireAll){MessageBox.Show("В ручном режиме позиция должна быть указана для каждого спортсмена.");return;}continue;}
            if(!int.TryParse(raw,out var p)||p<1||p>size){MessageBox.Show($"Позиции должны быть от 1 до {size}.");return;}
            if(!used.Add(p)){MessageBox.Show($"Позиция {p} указана повторно.");return;}result[id]=p;
        }
        Positions=result;DialogResult=DialogResult.OK;Close();
    }
}