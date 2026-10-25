using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
class Program
{
    static int checks;
    static void Check(bool ok,string name){checks++;if(!ok)throw new Exception(name);}
    static Terrain Tile(float z,float height) => new Terrain { transform=new Transform {position=new Vector3(0,-100,z)},sample=p=>height+100 };
    static void Main(string[] args)
    {
        foreach(string path in Directory.GetFiles(Path.Combine(args[0],"Assets/_Game"),"*.cs",SearchOption.AllDirectories))
            Check(!CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetDiagnostics().Any(d=>d.Severity==DiagnosticSeverity.Error),"C# syntax: "+path);
        var main=Tile(0,-20); var south=Tile(-100,1.5f);
        Terrain.activeTerrains=new[]{main,south}; Time.frameCount++;
        Check(WorldTerrain.TryGetHeight(new Vector3(50,0,-50),out float h)&&h==1.5f,"Southern terrain sampled instead of main edge");
        Check(WorldTerrain.TryGetHeight(new Vector3(50,0,50),out h)&&h==-20,"Original terrain retained");
        Check(WorldTerrain.TryGetHeight(new Vector3(50,0,0),out h)&&h==1.5f,"Highest surface at shared seam");
        Check(!WorldTerrain.TryGetHeight(new Vector3(50,0,-101),out h),"No clamped terrain beyond map");
        south.isActiveAndEnabled=false;
        Check(!WorldTerrain.TryGetHeight(new Vector3(50,0,-50),out h),"Disabled terrain ignored"); south.isActiveAndEnabled=true;
        var water=new OceanWater();
        south.sample=p=>p.z > -51 && p.z < -49 ? 102.5f : 80;
        Check(!WorldTerrain.SurfaceSegmentClear(new Vector3(50,0,-54),new Vector3(50,0,-46),water,.4f),"Retrieve cannot skip a narrow ridge");
        Check(WorldTerrain.SurfaceSegmentClear(new Vector3(50,0,-60),new Vector3(50,0,-55),water,.4f),"Deep retrieve remains clear");
        south.sample=p=>102.2f;
        Check(!WorldTerrain.SurfaceSegmentClear(new Vector3(50,0,-50),new Vector3(50,0,-49.9f),water,.4f),"Subdued fish lands before entering bank");
        south.sample=p=>80;
        var boat=new BoatData();var q=Quaternion.identity;
        Check(BoatClearance.Valid(boat,new Vector3(50,2.5f,-50),q,water),"Boat spawns over new terrain");
        Check(BoatClearance.Valid(boat,new Vector3(50,2.5f,0),q,water),"Hull spans terrain seam");
        Check(!BoatClearance.Valid(boat,new Vector3(50,2.5f,-101),q,water),"Boat still rejects unknown bathymetry");
        south.sample=p=>102;
        Check(!BoatClearance.Valid(boat,new Vector3(50,2.5f,-50),q,water),"Shallow hull rejected");
        Check(BoatClearance.ApproachingShallowWater(boat,new Vector3(50,2.5f,-50),q,water),"Southern terrain still brakes boats");
        south.sample=p=>80;
        Physics.overlaps=new[]{new Collider()};
        Check(!BoatClearance.Valid(boat,new Vector3(50,2.5f,-50),q,water),"Solid props still block placement");Physics.overlaps=Array.Empty<Collider>();
        var ray=new Ray(new Vector3(80,10,-900),new Vector3(0,-1,0));
        Check(BoatClearance.TryGetWaterTarget(ray,water,18,out float d)&&d==7.5f,"Placement target beyond original water box");
        Check(!BoatClearance.TryGetWaterTarget(ray,water,5,out d),"Placement range retained");
        Check(!BoatClearance.TryGetWaterTarget(new Ray(ray.origin,new Vector3(0,1,0)),water,18,out d),"Upward aim rejected");
        Check(!BoatClearance.TryGetWaterTarget(new Ray(ray.origin,new Vector3(1,0,0)),water,18,out d),"Horizontal aim rejected");
        Terrain.activeTerrains=new[]{main};Time.frameCount++;
        Check(!WorldTerrain.TryGetHeight(new Vector3(50,0,-50),out h),"Cache refreshes after tile removal");
        Console.WriteLine($"PASS: {checks} syntax and terrain/boat regression checks.");
    }
}
