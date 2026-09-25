using UnityEngine;

[CreateAssetMenu(menuName="Fishing/Boat")]
public sealed class BoatData : ScriptableObject
{
    public string ID;
    public int Cost;
    public float Speed=6;
    public int MaxCapacity=1;
    public Vector3 HullSize=new Vector3(3,1,5);
    public float Draft=.6f;
    public GameObject Prefab;
}
