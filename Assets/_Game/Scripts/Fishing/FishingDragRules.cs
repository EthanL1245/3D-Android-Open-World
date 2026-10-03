using UnityEngine;

// Central balance values shared by live combat, UI and regression checks.
public static class FishingDragRules
{
    public const int Low=0, Medium=1, High=2;
    public const float SkillChargeSeconds=12f;
    public const float PassiveDamageSeconds=1f;
    public const float HighTensionPerSecond=.025f;
    public static float EscapeMultiplier(int mode)=>mode==Low?1.5f:1f;
    public static float TensionMultiplier(int mode)=>mode==Low?.5f:1f;
    public static float DamageMultiplier(int mode)=>mode==Low?.5f:mode==High?2f:1f;
    public static float Charge(float current,int mode,float seconds)=>Mathf.Clamp01(current+(mode==High?Mathf.Max(0,seconds)/SkillChargeSeconds:0));
    public static bool IsQuickSwipe(float distance,float seconds,float screenShortSide)
        =>seconds>=.04f && seconds<=.35f && distance>=Mathf.Max(70f,screenShortSide*.12f)
            && distance/seconds>=screenShortSide*.8f;
    public static int SkillDamage(int normalRodDamage)=>normalRodDamage*10;
}
