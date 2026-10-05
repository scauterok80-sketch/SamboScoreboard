using System.Drawing.Printing;

namespace SamboSecretary;

public sealed partial class MainForm{
    void BuildDrawTab(){
        var page=Page("Жеребьёвка");
        drawSystem.Items.AddRange(["Авто","Круговая","Смешанная","Олимпийская"]);drawSystem.SelectedIndex=0;
        drawMode.Items.AddRange(["Ручная","Посев + автоматическая","Полностью автоматическая"]);drawMode.SelectedIndex=2;
        drawRepechage.Items.AddRange(["По положению","От финалистов","От полуфиналистов","Без утешительных встреч"]);drawRepechage.SelectedIndex=0;
        var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=86,Padding=new Padding(4),AutoScroll=true};
        bar.Controls.Add(new Label{Text="Категория:",AutoSize=true,Margin=new Padding(6,12,3,0)});bar.Controls.Add(drawCategory);
        bar.Controls.Add(new Label{Text="Система:",AutoSize=true,Margin=new Padding(10,12,3,0)});bar.Controls.Add(drawSystem);
        bar.Controls.Add(new Label{Text="Жеребьёвка:",AutoSize=true,Margin=new Padding(10,12,3,0)});bar.Controls.Add(drawMode);
        bar.Controls.Add(new Label{Text="Утешительные:",AutoSize=true,Margin=new Padding(10,12,3,0)});bar.Controls.Add(drawRepechage);
        bar.Controls.Add(separateTeams);
        bar.Controls.Add(Btn("Сформировать / пережеребьевать",GenerateDrawClick));
        bar.Controls.Add(Btn("Переставить вручную",(s,e)=>{if(drawMode.Items.Contains("Ручная"))drawMode.SelectedItem="Ручная";GenerateDrawClick(s,e);}));
        bar.Controls.Add(Btn("Утвердить",(s,e)=>{var id=SelectedCategory(drawCategory);if(!id.HasValue)return;if(db.DrawPositions(id.Value).Count<2){MessageBox.Show("Сначала сформируйте и проверьте жеребьёвку.");return;}db.ApproveDraw(id.Value,true);AutoBackup();ReloadAll();}));
        bar.Controls.Add(Btn("Разблокировать",(s,e)=>{var id=SelectedCategory(drawCategory);if(id.HasValue&&MessageBox.Show("Разблокировать жеребьёвку? Действие будет записано в журнал.","Подтверждение",MessageBoxButtons.YesNo)==DialogResult.Yes)UiGuard(()=>{db.ApproveDraw(id.Value,false);ReloadAll();},"Жеребьёвка не разблокирована");}));
        drawCategory.SelectedIndexChanged+=(s,e)=>LoadDrawSettings();
        page.Controls.Add(drawGrid);page.Controls.Add(bar);
    }

    void LoadDrawSettings(){
        var id=SelectedCategory(drawCategory);if(!id.HasValue)return;var c=db.Categories().FirstOrDefault(x=>x.Id==id.Value);if(c==null)return;
        SelectText(drawSystem,c.System);SelectText(drawMode,c.DrawMode);SelectText(drawRepechage,c.Repechage);ReloadDraw();
    }
    static void SelectText(ComboBox b,string value){if(string.IsNullOrWhiteSpace(value))return;for(int i=0;i<b.Items.Count;i++)if(string.Equals(Convert.ToString(b.Items[i]),value,StringComparison.OrdinalIgnoreCase)){b.SelectedIndex=i;return;}}

    void GenerateDrawClick(object? s,EventArgs e){
        var cid=SelectedCategory(drawCategory);if(!cid.HasValue){MessageBox.Show("Выберите категорию.");return;}
        var cat=db.Categories().First(x=>x.Id==cid.Value);
        if(cat.DrawApproved){MessageBox.Show("Жеребьёвка уже утверждена. Сначала разблокируйте её.");return;}
        var categoryAthletes=db.Athletes(cid.Value);
        var eligibility=EligibilityRules.ValidateForDraw(cat,categoryAthletes);
        if(eligibility.Count>0){
            MessageBox.Show("Жеребьёвка заблокирована. Исправьте следующие данные:\n\n"+string.Join("\n",eligibility.Take(20))+(eligibility.Count>20?"\n...":""),"Контроль перед жеребьёвкой",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            db.Audit($"Жеребьёвка категории {cid.Value} заблокирована: {eligibility.Count} ошибок допуска/данных");
            return;
        }
        var athletes=categoryAthletes.ToList();
        if(athletes.Count<2){MessageBox.Show("Для жеребьёвки требуется минимум два участника.");return;}
        string system=drawSystem.Text=="Авто"?AutoSystem(athletes.Count):drawSystem.Text;
        var allowed=TournamentEngine.Allowed(athletes.Count);
        var enumSystem=system switch{"Круговая"=>TournamentSystem.RoundRobin,"Смешанная"=>TournamentSystem.Mixed,"Олимпийская"=>TournamentSystem.Olympic,_=>TournamentSystem.RoundRobin};
        if(!allowed.Contains(enumSystem)){MessageBox.Show($"Система «{system}» недоступна для {athletes.Count} участников.");return;}
        if(system=="Олимпийская"&&drawRepechage.Text=="По положению"){
            MessageBox.Show("Для олимпийской системы перед жеребьёвкой обязательно выберите конкретный вариант: «От финалистов», «От полуфиналистов» или «Без утешительных встреч».","Требуется Положение соревнования");
            return;
        }
        try{
            db.UpdateCategorySettings(cid.Value,system,drawRepechage.Text,drawMode.Text);
            db.ClearBouts(cid.Value);db.ClearDrawPositions(cid.Value);
        }catch(Exception ex){MessageBox.Show(ex.Message,"Жеребьёвка не изменена",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}

        if(system=="Круговая")GenerateRoundRobin(cid.Value,athletes);
        else if(system=="Смешанная")GenerateMixed(cid.Value,athletes);
        else GenerateOlympic(cid.Value,athletes);
        db.Audit($"Сформирована предварительная жеребьёвка категории {cid.Value}: {system}, {drawMode.Text}");
        AutoBackup();ReloadAll();
    }
    static string AutoSystem(int n)=>n<=6?"Круговая":n==7?"Смешанная":"Олимпийская";

    List<Athlete> OrderedAthletes(List<Athlete> athletes,int slots){
        if(drawMode.Text=="Полностью автоматическая")return athletes.OrderBy(_=>Random.Shared.Next()).ToList();
        using var d=new DrawPositionDialog(athletes,slots,drawMode.Text=="Ручная");if(d.ShowDialog(this)!=DialogResult.OK)throw new OperationCanceledException();
        if(drawMode.Text=="Ручная")return athletes.OrderBy(a=>d.Positions[a.Id]).ToList();
        var fixedPos=d.Positions;var ordered=new Athlete?[slots];
        foreach(var a in athletes.Where(a=>fixedPos.ContainsKey(a.Id)))ordered[fixedPos[a.Id]-1]=a;
        var rest=athletes.Where(a=>!fixedPos.ContainsKey(a.Id)).OrderBy(_=>Random.Shared.Next()).ToList();int k=0;
        for(int i=0;i<ordered.Length&&k<rest.Count;i++)if(ordered[i]==null)ordered[i]=rest[k++];
        return ordered.Where(x=>x!=null).Select(x=>x!).ToList();
    }

    void GenerateRoundRobin(long cid,List<Athlete> athletes){
        List<Athlete> order;try{order=OrderedAthletes(athletes,athletes.Count);}catch(OperationCanceledException){return;}
        for(int i=0;i<order.Count;i++)db.AddDrawPosition(cid,i+1,order[i].Id,"Круг");
        var bouts=TournamentEngine.RoundRobin(order.Select(a=>a.Id).ToList());int mat=1;foreach(var b in bouts){db.AddBout(cid,b.No,b.Stage,b.Red,b.Blue,mat);mat=mat%(int)tMats.Value+1;}
    }

    void GenerateMixed(long cid,List<Athlete> athletes){
        var sizes=TournamentEngine.MixedGroups(athletes.Count);List<Athlete> order;
        if(separateTeams.Checked&&drawMode.Text=="Полностью автоматическая")order=SeparateMixedOrder(athletes,sizes.A,sizes.B);
        else{
            try{order=OrderedAthletes(athletes,athletes.Count);}catch(OperationCanceledException){return;}
            if(separateTeams.Checked&&drawMode.Text!="Полностью автоматическая")db.Audit($"Разведение команд в смешанной категории {cid}: ручные/сеяные позиции имеют приоритет");
        }
        var a=order.Take(sizes.A).ToList();var b=order.Skip(sizes.A).ToList();int pos=1;
        foreach(var x in a)db.AddDrawPosition(cid,pos++,x.Id,"A");foreach(var x in b)db.AddDrawPosition(cid,pos++,x.Id,"B");
        int no=1,mat=1;
        foreach(var bt in TournamentEngine.RoundRobin(a.Select(x=>x.Id).ToList())){db.AddBout(cid,no++,$"Группа A / {bt.Stage}",bt.Red,bt.Blue,mat);mat=mat%(int)tMats.Value+1;}
        foreach(var bt in TournamentEngine.RoundRobin(b.Select(x=>x.Id).ToList())){db.AddBout(cid,no++,$"Группа B / {bt.Stage}",bt.Red,bt.Blue,mat);mat=mat%(int)tMats.Value+1;}
    }

    void GenerateOlympic(long cid,List<Athlete> athletes){
        int size=TournamentEngine.BracketSize(athletes.Count);Dictionary<long,int>? fixedPos=null;
        if(drawMode.Text!="Полностью автоматическая"){
            using var d=new DrawPositionDialog(athletes,size,drawMode.Text=="Ручная");if(d.ShowDialog(this)!=DialogResult.OK)return;fixedPos=d.Positions;
        }
        var slots=separateTeams.Checked&&drawMode.Text!="Ручная"
            ?BuildSeparatedOlympicSlots(athletes,size,fixedPos)
            :TournamentEngine.Draw(athletes.Select(a=>a.Id).ToList(),size,drawMode.Text=="Ручная"?DrawMode.Manual:drawMode.Text.StartsWith("Посев")?DrawMode.SeededAuto:DrawMode.FullAuto,fixedPos);
        if(drawMode.Text=="Ручная"&&fixedPos!=null){slots=Enumerable.Repeat<long?>(null,size).ToList();foreach(var kv in fixedPos)slots[kv.Value-1]=kv.Key;}
        for(int i=0;i<size;i++)db.AddDrawPosition(cid,i+1,slots[i],i<size/2?"A":"B");
        string stage=size==8?"1/4":size==16?"1/8":"1/16";int no=1,mat=1;
        for(int i=0;i<size;i+=2)if(slots[i].HasValue&&slots[i+1].HasValue){db.AddBout(cid,no++,stage,slots[i],slots[i+1],mat);mat=mat%(int)tMats.Value+1;}
    }

    void ReloadDraw(){
        var id=SelectedCategory(drawCategory);if(!id.HasValue){drawGrid.DataSource=null;return;}
        drawGrid.DataSource=db.DrawPositions(id.Value).Select(x=>new{Позиция=x.Position,Группа=x.Group,Спортсмен=x.Athlete,ID=x.AthleteId}).ToList();
    }

    void BuildBoutsTab(){
        var page=Page("Поединки");
        var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=50,Padding=new Padding(4)};
        bar.Controls.Add(new Label{Text="Категория:",AutoSize=true,Margin=new Padding(6,12,3,0)});bar.Controls.Add(boutCategory);
        bar.Controls.Add(Btn("Статус / ковёр",ChangeBoutStateClick));bar.Controls.Add(Btn("Внести результат",EnterResultClick));bar.Controls.Add(Btn("Сформировать следующий этап",AdvanceStageClick));bar.Controls.Add(Btn("Обновить",(s,e)=>ReloadBouts()));
        boutCategory.SelectedIndexChanged+=(s,e)=>ReloadBouts();page.Controls.Add(boutGrid);page.Controls.Add(bar);
    }
    void ReloadBouts(){
        var id=SelectedCategory(boutCategory);if(!id.HasValue){boutGrid.DataSource=null;return;}
        var rules=db.BoutRules(id.Value).ToDictionary(x=>x.Id);
        boutGrid.DataSource=db.Bouts(id.Value).Select(x=>{
            var r=rules[x.Id];
            return new{ID=x.Id,Номер=x.DisplayNo,Порядок_сетки=x.BoutNo,План=x.ScheduledTime,Этап=x.Stage,Красный=x.RedName,Синий=x.BlueName,Ковер=x.Mat,Статус=x.Status,Счет_красного=x.RedScore,Счет_синего=x.BlueScore,Классификация=r.ResultCode,Время=$"{r.DurationSeconds/60}:{r.DurationSeconds%60:00}",Победитель=x.WinnerName,Причина=x.Reason,Судьи=x.Judges};
        }).ToList();
    }
    bool CompetitionUnlocked(long cid){
        var cat=db.Categories().FirstOrDefault(x=>x.Id==cid);
        if(cat==null||!cat.DrawApproved){MessageBox.Show("Сначала утвердите жеребьёвку категории. До утверждения сетка считается предварительной.","Категория не запущена",MessageBoxButtons.OK,MessageBoxIcon.Warning);return false;}
        return true;
    }

    void ChangeBoutStateClick(object? s,EventArgs e){
        var cid=SelectedCategory(boutCategory);var bid=SelectedId(boutGrid);if(!cid.HasValue||!bid.HasValue||!CompetitionUnlocked(cid.Value))return;
        var b=db.Bouts(cid.Value).FirstOrDefault(x=>x.Id==bid.Value);if(b==null)return;
        if(b.Status=="Завершён"){MessageBox.Show("Завершённый поединок изменяется через «Внести результат», чтобы сохранить корректировку в журнале.");return;}
        var status=Microsoft.VisualBasic.Interaction.InputBox("Статус: Ожидает / Готов / Вызван / Идёт","Статус поединка",b.Status);
        if(string.IsNullOrWhiteSpace(status))return;
        var allowed=new[]{"Ожидает","Готов","Вызван","Идёт"};
        if(!allowed.Contains(status.Trim(),StringComparer.OrdinalIgnoreCase)){MessageBox.Show("Разрешённые статусы: Ожидает, Готов, Вызван, Идёт. Для завершения внесите результат.");return;}
        var matText=Microsoft.VisualBasic.Interaction.InputBox($"Номер ковра (1–{tMats.Value})","Ковёр",b.Mat.ToString());
        if(!int.TryParse(matText,out var mat)||mat<1||mat>(int)tMats.Value){MessageBox.Show("Некорректный номер ковра.");return;}
        var displayText=Microsoft.VisualBasic.Interaction.InputBox("Видимый номер поединка (не меняет положение в сетке):","Номер поединка",b.DisplayNo.ToString());
        if(!int.TryParse(displayText,out var displayNo)||displayNo<1){MessageBox.Show("Некорректный видимый номер.");return;}
        var scheduled=Microsoft.VisualBasic.Interaction.InputBox("Плановое время, например 14:35 (можно оставить пустым):","Расписание",b.ScheduledTime);
        UiGuard(()=>{db.SetBoutStatus(b.Id,status.Trim(),mat,scheduled.Trim(),displayNo);AutoBackup();ReloadAll();},"Статус поединка не изменён");
    }

    void EnterResultClick(object? s,EventArgs e){
        var cid=SelectedCategory(boutCategory);var bid=SelectedId(boutGrid);if(!cid.HasValue||!bid.HasValue||!CompetitionUnlocked(cid.Value))return;
        var b=db.Bouts(cid.Value).FirstOrDefault(x=>x.Id==bid.Value);if(b==null||!b.RedId.HasValue||!b.BlueId.HasValue)return;
        var oldRule=db.BoutsByRuleId(b.Id);
        var assigned=db.JudgeAssignments(cid.Value).Where(x=>x.Mat==b.Mat).ToList();
        string def=assigned.Count>0?string.Join("; ",assigned.Select(x=>$"{x.Role}: {x.JudgeName}")):string.Join("; ",db.Judges().Where(j=>j.Role is "Руководитель ковра" or "Арбитр" or "Боковой судья").Take(3).Select(j=>$"{j.Role}: {j.Name}"));
        using var d=new ResultDialog(b.RedName,b.BlueName,def);if(d.ShowDialog(this)!=DialogResult.OK)return;
        long winner=d.Winner.SelectedIndex==0?b.RedId.Value:b.BlueId.Value;
        bool changingWinner=oldRule.Status=="Завершён"&&oldRule.WinnerId.HasValue&&oldRule.WinnerId.Value!=winner;
        if(changingWinner){
            bool anyFuture=db.Bouts(cid.Value).Any(x=>x.BoutNo>b.BoutNo&&(x.RedId==oldRule.WinnerId||x.BlueId==oldRule.WinnerId));
            if(anyFuture){
                if(db.HasCompletedFutureDependency(cid.Value,oldRule.WinnerId!.Value,b.BoutNo)){
                    var ans=MessageBox.Show("Есть уже проведённые зависимые поединки. Автоматически переписывать их нельзя. Продолжить только как ручное решение главного секретаря с записью в журнал?","Критическое изменение результата",MessageBoxButtons.YesNo,MessageBoxIcon.Warning);
                    if(ans!=DialogResult.Yes)return;
                    var reason=Microsoft.VisualBasic.Interaction.InputBox("Укажите причину ручного решения / исправления:","Причина изменения");
                    if(string.IsNullOrWhiteSpace(reason))return;
                    db.Audit($"Ручное решение главного секретаря по изменению результата поединка {b.Id}. Причина: {reason}");
                }else{
                    var ans=MessageBox.Show("Победитель этого поединка уже поставлен в следующий, ещё не проведённый поединок. При изменении результата программа автоматически заменит участника в зависимой встрече. Продолжить?","Перестроение зависимостей",MessageBoxButtons.YesNo,MessageBoxIcon.Warning);
                    if(ans!=DialogResult.Yes)return;
                }
            }
        }
        bool redWon=winner==b.RedId.Value;
        string requested=d.ClassCode.Text=="Авто"?"":d.ClassCode.Text;
        var cp=CompetitionRules.ClassificationFor(requested,(int)d.RedScore.Value,(int)d.BlueScore.Value,redWon,d.Reason.Text);
        db.SetBoutResult(b.Id,(int)d.RedScore.Value,(int)d.BlueScore.Value,winner,d.Reason.Text,d.Judges.Text,cp.Code,cp.Red,cp.Blue,d.DurationSeconds,d.Clean.Checked,
            (int)d.Red4.Value,(int)d.Red2.Value,(int)d.Red1.Value,(int)d.Blue4.Value,(int)d.Blue2.Value,(int)d.Blue1.Value);
        RefreshOperationalStatus(cid.Value,b.RedId.Value);RefreshOperationalStatus(cid.Value,b.BlueId.Value);
        if(changingWinner&&!db.HasCompletedFutureDependency(cid.Value,oldRule.WinnerId!.Value,b.BoutNo))
            db.ReplaceFutureParticipant(cid.Value,oldRule.WinnerId.Value,winner,b.BoutNo);
        var cat=db.Categories().First(x=>x.Id==cid.Value);
        if(cat.System=="Олимпийская")ProgressOlympicRepechage(cid.Value);
        AutoBackup();ReloadAll();
    }

    void RefreshOperationalStatus(long cid,long athleteId){
        var athlete=db.Athletes(cid).FirstOrDefault(a=>a.Id==athleteId);if(athlete==null)return;
        var losses=db.BoutRules(cid).Where(x=>x.Status=="Завершён"&&(x.RedId==athleteId||x.BlueId==athleteId)&&x.WinnerId!=athleteId).ToList();
        string? special=null;
        if(losses.Any(x=>(x.Reason??"").Contains("Дисквали",StringComparison.OrdinalIgnoreCase)))special="Дисквалифицирован";
        else if(losses.Any(x=>(x.Reason??"").Contains("Снятие врачом",StringComparison.OrdinalIgnoreCase)))special="Снят врачом";
        else if(losses.Any(x=>(x.Reason??"").Contains("Нокаут",StringComparison.OrdinalIgnoreCase)||(x.Reason??"").Contains("Два нокдауна",StringComparison.OrdinalIgnoreCase)||(x.Reason??"").Contains("Потеря сознания",StringComparison.OrdinalIgnoreCase)))special="Снят после травмирующего исхода";
        if(special!=null)db.SetOperationalAthleteStatus(athleteId,special);
        else if(athlete.Status is "Дисквалифицирован" or "Снят врачом" or "Снят после травмирующего исхода")db.SetOperationalAthleteStatus(athleteId,athlete.ActualWeight.HasValue?"Допущен":"Заявлен");
    }

    void AdvanceStageClick(object? s,EventArgs e){
        var cid=SelectedCategory(boutCategory);if(!cid.HasValue||!CompetitionUnlocked(cid.Value))return;var cat=db.Categories().First(x=>x.Id==cid.Value);
        try{
            if(cat.System=="Смешанная")AdvanceMixed(cid.Value);
            else if(cat.System=="Олимпийская")AdvanceOlympic(cid.Value);
            else MessageBox.Show("Для круговой системы следующий этап не создаётся: итог определяется по таблице результатов.");
            ReloadAll();
        }catch(OperationCanceledException){MessageBox.Show("Переход остановлен: требуется решение ГСК по спорному ранжированию.");}
        catch(Exception ex){MessageBox.Show(ex.Message,"Следующий этап не сформирован",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    }

    void AdvanceMixed(long cid){
        var bouts=db.Bouts(cid);var group=bouts.Where(x=>x.Stage.StartsWith("Группа ")).ToList();
        if(group.Count==0||group.Any(x=>x.Status!="Завершён")){MessageBox.Show("Сначала завершите все встречи в подгруппах.");return;}
        if(bouts.Any(x=>x.Stage=="Полуфинал")){var semis=bouts.Where(x=>x.Stage=="Полуфинал").OrderBy(x=>x.BoutNo).ToList();if(semis.Count==2&&semis.All(x=>x.Status=="Завершён")&&!bouts.Any(x=>x.Stage=="Финал")){db.AddBout(cid,bouts.Max(x=>x.BoutNo)+1,"Финал",semis[0].WinnerId,semis[1].WinnerId,1);db.Audit($"Создан финал смешанной системы категории {cid}");}else MessageBox.Show("Полуфиналы уже созданы.");return;}
        var pos=db.DrawPositions(cid);var a=pos.Where(x=>x.Group=="A"&&x.AthleteId.HasValue).Select(x=>x.AthleteId!.Value).ToList();var b=pos.Where(x=>x.Group=="B"&&x.AthleteId.HasValue).Select(x=>x.AthleteId!.Value).ToList();
        var ar=RankGroup(a,group.Where(x=>x.Stage.StartsWith("Группа A")).ToList());var br=RankGroup(b,group.Where(x=>x.Stage.StartsWith("Группа B")).ToList());
        if(ar.Count<2||br.Count<2)return;int no=bouts.Max(x=>x.BoutNo)+1;db.AddBout(cid,no++,"Полуфинал",ar[0],br[1],1);db.AddBout(cid,no,"Полуфинал",br[0],ar[1],Math.Min(2,(int)tMats.Value));db.Audit($"Созданы полуфиналы смешанной системы категории {cid}");
    }

    List<long> RankGroup(List<long> ids,List<BoutRow> bouts){
        if(bouts.Count==0)return ids;
        var boutIds=bouts.Select(x=>x.Id).ToHashSet();
        var rules=db.BoutRules(bouts[0].CategoryId).Where(x=>boutIds.Contains(x.Id)).ToList();
        return ResolveRanking(bouts[0].CategoryId,ids,rules,bouts[0].Stage.StartsWith("Группа A")?"подгруппа A":"подгруппа B");
    }

    void AdvanceOlympic(long cid){
        var pos=db.DrawPositions(cid);if(pos.Count==0)return;int size=pos.Count;var stages=size==8?new[]{"1/4","Полуфинал","Финал"}:size==16?new[]{"1/8","1/4","Полуфинал","Финал"}:new[]{"1/16","1/8","1/4","Полуфинал","Финал"};
        var all=db.Bouts(cid);string current=stages.LastOrDefault(s=>all.Any(b=>b.Stage==s))??stages[0];int idx=Array.IndexOf(stages,current);if(idx>=stages.Length-1){ProgressOlympicRepechage(cid);return;}
        var curBouts=all.Where(b=>b.Stage==current).OrderBy(b=>b.BoutNo).ToList();if(curBouts.Any(b=>b.Status!="Завершён")){MessageBox.Show($"Сначала завершите все встречи этапа {current}.");return;}
        string next=stages[idx+1];if(all.Any(b=>b.Stage==next)){if(next=="Финал")ProgressOlympicRepechage(cid);return;}
        List<long> survivors=new();
        if(idx==0){
            for(int i=0;i<pos.Count;i+=2){
                var p1=pos[i].AthleteId;var p2=pos[i+1].AthleteId;
                if(p1.HasValue&&!p2.HasValue)survivors.Add(p1.Value);else if(!p1.HasValue&&p2.HasValue)survivors.Add(p2.Value);else if(p1.HasValue&&p2.HasValue){
                    var b=curBouts.FirstOrDefault(x=>(x.RedId==p1&&x.BlueId==p2)||(x.RedId==p2&&x.BlueId==p1));if(b?.WinnerId is long w)survivors.Add(w);
                }
            }
        }else survivors=curBouts.Where(x=>x.WinnerId.HasValue).Select(x=>x.WinnerId!.Value).ToList();
        int no=all.Count==0?1:all.Max(x=>x.BoutNo)+1,mat=1;for(int i=0;i+1<survivors.Count;i+=2){db.AddBout(cid,no++,next,survivors[i],survivors[i+1],mat);mat=mat%(int)tMats.Value+1;}
        db.Audit($"Создан этап {next} олимпийской системы категории {cid}");
        if(next=="Финал")ProgressOlympicRepechage(cid);
        AutoBackup();
    }

    void BuildDocumentsTab(){
        var page=Page("Протоколы");
        var p=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),AutoScroll=true};
        p.Controls.Add(Btn("Список участников",(s,e)=>PrintLines("СПИСОК УЧАСТНИКОВ",ParticipantLines())));
        p.Controls.Add(Btn("Мандатный протокол / допуск",(s,e)=>PrintLines("МАНДАТНЫЙ ПРОТОКОЛ / ДОПУСК",CredentialLines())));
        p.Controls.Add(Btn("Пустой протокол взвешивания",(s,e)=>PrintLines("ПРОТОКОЛ ВЗВЕШИВАНИЯ",WeighLines(false))));
        p.Controls.Add(Btn("Заполненный протокол взвешивания",(s,e)=>PrintLines("ПРОТОКОЛ ВЗВЕШИВАНИЯ — РЕЗУЛЬТАТ",WeighLines(true))));
        p.Controls.Add(Btn("Список судей",(s,e)=>PrintLines("СУДЕЙСКИЙ КОРПУС",JudgeLines())));
        p.Controls.Add(Btn("Протокол жеребьёвки",(s,e)=>{var cid=SelectedCategory(drawCategory)??SelectedCategory(boutCategory);if(cid.HasValue)PrintLines("ПРОТОКОЛ ЖЕРЕБЬЁВКИ",DrawProtocolLines(cid.Value));else MessageBox.Show("Сначала выберите категорию.");}));
        p.Controls.Add(Btn("Ход соревнований / сетка",(s,e)=>{var cid=SelectedCategory(boutCategory)??SelectedCategory(drawCategory);if(cid.HasValue)PrintLines("ХОД СОРЕВНОВАНИЙ / СЕТКА",BracketProtocolLines(cid.Value));else MessageBox.Show("Сначала выберите категорию.");}));
        p.Controls.Add(Btn("Победители и призёры",(s,e)=>PrintLines("ПОБЕДИТЕЛИ И ПРИЗЁРЫ",PrizewinnersLines())));
        p.Controls.Add(Btn("Пустой пакет всех категорий",(s,e)=>PrintLines("ПУСТЫЕ ПРОТОКОЛЫ ВСЕХ КАТЕГОРИЙ",db.Categories().SelectMany(cat=>BlankCategoryLines(cat.Id)))));
        p.Controls.Add(Btn("Пустой протокол выбранной категории",(s,e)=>{var cid=SelectedCategory(boutCategory)??SelectedCategory(drawCategory);if(cid.HasValue)PrintLines("ПУСТОЙ ПРОТОКОЛ КАТЕГОРИИ",BlankCategoryLines(cid.Value));else MessageBox.Show("Сначала выберите категорию.");}));
        p.Controls.Add(Btn("Протокол выбранной категории",(s,e)=>{var cid=SelectedCategory(boutCategory)??SelectedCategory(drawCategory);if(cid.HasValue)PrintLines("ПРОТОКОЛ КАТЕГОРИИ",CategoryLines(cid.Value));else MessageBox.Show("Сначала выберите категорию на вкладке «Поединки» или «Жеребьёвка».");}));
        p.Controls.Add(Btn("Командный зачёт",(s,e)=>PrintLines("КОМАНДНЫЙ ЗАЧЁТ",TeamStandingLines())));
        p.Controls.Add(Btn("Сводный протокол соревнования",(s,e)=>PrintLines("СВОДНЫЙ ПРОТОКОЛ",SummaryLines())));
        p.Controls.Add(Btn("Полный пакет соревнования",(s,e)=>PrintLines("ПОЛНЫЙ ПАКЕТ СОРЕВНОВАНИЯ",FullProtocolPackageLines())));
        p.Controls.Add(Btn("Резервная копия базы",BackupClick));
        page.Controls.Add(p);
    }

    IEnumerable<string> HeaderLines(){var t=db.GetTournament();if(!string.IsNullOrWhiteSpace(t.ProtocolHeader))yield return t.ProtocolHeader;yield return t.Name;yield return $"Место: {t.Place}";yield return $"Даты: {t.StartDate} — {t.EndDate}";yield return $"Главный судья: {t.ChiefReferee}    Главный секретарь: {t.ChiefSecretary}";yield return "";}
    IEnumerable<string> ParticipantLines()=>HeaderLines().Concat(db.Athletes().Select((a,i)=>$"{i+1}. {a.FullName} | {a.Region} | {a.Team} | {a.Discipline} | {a.AgeGroup} | {a.WeightCategory} | {a.Status}"));
    IEnumerable<string> CredentialLines()=>HeaderLines().Concat(db.Athletes().Select((a,i)=>$"{i+1}. {a.FullName} | {a.BirthDate} | {a.Region} | {a.Organization} | {a.Discipline} | {a.AgeGroup} | {a.WeightCategory} | статус: {a.Status} | причина: {a.StatusReason}"));
    IEnumerable<string> WeighLines(bool filled)=>HeaderLines().Concat(db.Athletes().Select((a,i)=>$"{i+1}. {a.FullName} | категория {a.WeightCategory} | заявл. {a.DeclaredWeight?.ToString()??"___"} | факт. {(filled?a.ActualWeight?.ToString()??"___":"___")} | {(filled?a.Status:"________")} | причина {(filled?a.StatusReason:"________")}"));
    IEnumerable<string> JudgeLines()=>HeaderLines().Concat(db.Judges().Select((j,i)=>$"{i+1}. {j.Name} | {j.Region} | {j.Category} | {j.Role} | {j.Notes}"));
    IEnumerable<string> DrawProtocolLines(long cid){
        var cat=db.Categories().First(x=>x.Id==cid);var lines=new List<string>();lines.AddRange(HeaderLines());
        lines.Add($"{cat.Discipline}; {cat.Gender}; {cat.AgeGroup}; {cat.WeightCategory}");
        lines.Add($"Система: {cat.System}; утешительные: {cat.Repechage}; способ жеребьёвки: {cat.DrawMode}; утверждена: {(cat.DrawApproved?"да":"нет")}");
        lines.Add("");lines.Add("ПОЗИЦИИ ЖЕРЕБЬЁВКИ:");
        lines.AddRange(db.DrawPositions(cid).Select(x=>$"{x.Position}. {(string.IsNullOrWhiteSpace(x.Athlete)?"СВОБОДНО":x.Athlete)} {(string.IsNullOrWhiteSpace(x.Group)?"":$"[{x.Group}]")}"));
        return lines;
    }
    IEnumerable<string> BracketProtocolLines(long cid){
        var cat=db.Categories().First(x=>x.Id==cid);var lines=new List<string>();lines.AddRange(HeaderLines());
        lines.Add($"{cat.Discipline}; {cat.Gender}; {cat.AgeGroup}; {cat.WeightCategory}; {cat.System}; {cat.Repechage}");lines.Add("");
        var rules=db.BoutRules(cid).ToDictionary(x=>x.Id);
        foreach(var b in db.Bouts(cid)){
            var r=rules[b.Id];
            string result=b.Status=="Завершён"?$"{b.RedScore}:{b.BlueScore}; {r.ResultCode}; победитель {b.WinnerName}; {b.Reason}":"";
            lines.Add($"№{b.DisplayNo} | {b.Stage} | {b.RedName} — {b.BlueName} | ковёр {b.Mat} | {b.ScheduledTime} | {b.Status} | {result}");
        }
        return lines;
    }
    IEnumerable<string> PrizewinnersLines(){
        var lines=new List<string>();lines.AddRange(HeaderLines());
        foreach(var cat in db.Categories()){
            lines.Add($"{cat.Discipline} | {cat.Gender} | {cat.AgeGroup} | {cat.WeightCategory}");
            var p=db.Placements(cat.Id).Where(x=>x.Place<=3).OrderBy(x=>x.Place).ThenBy(x=>x.Athlete).ToList();
            if(p.Count==0)lines.Add("  Итоговые места ещё не рассчитаны.");
            else foreach(var x in p)lines.Add($"  {x.Place} место — {x.Athlete} | {x.Region} | {x.Team}");
            lines.Add("");
        }
        return lines;
    }

    IEnumerable<string> CategoryLines(long cid){
        var c=db.Categories().First(x=>x.Id==cid);var lines=new List<string>();lines.AddRange(HeaderLines());lines.Add($"{c.Discipline}; {c.Gender}; {c.AgeGroup}; {c.WeightCategory}; система: {c.System}; утешительные: {c.Repechage}");lines.Add("");
        var assignments=db.JudgeAssignments(cid);if(assignments.Count>0){lines.Add("СУДЕЙСКИЕ НАЗНАЧЕНИЯ:");lines.AddRange(assignments.Select(x=>$"Ковёр {x.Mat}: {x.Role} — {x.JudgeName}"));lines.Add("");}
        lines.Add("ЖЕРЕБЬЁВКА:");lines.AddRange(db.DrawPositions(cid).Select(x=>$"{x.Position}. {(x.Athlete==""?"СВОБОДНО":x.Athlete)} {(x.Group!=""?$"[{x.Group}]":"")}"));lines.Add("");lines.Add("ПОЕДИНКИ:");
        var rules=db.BoutRules(cid).ToDictionary(x=>x.Id);
        foreach(var b in db.Bouts(cid)){
            var r=rules[b.Id];string result=b.Status=="Завершён"?$"{b.RedScore}:{b.BlueScore}; классификация {r.ResultCode} ({r.RedClass}:{r.BlueClass}); время {r.DurationSeconds/60}:{r.DurationSeconds%60:00}; победитель {b.WinnerName}; {b.Reason}":"не проведён";
            lines.Add($"№{b.DisplayNo} {b.Stage}: {b.RedName} — {b.BlueName}; {result}; ковёр {b.Mat}; план {b.ScheduledTime}; судьи: {b.Judges}");
        }
        var places=db.Placements(cid);if(places.Count>0){lines.Add("");lines.Add("ИТОГОВЫЕ МЕСТА:");lines.AddRange(places.Select(p=>$"{p.Place} место — {p.Athlete} | {p.Team} | {p.Source}"));}
        return lines;
    }
    IEnumerable<string> SummaryLines(){
        var lines=new List<string>();lines.AddRange(HeaderLines());lines.Add($"Всего участников: {db.Athletes().Count}");lines.Add($"Всего категорий: {db.Categories().Count}");lines.Add($"Судей: {db.Judges().Count}");lines.Add("");
        foreach(var c in db.Categories()){
            lines.Add($"{c.Discipline} | {c.Gender} | {c.AgeGroup} | {c.WeightCategory} | {c.Status}");
            var places=db.Placements(c.Id);if(places.Count>0)foreach(var p in places.Where(x=>x.Place<=5))lines.Add($"  {p.Place} место: {p.Athlete} ({p.Team})");
            else{var final=db.Bouts(c.Id).FirstOrDefault(b=>b.Stage=="Финал"&&b.Status=="Завершён");if(final!=null)lines.Add($"  Финал завершён, итоговые места ещё не рассчитаны. Победитель: {final.WinnerName}");}
        }
        return lines;
    }

    void PrintLines(string title,IEnumerable<string> source){
        var lines=source.ToList();var template=db.GetTournament();if(!string.IsNullOrWhiteSpace(template.ProtocolFooter)){lines.Add("");lines.Add(template.ProtocolFooter);}int index=0;
        try{
            var dir=Path.Combine(root,"Protocols");Directory.CreateDirectory(dir);
            var invalid=Path.GetInvalidFileNameChars();var safe=new string(title.Select(ch=>invalid.Contains(ch)?'_':ch).ToArray());
            var path=Path.Combine(dir,$"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{safe}.txt");
            File.WriteAllLines(path,new[]{title}.Concat(lines),System.Text.Encoding.UTF8);db.Audit($"Сформирован снимок протокола: {Path.GetFileName(path)}");
        }catch{}
        var pd=new PrintDocument();pd.DocumentName=title;
        pd.PrintPage+=(s,e)=>{
            float y=e.MarginBounds.Top;using var head=new Font("Arial",14,FontStyle.Bold);using var font=new Font("Arial",9);
            e.Graphics!.DrawString(title,head,Brushes.Black,e.MarginBounds.Left,y);y+=head.GetHeight(e.Graphics)+14;
            while(index<lines.Count){
                string line=lines[index];var measured=e.Graphics.MeasureString(string.IsNullOrEmpty(line)?" ":line,font,e.MarginBounds.Width);
                float h=Math.Max(font.GetHeight(e.Graphics)+3,measured.Height+3);
                if(y+h>e.MarginBounds.Bottom){e.HasMorePages=true;return;}
                e.Graphics.DrawString(line,font,Brushes.Black,new RectangleF(e.MarginBounds.Left,y,e.MarginBounds.Width,h));
                y+=h;index++;
            }
            e.HasMorePages=false;
        };
        using var dlg=new PrintPreviewDialog{Document=pd,Width=1100,Height=800};dlg.ShowDialog(this);
    }
    void BackupClick(object? s,EventArgs e){var d=Path.Combine(root,"Backups");Directory.CreateDirectory(d);var p=Path.Combine(d,$"sambo_{DateTime.Now:yyyyMMdd_HHmmss}.db");File.Copy(db.FileName,p,true);MessageBox.Show($"Резервная копия создана:\n{p}");}

    void BuildAuditTab(){var page=Page("Журнал");page.Controls.Add(auditGrid);}
    void ReloadAudit(){auditGrid.DataSource=db.AuditRows().Select(x=>new{Время=x.Time,Действие=x.Action}).ToList();}
}