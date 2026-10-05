using System.Globalization;
using System.Text.RegularExpressions;
namespace SamboSecretary;

public readonly record struct WeightBounds(double? MinExclusive,double? MaxInclusive);

public static class WeightRules{
    static double? Number(string s){
        var m=Regex.Match(s,@"(\d+(?:[.,]\d+)?)");
        if(!m.Success)return null;
        return double.TryParse(m.Groups[1].Value.Replace(',','.'),NumberStyles.Any,CultureInfo.InvariantCulture,out var v)?v:null;
    }

    public static bool TryBounds(string? category,out WeightBounds bounds){
        bounds=new(null,null);
        if(string.IsNullOrWhiteSpace(category))return false;
        var s=category.Trim().ToLowerInvariant();
        var n=Number(s);if(!n.HasValue)return false;
        if(s.Contains("+")||s.Contains("свыше")||s.Contains("более")||s.Contains("от ")){bounds=new(n.Value,null);return true;}
        if(s.Contains("до ")||s.Contains("не более")||s.Contains("≤")||Regex.IsMatch(s,@"^\s*\d")){bounds=new(null,n.Value);return true;}
        bounds=new(null,n.Value);return true;
    }

    public static bool TryUpperLimit(string? category,out double max){
        max=0;if(!TryBounds(category,out var b)||!b.MaxInclusive.HasValue)return false;max=b.MaxInclusive.Value;return true;
    }

    public static bool IsMismatch(string? category,double actual,out string reason){
        reason="";
        if(!TryBounds(category,out var b))return false;
        if(b.MaxInclusive.HasValue&&actual>b.MaxInclusive.Value+0.0001){reason=$"вес {actual:0.##} кг превышает верхнюю границу {b.MaxInclusive.Value:0.##} кг";return true;}
        if(b.MinExclusive.HasValue&&actual<=b.MinExclusive.Value+0.0001){reason=$"вес {actual:0.##} кг не соответствует категории свыше {b.MinExclusive.Value:0.##} кг";return true;}
        return false;
    }

    public static bool IsOverweight(string? category,double actual,out double max){
        return TryUpperLimit(category,out max)&&actual>max+0.0001;
    }
}