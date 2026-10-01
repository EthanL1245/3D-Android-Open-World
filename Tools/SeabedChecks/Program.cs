using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
class Program
{
    static int count;
    static void Check(bool ok,string reason){count++;if(!ok)throw new Exception(reason);}
    static void Main(string[] args)
    {
        foreach(string file in Directory.GetFiles(Path.Combine(args[0],"Assets"),"*.cs",SearchOption.AllDirectories))
            Check(!CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetDiagnostics().Any(d=>d.Severity==DiagnosticSeverity.Error),"Syntax: "+file);
        float min=999,max=-999;int cliffs=0,samples=0;
        for(int z=-1000;z<=1000;z+=5)for(int x=-1000;x<=1000;x+=5)
        {
            float h=SeabedRelief.Height(x,z,-28,0);
            Check(h==SeabedRelief.Height(x,z,-28,0),"Deterministic generation");
            Check(h>=-28-SeabedRelief.ExtraDepth && h<=-.8f,"No bottom clipping or new dry land");
            min=Math.Min(min,h);max=Math.Max(max,h);
            float dx=(SeabedRelief.Height(x+1,z,-28,0)-SeabedRelief.Height(x-1,z,-28,0))*.5f;
            float dz=(SeabedRelief.Height(x,z+1,-28,0)-SeabedRelief.Height(x,z-1,-28,0))*.5f;
            float slope=(float)(Math.Atan(Math.Sqrt(dx*dx+dz*dz))*180/Math.PI);
            if(slope>35)cliffs++;samples++;
            foreach(float original in new[]{-.8f,0f,.1f,5f,18f})
                Check(SeabedRelief.Height(x,z,original,0)==original,"Dry land and shoreline unchanged");
        }
        Check(max-min>25,"Distinct ridges and valleys");
        Check(cliffs>samples/100,"Cliffs cover meaningful terrain area");
        for(int i=0;i<10000;i++)
        {
            float depth=i/100f;
            float h=SeabedRelief.Height(i*.37f,i*.71f,12-depth,12);
            Check(!float.IsNaN(h) && h>=12-depth-SeabedRelief.ExtraDepth,"Bounded relief at any sea level");
            if(depth>.8f)Check(h<=11.2f,"Submerged peaks");
        }
        for(int slope=0;slope<=90;slope++)
        {
            Check(SeabedRelief.RockWeight(0,slope)==0 && SeabedRelief.RockWeight(1.5f,slope)==0,"Dry/shore paint unchanged");
            Check(SeabedRelief.RockWeight(20,slope)>=0 && SeabedRelief.RockWeight(20,slope)<=1,"Normalized paint blend");
        }
        Check(SeabedRelief.RockWeight(20,50)==1 && SeabedRelief.RockWeight(20,0)==0,"Rock cliffs, sandy flats");
        Console.WriteLine($"PASS {count} checks. Shelf heights {min:F1} to {max:F1} m; {100f*cliffs/samples:F1}% steeper than 35 degrees.");
    }
}
