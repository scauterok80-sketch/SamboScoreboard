using System.Collections.Generic;
using System.Linq;
using Xunit;
using SamboSecretary;

public class CompetitionRulesTests{
    static BoutRuleRow B(long id,int no,long red,long blue,long winner,string code,int rc,int bc,int rs=0,int bs=0,int sec=0,bool clean=false,string stage="Круг 1")
        =>new(id,1,no,stage,red,blue,"Завершён",rs,bs,winner,code,rc,bc,sec,clean,0,0,0,0,0,0,"");

    [Theory]
    [InlineData("Чистая победа",8,0,true,"4:0",4,0)]
    [InlineData("По очкам",5,2,true,"3:1",3,1)]
    [InlineData("По очкам",5,0,true,"3:0",3,0)]
    [InlineData("По замечаниям",0,0,true,"2:0",2,0)]
    public void ClassificationIsCalculated(string reason,int red,int blue,bool redWon,string code,int rc,int bc){
        var x=CompetitionRules.ClassificationFor("",red,blue,redWon,reason);
        Assert.Equal(code,x.Code);Assert.Equal(rc,x.Red);Assert.Equal(bc,x.Blue);
    }

    [Fact]
    public void RoundRobinRanksByClassificationPoints(){
        var bouts=new[]{
            B(1,1,1,2,1,"4:0",4,0,8,0,70,true),
            B(2,2,1,3,1,"3:0",3,0,4,0,240,false),
            B(3,3,2,3,2,"3:1",3,1,5,2,240,false)
        };
        var r=CompetitionRules.RankRoundRobin(new long[]{1,2,3},bouts);
        Assert.Equal(new long[]{1,2,3},r.Ordered);Assert.Empty(r.Unresolved);
    }

    [Fact]
    public void CompleteThreeWayTieIsFlaggedForGskDecision(){
        var bouts=new[]{
            B(1,1,1,2,1,"3:1",3,1,2,1,240,false),
            B(2,2,2,3,2,"3:1",3,1,2,1,240,false),
            B(3,3,3,1,3,"3:1",3,1,2,1,240,false)
        };
        var r=CompetitionRules.RankRoundRobin(new long[]{1,2,3},bouts);
        Assert.Single(r.Unresolved);Assert.Equal(3,r.Unresolved[0].Count);
    }

    [Fact]
    public void DirectLossesFollowMainBracketPathInRoundOrder(){
        var bouts=new[]{
            B(1,1,1,2,1,"4:0",4,0,8,0,100,true,"1/8"),
            B(2,2,1,3,1,"3:0",3,0,5,0,240,false,"1/4"),
            B(3,3,1,4,1,"2:0",2,0,2,2,240,false,"Полуфинал")
        };
        Assert.Equal(new long[]{2,3},CompetitionRules.DirectLossesTo(1,bouts,false));
        Assert.Equal(new long[]{2,3,4},CompetitionRules.DirectLossesTo(1,bouts,true));
    }

    [Fact]
    public void TechnicalQualityBreaksOtherwiseEqualTie(){
        var b1=new BoutRuleRow(1,1,1,"Круг 1",1,2,"Завершён",4,2,1,"3:1",3,1,240,false,1,0,0,0,1,0,"");
        var b2=new BoutRuleRow(2,1,2,"Круг 1",2,3,"Завершён",4,2,2,"3:1",3,1,240,false,0,2,0,0,0,2,"");
        var b3=new BoutRuleRow(3,1,3,"Круг 1",3,1,"Завершён",4,2,3,"3:1",3,1,240,false,0,0,4,0,1,0,"");
        var r=CompetitionRules.RankRoundRobin(new long[]{1,2,3},new[]{b1,b2,b3});
        Assert.Equal(new long[]{1,2,3},r.Ordered);Assert.Empty(r.Unresolved);
    }

    [Fact]
    public void StageOrderIsChronological(){
        Assert.True(CompetitionRules.StageOrder("1/16")<CompetitionRules.StageOrder("1/8"));
        Assert.True(CompetitionRules.StageOrder("1/8")<CompetitionRules.StageOrder("1/4"));
        Assert.True(CompetitionRules.StageOrder("1/4")<CompetitionRules.StageOrder("Полуфинал"));
        Assert.True(CompetitionRules.StageOrder("Полуфинал")<CompetitionRules.StageOrder("Финал"));
    }
}