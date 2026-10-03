using System;
using System.Reflection;
using UnityEngine;
class Program {
 const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
 static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
 static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);
 static void Call(object o,string n)=>o.GetType().GetMethod(n,F).Invoke(o,null);
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static void Main(){
  var f=new FishingSystem();var h=new FishingHUD();var r=new FishingBurstDamageRuntime();
  Set(r,"fishing",f);Set(r,"hud",h);Set(r,"progress",new ShopProgress());
  void Tick(float dt){Time.deltaTime=dt;Call(r,"Update");}
  Tick(0);Check(r.SelectedDrag==1,"Default medium");h.ActionInput.IsHeld=true;Tick(.35f);Check(h.Damage==3,"Medium damage");
  r.SelectDrag(0);int before=h.Damage;Tick(.35f);Tick(.35f);Check(h.Damage-before==3,"Low exact half over odd rolls");
  r.SelectDrag(2);before=h.Damage;Tick(.35f);Check(h.Damage-before==6,"High double reel damage");
  h.ActionInput.IsHeld=false;before=h.Damage;Tick(.65f);Check(h.Damage-before==3,"High passive rod tick");
  for(int i=0;i<120;i++)Tick(.1f);
  Check(h.Charge>=.999f,"High fills meter");Tick(.1f);
  Check(!r.TrySkillSwipe(new Vector2(400,0),1),"Reject slow swipe");
  Check(!r.TrySkillSwipe(new Vector2(5,0),.1f),"Reject tiny swipe");
  ShopWorldHUD.MenuOpen=true;Check(!r.TrySkillSwipe(new Vector2(400,0),.2f),"Reject menu swipe");before=h.Damage;Tick(1);Check(h.Damage==before,"No passive damage in covered menu");ShopWorldHUD.MenuOpen=false;
  r.SelectDrag(0);Check(r.TrySkillSwipe(new Vector2(400,0),.2f),"Allow full meter after changing drag");Check(!r.TrySkillSwipe(new Vector2(400,0),.2f),"One spend only");
  before=h.Damage;Tick(0);Check(h.Damage-before==30 && h.Charge==0,"10x base rod strike, no Low multiplier");
  Set(r,"skillCharge",1f);Set(r,"authoritativeHp",5);Check(r.TrySkillSwipe(new Vector2(400,0),.2f),"Finisher queued");Tick(0);
  Check(Get<int>(f,"fishHealthPoints")==0 && Get<bool>(f,"fishUnconscious") && f.Presentations>0,"Skill KO uses existing presentation");
  Check(!r.TrySkillSwipe(new Vector2(400,0),.2f),"No strikes on unconscious fish");
  Set(f,"state","Idle");Tick(0);Check(!h.Fighting && r.SelectedDrag==1 && Get<float>(r,"skillCharge")==0,"Fight cleanup");
  Set(f,"state","Fighting");Tick(0);Check(r.SelectedDrag==1 && Get<float>(r,"skillCharge")==0,"Fresh fight");
  Check(FishingDragRules.EscapeMultiplier(0)==1.5f && FishingDragRules.TensionMultiplier(0)==.5f,"Low escape/tension");
  Check(FishingDragRules.EscapeMultiplier(1)==1 && FishingDragRules.DamageMultiplier(1)==1 && FishingDragRules.TensionMultiplier(1)==1,"Medium unchanged");
  Console.WriteLine("PASS: live burst owner: Low/Medium/High damage, passive ticks, charge, swipe gates, one-shot consumption, skill KO, menu pause, fight reset.");
 }
}
