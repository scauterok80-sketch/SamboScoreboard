using System.Globalization;
using System.Text.RegularExpressions;
namespace SamboSecretary;
public enum TournamentSystem { RoundRobin, Mixed, Olympic }
public enum DrawMode { Manual, SeededAuto, FullAuto }
public static class NameNormalizer {
 public static bool TryNormalizeImported(out string normalized,out string error,params string?[] parts){
  normalized="";error="";var raw=string.Join(" ",parts.Where(x=>!string.IsNullOrWhiteSpace(x))).Trim();
  if(string.IsNullOrWhiteSpace(raw)){error="пустое ФИО";return false;}
  if(raw.Any(char.IsDigit)||raw.Contains(',')||raw.Contains(';')){error="неоднозначные символы/разделители";return false;}
  var words=Regex.Split(raw,@"\s+").Where(x=>x.Length>0).ToArray();
  if(words.Length<2||words.Length>4){error=$"неоднозначное количество компонентов: {words.Length}";return false;}
  if(words.Any(w=>w.Length<2)){error="слишком короткий компонент ФИО";return false;}
  normalized=Normalize(words);return true;
 }
 public static string Normalize(params string?[] parts) {
  var w=parts.Where(x=>!string.IsNullOrWhiteSpace(x)).SelectMany(x=>Regex.Split(x!.Trim(),@"\s+")).Where(x=>x.Length>0).ToArray();
  if(w.Length==0)return "";
  var ru=new CultureInfo("ru-RU");
  string Cap(string s){s=s.ToLower(ru);return char.ToUpper(s[0],ru)+s[1..];}
  return string.Join(" ",new[]{w[0].ToUpper(ru)}.Concat(w.Skip(1).Select(Cap)));
 }
}
public record Bout(int No,long? Red,long? Blue,string Stage);
public static class TournamentEngine {
 public static TournamentSystem[] Allowed(int n)=>n switch{>=2 and <=4=>[TournamentSystem.RoundRobin],>=5 and <=6=>[TournamentSystem.RoundRobin,TournamentSystem.Mixed],7=>[TournamentSystem.Mixed],>=8 and <=32=>[TournamentSystem.Olympic],_=>throw new ArgumentOutOfRangeException(nameof(n))};
 public static int BracketSize(int n)=>n switch{8=>8,>=9 and <=16=>16,>=17 and <=32=>32,_=>throw new ArgumentOutOfRangeException(nameof(n))};
 public static (int A,int B) MixedGroups(int n)=>n switch{5=>(2,3),6=>(3,3),7=>(3,4),_=>throw new ArgumentOutOfRangeException(nameof(n))};
 public static List<Bout> RoundRobin(IReadOnlyList<long> ids){
  var a=ids.ToList();if(a.Count%2==1)a.Add(0);int n=a.Count,no=1;var r=new List<Bout>();
  for(int round=0;round<n-1;round++){for(int i=0;i<n/2;i++){var x=a[i];var y=a[n-1-i];if(x!=0&&y!=0)r.Add(new(no++,x,y,$"Круг {round+1}"));}var last=a[^1];a.RemoveAt(a.Count-1);a.Insert(1,last);}return r;
 }
 public static List<long?> Draw(IReadOnlyList<long> ids,int size,DrawMode mode,IDictionary<long,int>? seeded=null,int seed=12345){
  if(ids.Count>size)throw new ArgumentException("Участников больше сетки");
  if(size<2||size%2!=0)throw new ArgumentException("Некорректный размер сетки");
  var slots=Enumerable.Repeat<long?>(null,size).ToList();seeded??=new Dictionary<long,int>();
  foreach(var kv in seeded){if(kv.Value<1||kv.Value>size||slots[kv.Value-1]!=null||!ids.Contains(kv.Key))throw new ArgumentException("Некорректный посев");slots[kv.Value-1]=kv.Key;}
  if(mode==DrawMode.Manual){
   if(ids.Count>=size/2&&Enumerable.Range(0,size/2).Any(p=>slots[p*2]==null&&slots[p*2+1]==null))throw new ArgumentException("Ручная расстановка создаёт пустую пару BYE–BYE. Распределите спортсменов по всем парам первого круга.");
   return slots;
  }
  var rng=new Random(seed);var rest=ids.Where(x=>!seeded.ContainsKey(x)).OrderBy(_=>rng.Next()).ToList();
  var emptyPairs=Enumerable.Range(0,size/2).Where(p=>slots[p*2]==null&&slots[p*2+1]==null).OrderBy(_=>rng.Next()).ToList();
  if(rest.Count<emptyPairs.Count)throw new ArgumentException("Фиксированный посев создаёт пустую пару BYE–BYE. Измените позиции сеяных спортсменов.");
  int k=0;
  foreach(var p in emptyPairs){int target=p*2+(rng.Next(2));slots[target]=rest[k++];}
  var free=Enumerable.Range(0,size).Where(i=>slots[i]==null).OrderBy(_=>rng.Next()).ToList();
  for(int i=0;k<rest.Count;i++,k++)slots[free[i]]=rest[k];
  return slots;
 }
 public static (Bout,Bout) MixedSemis(long A1,long A2,long B1,long B2)=>(new(1,A1,B2,"Полуфинал"),new(2,B1,A2,"Полуфинал"));
}