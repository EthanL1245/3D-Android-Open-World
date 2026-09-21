using UnityEngine;

public sealed class ShopWorldEnvironment : MonoBehaviour
{
    public bool isHome;
    public GameObject emptyPondCover;
    private ShopProgress progress;
    private void Start()
    {
        if(!isHome)return;
        progress=FindFirstObjectByType<ShopProgress>();
        if(progress!=null) { progress.Changed+=RefreshHome; RefreshHome(); }
    }
    private void OnDestroy() { if(progress!=null)progress.Changed-=RefreshHome; }
    private void RefreshHome()
    {
        foreach(var h in habitats) h.gameObject.SetActive(progress.Data.Habitat(h.id)!=null);
        if(emptyPondCover!=null)emptyPondCover.SetActive(progress.Data.CurrentHabitat(true)==null);
    }
    public Transform spawn, gearPoint, marketPoint;
    public ShopHabitat[] habitats;
    public ShopHabitat Habitat(string id) => System.Array.Find(habitats,h=>h!=null && h.gameObject.activeInHierarchy && h.id==id);
    public Transform Point(string id) => id=="gear" ? gearPoint : id=="market" ? marketPoint : Habitat(id)?.interaction;
}
