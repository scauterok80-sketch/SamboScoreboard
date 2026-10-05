using System.Globalization;
using System.Text.RegularExpressions;
namespace SamboSecretary;

public static class WeightRules{
    public static bool TryUpperLimit(string? category,out double max){
        max=0;if(string.IsNullOrWhiteSpace(category))return false;
        var s=category.Trim().ToLowerInvariant();
        if(s.Contains("+")||s.Contains("свыше")||s.Contains("более"))return false;
        var m=Regex.Match(s,@"(d+(?:[.,]d+)?)");
        if(!m.Success)return false;
        return double.TryParse(m.Groups[1].Value.Replace(',','.'),NumberStyles.Any,CultureInfo.InvariantCulture,out max);
    }
    public static bool IsOverweight(string? category,double actual,out double max){
        if(TryUpperLimit(category,out max))return actual>max+0.0001;
        return false;
    }
}