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
                    float kg;Check(FishingTuning.TryRollWeight(id,biome,sample/100f,out kg),"Weight roll");
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
        Console.WriteLine("PASS "+checks+" assertions, real tuning/catalog/gear policies and C# syntax.");
    }
}
