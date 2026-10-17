using System;
using System.Reflection;
using UnityEngine;
class Program
{
 const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
 static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
 static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 static void Main()
 {
  var p=new AnglerProgression();
  Check(p.level==1 && p.Value(0)==0,"Fresh save");
  Check(p.AddXp(249,n=>0)==0 && p.experience==249,"XP threshold");
  Check(p.AddXp(1,n=>0)==1 && p.level==2 && p.stats[0]==1,"First point");
  var random=new System.Random(42);p.AddXp(int.MaxValue,n=>random.Next(n));
  Check(p.level==601 && p.experience==0,"Maximum level and overflow");
  foreach(int stat in p.stats)Check(stat==100,"Every stat capped without wasted rolls");
  for(int l=1;l<601;l++)Check(AnglerProgression.RequiredXp(l+1)>AnglerProgression.RequiredXp(l),"Increasing XP curve");
  Check(AnglerProgression.CatchXp(400,.5f)>AnglerProgression.CatchXp(100,.5f),"HP affects XP");
  Check(AnglerProgression.CatchXp(100,1)>AnglerProgression.CatchXp(100,.2f),"Difficulty affects XP");
  for(int i=0;i<4;i++){p.level=(i+1)*20-1;Check(!p.Unlocked(i),"Locked milestone");p.level++;Check(p.Unlocked(i),"Unlocked milestone");}
  p=new AnglerProgression{level=0,stats=null,experience=double.NaN};p.Normalize();Check(p.level==1 && p.stats.Length==6 && p.experience==0,"Old save migration");
  var f=new FishingSystem();var hud=new FishingHUD();var owner=new FishingBurstDamageRuntime();
  Set(owner,"fishing",f);Set(owner,"hud",hud);Set(owner,"progress",new ShopProgress());
  void Tick(float dt){Time.deltaTime=dt;typeof(FishingBurstDamageRuntime).GetMethod("Update",F).Invoke(owner,null);}
  Tick(0);Check(!owner.CanUseAbility(0),"Locked ability rejected");
  f.Progression.level=80;
  Set(f,"state","Idle");Tick(0);Set(f,"state","Fighting");Tick(0);
  Check(Get<int>(f,"fishHealthPoints")==900,"Hookset once after max HP known");Tick(0);Check(Get<int>(f,"fishHealthPoints")==900,"No repeated hookset");
  owner.UseAbility(0);Check(!owner.CanUseAbility(0),"Once per fight");
  Tick(1);Check(Get<int>(f,"fishHealthPoints")==880,"Burn ticks in authoritative HP");
  ShopWorldHUD.MenuOpen=true;Tick(5);Check(Get<int>(f,"fishHealthPoints")==880,"Menu pauses burn");ShopWorldHUD.MenuOpen=false;
  Tick(4);Check(Get<int>(f,"fishHealthPoints")==800,"Burn total 10 percent");Tick(1);Check(Get<int>(f,"fishHealthPoints")==800,"Burn ends");
  owner.UseAbility(2);owner.UseAbility(3);Check(owner.ImmortalitySeconds==5 && owner.SirenSeconds==4,"Timed abilities activate");
  Tick(2);Check(owner.ImmortalitySeconds==3 && owner.SirenSeconds==2,"Timed abilities count down");
  Set(f,"state","Idle");Tick(0);Check(owner.ImmortalitySeconds==0 && owner.SirenSeconds==0,"Cancel clears timers");
  Set(f,"state","Fighting");Tick(0);Check(owner.CanUseAbility(0),"Fresh fight resets consumption");
  f.Progression.stats[0]=10;f.Progression.stats[1]=100;hud.ActionInput.IsHeld=true;
  int hp=Get<int>(f,"fishHealthPoints");Tick(.35f);Check(hp-Get<int>(f,"fishHealthPoints")==26,"Raw then percent burst damage");
  hud.ActionInput.IsHeld=false;Set(owner,"authoritativeHp",5);owner.UseAbility(0);Tick(.5f);
  Check(Get<int>(f,"fishHealthPoints")==0 && Get<bool>(f,"fishUnconscious") && f.Presentations>0,"Burn KO presentation");
  Check(!owner.CanUseAbility(2),"No ability on unconscious fish");
  Set(f,"state","Idle");Tick(0);Set(f,"state","Fighting");Tick(0);owner.UseAbility(0);
  for(int i=0;i<301;i++)Tick(1f/60f);
  Check(Get<int>(f,"fishHealthPoints")==800,"Burn completes all ten ticks at 60 FPS");
  Console.WriteLine("PASS: progression, caps, migration, XP curve, unlocks, authoritative damage, burn KO, timing, menu pause and fight reset.");
 }
}
