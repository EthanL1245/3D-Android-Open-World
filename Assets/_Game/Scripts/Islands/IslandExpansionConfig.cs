using UnityEngine;

[CreateAssetMenu(menuName="Fishing/Island Expansion")]
public sealed class IslandExpansionConfig : ScriptableObject
{
    public string IslandName="Brinebreak Isle";
    public float OffsetBeyondSuncrest=550f;
    public Vector2 IslandRadii=new Vector2(95,65);
    public float ShelfDepth=28f;
    public float OceanDepth=65f;
    public float DropoffWidth=130f;
    public int HeightResolution=1025;
    public int ScenerySeed=260926;
    public float WaveAmplitudeMultiplier=2.8f;
    public float WaveSpeedMultiplier=1.65f;
    public float WaveBlendDistance=160f;
}

