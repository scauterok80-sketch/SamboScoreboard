using System.Collections.Generic;using System.Linq;using Xunit;using SamboSecretary;
public class CoreTests{
[Theory][InlineData("петров пЕТР пЕТРОВИЧ","ПЕТРОВ Петр Петрович")][InlineData("  Иванов   ИВАН  ","ИВАНОВ Иван")]public void Names(string a,string b)=>Assert.Equal(b,NameNormalizer.Normalize(a));
[Theory][InlineData(8,8)][InlineData(9,16)][InlineData(16,16)][InlineData(17,32)][InlineData(32,32)]public void Brackets(int n,int s)=>Assert.Equal(s,TournamentEngine.BracketSize(n));
[Theory][InlineData(2,1)][InlineData(3,3)][InlineData(4,6)][InlineData(5,10)][InlineData(6,15)]public void RoundRobinCount(int n,int c)=>Assert.Equal(c,TournamentEngine.RoundRobin(Enumerable.Range(1,n).Select(x=>(long)x).ToList()).Count);
[Fact]public void Mixed(){Assert.Equal((2,3),TournamentEngine.MixedGroups(5));Assert.Equal((3,3),TournamentEngine.MixedGroups(6));Assert.Equal((3,4),TournamentEngine.MixedGroups(7));}
[Fact]public void Cross(){var x=TournamentEngine.MixedSemis(1,2,3,4);Assert.Equal((1L,4L),(x.Item1.Red!.Value,x.Item1.Blue!.Value));Assert.Equal((3L,2L),(x.Item2.Red!.Value,x.Item2.Blue!.Value));}
[Fact]public void SeededStays(){var ids=Enumerable.Range(1,10).Select(x=>(long)x).ToList();var d=TournamentEngine.Draw(ids,16,DrawMode.SeededAuto,new Dictionary<long,int>{{1,1},{2,16}},42);Assert.Equal(1,d[0]);Assert.Equal(2,d[15]);Assert.Equal(10,d.Count(x=>x!=null));}
[Fact]public void Systems(){Assert.Equal(new[]{TournamentSystem.RoundRobin,TournamentSystem.Mixed},TournamentEngine.Allowed(5));Assert.Equal(new[]{TournamentSystem.Mixed},TournamentEngine.Allowed(7));Assert.Equal(new[]{TournamentSystem.Olympic},TournamentEngine.Allowed(8));}
}