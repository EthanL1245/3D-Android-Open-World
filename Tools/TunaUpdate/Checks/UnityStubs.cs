// Pure rules tests only. These are not substitutes for Unity compilation/Play Mode.
namespace UnityEngine {
 public struct Color {public Color(float r,float g,float b,float a=1){}}
 public static class PlayerPrefs {public static int GetInt(string key,int fallback=0)=>fallback;}
 public static class Random {public static float value=>.5f;}
 public static class Mathf {
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
