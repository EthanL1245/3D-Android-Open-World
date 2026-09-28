#!/usr/bin/env python3
"""Offline authoring step. Unity consumes the committed exports without Blender."""
import argparse,pathlib,subprocess,tempfile,zipfile,shutil
ap=argparse.ArgumentParser();ap.add_argument('--blender',required=True);ap.add_argument('--packages',required=True);args=ap.parse_args()
root=pathlib.Path(__file__).resolve().parents[2];scripts=pathlib.Path(__file__).parent/'Blender';source=root/'Assets/_Game/ContentUpdate/Source';fish=root/'Assets/_Game/Reef/Source'
source.mkdir(parents=True,exist_ok=True);fish.mkdir(parents=True,exist_ok=True)
def run(blend,script,*tail):
 cmd=[args.blender,'--background']
 if blend:cmd.append(str(blend))
 cmd+=['--python',str(scripts/script),'--']+list(map(str,tail));subprocess.run(cmd,check=True)
with tempfile.TemporaryDirectory(prefix='fishing-source-') as tmp:
 folders={}
 for package in ['Albacore','Greater Amberjack','Level 4 Fishing Rod','Level 4 Fishing Reel','Dock and Dock Post']:
  folder=pathlib.Path(tmp)/package;folder.mkdir();folders[package]=folder
  with zipfile.ZipFile(pathlib.Path(args.packages)/(package+'.zip')) as z:
   # Extract known content by basename only: never permit ZIP traversal.
   for info in z.infolist():
    name=pathlib.PurePosixPath(info.filename).name
    if name and not info.is_dir(): (folder/name).write_bytes(z.read(info))
 for package,asset in [('Albacore','Albacore'),('Greater Amberjack','GreaterAmberjack')]:
  folder=folders[package];run(folder/(package+'.blend'),'Fish.py',fish/(asset+'.fbx'))
  shutil.copy(next(folder.glob('*.jpg')),fish/(asset+'Texture.jpg'))
 for package,script,stem in [('Level 4 Fishing Rod','Rod.py','Level4Rod'),('Level 4 Fishing Reel','Reel.py','Level4Reel')]:
  folder=folders[package];run(folder/(package+'.blend'),script,source/(stem+'.json'))
  shutil.copy(next(folder.glob('*.jpg')),source/(stem+'Texture.jpg'))
 run(None,'Dock.py',folders['Dock and Dock Post'],source)
subprocess.run(['python',str(pathlib.Path(__file__).parent/'check_assets.py')],check=True)
