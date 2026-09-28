#!/usr/bin/env python3
"""Compile real pure rules plus extracted production policies; no Unity/Play Mode claim."""
import argparse,pathlib,subprocess,tempfile,shutil,json,re
ap=argparse.ArgumentParser();ap.add_argument('--dotnet',default=shutil.which('dotnet'));args=ap.parse_args()
if not args.dotnet:ap.error('--dotnet must point to a .NET 8 SDK host')
root=pathlib.Path(__file__).resolve().parents[2];host=pathlib.Path(args.dotnet).resolve();sdk=sorted((host.parent/'sdk').glob('*/Roslyn/bincore'))[-1];refs=sorted((host.parent/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
def method(file,name):
 s=(root/file).read_text();m=re.search(r'public static [^\n]+\b'+name+r'\(',s);assert m,name;start=m.start();i=s.index(')',start)+1
 if s[i:].lstrip().startswith('=>'):return s[start:s.index(';',i)+1]
 a=s.index('{',i);depth=1;i=a+1
 while depth:
  if s[i]=='{':depth+=1
  elif s[i]=='}':depth-=1
  i+=1
 return s[start:i]
with tempfile.TemporaryDirectory(prefix='fishing-checks-') as temp:
 d=pathlib.Path(temp);policy='using UnityEngine;\n'
 for cls,names in [('FishingBurstDamageRuntime',['NormalMinimumForTier','NormalMaximumForTier','CriticalChanceForTier','RollBurstForTier']),('Level2FishingReelRuntime',['ReelMultiplier']),('FishingCastQualityRuntime',['FightQuality'])]:
  policy+='public static class '+cls+' {\n'+'\n'.join(method('Assets/_Game/Scripts/Fishing/'+cls+'.cs',n) for n in names)+'\n}\n'
 (d/'Policies.cs').write_text(policy)
 sources=[root/'Tools/ContentUpdate/Checks/Program.cs',root/'Tools/ContentUpdate/Checks/UnityStubs.cs',d/'Policies.cs']
 for p in ['Fishing/FishingTuning','Fishing/FishCatalog','Fishing/FishSizeTable','Reef/ReefCatalog','ShopWorld/ShopCatalog','ShopWorld/ShopLedger']:sources.append(root/('Assets/_Game/Scripts/'+p+'.cs'))
 rsp=['-nologo','-target:exe','-out:'+str(d/'Checks.dll')]+['-r:'+str(p) for p in refs.glob('*.dll')]+['-r:'+str(sdk/n) for n in ['Microsoft.CodeAnalysis.dll','Microsoft.CodeAnalysis.CSharp.dll']]+[str(p) for p in sources]
 (d/'compile.rsp').write_text('\n'.join('"'+x+'"' for x in rsp));subprocess.run([str(host),str(sdk/'csc.dll'),'@'+str(d/'compile.rsp')],check=True)
 (d/'Checks.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':'8.0.0'}}}))
 for n in ['Microsoft.CodeAnalysis.dll','Microsoft.CodeAnalysis.CSharp.dll']:shutil.copy(sdk/n,d/n)
 subprocess.run([str(host),str(d/'Checks.dll'),str(root)],check=True)
