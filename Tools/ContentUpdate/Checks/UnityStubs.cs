// Pure rules tests only. These are not substitutes for Unity compilation/Play Mode.
namespace UnityEngine {
 public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}}
 public struct Vector2 {public float x,y;public Vector2(float a,float b){x=a;y=b;}public static Vector2 operator +(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);}
 public struct Color {public Color(float r,float g,float b,float a=1){}}
 public static class PlayerPrefs {public static int GetInt(string key,int fallback=0)=>fallback;}
 public static class Random {public static float value=>.5f;}
 public static class Mathf {
  public static float Clamp(float x,float a,float b)=>System.Math.Clamp(x,a,b);
  public static int FloorToInt(float x)=>(int)System.MathF.Floor(x);
  public static float Min(float a,float b)=>System.Math.Min(a,b);
  public static float SmoothStep(float a,float b,float t){t=Clamp01(t);return a+(b-a)*t*t*(3-2*t);}
  public static float Max(float a,float b)=>System.Math.Max(a,b);
  public static int Max(int a,int b)=>System.Math.Max(a,b);
  public static int Clamp(int x,int a,int b)=>System.Math.Clamp(x,a,b);
  public static float Clamp01(float v)=>System.Math.Clamp(v,0,1);
  public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp01(t);
  public static float InverseLerp(float a,float b,float x)=>Clamp01((x-a)/(b-a));
  public static float Pow(float x,float y)=>System.MathF.Pow(x,y);
  public static float Sqrt(float x)=>System.MathF.Sqrt(x);
  public static int RoundToInt(float x)=>(int)System.MathF.Round(x);
  public static float PingPong(float x,float length){float t=x-System.MathF.Floor(x/(2*length))*2*length;return length-System.MathF.Abs(t-length);}
 }
}

namespace UnityEngine {
 public class TextAsset {public string text;public TextAsset(string s){text=s;}}
 public static class Resources {public static string Root;public static T Load<T>(string path) where T:class => new TextAsset(System.IO.File.ReadAllText(System.IO.Path.Combine(Root,"Assets/Resources",path+".csv"))) as T;}
 public static class Debug {public static void LogError(object o)=>System.Console.Error.WriteLine(o);}
}
