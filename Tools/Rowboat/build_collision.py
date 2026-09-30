# Run from repository root. Requires numpy and scipy; not needed by Unity.
import json,numpy as np
from collections import defaultdict
p=json.load(open('Assets/_Game/Boats/Rowboat/Source/Tandem.json'))['parts'][0]
v=np.array([[x['x'],x['y'],x['z']] for x in p['vertices']])+list(p['positions'][0].values())
t=np.array(p['triangles']).reshape(-1,3);ids={};vs=[];ti=[]
for tri in t:
 row=[]
 for i in tri:
  k=tuple(np.round(v[i],5))
  if k not in ids:ids[k]=len(vs);vs.append(v[i])
  row.append(ids[k])
 ti.append(row)
v=np.array(vs);t=np.array(ti);adj=defaultdict(set)
for a,b,c in t:
 adj[a].update([b,c]);adj[b].update([a,c]);adj[c].update([a,b])
seen=set();groups=[]
for a in adj:
 if a in seen:continue
 todo=[a];group=[];seen.add(a)
 while todo:
  x=todo.pop();group.append(x)
  for b in adj[x]-seen:seen.add(b);todo.append(b)
 groups.append(group)
from scipy.spatial import ConvexHull
from pathlib import Path
pieces=[]
def add(points,name):
 points=np.unique(np.round(points,6),axis=0)
 if len(points)<4 or np.linalg.matrix_rank(points-points[0])<3:return
 h=ConvexHull(points); faces=[]
 for face,eq in zip(h.simplices,h.equations):
  a,b,c=face
  if np.dot(np.cross(points[b]-points[a],points[c]-points[a]),eq[:3])<0:b,c=c,b
  faces.extend([int(a),int(b),int(c)])
 pieces.append(dict(name=name,vertices=[dict(zip('xyz',map(float,x))) for x in points],triangles=faces))
def clip(poly,axis,limit,greater):
 out=[]
 for a,b in zip(poly,poly[1:]+poly[:1]):
  ai=(a[axis]>=limit-1e-8) if greater else(a[axis]<=limit+1e-8)
  bi=(b[axis]>=limit-1e-8) if greater else(b[axis]<=limit+1e-8)
  if ai:out.append(a)
  if ai!=bi:out.append(a+(b-a)*((limit-a[axis])/(b[axis]-a[axis])))
 return out
hulltris=[tri for tri in t if all(i in groups[0] for i in tri)]
cuts=[-3.93,-3.75,-3,-2,-1,0,1,2,3,3.75,3.93]
for j,(lo,hi) in enumerate(zip(cuts,cuts[1:])):
 for region in ['Floor','Port','Starboard']:
  points=[]
  for tri in hulltris:
   poly=list(v[tri]);planes=[(2,lo,True),(2,hi,False),(1,-.20,region!='Floor')]
   if region!='Floor':planes.append((0,0,region=='Starboard'))
   for axis,limit,greater in planes:
    if poly:poly=clip(poly,axis,limit,greater)
   points.extend(poly)
  add(points,region+str(j))
for i,g in enumerate(groups[1:]):add(v[g],'Bench'+str(i) if i<2 else 'OarMount'+str(i-2))
p=Path('Assets/_Game/Boats/Rowboat/Source/Collision.json');p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(dict(pieces=pieces),separators=(',',':')))
print(len(pieces),'convex pieces; maximum triangles',max(len(p['triangles'])//3 for p in pieces))
# Interior at both rower feet remains empty; seats/bottom/underbody are solid.
def inside(pt,p):return np.max(ConvexHull(np.array([list(a.values()) for a in p['vertices']])).equations@np.r_[pt,1])<1e-5
for pt in [(0,0,.97),(0,0,-1.42)]:assert not any(inside(pt,p) for p in pieces),pt
for pt in [(0,-.30,0),(0,.23,.58),(0,.23,-1.8)]:assert any(inside(pt,p) for p in pieces),pt
print('floor, benches and open rowing-space checks passed')
