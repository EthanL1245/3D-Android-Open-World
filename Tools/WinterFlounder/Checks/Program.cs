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
  Resources.Root=Path.GetFullPath(args[0]);
  foreach(string f in Directory.GetFiles(Path.Combine(Resources.Root,"Assets"),"*.cs",SearchOption.AllDirectories))
   Check(!CSharpSyntaxTree.ParseText(File.ReadAllText(f)).GetDiagnostics().Any(d=>d.Severity==DiagnosticSeverity.Error),"C# syntax: "+f);
  FishingTuning.Reload();Check(FishingTuning.IsValid,FishingTuning.ValidationError);
  int id=FishCatalog.WinterFlounderId;
  Check(id==27 && FishCatalog.Count==28 && FishCatalog.CanonicalId(4)==5,"Save-safe appended ID");
  Check(FishCatalog.Get(id).Difficulty==FishCatalog.Get(3).Difficulty,"Yellowtail difficulty");
  Check(FishingTuning.TryGetSpeciesStats(id,out var stats),"Flounder stats");
  Check(FishingTuning.TryGetSpeciesStats(3,out var yellow),"Yellowtail stats");
  Check(stats.MinHealth==yellow.MinHealth && stats.MaxHealth==yellow.MaxHealth && stats.YellowSpeedMps==yellow.YellowSpeedMps,"Yellowtail health and speed");
  for(int b=0;b<5;b++)for(int bait=0;bait<5;bait++)for(int lure=0;lure<(bait==4?5:1);lure++)
  {
   ShopCatalog.ActiveLureVariant=lure;
   Check(FishingTuning.TryGetChance(id,bait,b,out var chance),"Complete chances");
   FishingTuning.TryGetChance(1,bait,b,out var snapper);
   Check(b==0?chance==snapper && chance>0:chance==0,"Exclusive Suncrest and matching Red Snapper odds");
   int[] counts=new int[FishCatalog.Count];
   for(int n=0;n<10000;n++)counts[ReefCatalog.Roll((n+.5f)/10000,bait,b)]++;
   Check(Math.Abs(counts[id]-chance*100)<=1,"Actual spawn rolls");
   Check(FishingTuning.TryRollWeight(id,b,.5f,out var weight)==(b==0),"Biome weight availability");
  }
  for(int i=0;i<=100;i++)
  {
   FishingTuning.TryRollWeight(id,0,i/100f,out var flounderKg);
   FishingTuning.TryRollWeight(3,0,i/100f,out var yellowKg);
   FishingTuning.TryGetHealth(id,flounderKg,out var h1);FishingTuning.TryGetHealth(3,yellowKg,out var h2);
   Check(Math.Abs(h1-h2)<=1,"Equal fight health at matching catch percentile");
   float t1=Mathf.InverseLerp(stats.MinWeightKg,stats.MaxWeightKg,flounderKg),t2=Mathf.InverseLerp(yellow.MinWeightKg,yellow.MaxWeightKg,yellowKg);
   Check(Math.Abs(t1-t2)<.00001,"Equal effective size difficulty");
   var length=FishSizeTable.LengthMetres(id,flounderKg);
   Check(length>0 && Math.Abs(FishSizeTable.WeightForLength(id,length)-flounderKg)<.0001,"Size conversion round trip");
  }
  Resources.Overrides["FishingTuning/FishStats"]="";FishingTuning.Reload();
  Check(!FishingTuning.IsValid,"Fallback test invalidates tuning deliberately");
  for(int b=0;b<5;b++)for(int bait=0;bait<5;bait++)
  {
   float c=ReefCatalog.EquippedChance(id,bait,b);
   Check(b==0?c>0 && c==ReefCatalog.EquippedChance(1,bait,b):c==0,"Fallback exclusivity/odds");
  }
  Console.WriteLine("PASS "+checks+" checks: syntax, 45 tables/450,000 rolls, fight parity, sizes, fallback and stable IDs.");
 }
}
