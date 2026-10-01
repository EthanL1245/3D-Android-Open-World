using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UnityEngine;
class Program
{
    static int checks;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static void Main(string[] args)
    {
        string root=Path.GetFullPath(args[0]);Resources.Root=root;
        foreach(string file in Directory.GetFiles(Path.Combine(root,"Assets"),"*.cs",SearchOption.AllDirectories))
            Check(!CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetDiagnostics().Any(d=>d.Severity==DiagnosticSeverity.Error),"C# syntax: "+file);
        FishingTuning.Reload();Check(FishingTuning.IsValid,FishingTuning.ValidationError);
        Check(FishCatalog.Count==17 && FishCatalog.ActiveIds.Length==16 && FishCatalog.CanonicalId(4)==5,"Stable save IDs");
        for(int biome=0;biome<3;biome++)for(int bait=0;bait<5;bait++)for(int lure=0;lure<(bait==4?5:1);lure++)
        {
            ShopCatalog.ActiveLureVariant=lure;
            float expected=biome==0?0:(bait==4 && lure==4?55:2);
            Check(FishingTuning.TryGetChance(15,bait,biome,out float chance) && chance==expected,"Shark odds");
            float expectedSnapper=biome==0?(bait<=1?13:bait==2?15:bait==3?8:2):0;
            Check(FishingTuning.TryGetChance(16,bait,biome,out float snapperChance) && snapperChance==expectedSnapper,"Yellowtail snapper odds");
            if(biome==0)
            {
                FishingTuning.TryGetChance(6,bait,biome,out float goatA);
                FishingTuning.TryGetChance(7,bait,biome,out float goatB);
                Check(Math.Abs(goatA-snapperChance)<=1 && Math.Abs(goatB-snapperChance)<=1,"Goatfish pool shared evenly");
            }
            var bins=new int[17];
            for(int i=0;i<10000;i++)
            {Check(FishingTuning.TryRollSpecies((i+.5f)/10000,bait,biome,out int id),"Runtime roll");bins[id]++;}
            foreach(int id in FishCatalog.ActiveIds)
            {FishingTuning.TryGetChance(id,bait,biome,out float c);Check(Math.Abs(bins[id]-100*c)<2,"Roll matches CSV");}
            Check(bins[4]==0,"Retired ID excluded");
        }
        for(int biome=0;biome<3;biome++)for(int i=0;i<=100;i++)
        {
            bool ok=FishingTuning.TryRollWeight(15,biome,i/100f,out float kg);
            if(biome==0){Check(!ok,"Sharks excluded from Suncrest");continue;}
            Check(ok && kg>=(biome==1?5:12) && kg<=(biome==1?14:30),"Shark weight bounds");
            float length=FishSizeTable.LengthMetres(15,kg);
            Check(length>.9f && length<1.9f,"Shark length scales in metres");
        }
        Check(FishCatalog.Get(15).Name=="Blacktip Shark","Shark display name");
        Check(FishCatalog.Get(1).Name=="Red Snapper" && FishCatalog.Get(16).Name=="Yellowtail Snapper","Separate stable snapper IDs");
        for(int biome=0;biome<3;biome++)for(int i=0;i<=100;i++)
        {
            bool ok=FishingTuning.TryRollWeight(16,biome,i/100f,out float kg);
            Check(biome==0 ? ok && kg>=.25f && kg<=4.1f : !ok,"Starter-only snapper weight range");
            if(ok)Check(FishSizeTable.LengthMetres(16,kg)>.25f && FishSizeTable.LengthMetres(16,kg)<.8f,"Snapper length in metres");
        }
        FishingTuning.TryGetHealth(15,5,out int small);FishingTuning.TryGetHealth(15,30,out int big);
        Check(small==9000 && big==20000 && FishingTuning.YellowSpeed(15,0)==5.2f,"Extreme fight tuning");
        Console.WriteLine("PASS: "+checks+" checks; 27 actual probability tables, save IDs, sizes, health and C# syntax.");
    }
}
