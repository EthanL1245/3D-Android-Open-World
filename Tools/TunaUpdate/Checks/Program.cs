using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
class Program {
 static int checks;
 static void Check(bool result,string message){checks++;if(!result)throw new Exception(message);}
 static void Main(string[] args){
  string root=args.Length>0?args[0]:Path.GetFullPath("../../..");
  foreach(string file in Directory.GetFiles(Path.Combine(root,"Assets"),"*.cs",SearchOption.AllDirectories)){
   var errors=CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
   Check(errors.Length==0,file+": "+string.Join("; ",errors.Select(e=>e.ToString())));
  }
  Check(FishCatalog.ActiveIds.Length==8,"Eight active species");
  Check(FishCatalog.CanonicalId(4)==5,"Retired save migration");
  Check(ReefCatalog.Starter.Unlocked,"Starter unlocked");
  Check(ReefCatalog.Starter.weights.Sum()==100,"Total odds");
  Check(ReefCatalog.Weight(0)==20 && ReefCatalog.Weight(8)==2*ReefCatalog.Weight(5),"Mackerel/tuna odds");
  Check(ReefCatalog.Weight(6)==22 && ReefCatalog.Weight(7)==22,"Equal goatfish redistribution");
  var counts=new int[FishCatalog.Count];for(int i=0;i<100000;i++)counts[ReefCatalog.Roll((i+.5f)/100000)]++;
  for(int id=0;id<counts.Length;id++)Check(Math.Abs(counts[id]-1000*ReefCatalog.Weight(id))<=1,"Roll distribution "+id);
  for(int bait=0;bait<4;bait++){
   var baitCounts=new int[FishCatalog.Count];for(int i=0;i<100000;i++)baitCounts[ReefCatalog.Roll((i+.5f)/100000,bait)]++;
   Check(baitCounts[4]==0,"Retired fish excluded");Check(Math.Abs(baitCounts[8]-2*baitCounts[5])<=2,"Tuna ratio with bait");
  }
  Check(FishingRules.CastPower(0)==0 && FishingRules.CastPower(.85f)==1 && FishingRules.CastPower(1.7f)==0,"Power oscillation");
  Check(FishingRules.CastDistance(0,45)==2 && FishingRules.CastDistance(1,45)==45,"Close/far endpoints");
  foreach(int id in FishCatalog.ActiveIds){
   var fish=FishCatalog.Get(id);
   Check(FishingRules.WeightAtDepth(id,6,1)==fish.MaxWeightKg,"Deep max size "+id);
   Check(FishingRules.MaxHealth(id,fish.MaxWeightKg)>FishingRules.MaxHealth(id,fish.MinWeightKg),"HP scales with size");
   for(int q=0;q<=100;q++){
    float last=fish.MinWeightKg;
    for(int d=0;d<=60;d++){
     float weight=FishingRules.WeightAtDepth(id,d*.1f,q*.01f);
     Check(weight>=last-.0001f && weight>=fish.MinWeightKg-.0001f && weight<=fish.MaxWeightKg+.0001f,"Depth monotonic/bounded");last=weight;
    }
   }
   for(int i=0;i<=100;i++){
    float length=.05f+.07f*i/100;float kg=FishSizeTable.WeightForLength(id,length);
    Check(Math.Abs(FishSizeTable.LengthMetres(id,kg)-length)<.00001,"Pond length inversion");
    Check(FishingRules.MaxHealth(id,kg)<FishingRules.MaxHealth(id,fish.MinWeightKg),"Tiny fish HP");
   }
  }
  Check(FishingRules.MaxHealth(8,40)>10*FishingRules.MaxHealth(0,FishSizeTable.WeightForLength(0,.1f)),"Big rare fish substantially stronger");
  Console.WriteLine($"PASS: {checks:N0} assertions; C# syntax, actual fishing rules, probabilities, depth/HP curves and pond sizes.");
 }
}
