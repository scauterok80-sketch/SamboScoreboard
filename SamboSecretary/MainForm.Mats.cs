namespace SamboSecretary;

public sealed partial class MainForm{
    readonly DataGridView matSummaryGrid=Grid();
    readonly DataGridView matQueueGrid=Grid();

    void BuildMatsTab(){
        var page=Page("Ковры");
        var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=48,Padding=new Padding(4)};
        bar.Controls.Add(Btn("Обновить ковры",(s,e)=>ReloadMats()));
        var split=new SplitContainer{Dock=DockStyle.Fill,Orientation=Orientation.Horizontal,SplitterDistance=230};
        split.Panel1.Controls.Add(matSummaryGrid);split.Panel2.Controls.Add(matQueueGrid);
        page.Controls.Add(split);page.Controls.Add(bar);
    }

    void ReloadMats(){
        int mats=(int)tMats.Value;var cats=db.Categories().ToDictionary(x=>x.Id,x=>$"{x.Discipline} | {x.Gender} | {x.AgeGroup} | {x.WeightCategory}");
        var all=db.Categories().SelectMany(c=>db.Bouts(c.Id)).OrderBy(b=>b.Mat).ThenBy(b=>b.Status=="Идёт"?0:b.Status=="Вызван"?1:b.Status=="Готов"?2:3).ThenBy(b=>string.IsNullOrWhiteSpace(b.ScheduledTime)?"99:99":b.ScheduledTime).ThenBy(b=>b.DisplayNo).ToList();
        var summary=new List<object>();
        for(int m=1;m<=mats;m++){
            var q=all.Where(b=>b.Mat==m&&b.Status!="Завершён").ToList();
            var current=q.FirstOrDefault(b=>b.Status=="Идёт")??q.FirstOrDefault(b=>b.Status=="Вызван");
            var next=q.FirstOrDefault(b=>b.Id!=current?.Id&&(b.Status=="Готов"||b.Status=="Ожидает"));
            summary.Add(new{Ковёр=m,Текущая=current==null?"—":$"№{current.DisplayNo} {current.RedName} — {current.BlueName}",Следующая=next==null?"—":$"№{next.DisplayNo} {next.RedName} — {next.BlueName}",Ожидают=q.Count(b=>b.Id!=current?.Id&&b.Id!=next?.Id)});
        }
        matSummaryGrid.DataSource=summary;
        matQueueGrid.DataSource=all.Select(b=>new{Ковёр=b.Mat,Категория=cats.TryGetValue(b.CategoryId,out var n)?n:b.CategoryId.ToString(),Номер=b.DisplayNo,План=b.ScheduledTime,Этап=b.Stage,Красный=b.RedName,Синий=b.BlueName,Статус=b.Status}).ToList();
    }
}