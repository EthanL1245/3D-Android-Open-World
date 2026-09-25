using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Habitat management is its own task, like BAIT / LURES. Hide the global
/// TRAVEL / FISH BAG / EQUIPMENT tabs as soon as VIEW HABITAT opens, not only
/// after entering the secondary ADD FISH picker.
/// </summary>
[DefaultExecutionOrder(10000)]
public sealed class HabitatMenuIsolationFix : MonoBehaviour
{
    private ShopWorldHUD hud;
    private FieldInfo pageField,navigationTabsField,headingField;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        foreach(ShopWorldHUD target in FindObjectsByType<ShopWorldHUD>(FindObjectsSortMode.None))
            if(target!=null && target.GetComponent<HabitatMenuIsolationFix>()==null)
                target.gameObject.AddComponent<HabitatMenuIsolationFix>();

        SceneManager.sceneLoaded-=OnSceneLoadedStatic;
        SceneManager.sceneLoaded+=OnSceneLoadedStatic;
    }

    private static void OnSceneLoadedStatic(Scene scene,LoadSceneMode mode)
    {
        foreach(ShopWorldHUD target in FindObjectsByType<ShopWorldHUD>(FindObjectsSortMode.None))
            if(target!=null && target.GetComponent<HabitatMenuIsolationFix>()==null)
                target.gameObject.AddComponent<HabitatMenuIsolationFix>();
    }

    private void Awake()
    {
        hud=GetComponent<ShopWorldHUD>();
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        Type type=typeof(ShopWorldHUD);
        pageField=type.GetField("page",flags);
        navigationTabsField=type.GetField("navigationTabs",flags);
        headingField=type.GetField("heading",flags);
        if(hud==null || pageField==null || navigationTabsField==null || headingField==null)
        {
            Debug.LogError("HabitatMenuIsolationFix could not bind ShopWorldHUD fields.");
            enabled=false;
        }
    }

    private void LateUpdate()
    {
        if(hud==null)return;
        string page=pageField.GetValue(hud) as string;
        if(string.IsNullOrEmpty(page))return;

        const string addPrefix="habitat-add:";
        string habitatId=page.StartsWith(addPrefix,StringComparison.Ordinal)
            ? page.Substring(addPrefix.Length)
            : page;
        var definition=ShopCatalog.Habitat(habitatId);
        if(definition==null)return;

        var tabs=navigationTabsField.GetValue(hud) as List<GameObject>;
        if(tabs!=null)
            for(int i=0;i<tabs.Count;i++)if(tabs[i]!=null)tabs[i].SetActive(false);

        Text heading=headingField.GetValue(hud) as Text;
        if(heading!=null)
            heading.text=page.StartsWith(addPrefix,StringComparison.Ordinal)
                ? "ADD FISH • "+definition.name.ToUpperInvariant()
                : definition.name.ToUpperInvariant();
    }
}
