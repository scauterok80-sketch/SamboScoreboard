namespace SamboSecretary;

public sealed partial class MainForm{
    readonly DataGridView resultGrid=Grid();
    readonly ComboBox resultCategory=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=360};

    void BuildResultsTab(){
        var page=Page("Итоги");
        var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=50,Padding=new Padding(4)};
        bar.Controls.Add(new Label{Text="Категория:",AutoSize=true,Margin=new Padding(6,12,3,0)});bar.Controls.Add(resultCategory);
        bar.Controls.Add(Btn("Рассчитать итоговые места",FinalizeCategoryClick));
        bar.Controls.Add(Btn("Обновить",(s,e)=>ReloadResults()));
        resultCategory.SelectedIndexChanged+=(s,e)=>ReloadResults();
        page.Controls.Add(resultGrid);page.Controls.Add(bar);
    }

    void ReloadResults(){
        var cid=SelectedCategory(resultCategory);
        resultGrid.DataSource=cid.HasValue?db.Placements(cid.Value).Select(x=>new{Место=x.Place,ФИО=x.Athlete,Команда=x.Team,Регион=x.Region,Основание=x.Source}).ToList():null;
    }

    void FinalizeCategoryClick(object? s,EventArgs e){
        var cid=SelectedCategory(resultCategory);if(!cid.HasValue)return;
        var c=db.Categories().First(x=>x.Id==cid.Value);
        try{
            if(c.System=="Круговая")FinalizeRoundRobin(cid.Value);
            else if(c.System=="Смешанная")FinalizeMixed(cid.Value);
            else if(c.System=="Олимпийская")FinalizeOlympic(cid.Value);
            else{MessageBox.Show("Сначала сформируйте жеребьёвку и выберите систему.");return;}
            AutoBackup();
            ReloadAll();MessageBox.Show("Итоговые места рассчитаны и сохранены.");
        }catch(OperationCanceledException){MessageBox.Show("Расчёт мест отменён: требуется решение ГСК.");}
        catch(Exception ex){MessageBox.Show(ex.Message,"Невозможно рассчитать места");}
    }

    List<long> ResolveRanking(long cid,IReadOnlyCollection<long> ids,IReadOnlyCollection<BoutRuleRow> bouts,string scope){
        var disqualified=ids.Where(id=>CompetitionRules.IsDisqualified(id,bouts)).ToHashSet();
        var eligible=ids.Where(id=>!disqualified.Contains(id)).ToList();
        var validBouts=bouts.Where(b=>(!b.RedId.HasValue||!disqualified.Contains(b.RedId.Value))&&(!b.BlueId.HasValue||!disqualified.Contains(b.BlueId.Value))).ToList();
        foreach(var id in disqualified)db.Audit($"Спортсмен {id} исключён из итогового места ({scope}) из-за дисквалификации");
        var rr=CompetitionRules.RankRoundRobin(eligible,validBouts);var order=rr.Ordered.ToList();
        foreach(var tied in rr.Unresolved){
            var athletes=db.Athletes(cid).Where(a=>tied.Contains(a.Id)).ToList();
            using var d=new RankingOverrideDialog(athletes,rr.Metrics);
            if(d.ShowDialog(this)!=DialogResult.OK)throw new OperationCanceledException();
            int first=order.Select((id,i)=>(id,i)).Where(x=>tied.Contains(x.id)).Min(x=>x.i);
            order.RemoveAll(id=>tied.Contains(id));order.InsertRange(first,d.OrderedIds);
            db.Audit($"Ручное решение ГСК ({scope}): "+string.Join(" > ",d.OrderedIds.Select(id=>athletes.First(a=>a.Id==id).FullName)));
        }
        return order;
    }

    void FinalizeRoundRobin(long cid){
        var bouts=db.BoutRules(cid);if(bouts.Count==0||bouts.Any(b=>b.Status!="Завершён"))throw new InvalidOperationException("Сначала завершите все схватки круговой системы.");
        var ids=db.Athletes(cid).Where(a=>a.Status!="Не допущен").Select(a=>a.Id).ToList();
        var order=ResolveRanking(cid,ids,bouts,"круговая система");
        db.ClearPlacements(cid);for(int i=0;i<order.Count;i++)db.SavePlacement(cid,order[i],i+1,"Круговая система — официальный каскад критериев");
        db.Audit($"Итоговые места круговой категории {cid} рассчитаны");
    }

    void FinalizeMixed(long cid){
        var bouts=db.BoutRules(cid);var final=bouts.FirstOrDefault(b=>b.Stage=="Финал");
        var semis=bouts.Where(b=>b.Stage=="Полуфинал").OrderBy(b=>b.BoutNo).ToList();
        if(final==null||final.Status!="Завершён"||semis.Count!=2||semis.Any(b=>b.Status!="Завершён"))throw new InvalidOperationException("Сначала завершите оба полуфинала и финал.");
        var pos=db.DrawPositions(cid);
        var a=pos.Where(x=>x.Group=="A"&&x.AthleteId.HasValue).Select(x=>x.AthleteId!.Value).ToList();
        var b=pos.Where(x=>x.Group=="B"&&x.AthleteId.HasValue).Select(x=>x.AthleteId!.Value).ToList();
        var ar=ResolveRanking(cid,a,bouts.Where(x=>x.Stage.StartsWith("Группа A")).ToList(),"подгруппа A");
        var br=ResolveRanking(cid,b,bouts.Where(x=>x.Stage.StartsWith("Группа B")).ToList(),"подгруппа B");
        long champion=final.WinnerId!.Value, silver=final.RedId==champion?final.BlueId!.Value:final.RedId!.Value;
        long bronzeA=semis[0].RedId==semis[0].WinnerId?semis[0].BlueId!.Value:semis[0].RedId!.Value;
        long bronzeB=semis[1].RedId==semis[1].WinnerId?semis[1].BlueId!.Value:semis[1].RedId!.Value;
        db.ClearPlacements(cid);db.SavePlacement(cid,champion,1,"Победитель финала");
        if(!CompetitionRules.IsDisqualified(silver,bouts))db.SavePlacement(cid,silver,2,"Финалист");else db.Audit($"Финалист {silver} не получает место из-за дисквалификации");
        if(!CompetitionRules.IsDisqualified(bronzeA,bouts))db.SavePlacement(cid,bronzeA,3,"Проигравший полуфинал");else db.Audit($"Полуфиналист {bronzeA} не получает место из-за дисквалификации");
        if(!CompetitionRules.IsDisqualified(bronzeB,bouts))db.SavePlacement(cid,bronzeB,3,"Проигравший полуфинал");else db.Audit($"Полуфиналист {bronzeB} не получает место из-за дисквалификации");
        int place=5;int max=Math.Max(ar.Count,br.Count);
        for(int i=2;i<max;i++){
            var tier=new List<long>();if(i<ar.Count)tier.Add(ar[i]);if(i<br.Count)tier.Add(br[i]);
            foreach(var id in tier.Where(id=>!CompetitionRules.IsDisqualified(id,bouts)))db.SavePlacement(cid,id,place,$"Место {i+1} в подгруппе");
            place+=tier.Count;
        }
        db.Audit($"Итоговые места смешанной категории {cid} рассчитаны");
    }

    void FinalizeOlympic(long cid){
        var cat=db.Categories().First(x=>x.Id==cid);var bouts=db.BoutRules(cid);var final=bouts.FirstOrDefault(b=>b.Stage=="Финал");
        var semis=bouts.Where(b=>b.Stage=="Полуфинал").OrderBy(b=>b.BoutNo).ToList();
        if(final==null||final.Status!="Завершён"||semis.Count!=2||semis.Any(b=>b.Status!="Завершён"))throw new InvalidOperationException("Сначала завершите полуфиналы и финал.");
        long champion=final.WinnerId!.Value,silver=final.RedId==champion?final.BlueId!.Value:final.RedId!.Value;
        var semiLosers=semis.Select(s=>s.RedId==s.WinnerId?s.BlueId!.Value:s.RedId!.Value).ToList();
        db.ClearPlacements(cid);db.SavePlacement(cid,champion,1,"Победитель финала");
        var placed=new HashSet<long>{champion};
        if(!CompetitionRules.IsDisqualified(silver,bouts)){db.SavePlacement(cid,silver,2,"Финалист");placed.Add(silver);}else db.Audit($"Финалист {silver} не получает место из-за дисквалификации");

        if(cat.Repechage=="Без утешительных встреч"){
            foreach(var id in semiLosers){
                if(CompetitionRules.IsDisqualified(id,bouts)){db.Audit($"Полуфиналист {id} не получает место из-за дисквалификации");continue;}
                db.SavePlacement(cid,id,3,"Проигравший полуфинал — без утешительных");placed.Add(id);
            }
        }else{
            var bronze=bouts.Where(b=>b.Stage.StartsWith("Бронза")).OrderBy(b=>b.BoutNo).ToList();
            if(bronze.Any(b=>b.Status!="Завершён"))throw new InvalidOperationException("Сначала завершите все бронзовые встречи.");
            foreach(var br in bronze){
                if(!br.WinnerId.HasValue)continue;long loser=br.RedId==br.WinnerId?br.BlueId!.Value:br.RedId!.Value;
                db.SavePlacement(cid,br.WinnerId.Value,3,"Победитель бронзовой встречи");placed.Add(br.WinnerId.Value);
                if(!CompetitionRules.IsDisqualified(loser,bouts)){db.SavePlacement(cid,loser,5,"Проигравший бронзовую встречу");placed.Add(loser);}else db.Audit($"Проигравший бронзовой встречи {loser} исключён из мест из-за дисквалификации");
            }
            foreach(var sl in semiLosers.Where(x=>!placed.Contains(x))){
                bool hasBronze=bronze.Any(x=>x.RedId==sl||x.BlueId==sl);
                if(!hasBronze&&!CompetitionRules.IsDisqualified(sl,bouts)){db.SavePlacement(cid,sl,3,"Бронза без соперника по утешительной ветке");placed.Add(sl);}
            }
        }

        var allIds=db.Athletes(cid).Select(a=>a.Id).ToList();
        var disqualified=allIds.Where(id=>CompetitionRules.IsDisqualified(id,bouts)).ToHashSet();
        var remaining=allIds.Where(id=>!placed.Contains(id)&&!disqualified.Contains(id)).ToList();
        int nextPlace=cat.Repechage=="Без утешительных встреч"?5:7;

        if(cat.Repechage!="Без утешительных встреч"){
            var depths=CompetitionRules.ConsolationLossDepth(bouts);
            foreach(var tier in remaining.Where(depths.ContainsKey).GroupBy(id=>depths[id]).OrderBy(g=>g.Key)){
                foreach(var id in tier){db.SavePlacement(cid,id,nextPlace,$"Выбыл в утешительной ветке, уровень {tier.Key}");placed.Add(id);}
                nextPlace+=tier.Count();
            }
            remaining=remaining.Where(id=>!placed.Contains(id)).ToList();
        }

        var mainLossRound=remaining.ToDictionary(id=>id,id=>{
            var loss=bouts.Where(b=>b.Status=="Завершён"&&(b.RedId==id||b.BlueId==id)&&b.WinnerId!=id&&CompetitionRules.StageOrder(b.Stage)>0)
                .OrderByDescending(b=>CompetitionRules.StageOrder(b.Stage)).FirstOrDefault();
            return loss==null?0:CompetitionRules.StageOrder(loss.Stage);
        });
        foreach(var tier in remaining.GroupBy(id=>mainLossRound[id]).OrderByDescending(g=>g.Key)){
            foreach(var id in tier)db.SavePlacement(cid,id,nextPlace,tier.Key>0?"Выбыл в одном круге основной сетки":"Не имеет завершённой встречи для ранжирования");
            nextPlace+=tier.Count();
        }
        db.Audit($"Итоговые места олимпийской категории {cid} рассчитаны");
    }

    (bool Ready,long? Winner) AdvanceChain(long cid,string prefix,List<long> losses,ref int nextNo){
        if(losses.Count==0)return(true,null);if(losses.Count==1)return(true,losses[0]);
        var existing=db.BoutRules(cid).Where(b=>b.Stage.StartsWith(prefix)).OrderBy(b=>b.BoutNo).ToList();
        if(existing.Count==0){db.AddBout(cid,nextNo++,$"{prefix} шаг 1",losses[0],losses[1],1);return(false,null);}
        var last=existing[^1];if(last.Status!="Завершён"||!last.WinnerId.HasValue)return(false,null);
        if(existing.Count<losses.Count-1){db.AddBout(cid,nextNo++,$"{prefix} шаг {existing.Count+1}",last.WinnerId,losses[existing.Count+1],Math.Min(2,(int)tMats.Value));return(false,null);}
        return(true,last.WinnerId);
    }

    (bool Ready,long? Winner) MergeChains(long cid,string stage,long? first,long? second,bool firstReady,bool secondReady,ref int nextNo){
        if(!firstReady||!secondReady)return(false,null);if(!first.HasValue&&!second.HasValue)return(true,null);if(first.HasValue&&!second.HasValue)return(true,first);if(!first.HasValue&&second.HasValue)return(true,second);
        var b=db.BoutRules(cid).FirstOrDefault(x=>x.Stage==stage);if(b==null){db.AddBout(cid,nextNo++,stage,first,second,1);return(false,null);}
        return b.Status=="Завершён"&&b.WinnerId.HasValue?(true,b.WinnerId):(false,null);
    }

    void EnsureBronze(long cid,string stage,long? challenger,long semiLoser,ref int nextNo){
        if(!challenger.HasValue)return;if(db.BoutRules(cid).Any(b=>b.Stage==stage))return;
        db.AddBout(cid,nextNo++,stage,challenger,semiLoser,1);
    }

    void ProgressOlympicRepechage(long cid){
        var cat=db.Categories().First(x=>x.Id==cid);if(cat.Repechage=="Без утешительных встреч"||cat.Repechage=="По положению")return;
        var rules=db.BoutRules(cid);var semis=rules.Where(b=>b.Stage=="Полуфинал").OrderBy(b=>b.BoutNo).ToList();
        if(semis.Count!=2||semis.Any(b=>b.Status!="Завершён"||!b.WinnerId.HasValue))return;
        var main=rules.Where(b=>CompetitionRules.StageOrder(b.Stage)>0&&b.Stage!="Финал").ToList();
        int nextNo=rules.Count==0?1:rules.Max(b=>b.BoutNo)+1;
        long semiLoserA=semis[0].RedId==semis[0].WinnerId?semis[0].BlueId!.Value:semis[0].RedId!.Value;
        long semiLoserB=semis[1].RedId==semis[1].WinnerId?semis[1].BlueId!.Value:semis[1].RedId!.Value;

        if(cat.Repechage=="От финалистов"){
            long finA=semis[0].WinnerId!.Value,finB=semis[1].WinnerId!.Value;
            var ca=AdvanceChain(cid,"Утешение Ф-A /",CompetitionRules.DirectLossesTo(finA,main,false),ref nextNo);
            var cb=AdvanceChain(cid,"Утешение Ф-B /",CompetitionRules.DirectLossesTo(finB,main,false),ref nextNo);
            if(ca.Ready)EnsureBronze(cid,"Бронза A",ca.Winner,semiLoserB,ref nextNo);
            if(cb.Ready)EnsureBronze(cid,"Бронза B",cb.Winner,semiLoserA,ref nextNo);
        }else if(cat.Repechage=="От полуфиналистов"){
            long a1=semis[0].RedId!.Value,a2=semis[0].BlueId!.Value,b1=semis[1].RedId!.Value,b2=semis[1].BlueId!.Value;
            var a1c=AdvanceChain(cid,"Утешение ПФ-A1 /",CompetitionRules.DirectLossesTo(a1,main,false),ref nextNo);
            var a2c=AdvanceChain(cid,"Утешение ПФ-A2 /",CompetitionRules.DirectLossesTo(a2,main,false),ref nextNo);
            var b1c=AdvanceChain(cid,"Утешение ПФ-B1 /",CompetitionRules.DirectLossesTo(b1,main,false),ref nextNo);
            var b2c=AdvanceChain(cid,"Утешение ПФ-B2 /",CompetitionRules.DirectLossesTo(b2,main,false),ref nextNo);
            var ga=MergeChains(cid,"Утешение группа A",a1c.Winner,a2c.Winner,a1c.Ready,a2c.Ready,ref nextNo);
            var gb=MergeChains(cid,"Утешение группа B",b1c.Winner,b2c.Winner,b1c.Ready,b2c.Ready,ref nextNo);
            if(ga.Ready)EnsureBronze(cid,"Бронза A",ga.Winner,semiLoserB,ref nextNo);
            if(gb.Ready)EnsureBronze(cid,"Бронза B",gb.Winner,semiLoserA,ref nextNo);
        }
    }

    IEnumerable<string> BlankCategoryLines(long cid){
        var cat=db.Categories().First(x=>x.Id==cid);var lines=new List<string>();lines.AddRange(HeaderLines());
        lines.Add($"{cat.Discipline}; {cat.Gender}; {cat.AgeGroup}; {cat.WeightCategory}; система: {cat.System}; утешительные: {cat.Repechage}");
        lines.Add("");lines.Add("СОСТАВ КАТЕГОРИИ:");
        var athletes=db.Athletes(cid);for(int i=0;i<athletes.Count;i++)lines.Add($"{i+1}. {athletes[i].FullName} | {athletes[i].Team} | вес ______ | допуск ______");
        lines.Add("");lines.Add("ХОД СОРЕВНОВАНИЙ:");
        int count=Math.Max(8,db.Bouts(cid).Count);for(int i=1;i<=count;i++)lines.Add($"№{i} ____________________ — ____________________  счёт ______  победитель ____________________");
        lines.Add("");lines.Add("ИТОГОВЫЕ МЕСТА:");for(int i=1;i<=Math.Min(8,Math.Max(4,athletes.Count));i++)lines.Add($"{i} место: __________________________________");
        return lines;
    }

    IEnumerable<string> TeamStandingLines(){
        var all=db.Categories().SelectMany(c=>db.Placements(c.Id)).Where(p=>!string.IsNullOrWhiteSpace(p.Team)).ToList();
        var scheme=(db.GetTournament().TeamScheme??"7,5,3,1").Split(new[]{',',';','/'},StringSplitOptions.RemoveEmptyEntries).Select(x=>int.TryParse(x.Trim(),out var v)?v:0).ToList();
        while(scheme.Count<4)scheme.Add(new[]{7,5,3,1}[scheme.Count]);
        int Points(int place)=>place switch{1=>scheme[0],2=>scheme[1],3=>scheme[2],5 or 6=>scheme[3],_=>0};
        var rows=all.GroupBy(p=>p.Team).Select(g=>new{
            Team=g.Key,Points=g.Sum(x=>Points(x.Place)),Gold=g.Count(x=>x.Place==1),Silver=g.Count(x=>x.Place==2),
            Bronze=g.Count(x=>x.Place==3),FifthSixth=g.Count(x=>x.Place is 5 or 6)
        }).OrderByDescending(x=>x.Points).ThenByDescending(x=>x.Gold).ThenByDescending(x=>x.Silver).ThenByDescending(x=>x.Bronze).ThenByDescending(x=>x.FifthSixth).ThenBy(x=>x.Team).ToList();
        var lines=new List<string>();lines.AddRange(HeaderLines());lines.Add($"Схема командных очков 1/2/3/5-6: {string.Join("/",scheme.Take(4))}");
        int n=1;foreach(var x in rows)lines.Add($"{n++}. {x.Team} | {x.Points} очк. | 1-х: {x.Gold} | 2-х: {x.Silver} | 3-х: {x.Bronze} | 5-6-х: {x.FifthSixth}");return lines;
    }

    IEnumerable<string> FullProtocolPackageLines(){
        var lines=new List<string>();lines.AddRange(HeaderLines());lines.Add("=== УЧАСТНИКИ ===");lines.AddRange(ParticipantLines().Skip(5));lines.Add("");lines.Add("=== СУДЬИ ===");lines.AddRange(JudgeLines().Skip(5));
        foreach(var c in db.Categories()){lines.Add("");lines.Add("================================================");lines.AddRange(CategoryLines(c.Id));}
        lines.Add("");lines.Add("=== КОМАНДНЫЙ ЗАЧЁТ ===");lines.AddRange(TeamStandingLines().Skip(5));return lines;
    }

    void AutoBackup(){
        try{
            var d=Path.Combine(root,"AutoBackups");Directory.CreateDirectory(d);var p=Path.Combine(d,$"sambo_{DateTime.Now:yyyyMMdd_HHmmss_fff}.db");File.Copy(db.FileName,p,true);
            foreach(var old in new DirectoryInfo(d).GetFiles("*.db").OrderByDescending(x=>x.CreationTimeUtc).Skip(20))old.Delete();
        }catch{}
    }
}