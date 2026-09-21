using UnityEngine;

public sealed class ShopWorldEnvironment : MonoBehaviour
{
    public Transform spawn, gearPoint, marketPoint;
    public ShopHabitat[] habitats;
    public ShopHabitat Habitat(string id) => System.Array.Find(habitats,h=>h!=null && h.id==id);
    public Transform Point(string id) => id=="gear" ? gearPoint : id=="market" ? marketPoint : Habitat(id)?.interaction;
}
