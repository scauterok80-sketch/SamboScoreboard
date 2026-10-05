namespace SamboSecretary;

public sealed partial class MainForm{
    readonly ComboBox judgeAssignmentCategory=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=340};
    readonly NumericUpDown judgeAssignmentMat=new(){Minimum=1,Maximum=12,Value=1,Width=70};
    readonly DataGridView judgeAssignmentGrid=Grid();
    readonly Label tournamentStats=new(){AutoSize=true,Font=new Font("Segoe UI",11,FontStyle.Bold),Padding=new Padding(8),Margin=new Padding(3,18,3,3)};
    void BuildTournamentTab(){
        var page=Page("Турнир");
        var p=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Padding=new Padding(18),MaximumSize=new Size(900,0)};
        AddField(p,"Название соревнования",tName);AddField(p,"Место проведения",tPlace);AddField(p,"Дата начала",tStart);AddField(p,"Дата окончания",tEnd);AddField(p,"Количество ковров",tMats);AddField(p,"Главный судья",tChiefRef);AddField(p,"Главный секретарь",tChiefSec);AddField(p,"Командные очки 1/2/3/5-е места",tTeamScheme);
        var save=Btn("Сохранить данные турнира",(s,e)=>{db.SaveTournament(tName.Text,tPlace.Text,tStart.Text,tEnd.Text,(int)tMats.Value,tChiefRef.Text,tChiefSec.Text,tTeamScheme.Text);AutoBackup();MessageBox.Show("Данные турнира сохранены.");ReloadAll();});
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};
        buttons.Controls.Add(save);buttons.Controls.Add(Btn("Новый турнир",NewTournamentClick));buttons.Controls.Add(Btn("Открыть архив",OpenTournamentArchiveClick));
        p.Controls.Add(buttons,1,p.RowCount);int rr=p.RowCount++;p.Controls.Add(tournamentStats,0,rr);p.SetColumnSpan(tournamentStats,2);page.Controls.Add(p);
    }
    void ReloadDashboard(){
        var athletes=db.Athletes();var cats=db.Categories();var bouts=cats.SelectMany(x=>db.Bouts(x.Id)).ToList();
        tournamentStats.Text=$"Участников: {athletes.Count}    Допущено: {athletes.Count(x=>x.Status=="Допущен")}    Взвешено: {athletes.Count(x=>x.ActualWeight.HasValue)}\n"+
            $"Категорий: {cats.Count}    Жеребьёвок утверждено: {cats.Count(x=>x.DrawApproved)}    Поединков: {bouts.Count(x=>x.Status=="Завершён")}/{bouts.Count}    Ковров: {(int)tMats.Value}";
    }
    void NewTournamentClick(object? s,EventArgs e){
        var ans=MessageBox.Show("Текущий турнир будет сохранён в архив, после чего рабочая база будет очищена. Создать новый турнир?","Новый турнир",MessageBoxButtons.YesNo,MessageBoxIcon.Warning);
        if(ans!=DialogResult.Yes)return;
        try{
            var t=db.GetTournament();var dir=Path.Combine(root,"TournamentArchive");Directory.CreateDirectory(dir);
            string raw=string.IsNullOrWhiteSpace(t.Name)?"Tournament":t.Name;
            var invalid=Path.GetInvalidFileNameChars();var safe=new string(raw.Select(ch=>invalid.Contains(ch)?'_':ch).ToArray());
            var archive=Path.Combine(dir,$"{safe}_{DateTime.Now:yyyyMMdd_HHmmss}.db");File.Copy(db.FileName,archive,true);
            db.ResetForNewTournament();LoadTournament();ReloadAll();MessageBox.Show($"Новый турнир создан.\nАрхив предыдущего: {archive}");
        }catch(Exception ex){MessageBox.Show(ex.Message,"Ошибка создания нового турнира");}
    }

    void OpenTournamentArchiveClick(object? s,EventArgs e){
        using var o=new OpenFileDialog{Filter="База турнира (*.db)|*.db",InitialDirectory=Path.Combine(root,"TournamentArchive"),Title="Открыть архив турнира"};
        if(o.ShowDialog(this)!=DialogResult.OK)return;
        var ans=MessageBox.Show("Текущий турнир будет автоматически сохранён в архив и заменён выбранной базой. Продолжить?","Открыть архив",MessageBoxButtons.YesNo,MessageBoxIcon.Warning);
        if(ans!=DialogResult.Yes)return;
        try{
            var t=db.GetTournament();var dir=Path.Combine(root,"TournamentArchive");Directory.CreateDirectory(dir);
            string raw=string.IsNullOrWhiteSpace(t.Name)?"Tournament":t.Name;var invalid=Path.GetInvalidFileNameChars();var safe=new string(raw.Select(ch=>invalid.Contains(ch)?'_':ch).ToArray());
            File.Copy(db.FileName,Path.Combine(dir,$"{safe}_{DateTime.Now:yyyyMMdd_HHmmss}_before_restore.db"),true);
            File.Copy(o.FileName,db.FileName,true);LoadTournament();ReloadAll();MessageBox.Show("Архивный турнир открыт.");
        }catch(Exception ex){MessageBox.Show(ex.Message,"Ошибка открытия архива");}
    }

    static void AddField(TableLayoutPanel p,string label,Control c){int r=p.RowCount++;p.RowStyles.Add(new RowStyle(SizeType.AutoSize));p.Controls.Add(new Label{Text=label,AutoSize=true,Margin=new Padding(3,10,15,3)},0,r);c.Dock=DockStyle.Fill;c.Width=500;p.Controls.Add(c,1,r);}
    void LoadTournament(){var t=db.GetTournament();tName.Text=t.Name;tPlace.Text=t.Place;tStart.Text=t.StartDate;tEnd.Text=t.EndDate;tMats.Value=Math.Clamp(t.Mats,1,12);tChiefRef.Text=t.ChiefReferee;tChiefSec.Text=t.ChiefSecretary;tTeamScheme.Text=t.TeamScheme;}

    void BuildCategoriesTab(){
        var page=Page("Категории");
        var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=48,Padding=new Padding(4)};
        bar.Controls.Add(Btn("Добавить категорию",AddCategoryClick));
        bar.Controls.Add(Btn("Редактировать выбранную",EditCategoryClick));
        bar.Controls.Add(Btn("Удалить выбранную",(s,e)=>{var id=SelectedId(categoryGrid);if(!id.HasValue)return;if(MessageBox.Show("Удалить выбранную категорию?","Подтверждение",MessageBoxButtons.YesNo)==DialogResult.Yes)UiGuard(()=>{db.DeleteCategory(id.Value);ReloadAll();},"Категория не удалена");}));
        page.Controls.Add(categoryGrid);page.Controls.Add(bar);
    }
    void AddCategoryClick(object? s,EventArgs e){
        using var d=new CategoryDialog();if(d.ShowDialog(this)!=DialogResult.OK)return;
        if(string.IsNullOrWhiteSpace(d.Weight.Text)){MessageBox.Show("Укажите весовую категорию.");return;}
        db.AddCategory(d.Discipline.Text,d.Gender.Text,d.Age.Text.Trim(),d.Weight.Text.Trim(),d.SystemBox.Text,d.Repechage.Text,d.DrawModeBox.Text);ReloadAll();
    }
    void EditCategoryClick(object? s,EventArgs e){
        var id=SelectedId(categoryGrid);if(!id.HasValue)return;var x=db.Categories().FirstOrDefault(c=>c.Id==id.Value);if(x==null)return;
        if(x.DrawApproved){MessageBox.Show("Сначала разблокируйте утверждённую жеребьёвку категории.");return;}
        using var d=new CategoryDialog();d.Discipline.SelectedItem=x.Discipline;d.Gender.SelectedItem=x.Gender;d.Age.Text=x.AgeGroup;d.Weight.Text=x.WeightCategory;
        if(d.SystemBox.Items.Contains(x.System))d.SystemBox.SelectedItem=x.System;if(d.Repechage.Items.Contains(x.Repechage))d.Repechage.SelectedItem=x.Repechage;if(d.DrawModeBox.Items.Contains(x.DrawMode))d.DrawModeBox.SelectedItem=x.DrawMode;
        if(d.ShowDialog(this)!=DialogResult.OK)return;
        db.UpdateCategory(id.Value,d.Discipline.Text,d.Gender.Text,d.Age.Text.Trim(),d.Weight.Text.Trim(),d.SystemBox.Text,d.Repechage.Text,d.DrawModeBox.Text);AutoBackup();ReloadAll();
    }

    void ReloadCategories(){
        categoryGrid.DataSource=db.Categories().Select(x=>new{ID=x.Id,Дисциплина=x.Discipline,Пол=x.Gender,Возраст=x.AgeGroup,Весовая_категория=x.WeightCategory,Система=x.System,Утешительные=x.Repechage,Жеребьёвка=x.DrawMode,Утверждена=x.DrawApproved,Статус=x.Status}).ToList();
    }

    void BuildAthletesTab(){
        var page=Page("Участники");
        var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=50,Padding=new Padding(4),AutoScroll=true};
        bar.Controls.Add(Btn("Добавить спортсмена",AddAthleteClick));
        bar.Controls.Add(Btn("Редактировать",EditAthleteClick));
        bar.Controls.Add(Btn("Удалить",DeleteAthleteClick));
        bar.Controls.Add(Btn("Импорт Excel",ImportExcelClick));
        bar.Controls.Add(Btn("Создать Excel-шаблон",(s,e)=>{using var save=new SaveFileDialog{Filter="Excel (*.xlsx)|*.xlsx",FileName="Заявка_Самбо.xlsx"};if(save.ShowDialog(this)==DialogResult.OK){ExcelImporter.CreateTemplate(save.FileName);MessageBox.Show("Шаблон сохранён.");}}));
        bar.Controls.Add(new Label{Text="Назначить категорию:",AutoSize=true,Margin=new Padding(15,12,4,0)});
        bar.Controls.Add(assignCategory);
        bar.Controls.Add(Btn("Назначить",(s,e)=>{var id=SelectedId(athleteGrid);var cid=SelectedCategory(assignCategory);if(id.HasValue&&cid.HasValue)UiGuard(()=>{db.AssignCategory(id.Value,cid.Value);ReloadAll();},"Категория не изменена");}));
        page.Controls.Add(athleteGrid);page.Controls.Add(bar);
    }
    void AddAthleteClick(object? s,EventArgs e){
        using var d=new AthleteDialog();if(d.ShowDialog(this)!=DialogResult.OK)return;
        if(string.IsNullOrWhiteSpace(d.FullName.Text)){MessageBox.Show("Введите ФИО.");return;}
        double? dw=ParseDouble(d.DeclaredWeight.Text);
        long? cid=FindCategory(d.Discipline.Text,d.Gender.Text,d.Age.Text,d.Weight.Text);
        db.AddAthlete(d.FullName.Text,d.Birth.Text,d.Gender.Text,d.Region.Text,d.Organization.Text,d.Team.Text,d.Coach.Text,d.Rank.Text,d.Discipline.Text,d.Age.Text,d.Weight.Text,dw,cid);ReloadAll();
    }
    void EditAthleteClick(object? s,EventArgs e){
        var id=SelectedId(athleteGrid);if(!id.HasValue)return;var a=db.Athletes().FirstOrDefault(x=>x.Id==id.Value);if(a==null)return;
        using var d=new AthleteDialog();d.FullName.Text=a.FullName;d.Birth.Text=a.BirthDate;if(d.Gender.Items.Contains(a.Gender))d.Gender.SelectedItem=a.Gender;
        d.Region.Text=a.Region;d.Organization.Text=a.Organization;d.Team.Text=a.Team;d.Coach.Text=a.Coach;d.Rank.Text=a.Rank;if(d.Discipline.Items.Contains(a.Discipline))d.Discipline.SelectedItem=a.Discipline;
        d.Age.Text=a.AgeGroup;d.Weight.Text=a.WeightCategory;d.DeclaredWeight.Text=a.DeclaredWeight?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"";
        if(d.ShowDialog(this)!=DialogResult.OK||string.IsNullOrWhiteSpace(d.FullName.Text))return;
        long? cid=FindCategory(d.Discipline.Text,d.Gender.Text,d.Age.Text,d.Weight.Text)??a.CategoryId;
        UiGuard(()=>{db.UpdateAthlete(a.Id,d.FullName.Text,d.Birth.Text,d.Gender.Text,d.Region.Text,d.Organization.Text,d.Team.Text,d.Coach.Text,d.Rank.Text,d.Discipline.Text,d.Age.Text,d.Weight.Text,ParseDouble(d.DeclaredWeight.Text),cid);AutoBackup();ReloadAll();},"Карточка не изменена");
    }
    void DeleteAthleteClick(object? s,EventArgs e){
        var id=SelectedId(athleteGrid);if(!id.HasValue)return;
        if(MessageBox.Show("Удалить выбранного спортсмена? Если он уже присутствует в поединках, удаление будет запрещено.","Удаление",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
        if(!db.DeleteAthleteIfUnused(id.Value))MessageBox.Show("Спортсмен уже участвует в сформированных/проведённых поединках. Удаление запрещено; измените его статус вместо удаления.");
        ReloadAll();
    }

    long? FindCategory(string discipline,string gender,string age,string weight){
        var c=db.Categories().FirstOrDefault(x=>Eq(x.Discipline,discipline)&&GenderEq(x.Gender,gender)&&Eq(x.AgeGroup,age)&&Eq(x.WeightCategory,weight));return c?.Id;
    }
    static bool Eq(string a,string b)=>string.Equals(a?.Trim(),b?.Trim(),StringComparison.OrdinalIgnoreCase);
    static bool GenderEq(string a,string b){
        if(Eq(a,b))return true;
        string Code(string s){s=(s??"").ToLowerInvariant();if(s.Contains("жен")||s.Contains("дев"))return "F";if(s.Contains("муж")||s.Contains("юн"))return "M";return "";}
        var x=Code(a);return x!=""&&x==Code(b);
    }
    static double? ParseDouble(string? s){if(string.IsNullOrWhiteSpace(s))return null;return double.TryParse(s.Replace(',','.'),System.Globalization.NumberStyles.Any,System.Globalization.CultureInfo.InvariantCulture,out var v)?v:null;}

    void ImportExcelClick(object? s,EventArgs e){
        using var o=new OpenFileDialog{Filter="Excel (*.xlsx)|*.xlsx",Title="Выберите файл заявок"};if(o.ShowDialog(this)!=DialogResult.OK)return;
        try{
            var data=ExcelImporter.Read(o.FileName);if(data.Rows.Count==0){MessageBox.Show("В файле нет строк для импорта.");return;}
            using var mapDlg=new ImportMappingDialog(data.Headers,data.Rows);if(mapDlg.ShowDialog(this)!=DialogResult.OK)return;var m=mapDlg.Mapping;
            string M(string field)=>m.TryGetValue(field,out var h)?h:"";
            string V(Dictionary<string,string> r,string field)=>ExcelImporter.Value(r,M(field));
            var parsed=new List<(Dictionary<string,string> Row,string Name)>();
            var invalid=new List<string>();int rowNo=1;
            foreach(var row in data.Rows){
                rowNo++;var whole=V(row,"ФИО");string name,error;bool ok;
                if(whole!="")ok=NameNormalizer.TryNormalizeImported(out name,out error,whole);
                else ok=NameNormalizer.TryNormalizeImported(out name,out error,V(row,"Фамилия"),V(row,"Имя"),V(row,"Отчество"));
                if(ok)parsed.Add((row,name));else invalid.Add($"строка {rowNo}: {error}");
            }
            if(invalid.Count>0){
                var preview=string.Join("\n",invalid.Take(15))+(invalid.Count>15?"\n...":"");
                if(MessageBox.Show($"Обнаружено неоднозначных/ошибочных строк: {invalid.Count}. Они НЕ будут импортированы.\n\n{preview}\n\nПродолжить импорт корректных строк?","Проверка импорта",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
            }

            int added=0,merged=0,edited=0,skipped=0;
            foreach(var item in parsed){
                var row=item.Row;
                string birth=V(row,"Дата рождения"),gender=V(row,"Пол"),region=V(row,"Регион"),organization=V(row,"Организация"),team=V(row,"Команда"),coach=V(row,"Тренер"),rank=V(row,"Разряд");
                string discipline=V(row,"Дисциплина");if(discipline=="")discipline="Спортивное самбо";
                string age=V(row,"Возрастная группа"),weight=V(row,"Весовая категория");double? declared=ParseDouble(V(row,"Заявленный вес"));
                long? cid=FindCategory(discipline,gender,age,weight);
                var candidates=db.Athletes().Where(a=>string.Equals(a.FullName,item.Name,StringComparison.OrdinalIgnoreCase)).ToList();
                var existing=candidates.FirstOrDefault(a=>!string.IsNullOrWhiteSpace(birth)&&string.Equals(a.BirthDate,birth,StringComparison.OrdinalIgnoreCase))??candidates.FirstOrDefault();
                ImportDuplicateAction action=ImportDuplicateAction.KeepBoth;
                if(existing!=null){
                    using var dd=new DuplicateImportDialog(existing,item.Name,birth,team,weight);
                    if(dd.ShowDialog(this)!=DialogResult.OK){skipped++;continue;}action=dd.Action;
                }
                if(action==ImportDuplicateAction.Skip){skipped++;continue;}
                if(action==ImportDuplicateAction.Merge&&existing!=null){
                    string Pick(string oldValue,string newValue)=>string.IsNullOrWhiteSpace(oldValue)?newValue:oldValue;
                    try{
                        db.UpdateAthlete(existing.Id,existing.FullName,Pick(existing.BirthDate,birth),Pick(existing.Gender,gender),Pick(existing.Region,region),Pick(existing.Organization,organization),Pick(existing.Team,team),Pick(existing.Coach,coach),Pick(existing.Rank,rank),Pick(existing.Discipline,discipline),Pick(existing.AgeGroup,age),Pick(existing.WeightCategory,weight),existing.DeclaredWeight??declared,existing.CategoryId??cid);
                        db.Audit($"Импорт: объединена карточка спортсмена {existing.Id}");merged++;
                    }catch(Exception ex){MessageBox.Show(ex.Message,$"Не удалось объединить {item.Name}",MessageBoxButtons.OK,MessageBoxIcon.Warning);skipped++;}
                    continue;
                }
                if(action==ImportDuplicateAction.Edit){
                    using var d=new AthleteDialog();d.FullName.Text=item.Name;d.Birth.Text=birth;if(d.Gender.Items.Contains(gender))d.Gender.SelectedItem=gender;d.Region.Text=region;d.Organization.Text=organization;d.Team.Text=team;d.Coach.Text=coach;d.Rank.Text=rank;if(d.Discipline.Items.Contains(discipline))d.Discipline.SelectedItem=discipline;d.Age.Text=age;d.Weight.Text=weight;d.DeclaredWeight.Text=declared?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"";
                    if(d.ShowDialog(this)!=DialogResult.OK||string.IsNullOrWhiteSpace(d.FullName.Text)){skipped++;continue;}
                    cid=FindCategory(d.Discipline.Text,d.Gender.Text,d.Age.Text,d.Weight.Text);
                    db.AddAthlete(d.FullName.Text,d.Birth.Text,d.Gender.Text,d.Region.Text,d.Organization.Text,d.Team.Text,d.Coach.Text,d.Rank.Text,d.Discipline.Text,d.Age.Text,d.Weight.Text,ParseDouble(d.DeclaredWeight.Text),cid);edited++;continue;
                }
                db.AddAthlete(item.Name,birth,gender,region,organization,team,coach,rank,discipline,age,weight,declared,cid);added++;
            }
            MessageBox.Show($"Импорт завершён.\nДобавлено: {added}\nОбъединено: {merged}\nДобавлено после редактирования: {edited}\nПропущено: {skipped}\nОшибочных строк: {invalid.Count}");
            db.Audit($"Импорт Excel: добавлено {added}, объединено {merged}, отредактировано {edited}, пропущено {skipped}, ошибочных строк {invalid.Count}");AutoBackup();ReloadAll();
        }catch(Exception ex){MessageBox.Show(ex.Message,"Ошибка импорта",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    }

    void ReloadAthletes(){
        athleteGrid.DataSource=db.Athletes().Select(x=>new{ID=x.Id,ФИО=x.FullName,Дата_рождения=x.BirthDate,Пол=x.Gender,Регион=x.Region,Организация=x.Organization,Команда=x.Team,Тренер=x.Coach,Разряд=x.Rank,Дисциплина=x.Discipline,Возраст=x.AgeGroup,Весовая_категория=x.WeightCategory,Заявленный_вес=x.DeclaredWeight,Фактический_вес=x.ActualWeight,Статус=x.Status,Категория_ID=x.CategoryId}).ToList();
    }

    void BuildAdmissionTab(){
        var page=Page("Допуск");
        var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=54,Padding=new Padding(4),AutoScroll=true};
        var status=new ComboBox{Width=190,DropDownStyle=ComboBoxStyle.DropDownList};
        status.Items.AddRange(["Заявлен","Документы проверены","Допущен","Не допущен","Ожидает решения"]);status.SelectedIndex=1;
        var reason=new TextBox{Width=360};
        bar.Controls.Add(new Label{Text="Статус:",AutoSize=true,Margin=new Padding(6,13,4,0)});bar.Controls.Add(status);
        bar.Controls.Add(new Label{Text="Причина / примечание:",AutoSize=true,Margin=new Padding(12,13,4,0)});bar.Controls.Add(reason);
        bar.Controls.Add(Btn("Сохранить решение",(s,e)=>{
            var id=SelectedId(admissionGrid);if(!id.HasValue){MessageBox.Show("Выберите спортсмена.");return;}
            UiGuard(()=>{db.UpdateAthleteStatus(id.Value,status.Text,reason.Text.Trim());AutoBackup();ReloadAll();},"Решение о допуске не изменено");
        }));
        page.Controls.Add(admissionGrid);page.Controls.Add(bar);
    }
    void ReloadAdmission(){
        admissionGrid.DataSource=db.Athletes().Select(x=>new{ID=x.Id,ФИО=x.FullName,Дата_рождения=x.BirthDate,Регион=x.Region,Организация=x.Organization,Дисциплина=x.Discipline,Возраст=x.AgeGroup,Категория=x.WeightCategory,Статус=x.Status,Причина=x.StatusReason}).ToList();
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
            if(WeightRules.IsMismatch(a.WeightCategory,v.Value,out var why)){
                var ans=MessageBox.Show($"{why}. Заявленная категория: {a.WeightCategory}.\n\nДа — сохранить выбранное решение «{status.Text}» как решение комиссии.\nНет — сохранить вес со статусом «Ожидает решения».","Несоответствие весовой категории",MessageBoxButtons.YesNo,MessageBoxIcon.Warning);
                if(ans==DialogResult.No)decision="Ожидает решения";
                db.Audit($"Предупреждение по весу: {a.FullName}, категория {a.WeightCategory}, фактический вес {v.Value:0.##}; решение: {decision}");
            }
            UiGuard(()=>{db.SetWeigh(id.Value,v.Value,decision);AutoBackup();ReloadAll();},"Взвешивание не изменено");
        }));
        bar.Controls.Add(Btn("Изменить только допуск",(s,e)=>{var id=SelectedId(weighGrid);if(id.HasValue)UiGuard(()=>{db.UpdateAthleteStatus(id.Value,status.Text);ReloadAll();},"Допуск не изменён");}));
        page.Controls.Add(weighGrid);page.Controls.Add(bar);
    }
    void ReloadWeigh(){weighGrid.DataSource=db.Athletes().Select(x=>new{ID=x.Id,ФИО=x.FullName,Категория=x.WeightCategory,Заявленный=x.DeclaredWeight,Фактический=x.ActualWeight,Статус=x.Status,Причина=x.StatusReason,Команда=x.Team}).ToList();}

    void BuildJudgesTab(){
        var page=Page("Судьи");var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=88,Padding=new Padding(4),AutoScroll=true};
        bar.Controls.Add(Btn("Добавить судью",(s,e)=>{using var d=new JudgeDialog();if(d.ShowDialog(this)==DialogResult.OK&&d.NameBox.Text.Trim()!=""){db.AddJudge(d.NameBox.Text,d.Region.Text,d.Category.Text,d.Role.Text);ReloadAll();}}));
        bar.Controls.Add(Btn("Удалить судью",(s,e)=>{var id=SelectedId(judgeGrid);if(id.HasValue&&MessageBox.Show("Удалить выбранного судью и его назначения?","Удаление",MessageBoxButtons.YesNo)==DialogResult.Yes){db.DeleteJudge(id.Value);ReloadAll();}}));
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