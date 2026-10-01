using System;

// Deterministic world-space relief. Independent of fishing zones and terrain resolution.
public static class SeabedRelief
{
    public const float ExtraDepth=35f;
    private static float Clamp01(float v)=>Math.Max(0f,Math.Min(1f,v));
    private static float Smooth(float a,float b,float v)
    {float t=Clamp01((v-a)/(b-a));return t*t*(3f-2f*t);}
    private static float Mix(float a,float b,float t)=>a+(b-a)*t;
    private static float Hash(int x,int z)
    {
        unchecked
        {
            uint h=(uint)x*374761393u+(uint)z*668265263u+260926u;
            h=(h^(h>>13))*1274126177u;
            return (h^(h>>16))*(1f/uint.MaxValue);
        }
    }
    public static float Noise(float x,float z)
    {
        int ix=(int)Math.Floor(x),iz=(int)Math.Floor(z);
        float tx=Smooth(0,1,x-ix),tz=Smooth(0,1,z-iz);
        return Mix(Mix(Hash(ix,iz),Hash(ix+1,iz),tx),Mix(Hash(ix,iz+1),Hash(ix+1,iz+1),tx),tz);
    }
    public static float Height(float x,float z,float originalHeight,float sea)
    {
        float depth=sea-originalHeight;
        // Preserve dry islands, pond banks and the first 80 cm of submerged beach exactly.
        if(depth<=.8f)return originalHeight;
        float strength=Smooth(.8f,9f,depth);
        float warpX=x+42f*(Noise(x*.004f+7,z*.004f+29)-.5f);
        float warpZ=z+36f*(Noise(x*.004f+83,z*.004f+11)-.5f);
        float mass=Noise(warpX*.012f,warpZ*.014f);
        // Smooth narrow transitions create distinct escarpments instead of rounded hills.
        float shelves=-7f+8f*Smooth(.34f,.40f,mass)+7f*Smooth(.62f,.68f,mass);
        float channel=(float)Math.Sin(warpX*.018f+1.6f*Noise(warpX*.003f,warpZ*.006f))*55f;
        channel+=12f*(float)Math.Sin(warpZ*.026f);
        float canyon=1f-Smooth(7f,24f,Math.Abs(channel));
        float tributary=1f-Smooth(4f,15f,Math.Abs((float)Math.Sin(warpZ*.022f+Noise(warpX*.008f,warpZ*.004f))*48f));
        float basin=-4f*(1f-Smooth(.20f,.55f,Noise(x*.005f+31,z*.005f+67)));
        float rubble=(Noise(x*.085f,z*.085f)-.5f)*2f;
        float relief=shelves-16f*canyon-5f*tributary+basin+rubble;
        // Bound crests below the water even on the shallow reef. Never create new land.
        return Math.Min(sea-.8f,originalHeight+strength*relief);
    }
    public static float RockWeight(float depth,float slopeDegrees)
        => Smooth(1.5f,5f,depth)*Smooth(16f,42f,slopeDegrees);
}
