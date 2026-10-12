using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

// Initial guest-only IP endpoint. Replace with HTTPS before adding accounts/data.
[InitializeOnLoad]
public sealed class FishingMultiplayerBuildSettings : IPreprocessBuildWithReport
{
    static FishingMultiplayerBuildSettings(){EditorApplication.delayCall+=Configure;}
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report){Configure();}
    private static void Configure()
    {
        PlayerSettings.Android.forceInternetPermission=true;
        PlayerSettings.insecureHttpOption=InsecureHttpOption.AlwaysAllowed;
    }
}
