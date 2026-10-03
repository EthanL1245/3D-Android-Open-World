using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
class Program
{
 static void Check(bool yes,string message){if(!yes)throw new Exception(message);}
 static void Main(string[] args)
 {
  Resources.Root=Path.GetFullPath(args[0]);
  foreach(string file in Directory.GetFiles(Path.Combine(Resources.Root,"Assets"),"*.cs",SearchOption.AllDirectories))
   Check(!CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetDiagnostics().Any(d=>d.Severity==DiagnosticSeverity.Error),file);
  FishingTuning.Reload();Check(FishingTuning.IsValid,FishingTuning.ValidationError);
  for(int biome=0;biome<5;biome++)for(int bait=0;bait<9;bait++)
  {
   int key=bait<4?bait:4;ShopCatalog.ActiveLureVariant=Math.Max(0,bait-4);
   float total=FishCatalog.ActiveIds.Sum(id=>ReefCatalog.EquippedChance(id,key,biome));Check(Math.Abs(total-100)<.001,"Total");
   float reef=ReefCatalog.EquippedChance(18,key,biome),mako=ReefCatalog.EquippedChance(19,key,biome),scar=ReefCatalog.EquippedChance(20,key,biome);
   Check(reef==((biome==1||biome==4)?2:0),"Reef shark habitat");
   Check(mako==((biome==2||biome==3)?2:0)&&scar==mako*.5f,"Mako habitat/odds");
   for(int i=0;i<1000;i++)
   {
    int id=ReefCatalog.Roll(i/999f,key,biome);
    Check(ReefCatalog.EquippedChance(id,key,biome)>0,"Zero-chance fish rolled");
    if(biome==4)Check(id==1||id==16||id==17||id==18,"Snapper catch");
   }
  }
  for(int biome=2;biome<=3;biome++)for(int i=0;i<=100;i++)
  {
   FishingTuning.TryRollWeight(19,biome,i/100f,out float m);FishingTuning.TryRollWeight(20,biome,i/100f,out float s);
   Check(Math.Abs(s-m*1.5f)<.001,"Weight ratio");
   Check(Math.Abs(FishSizeTable.LengthMetres(20,s)-1.5f*FishSizeTable.LengthMetres(19,m))<.0001,"Length ratio");
   FishingTuning.TryGetHealth(19,m,out int hm);FishingTuning.TryGetHealth(20,s,out int hs);
   Check(Math.Abs(hs-hm*1.5f)<=1,"Health ratio");
  }
  Check(Math.Abs(FishingTuning.YellowSpeed(20,1)/FishingTuning.YellowSpeed(19,1)-1.5f)<.0001,"Speed ratio");
  ReefCatalog.SnapperDiscovered=false;Check(!ReefCatalog.Zones[4].Unlocked,"Starts locked");
  ReefCatalog.SnapperDiscovered=true;Check(ReefCatalog.Zones[4].Unlocked,"Unlocks");
  Check(ReefCatalog.IslandIndexOrder.SequenceEqual(new[]{0,4,1,3,2}),"Index order");
  Console.WriteLine("Passed: C# syntax, all 45 bait/biome tables, 45,000 species rolls, paired size/HP ratios, speed, discovery and index order.");
 }
}
