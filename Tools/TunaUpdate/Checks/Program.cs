using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
[Serializable] public class CaughtFishRecord { public int speciesId;public float weightKg;public long caughtUtcTicks; }
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
  Check(FishingRules.CastPower(0)==0 && FishingRules.CastPower(1.25f)==1 && FishingRules.CastPower(2.5f)==0,"Power oscillation");
  Check(FishingRules.CastDistance(0,30)==5 && FishingRules.CastDistance(1,30)==30,"Close/far endpoints");
  Check(FishingRules.ContinuousCastRange(new[]{true,true,true},out float allMin) && allMin==0,"All water enabled");
  Check(FishingRules.ContinuousCastRange(new[]{false,false,true,true,true},out float min) && min==.5f,"Invalid prefix greyed");
  Check(FishingRules.ContinuousCastRange(new[]{true,false,true,true},out _),"Valid interval beyond gap is usable");
  Check(FishingRules.ContinuousCastRange(new[]{true,true,false},out _),"Blocked max does not disable nearer interval");
  Check(!FishingRules.ContinuousCastRange(new[]{false,false,false},out _),"All land disables");
  Check(!FishingRules.ContinuousCastRange(new[]{false,false,true},out _),"Single endpoint is not a continuous range");
  for(int bits=0;bits<256;bits++){
   var samples=Enumerable.Range(0,8).Select(i=>(bits&(1<<i))!=0).ToArray();
   int first=Array.IndexOf(samples,true);
   bool expected=Enumerable.Range(0,7).Any(i=>samples[i] && samples[i+1]);
   Check(FishingRules.ContinuousCastRange(samples,out _)==expected,"Exhaustive range topology");
   for(int i=0;i<7;i++)Check(FishingRules.IsCastPowerAvailable(samples,(i+.5f)/7)==(samples[i] && samples[i+1]),"Gauge interval mask matches cast eligibility");
  }
  float early=FishingRules.CastPower(.2f)-FishingRules.CastPower(.1f),late=FishingRules.CastPower(1.24f)-FishingRules.CastPower(1.14f);
  Check(late>3*early,"Needle speeds up near red");
  var save=new ShopLedger();save.bag.Add(new CaughtFishRecord{speciesId=0,weightKg=1});
  Check(save.EnsureCatchStats() && save.totalCaught.Sum()==0,"Existing bag starts with zero catch history");
  save.RecordCatch(0,.5f);save.RecordCatch(0,.3f);save.RecordCatch(8,20);
  Check(save.totalCaught[0]==2 && save.personalBestKg[0]==.5f && save.totalCaught[8]==1,"Counts and best per species");
  Check(!save.EnsureCatchStats() && save.totalCaught[0]==2,"Stats initialization never resets progress again");
  var options=new System.Text.Json.JsonSerializerOptions{IncludeFields=true};
  string json=System.Text.Json.JsonSerializer.Serialize(save,options);
  var restored=System.Text.Json.JsonSerializer.Deserialize<ShopLedger>(json,options);
  restored.EnsureCatchStats();Check(restored.totalCaught[0]==2 && restored.personalBestKg[8]==20,"Save round trip preserves records");
  int coins=save.coins;Check(!save.BuyBait(1) && save.coins==coins,"Cannot buy free worms");
  for(int i=0;i<100;i++)Check(save.ConsumeBait()==0,"Infinite worms never run out");
  save.baitEquipped=2;save.bait[2]=1;Check(save.ConsumeBait()==2 && save.baitEquipped==0 && save.bait[2]==0,"Specialty bait returns to worms");
  Check(FishingRules.MaxHealth(0,.25f)>=72 && FishingRules.MaxHealth(0,.25f)<=74,"Health increased across beginner fish");
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
