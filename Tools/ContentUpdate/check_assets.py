#!/usr/bin/env python3
import pathlib,csv,json,math,struct
root=pathlib.Path(__file__).resolve().parents[2];src=root/'Assets/_Game/ContentUpdate/Source';tuning=root/'Assets/Resources/FishingTuning'
def rows(name):return list(csv.DictReader(l for l in (tuning/name).read_text().splitlines() if l and not l.startswith('#')))
stats=rows('FishStats.csv');ids=[int(x['speciesId']) for x in stats];assert ids==[0,1,2,3,5,6,7,8,9,10,11,12,13,14]
assert all(float(s['yellowSpeedMps'])>0 for s in stats)
chances=rows('BiomeBaitSpeciesChance.csv');assert len(chances)==27
for row in chances:
 values=[int(v) for k,v in row.items() if k not in ('biomeId','baitKey')];assert len(values)==14 and sum(values)==100 and min(values)>=0
weights=rows('BiomeFishWeights.csv');assert len(weights)==42
for row in weights:
 s=next(s for s in stats if s['speciesId']==row['speciesId'])
 if float(row['minKg'])==0 and float(row['maxKg'])==0:
  for chance in chances:
   if chance['biomeId']==row['biomeId']:
    key=next(k for k in chance if k.startswith(row['speciesId']+'_'))
    assert int(chance[key])==0,('Unavailable species has positive chance',row['biomeId'],row['speciesId'],chance['baitKey'])
 else:assert float(s['minWeightKg'])<=float(row['minKg'])<float(row['maxKg'])<=float(s['maxWeightKg'])
def part(p):
 v=p['vertices'];uv=p['uv'];tri=p['triangles'];assert len(v)%3==0 and len(tri)%3==0 and len(uv)==len(v)//3*2
 assert all(math.isfinite(x) for x in v+uv) and min(tri)>=0 and max(tri)<len(v)//3
 assert max(uv)-min(uv)>.1,'Missing/collapsed UVs'
 if 'normals' in p:assert len(p['normals'])==len(v) and any(abs(n)>.5 for n in p['normals'])
 return len(tri)//3
rod=json.loads((src/'Level4Rod.json').read_text());assert part(rod)==1340
reel=json.loads((src/'Level4Reel.json').read_text());assert sum(part(p) for p in reel['parts'])==1772
assert set(p['name'] for p in reel['parts'])==set(['ReelFootAndBody','Rotor','ReelHousing','Spool','Handle'])
dock=json.loads((src/'Dock.json').read_text());assert set(p['name'] for p in dock['parts'])=={'Deck','EdgeTrim','Post'}
assert sum(part(p) for p in dock['parts'])==156
for name in ['Albacore','GreaterAmberjack']:
 data=(root/'Assets/_Game/Reef/Source'/(name+'.fbx')).read_bytes();assert data.startswith(b'Kaydara FBX Binary')
 assert b'Bone.004' in data and b'AnimationCurve' in data and b'UV' in data
 data=(root/'Assets/_Game/Reef/Source'/(name+'Texture.jpg')).read_bytes();assert data[:3]==b'\xff\xd8\xff'
print('PASS: 14 species, 27 exact 100% chance rows, 42 weight ranges; rod/reel/dock topology and UVs; 2 animated FBXs/textures.')
# Exact triangle clipping verifies the authored trim does not cover the deck top.
def cross(a,b,c):return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
def area(p):return abs(sum(p[i][0]*p[(i+1)%len(p)][1]-p[(i+1)%len(p)][0]*p[i][1] for i in range(len(p))))*.5 if p else 0

def top_triangles(part):
 v=list(zip(part['vertices'][::3],part['vertices'][1::3],part['vertices'][2::3]));result=[]
 for i in range(0,len(part['triangles']),3):
  p=[v[j] for j in part['triangles'][i:i+3]];q=[(x,z) for x,y,z in p]
  if cross(*q)<-1e-6:result.append(list(reversed(q))) # upward Unity-Y normal
 return result

def intersection(subject,clip):
 out=subject
 for i,a in enumerate(clip):
  b=clip[(i+1)%len(clip)];inp=out;out=[]
  if not inp:break
  previous=inp[-1];dp=cross(a,b,previous)
  for current in inp:
   dc=cross(a,b,current)
   if (dc>=0)!=(dp>=0):
    t=dp/(dp-dc);out.append((previous[0]+t*(current[0]-previous[0]),previous[1]+t*(current[1]-previous[1])))
   if dc>=0:out.append(current)
   previous=current;dp=dc
 return out
surfaces={p['name']:top_triangles(p) for p in dock['parts']}
overlap=sum(area(intersection(a,b)) for a in surfaces['Deck'] for b in surfaces['EdgeTrim'])
assert overlap<1e-4,('Overlapping dock top faces',overlap)
print('PASS: deck/trim projected top-face overlap = %.9f square metres.'%overlap)
