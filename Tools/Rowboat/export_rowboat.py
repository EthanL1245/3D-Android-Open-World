import bpy,json,sys,math
from pathlib import Path
from mathutils import Matrix,Vector
src,out=sys.argv[sys.argv.index('--')+1:]
bpy.ops.wm.open_mainfile(filepath=src)
if bpy.context.object and bpy.context.object.mode!='OBJECT': bpy.ops.object.mode_set(mode='OBJECT')
s=bpy.context.scene;s.frame_set(1)
C=Matrix(((0,-1,0),(0,0,1),(1,0,0)))
center=Vector((-.2761713,1.0005022,2.38198624))
def v(v):return dict(zip(('x','y','z'),map(float,v)))
def q(q):return {'x':q.x,'y':q.y,'z':q.z,'w':q.w}
parts=[];records=[]
for o in sorted(bpy.data.objects,key=lambda o:o.name):
 if o.type!='MESH':continue
 assert len(o.modifiers)==0, o.name
 m=o.data;m.calc_loop_triangles()
 M=o.matrix_world.copy();pivot=M.translation.copy()
 normals=M.to_3x3().inverted().transposed()
 verts=[];uv=[];ns=[];tris=[]
 for t in m.loop_triangles:
  start=len(verts)
  for li in t.loops:
   loop=m.loops[li];verts.append(v(C@(M@m.vertices[loop.vertex_index].co-pivot)))
   ns.append(v((C@(normals@m.corner_normals[li].vector)).normalized()))
   u=m.uv_layers.active.data[li].uv;uv.append({'x':u.x,'y':u.y})
  # Unity left-handed winding, including reflected oar parent.
  tris.extend([start,start+2,start+1] if (C@M.to_3x3()).determinant()<0 else [start,start+1,start+2])
 d={'name':o.name,'moving':o.parent is not None,'vertices':verts,'normals':ns,'uv':uv,'triangles':tris,'positions':[],'rotations':[]}
 parts.append(d);records.append((o,M,pivot,d))
for i in range(145):
 f=1+i/4;s.frame_set(int(f),subframe=f-int(f))
 for o,M,pivot,d in records:
  delta=o.matrix_world@M.inverted();R=C@delta.to_3x3()@C.inverted()
  assert max(abs(x-1) for x in R.to_scale())<1e-4
  d['positions'].append(v(C@(delta@pivot-center)))
  d['rotations'].append(q(R.to_quaternion().normalized()))
for d in parts:
 motion=max(sum((a[k]-d['rotations'][0][k])**2 for k in ('x','y','z','w')) for a in d['rotations'])
 assert (motion>0.01)==d['moving'],(d['name'],motion)
Path(out).parent.mkdir(parents=True,exist_ok=True)
Path(out).write_text(json.dumps({'fps':96,'duration':1.5,'parts':parts},separators=(',',':')))
print('EXPORTED',out,len(parts),'parts; all moving parts verified; frames1-37/24fps')
