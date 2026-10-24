"""Offline model checks using the actual binary TerrainData (UnityPy + numpy)."""
from pathlib import Path
import re
import subprocess
import UnityPy
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
asset = ROOT/'Assets/_Game/EditableFishingMap/LockedSuncrestGrass-20261010-101340353/ProtectedSuncrestTerrain.asset'
t = next(o.read_typetree() for o in UnityPy.load(str(asset)).objects if o.type.name == 'TerrainData')
hm = t['m_Heightmap']; n = hm['m_Resolution']
source = np.array(hm['m_Heights']).reshape(n,n)/32767
size = hm['m_Scale']['x']*(n-1)
vertical = hm['m_Scale']['y']
sea = 2.5
sx, sy, sz = -575.2248, -107.5, -843.6983

def smooth(a,b,v):
    t = np.clip((v-a)/(b-a),0,1)
    return t*t*(3-2*t)

def hash01(x,z):
    h = (x.astype(np.int64)*374761393 + z.astype(np.int64)*668265263 + 260926) & 0xffffffff
    h = ((h ^ (h >> 13))*1274126177) & 0xffffffff
    return (h ^ (h >> 16))/4294967295

def noise(x,z):
    ix,iz = np.floor(x),np.floor(z)
    tx,tz = smooth(0,1,x-ix),smooth(0,1,z-iz)
    a = hash01(ix,iz)*(1-tx)+hash01(ix+1,iz)*tx
    b = hash01(ix,iz+1)*(1-tx)+hash01(ix+1,iz+1)*tx
    return a*(1-tz)+b*tz

def relief(x,z):
    wx = x+42*(noise(x*.004+7,z*.004+29)-.5)
    wz = z+36*(noise(x*.004+83,z*.004+11)-.5)
    mass = noise(wx*.012,wz*.014)
    shelves = -7+8*smooth(.34,.40,mass)+7*smooth(.62,.68,mass)
    channel = np.sin(wx*.018+1.6*noise(wx*.003,wz*.006))*55 +12*np.sin(wz*.026)
    canyon = 1-smooth(7,24,np.abs(channel))
    tributary = 1-smooth(4,15,np.abs(np.sin(wz*.022+noise(wx*.008,wz*.004))*48))
    basin = -4*(1-smooth(.20,.55,noise(x*.005+31,z*.005+67)))
    rubble = (noise(x*.085,z*.085)-.5)*2
    return shelves-16*canyon-5*tributary+basin+rubble

x = sx+np.linspace(0,size,n)[None,:]
z = sz-size+np.linspace(0,size,n)[:,None]
join = sy+source[0,:]*vertical
blend = smooth(0,96,sz-z)
height = np.minimum(sea-5, join[None,:]*(1-blend)+(sea-65+relief(x,z))*blend)
height = sy+np.clip((height-sy)/vertical,0,1)*vertical
assert np.isfinite(height).all()
assert height.max() <= sea-5
assert np.max(np.abs(height[-1,:]-join)) < 1e-9
assert float(np.ptp(height)) > 25, 'Expected varied ridges and valleys'
assert np.isclose(z[-1,0],sz) and z[0,0] < sz
print(f'PASS {n*n:,} height samples: {sea-height.max():.2f}–{sea-height.min():.2f} m underwater; no islands')
print(f'PASS {n} shared edge samples match the original; new extent {size:.2f} m south')

scene_path = 'Assets/Scenes/PrototypeWorld.unity'
scene = (ROOT/scene_path).read_text()
old = subprocess.check_output(['git','show','HEAD:'+scene_path],cwd=ROOT,text=True)
def blocks(s):
    return {int(m.group(1)):m.group(0) for m in re.finditer(r'^--- !u!\d+ &(\d+).*?(?=^--- !u!|\Z)',s,re.M|re.S)}
a,b = blocks(old),blocks(scene)
for key,value in a.items():
    if key != 9223372036854775807:
        assert b[key] == value, ('Existing scene object changed',key)
assert len(re.findall(r'^  m_Name: South Ocean Seabed$',scene,re.M)) == 1
assert 'sourceTerrain: {fileID: 869948846}' in scene
assert 'water: {fileID: 1680097373}' in scene
assert len(re.findall(r'^--- !u!\d+ &(\d+)',scene,re.M)) == len(b)
print('PASS: scene references unique; every pre-existing scene object remains unchanged')
