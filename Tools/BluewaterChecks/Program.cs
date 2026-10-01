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
  foreach(string file in Directory.GetFiles(Path.Combine(Resources.Root,"Assets"),"*.cs",SearchOption.AllDirectories))
   Check(!CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetDiagnostics().Any(d=>d.Severity==DiagnosticSeverity.Error),"C# syntax: "+file);
  FishingTuning.Reload();Check(FishingTuning.IsValid,FishingTuning.ValidationError);
  Check(ReefCatalog.Zones.Length==4 && ReefCatalog.Zones[2].id=="deep-ocean" && ReefCatalog.Zones[3].id=="bluewater-cay","Stable biome IDs");
  Check(!ReefCatalog.Zones[3].Unlocked,"New save starts undiscovered");
  ReefCatalog.BluewaterDiscovered=true;Check(ReefCatalog.Zones[3].Unlocked,"Discovery unlocks new biome");
  int[] allowed={0,5,8,9,13};
  for(int biome=0;biome<4;biome++)for(int bait=0;bait<5;bait++)for(int lure=0;lure<(bait==4?5:1);lure++)
  {
   ShopCatalog.ActiveLureVariant=lure;float sum=0;
   foreach(int id in FishCatalog.ActiveIds)
   {
    Check(FishingTuning.TryGetChance(id,bait,biome,out float chance),"Chance row exists");sum+=chance;
    if(biome==3)Check(allowed.Contains(id)?chance>0:chance==0,"Pelagic-only table");
   }
   Check(sum==100,"100% table");
   var bins=new int[FishCatalog.Count];
   for(int n=0;n<10000;n++){int id=ReefCatalog.Roll((n+.5f)/10000,bait,biome);bins[id]++;if(biome==3)Check(allowed.Contains(id),"No excluded species rolls");}
   foreach(int id in FishCatalog.ActiveIds)
   {FishingTuning.TryGetChance(id,bait,biome,out float c);Check(Math.Abs(bins[id]-100*c)<2,"Actual roll agrees with configured odds");}
  }
  foreach(int id in FishCatalog.ActiveIds)
  {
   bool ok=FishingTuning.TryRollWeight(id,3,.5f,out float kg);
   Check(ok==allowed.Contains(id),"Unavailable fish cannot roll weights");
   if(ok){var stats=FishCatalog.Get(id);Check(kg>=stats.MinWeightKg && kg<=stats.MaxWeightKg,"Valid weight bounds");}
  }
  var brine=new Vector3(700,0,35);var center=PelagicIslandGeometry.Center(brine,new Vector2(95,65));
  Check(center.x>0 && center.z<0,"Southeast of main island");
  double nearest=double.MaxValue;
  for(int i=0;i<36000;i++)
  {double a=i*Math.PI/18000;double dx=center.x-brine.x-95*Math.Cos(a),dz=center.z-brine.z-65*Math.Sin(a);nearest=Math.Min(nearest,Math.Sqrt(dx*dx+dz*dz));}
  double gap=nearest-PelagicIslandGeometry.Radius;
  Check(gap>=190 && gap<=205,"About 200 m shore-to-shore gap");
  for(int degree=0;degree<360;degree++)
  {
   float x=(float)Math.Cos(degree*Math.PI/180),z=(float)Math.Sin(degree*Math.PI/180);
   Check(PelagicIslandGeometry.Contains(center+new Vector3(x*132.98f,0,z*132.98f),center),"75m shore margin inside");
   Check(!PelagicIslandGeometry.Contains(center+new Vector3(x*133.02f,0,z*133.02f),center),"75m shore margin outside");
  }
  Console.WriteLine($"PASS {checks} checks: 36 runtime tables, pelagic-only rolls, discovery flags and full 75m perimeter. Shore gap {gap:F1}m.");
 }
}
