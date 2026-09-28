using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
[Serializable] public class CaughtFishRecord {public int speciesId;public float weightKg;public long caughtUtcTicks;}
class Program
{
 static int count;
 static void Check(bool ok,string reason){count++;if(!ok)throw new Exception(reason);}
 static void Main(string[] args)
 {
  foreach(var file in Directory.GetFiles(Path.Combine(args[0],"Assets"),"*.cs",SearchOption.AllDirectories))
  {var errors=CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();Check(errors.Length==0,file+string.Join(";",errors.Select(x=>x.ToString())));}
  for(int lure=0;lure<ShopCatalog.LureVariantCount;lure++)
  {
   ShopCatalog.SetActiveLureVariant(lure);
   for(int bait=0;bait<5;bait++)
   {
    for(int id=0;id<FishCatalog.Count;id++)Check(ReefCatalog.EquippedChance(id,bait)==SuncrestBaseline.EquippedChance(id,bait),"Suncrest odds unchanged");
    for(int sample=0;sample<=1000;sample++)Check(ReefCatalog.Roll(sample/1000f,bait)==SuncrestBaseline.Roll(sample/1000f,bait),"Suncrest roll unchanged");
    for(int biome=0;biome<3;biome++)
    {
     Check(Math.Abs(FishCatalog.ActiveIds.Sum(id=>ReefCatalog.EquippedChance(id,bait,biome))-100)<.001,"Biome normalization");
     Check(ReefCatalog.EquippedChance(4,bait,biome)==0,"Retired fish excluded");
     var bins=new int[FishCatalog.Count];for(int n=0;n<10000;n++)bins[ReefCatalog.Roll((n+.5f)/10000,bait,biome)]++;
     foreach(int id in FishCatalog.ActiveIds)Check(Math.Abs(bins[id]-100*ReefCatalog.EquippedChance(id,bait,biome))<=2,"Index matches actual roll distribution");
    }
    float Rare(int b)=>new[]{3,5,8}.Sum(id=>ReefCatalog.EquippedChance(id,bait,b));
    Check(Rare(1)>Rare(0) && Rare(2)>Rare(0),"Rare fish concentration increases for every bait/lure");
   }
  }
  var newCenter=new Vector3(400,0,35);var radii=new Vector2(95,65);var shelfCenter=new Vector3(170,0,17.5f);var shelf=new Vector2(475,355);
  for(int x=0;x<=400;x++)
  {
   var p=new Vector3(x,0,35f*x/400f);Check(IslandGeometry.Biome(p,Vector3.zero,newCenter)!=2,"No ocean biome between islands");
   Check(IslandGeometry.Beyond(p,shelfCenter,shelf)==0,"No outer drop between islands");
  }
  Check(IslandGeometry.Biome(newCenter,Vector3.zero,newCenter)==1,"New island biome");
  Check(IslandGeometry.Biome(new Vector3(1000,0,0),Vector3.zero,newCenter)==2,"Outer ocean biome");
  Check(IslandGeometry.Biome(newCenter+new Vector3(0,0,220),Vector3.zero,newCenter)==2,"Outside Brinebreak must not fall back to Suncrest");
  Check(IslandGeometry.Biome(new Vector3(-220,0,0),Vector3.zero,newCenter)==2,"Outside Suncrest radius is ocean");
  Check(IslandGeometry.Biome(new Vector3(0,500,0),Vector3.zero,newCenter)==0,"Zone ignores elevation");
  Check(IslandGeometry.ShelfFloor(0,14,65,0,130)==-14,"Deep shared shelf");
  Check(IslandGeometry.ShelfFloor(0,14,65,130,130)==-65,"Outer abyss");
  Check(IslandGeometry.CoastalFloor(-65,0,500,14)==-65,"Distant coast cannot erase outer drop");
  float prior=1;for(int x=0;x<=400;x++)
  {float blend=IslandGeometry.StormBlend(new Vector3(400+x,0,35),newCenter,135,295);Check(blend<=prior && blend>=0 && blend<=1,"Continuous monotonic wave blend");Check(prior-blend<.011f,"No wave blend steps");prior=blend;}
  Check(prior==0,"Calm outside transition");
  foreach(int id in FishCatalog.ActiveIds)
  {Check(ReefCatalog.MaximumWeight(id,1)>ReefCatalog.MaximumWeight(id,0),"Brinebreak size cap larger");Check(ReefCatalog.HealthMultiplier(1)==1,"Biome no longer changes fish health");}
  Check(!new ShopLedger().brinebreakDiscovered,"Existing saves locked by default");
  ReefCatalog.BrinebreakDiscovered=false;Check(!ReefCatalog.Zones[1].Unlocked,"Travel locked");
  ReefCatalog.BrinebreakDiscovered=true;Check(ReefCatalog.Zones[1].Unlocked,"Discovery unlock");
  Console.WriteLine($"PASS: {count} assertions / syntax checks; exact Suncrest compatibility, biome rolls, shelf boundaries and wave blend.");
 }
}
