namespace SamboSecretary;

public sealed partial class MainForm{
    readonly ComboBox judgeAssignmentCategory=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=340};
    readonly NumericUpDown judgeAssignmentMat=new(){Minimum=1,Maximum=12,Value=1,Width=70};
    readonly DataGridView judgeAssignmentGrid=Grid();
    void BuildTournamentTab(){
        var page=Page("Турнир");
        var p=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Padding=new Padding(18),MaximumSize=new Size(900,0)};
        AddField(p,"Название соревнования",tName);AddField(p,"Место проведения",tPlace);AddField(p,"Дата начала",tStart);AddField(p,"Дата окончания",tEnd);AddField(p,"Количество ковров",tMats);AddField(p,"Главный судья",tChiefRef);AddField(p,"Главный секретарь",tChiefSec);
        var save=Btn("Сохранить данные турнира",(s,e)=>{db.SaveTournament(tName.Text,tPlace.Text,tStart.Text,tEnd.Text,(int)tMats.Value,tChiefRef.Text,tChiefSec.Text);MessageBox.Show("Данные турнира сохранены.");ReloadAudit();});
        p.Controls.Add(save,1,p.RowCount);page.Controls.Add(p);
    }
    static void AddField(TableLayoutPanel p,string label,Control c){int r=p.RowCount++;p.RowStyles.Add(new RowStyle(SizeType.AutoSize));p.Controls.Add(new Label{Text=label,AutoSize=true,Margin=new Padding(3,10,15,3)},0,r);c.Dock=DockStyle.Fill;c.Width=500;p.Controls.Add(c,1,r);}
    void LoadTournament(){var t=db.GetTournament();tName.Text=t.Name;tPlace.Text=t.Place;tStart.Text=t.StartDate;tEnd.Text=t.EndDate;tMats.Value=Math.Clamp(t.Mats,1,12);tChiefRef.Text=t.ChiefReferee;tChiefSec.Text=t.ChiefSecretary;}

    void BuildCategoriesTab(){
        var page=Page("Категории");
        var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=48,Padding=new Padding(4)};
        bar.Controls.Add(Btn("Добавить категорию",AddCategoryClick));
        bar.Controls.Add(Btn("Удалить выбранную",(s,e)=>{var id=SelectedId(categoryGrid);if(!id.HasValue)return;if(MessageBox.Show("Удалить выбранную категорию?","Подтверждение",MessageBoxButtons.YesNo)==DialogResult.Yes){db.DeleteCategory(id.Value);ReloadAll();}}));
        page.Controls.Add(categoryGrid);page.Controls.Add(bar);
    }
    void AddCategoryClick(object? s,EventArgs e){
        using var d=new CategoryDialog();if(d.ShowDialog(this)!=DialogResult.OK)return;
        if(string.IsNullOrWhiteSpace(d.Weight.Text)){MessageBox.Show("Укажите весовую категорию.");return;}
        db.AddCategory(d.Discipline.Text,d.Gender.Text,d.Age.Text.Trim(),d.Weight.Text.Trim(),d.SystemBox.Text,d.Repechage.Text,d.DrawModeBox.Text);ReloadAll();
    }
    void ReloadCategories(){
        categoryGrid.DataSource=db.Categories().Select(x=>new{ID=x.Id,Дисциплина=x.Discipline,Пол=x.Gender,Возраст=x.AgeGroup,Весовая_категория=x.WeightCategory,Система=x.System,Утешительные=x.Repechage,Жеребьёвка=x.DrawMode,Утверждена=x.DrawApproved,Статус=x.Status}).ToList();
    }

    void BuildAthletesTab(){
        var page=Page("Участники");
        var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=50,Padding=new Padding(4),AutoScroll=true};
        bar.Controls.Add(Btn("Добавить спортсмена",AddAthleteClick));
        bar.Controls.Add(Btn("Импорт Excel",ImportExcelClick));
        bar.Controls.Add(Btn("Создать Excel-шаблон",(s,e)=>{using var save=new SaveFileDialog{Filter="Excel (*.xlsx)|*.xlsx",FileName="Заявка_Самбо.xlsx"};if(save.ShowDialog(this)==DialogResult.OK){ExcelImporter.CreateTemplate(save.FileName);MessageBox.Show("Шаблон сохранён.");}}));
        bar.Controls.Add(new Label{Text="Назначить категорию:",AutoSize=true,Margin=new Padding(15,12,4,0)});
        bar.Controls.Add(assignCategory);
        bar.Controls.Add(Btn("Назначить",(s,e)=>{var id=SelectedId(athleteGrid);var cid=SelectedCategory(assignCategory);if(id.HasValue&&cid.HasValue){db.AssignCategory(id.Value,cid.Value);ReloadAll();}}));
        page.Controls.Add(athleteGrid);page.Controls.Add(bar);
    }
    void AddAthleteClick(object? s,EventArgs e){
        using var d=new AthleteDialog();if(d.ShowDialog(this)!=DialogResult.OK)return;
        if(string.IsNullOrWhiteSpace(d.FullName.Text)){MessageBox.Show("Введите ФИО.");return;}
        double? dw=ParseDouble(d.DeclaredWeight.Text);
        long? cid=FindCategory(d.Discipline.Text,d.Gender.Text,d.Age.Text,d.Weight.Text);
        db.AddAthlete(d.FullName.Text,d.Birth.Text,d.Gender.Text,d.Region.Text,d.Organization.Text,d.Team.Text,d.Coach.Text,d.Rank.Text,d.Discipline.Text,d.Age.Text,d.Weight.Text,dw,cid);ReloadAll();
    }
    long? FindCategory(string discipline,string gender,string age,string weight){
        var c=db.Categories().FirstOrDefault(x=>Eq(x.Discipline,discipline)&&Eq(x.Gender,gender)&&Eq(x.AgeGroup,age)&&Eq(x.WeightCategory,weight));return c?.Id;
    }
    static bool Eq(string a,string b)=>string.Equals(a?.Trim(),b?.Trim(),StringComparison.OrdinalIgnoreCase);
    static double? ParseDouble(string? s){if(string.IsNullOrWhiteSpace(s))return null;return double.TryParse(s.Replace(',','.'),System.Globalization.NumberStyles.Any,System.Globalization.CultureInfo.InvariantCulture,out var v)?v:null;}

    void ImportExcelClick(object? s,EventArgs e){
        using var o=new OpenFileDialog{Filter="Excel (*.xlsx)|*.xlsx",Title="Выберите файл заявок"};if(o.ShowDialog(this)!=DialogResult.OK)return;
        try{
            var data=ExcelImporter.Read(o.FileName);if(data.Rows.Count==0){MessageBox.Show("В файле нет строк для импорта.");return;}
            using var mapDlg=new ImportMappingDialog(data.Headers,data.Rows);if(mapDlg.ShowDialog(this)!=DialogResult.OK)return;var m=mapDlg.Mapping;
            string M(string field)=>m.TryGetValue(field,out var h)?h:"";
            string V(Dictionary<string,string> r,string field)=>ExcelImporter.Value(r,M(field));
            var parsed=new List<(Dictionary<string,string> Row,string Name)>();
            foreach(var row in data.Rows){
                var whole=V(row,"ФИО");
                var name=whole!=""?NameNormalizer.Normalize(whole):NameNormalizer.Normalize(V(row,"Фамилия"),V(row,"Имя"),V(row,"Отчество"));
                if(name!="")parsed.Add((row,name));
            }
            var existing=db.Athletes().Select(a=>a.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            int dup=parsed.Count(x=>existing.Contains(x.Name));bool importDup=false;
            if(dup>0){
                var ans=MessageBox.Show($"Найдено совпадений по ФИО: {dup}.\n\nДа — импортировать и совпадения.\nНет — пропустить совпадения.\nОтмена — отменить импорт.","Дубликаты",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);
                if(ans==DialogResult.Cancel)return;importDup=ans==DialogResult.Yes;
            }
            int added=0,skipped=0;
            foreach(var item in parsed){
                if(existing.Contains(item.Name)&&!importDup){skipped++;continue;}
                var row=item.Row;var discipline=V(row,"Дисциплина");if(discipline=="")discipline="Спортивное самбо";
                var gender=V(row,"Пол");var age=V(row,"Возрастная группа");var weight=V(row,"Весовая категория");
                var cid=FindCategory(discipline,gender,age,weight);
                db.AddAthlete(item.Name,V(row,"Дата рождения"),gender,V(row,"Регион"),V(row,"Организация"),V(row,"Команда"),V(row,"Тренер"),V(row,"Разряд"),discipline,age,weight,ParseDouble(V(row,"Заявленный вес")),cid);
                added++;
            }
            MessageBox.Show($"Импорт завершён.\nДобавлено: {added}\nПропущено совпадений: {skipped}");ReloadAll();
        }catch(Exception ex){MessageBox.Show(ex.Message,"Ошибка импорта",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    }

    void ReloadAthletes(){
        athleteGrid.DataSource=db.Athletes().Select(x=>new{ID=x.Id,ФИО=x.FullName,Дата_рождения=x.BirthDate,Пол=x.Gender,Регион=x.Region,Организация=x.Organization,Команда=x.Team,Тренер=x.Coach,Разряд=x.Rank,Дисциплина=x.Discipline,Возраст=x.AgeGroup,Весовая_категория=x.WeightCategory,Заявленный_вес=x.DeclaredWeight,Фактический_вес=x.ActualWeight,Статус=x.Status,Категория_ID=x.CategoryId}).ToList();
    }

    void BuildWeighTab(){
        var page=Page("Взвешивание");
        var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=52,Padding=new Padding(4)};
        var kg=new TextBox{Width=90};var status=new ComboBox{Width=170,DropDownStyle=ComboBoxStyle.DropDownList};status.Items.AddRange(["Допущен","Не допущен","Ожидает решения"]);status.SelectedIndex=0;
        bar.Controls.Add(new Label{Text="Фактический вес, кг:",AutoSize=true,Margin=new Padding(6,13,4,0)});bar.Controls.Add(kg);
        bar.Controls.Add(new Label{Text="Решение:",AutoSize=true,Margin=new Padding(12,13,4,0)});bar.Controls.Add(status);
        bar.Controls.Add(Btn("Сохранить взвешивание",(s,e)=>{
            var id=SelectedId(weighGrid);var v=ParseDouble(kg.Text);if(!id.HasValue||!v.HasValue){MessageBox.Show("Выберите спортсмена и укажите вес.");return;}
            var a=db.Athletes().First(x=>x.Id==id.Value);string decision=status.Text;
            if(WeightRules.IsOverweight(a.WeightCategory,v.Value,out var max)){
                var ans=MessageBox.Show($"Фактический вес {v.Value:0.##} кг превышает верхнюю границу заявленной категории {a.WeightCategory} ({max:0.##} кг).\n\nДа — сохранить выбранное решение «{status.Text}» как решение комиссии.\nНет — сохранить вес со статусом «Ожидает решения».","Несоответствие весовой категории",MessageBoxButtons.YesNo,MessageBoxIcon.Warning);
                if(ans==DialogResult.No)decision="Ожидает решения";
                db.Audit($"Предупреждение о перевесе: {a.FullName}, категория {a.WeightCategory}, фактический вес {v.Value:0.##}; решение: {decision}");
            }
            db.SetWeigh(id.Value,v.Value,decision);AutoBackup();ReloadAll();
        }));
        bar.Controls.Add(Btn("Изменить только допуск",(s,e)=>{var id=SelectedId(weighGrid);if(id.HasValue){db.UpdateAthleteStatus(id.Value,status.Text);ReloadAll();}}));
        page.Controls.Add(weighGrid);page.Controls.Add(bar);
    }
    void ReloadWeigh(){weighGrid.DataSource=db.Athletes().Select(x=>new{ID=x.Id,ФИО=x.FullName,Категория=x.WeightCategory,Заявленный=x.DeclaredWeight,Фактический=x.ActualWeight,Статус=x.Status,Команда=x.Team}).ToList();}

    void BuildJudgesTab(){
        var page=Page("Судьи");var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=88,Padding=new Padding(4),AutoScroll=true};
        bar.Controls.Add(Btn("Добавить судью",(s,e)=>{using var d=new JudgeDialog();if(d.ShowDialog(this)==DialogResult.OK&&d.NameBox.Text.Trim()!=""){db.AddJudge(d.NameBox.Text,d.Region.Text,d.Category.Text,d.Role.Text);ReloadAll();}}));
        bar.Controls.Add(new Label{Text="Категория:",AutoSize=true,Margin=new Padding(12,13,3,0)});bar.Controls.Add(judgeAssignmentCategory);
        bar.Controls.Add(new Label{Text="Ковёр:",AutoSize=true,Margin=new Padding(10,13,3,0)});bar.Controls.Add(judgeAssignmentMat);
        bar.Controls.Add(Btn("Назначить выбранного судью",(s,e)=>{
            var jid=SelectedId(judgeGrid);var cid=SelectedCategory(judgeAssignmentCategory);if(!jid.HasValue||!cid.HasValue){MessageBox.Show("Выберите судью и категорию.");return;}
            var j=db.Judges().First(x=>x.Id==jid.Value);db.AssignJudge(cid.Value,jid.Value,(int)judgeAssignmentMat.Value,j.Role);ReloadJudgeAssignments();
        }));
        bar.Controls.Add(Btn("Удалить назначение",(s,e)=>{var id=SelectedId(judgeAssignmentGrid);if(id.HasValue){db.RemoveJudgeAssignment(id.Value);ReloadJudgeAssignments();}}));
        judgeAssignmentCategory.SelectedIndexChanged+=(s,e)=>ReloadJudgeAssignments();
        var split=new SplitContainer{Dock=DockStyle.Fill,Orientation=Orientation.Horizontal,SplitterDistance=300};split.Panel1.Controls.Add(judgeGrid);split.Panel2.Controls.Add(judgeAssignmentGrid);
        page.Controls.Add(split);page.Controls.Add(bar);
    }
    void ReloadJudges(){judgeGrid.DataSource=db.Judges().Select(x=>new{ID=x.Id,ФИО=x.Name,Регион=x.Region,Категория=x.Category,Роль=x.Role}).ToList();}
    void ReloadJudgeAssignments(){
        var cid=SelectedCategory(judgeAssignmentCategory);var cats=db.Categories().ToDictionary(x=>x.Id,x=>$"{x.Discipline} {x.Gender} {x.AgeGroup} {x.WeightCategory}");
        judgeAssignmentGrid.DataSource=db.JudgeAssignments(cid).Select(x=>new{ID=x.Id,Категория=cats.TryGetValue(x.CategoryId,out var n)?n:x.CategoryId.ToString(),Ковёр=x.Mat,ФИО=x.JudgeName,Роль=x.Role}).ToList();
    }
}