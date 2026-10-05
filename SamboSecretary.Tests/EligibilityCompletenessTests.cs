using Xunit;
using SamboSecretary;

public class EligibilityCompletenessTests{
    [Fact]
    public void MissingSexAgeOrWeightBlocksDraw(){
        var cat=new CategoryRow(1,"Спортивное самбо","Мужчины","18+","71 кг","Круговая","","",false,"Подготовка");
        var a=new Athlete(1,"ИВАНОВ Иван Иванович","2000-01-01","","","","","","","Спортивное самбо","","",70,70,"Допущен",1);
        var errors=EligibilityRules.ValidateForDraw(cat,new[]{a});
        Assert.Contains(errors,x=>x.Contains("не указан пол"));
        Assert.Contains(errors,x=>x.Contains("не указана возрастная группа"));
        Assert.Contains(errors,x=>x.Contains("не указана весовая категория"));
    }
}