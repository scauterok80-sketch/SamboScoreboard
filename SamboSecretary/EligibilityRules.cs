namespace SamboSecretary;

public static class EligibilityRules{
    static bool Same(string? a,string? b)=>string.Equals(a?.Trim(),b?.Trim(),StringComparison.OrdinalIgnoreCase);
    static string SexGroup(string? s){
        s=(s??"").Trim().ToLowerInvariant();
        if(s.Contains("жен")||s.Contains("дев"))return "F";
        if(s.Contains("муж")||s.Contains("юн"))return "M";
        return "";
    }

    public static List<string> ValidateForDraw(CategoryRow cat,IEnumerable<Athlete> athletes){
        var list=athletes.ToList();var errors=new List<string>();
        foreach(var a in list){
            if(a.Status!="Допущен")errors.Add($"{a.FullName}: статус «{a.Status}», требуется «Допущен»");
            if(!a.ActualWeight.HasValue)errors.Add($"{a.FullName}: отсутствует результат взвешивания");
            if(!Same(a.Discipline,cat.Discipline))errors.Add($"{a.FullName}: дисциплина «{a.Discipline}» не совпадает с категорией «{cat.Discipline}»");
            if(!string.IsNullOrWhiteSpace(cat.AgeGroup)&&string.IsNullOrWhiteSpace(a.AgeGroup))errors.Add($"{a.FullName}: не указана возрастная группа");
            else if(!string.IsNullOrWhiteSpace(a.AgeGroup)&&!string.IsNullOrWhiteSpace(cat.AgeGroup)&&!Same(a.AgeGroup,cat.AgeGroup))errors.Add($"{a.FullName}: возрастная группа «{a.AgeGroup}» не совпадает с «{cat.AgeGroup}»");
            var asx=SexGroup(a.Gender);var csx=SexGroup(cat.Gender);
            if(csx!=""&&asx=="")errors.Add($"{a.FullName}: не указан пол");
            else if(asx!=""&&csx!=""&&asx!=csx)errors.Add($"{a.FullName}: пол не соответствует категории «{cat.Gender}»");
            if(!string.IsNullOrWhiteSpace(cat.WeightCategory)&&string.IsNullOrWhiteSpace(a.WeightCategory))errors.Add($"{a.FullName}: не указана весовая категория");
            else if(!string.IsNullOrWhiteSpace(a.WeightCategory)&&!Same(a.WeightCategory,cat.WeightCategory))errors.Add($"{a.FullName}: заявленная категория «{a.WeightCategory}» не совпадает с «{cat.WeightCategory}»");
            if(a.ActualWeight.HasValue&&WeightRules.IsMismatch(cat.WeightCategory,a.ActualWeight.Value,out var why))errors.Add($"{a.FullName}: {why}");
        }
        foreach(var g in list.GroupBy(a=>$"{a.FullName.Trim().ToUpperInvariant()}|{a.BirthDate.Trim()}",StringComparer.OrdinalIgnoreCase).Where(g=>g.Count()>1))
            errors.Add($"Дубликат: {string.Join(", ",g.Select(x=>$"{x.FullName} (ID {x.Id})"))}");
        return errors.Distinct().ToList();
    }
}