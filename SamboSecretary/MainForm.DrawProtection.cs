namespace SamboSecretary;

// Дополнение BETA9.3: понятная защита сформированной сетки и её статус в категориях.
public sealed partial class MainForm{
    protected override void OnShown(EventArgs e){
        base.OnShown(e);
        RenameDrawProtectionButtons(this);
        categoryGrid.DataBindingComplete+=(s,args)=>UpdateCategoryDrawStatuses();
        UpdateCategoryDrawStatuses();
    }

    static void RenameDrawProtectionButtons(Control parent){
        foreach(Control control in parent.Controls){
            if(control is Button button){
                if(button.Text=="Утвердить")button.Text="🔒 Защитить сетку";
                else if(button.Text=="Разблокировать")button.Text="🔓 Снять защиту";
            }
            if(control.HasChildren)RenameDrawProtectionButtons(control);
        }
    }

    void UpdateCategoryDrawStatuses(){
        if(categoryGrid.Columns.Contains("Утверждена"))categoryGrid.Columns["Утверждена"].HeaderText="Защита";
        if(!categoryGrid.Columns.Contains("Статус сетки")){
            categoryGrid.Columns.Add(new DataGridViewTextBoxColumn{
                Name="Статус сетки",HeaderText="Статус сетки",ReadOnly=true,AutoSizeMode=DataGridViewAutoSizeColumnMode.AllCells
            });
        }
        foreach(DataGridViewRow row in categoryGrid.Rows){
            if(row.IsNewRow||row.Cells["ID"].Value==null)continue;
            if(!long.TryParse(row.Cells["ID"].Value.ToString(),out var categoryId))continue;
            bool protectedDraw=row.Cells["Утверждена"].Value is bool value&&value;
            bool formed=db.DrawPositions(categoryId).Count>0;
            row.Cells["Статус сетки"].Value=protectedDraw
                ?"🔒 Сетка сформирована и защищена"
                :formed?"Сетка сформирована"
                :"Подготовка";
        }
    }
}
