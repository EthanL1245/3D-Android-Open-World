using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
[Serializable] public class CaughtFishRecord {public int speciesId;public float weightKg;public long caughtUtcTicks;}
class Program
{
 static int checks;
 static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static void Main(string[] args)
 {
  foreach(var path in Directory.GetFiles(Path.Combine(args[0],"Assets"),"*.cs",SearchOption.AllDirectories))
  {var errors=CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error);Check(!errors.Any(),path+string.Join(";",errors));}
  var save=new ShopLedger{coins=200};
  Check(!save.OwnsBoat("raft"),"Existing players do not accidentally own boats");
  Check(!save.EquipBoat("raft"),"Cannot equip unowned boat");
  Check(!save.BuyBoat("raft",201) && save.coins==200,"Insufficient funds leave balance intact");
  Check(!save.BuyBoat("raft",-1) && save.coins==200,"Negative price rejected");
  Check(!save.BuyBoat("",10),"Empty ID rejected");
  Check(save.BuyBoat("raft",150) && save.coins==50 && save.boatEquipped=="raft","Atomic purchase and selection");
  Check(!save.BuyBoat("raft",150) && save.coins==50 && save.boats.Count==1,"Duplicate purchase does not charge");
  for(int i=0;i<100;i++)Check(save.EquipBoat("raft") && save.coins==50 && save.boats.Count==1,"Repeated equip never consumes unlock or coins");
  save.coins=2000;Check(save.BuyBoat("sailboat",1800) && save.OwnsBoat("raft"),"Buying second boat preserves first");
  Check(save.EquipBoat("raft") && save.boatEquipped=="raft","Switch owned boat");
  save.boats=null;Check(!save.OwnsBoat("raft") && save.BuyBoat("raft",150),"Null legacy list handled");
  Console.WriteLine($"PASS: {checks} boat economy assertions and C# source syntax checks.");
 }
}
