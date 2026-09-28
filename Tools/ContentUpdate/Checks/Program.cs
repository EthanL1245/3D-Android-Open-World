using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UnityEngine;
[Serializable] public class CaughtFishRecord {public int speciesId; public float weightKg; public long caughtUtcTicks;}
class Program
{
    static int checks;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static void Main(string[] args)
    {
        string root=Path.GetFullPath(args[0]);Resources.Root=root;
        foreach(var file in Directory.GetFiles(Path.Combine(root,"Assets"),"*.cs",SearchOption.AllDirectories))
        {
            var errors=CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetDiagnostics().Where(x=>x.Severity==DiagnosticSeverity.Error).ToArray();
            Check(errors.Length==0,file+": "+string.Join(";",errors.Select(x=>x.ToString())));
        }
        FishingTuning.Reload();Check(FishingTuning.IsValid,FishingTuning.ValidationError);
        Check(FishCatalog.ActiveIds.Length==14 && FishCatalog.Count==15,"Stable species IDs");
        for(int biome=0;biome<3;biome++)for(int bait=0;bait<5;bait++)for(int lure=0;lure<(bait==4?5:1);lure++)
        {
            ShopCatalog.SetActiveLureVariant(lure);
            Check(Math.Abs(FishCatalog.ActiveIds.Sum(id=>ReefCatalog.EquippedChance(id,bait,biome))-100)<.001,"100% table sum");
            int[] bins=new int[FishCatalog.Count];
            for(int i=0;i<10000;i++)bins[ReefCatalog.Roll((i+.5f)/10000,bait,biome)]++;
            foreach(int id in FishCatalog.ActiveIds)
            {
                float chance=ReefCatalog.EquippedChance(id,bait,biome);
                Check(Math.Abs(bins[id]-chance*100)<2,"Actual roll/index parity");
                FishingTuning.SpeciesStats stats;Check(FishingTuning.TryGetSpeciesStats(id,out stats),"Stats exist");
                Check(stats.YellowSpeedMps>0 && FishingTuning.YellowSpeed(id,0)==FishingTuning.YellowSpeed(id,1),"Yellow is direct baseline");
                for(int sample=0;sample<=100;sample++)
                {
                    float kg;
                    if(ReefCatalog.MinimumWeight(id,biome)==0f && ReefCatalog.MaximumWeight(id,biome)==0f)
                    {Check(!FishingTuning.TryRollWeight(id,biome,sample/100f,out kg),"Disabled weight cannot roll");continue;}
                    Check(FishingTuning.TryRollWeight(id,biome,sample/100f,out kg),"Weight roll");
                    Check(kg>=ReefCatalog.MinimumWeight(id,biome)-.0001 && kg<=ReefCatalog.MaximumWeight(id,biome)+.0001,"Weight bounds");
                }
            }
            foreach(float boundary in new[]{0f,1f})
            {
                int id;Check(FishingTuning.TryRollSpecies(boundary,bait,biome,out id),"Boundary roll succeeds");
                Check(ReefCatalog.EquippedChance(id,bait,biome)>0,"Zero% never rolled at endpoints");
            }
        }
        Check(Math.Abs(FishingTuning.GreenToYellowSpeed-.7037037f)<.000001,"Green ratio");
        Check(Math.Abs(FishingTuning.RedToYellowSpeed-1.2962963f)<.000001,"Red ratio");
        Check(Math.Abs(FishingCastQualityRuntime.FightQuality(.2f)-.6f)<.000001,"80% size loss -> 40% fight loss");
        Check(FishingCastQualityRuntime.FightQuality(1f)==1f,"No offshore penalty");
        Check(ShopCatalog.RodPrices[3]==10000 && ShopCatalog.ReelPrices[3]==6500,"New prices");
        Check(Level2FishingReelRuntime.ReelMultiplier(3)==1.6f,"New reel bonus");
        for(int i=0;i<10000;i++)
        {
            bool crit;int d=FishingBurstDamageRuntime.RollBurstForTier(3,(i+.5f)/10000,.99f,out crit);
            Check(!crit && d>=10 && d<=20,"Normal Level4 damage");
            int c=FishingBurstDamageRuntime.RollBurstForTier(3,(i+.5f)/10000,0,out crit);
            Check(crit && c==d*2,"Whole burst doubled");
        }
        int criticals=0;for(int i=0;i<10000;i++){bool c;FishingBurstDamageRuntime.RollBurstForTier(3,.5f,(i+.5f)/10000,out c);if(c)criticals++;}
        Check(criticals==1000,"10% critical probability");
        var save=new ShopLedger{coins=16500};save.EnsureGearOwnership();save.EnsureCatchStats();
        Check(save.BuyGear(GearKind.Rod,3) && save.BuyGear(GearKind.Reel,3) && save.coins==0,"Buy Level4 directly");
        Check(save.totalCaught.Length==15,"New species counters");
        CheckZeroRanges(root);
        Console.WriteLine("PASS "+checks+" assertions, real tuning/catalog/gear policies and C# syntax.");
    }
    static void CheckZeroRanges(string root)
    {
        const string wk="FishingTuning/BiomeFishWeights",ck="FishingTuning/BiomeBaitSpeciesChance";
        string weights=File.ReadAllText(Path.Combine(root,"Assets/Resources",wk+".csv"));
        string chances=File.ReadAllText(Path.Combine(root,"Assets/Resources",ck+".csv"));
        // Disable a normally catchable species and redistribute its odds in memory only.
        string disabled=System.Text.RegularExpressions.Regex.Replace(weights,@"(?m)^suncrest-reef,5,([^,]+),[^,\r\n]+,[^\r\n]+", "suncrest-reef,5,$1,0,0");
        string header=chances.Split('\n').First(l=>l.StartsWith("biomeId,")).Trim();
        int speciesColumn=Array.FindIndex(header.Split(','),c=>c.StartsWith("5_"));
        string[] lines=chances.Split('\n');
        for(int i=0;i<lines.Length;i++)if(lines[i].StartsWith("suncrest-reef,"))
        {var c=lines[i].TrimEnd('\r').Split(',');c[2]=(int.Parse(c[2])+int.Parse(c[speciesColumn])).ToString();c[speciesColumn]="0";lines[i]=string.Join(",",c);}
        string consistent=string.Join("\n",lines);
        Resources.Overrides[wk]=disabled;Resources.Overrides[ck]=consistent;FishingTuning.Reload();
        Check(FishingTuning.IsValid,"0,0 + all zero chances accepted: "+FishingTuning.ValidationError);
        float min,max,kg,a,b,cq;
        Check(FishingTuning.TryGetWeightRange(5,0,out min,out max)&&min==0&&max==0,"Sentinel retained");
        Check(!FishingTuning.TryRollWeight(5,0,.5f,out kg),"No zero-weight fish generated");
        Check(!FishingTuning.TryGetQuartiles(5,0,out a,out b,out cq),"Disabled distribution has no quartiles");
        for(int bait=0;bait<5;bait++)for(int lure=0;lure<5;lure++)
        {
            ShopCatalog.SetActiveLureVariant(lure);
            Check(ReefCatalog.EquippedChance(5,bait,0)==0,"Disabled fish hidden for every bait");
            for(int n=0;n<=1000;n++)Check(ReefCatalog.Roll(n/1000f,bait,0)!=5,"Disabled fish never caught, including endpoints");
        }
        Check(FishingTuning.TryRollWeight(5,1,.5f,out kg)&&kg>0,"Other biomes unaffected");
        foreach(string bait in new[]{"worms","worms-legacy","shrimp","squid","lure:0","lure:1","lure:2","lure:3","lure:4"})
        {
            string[] bad=consistent.Split('\n');
            int index=Array.FindIndex(bad,l=>l.StartsWith("suncrest-reef,"+bait+","));var cols=bad[index].Split(',');
            cols[2]=(int.Parse(cols[2])-1).ToString();cols[speciesColumn]="1";bad[index]=string.Join(",",cols);
            Resources.Overrides[ck]=string.Join("\n",bad);FishingTuning.Reload();
            Check(!FishingTuning.IsValid && FishingTuning.ValidationError.Contains("species 5") && FishingTuning.ValidationError.Contains(" + "+bait+" "),"Cross-file contradiction rejected: "+bait);
        }
        Resources.Overrides[ck]=consistent;
        foreach(string range in new[]{"0,1","1,0","-1,0","1,1","2,1"})
        {
            Resources.Overrides[wk]=disabled.Replace("suncrest-reef,5,Yellowfin Tuna,0,0","suncrest-reef,5,Yellowfin Tuna,"+range);
            FishingTuning.Reload();Check(!FishingTuning.IsValid,"Invalid partial/reversed range rejected: "+range);
        }
        Resources.Overrides.Clear();FishingTuning.Reload();Check(FishingTuning.IsValid,"Original tuning restored");
    }
}
