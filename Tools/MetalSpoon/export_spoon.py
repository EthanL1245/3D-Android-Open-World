"""Blender 5.2: blender -b 'Metal Spoon.blend' --python export_spoon.py -- OUTPUT_JSON.
Bake the user's UVs, split normals and two independent cyclic actions; no remodelling.
Unity consumes the JSON without needing Blender installed.
"""
import bpy, json, sys
from pathlib import Path
from mathutils import Matrix, Vector
scene=bpy.context.scene
scene.frame_set(1)
body=bpy.data.objects['BézierCurve.001']
hook=bpy.data.objects['BézierCurve.002']
# Canonical Unity frame: non-hook end +Z, face normal +Y. Reflect handedness.
C=Matrix(((-1,0,0),(0,0,1),(0,-1,0))) @ body.matrix_world.to_3x3().normalized().inverted()
scale=0.032892715  # Same world-size scale as the outgoing Deep Flash model.
C=C*scale
origin=body.matrix_world.translation.copy()
# The tiny ring at mesh -Y, opposite the hook. Use its centre, not the AABB tip.
eye=[v.co for v in body.data.vertices if v.co.y < 0.03]
assert len(eye)>=8
line_local=sum(eye,Vector())/len(eye)
line=C@(body.matrix_world@line_local-origin)
parts=[]
for obj,name in [(body,'SpoonBody'),(hook,'SpoonHook')]:
 scene.frame_set(1)
 assert not obj.modifiers and obj.data.uv_layers.active
 mesh=obj.data;mesh.calc_loop_triangles();rest=obj.matrix_world.copy();pivot=rest.translation.copy()
 normal_matrix=(C@rest.to_3x3()).inverted().transposed()
 part=dict(name=name,vertices=[],normals=[],uv=[],triangles=[],frames=[])
 for tri in mesh.loop_triangles:
  start=len(part['vertices'])
  for li in tri.loops:
   co=C@(rest@mesh.vertices[mesh.loops[li].vertex_index].co-pivot)
   normal=(normal_matrix@mesh.corner_normals[li].vector).normalized()
   uv=mesh.uv_layers.active.data[li].uv
   part['vertices'].append(dict(zip('xyz',co)))
   part['normals'].append(dict(zip('xyz',normal)))
   part['uv'].append(dict(zip('xy',uv)))
  # Reflection reverses winding; use Unity's clockwise front faces.
  part['triangles'].extend([start,start+2,start+1])
 lo,hi=obj.animation_data.action.frame_range
 previous=None
 for i in range(73):
  timeline=1+9*i/72
  frame=lo+(timeline-lo)%(hi-lo)  # Retain the hook's one-frame phase offset.
  scene.frame_set(int(frame),subframe=frame-int(frame))
  current=obj.matrix_world.copy()
  delta=C@current.to_3x3()@rest.to_3x3().inverted()@C.inverted()
  quat=delta.to_quaternion().normalized()
  if previous and quat.dot(previous)<0:quat.negate()
  previous=quat.copy()
  pos=C@(current.translation-origin)
  part['frames'].append(dict(time=i/192,position=dict(zip('xyz',pos)),rotation=dict(zip('xyzw',(quat.x,quat.y,quat.z,quat.w)))))
 parts.append(part)
out=Path(sys.argv[sys.argv.index('--')+1]);out.parent.mkdir(parents=True,exist_ok=True)
data=dict(version=1,duration=9/24,fps=192,lineAttach=dict(zip('xyz',line)),parts=parts)
out.write_text(json.dumps(data,separators=(',',':')))
print('EXPORT',out,'triangles',sum(len(p['triangles'])//3 for p in parts),'line',list(line),'ring vertices',len(eye))
for p in parts:
 assert max(sum((f['rotation'][k]-p['frames'][0]['rotation'][k])**2 for k in 'xyzw') for f in p['frames'])>.01
 assert sum((p['frames'][0]['rotation'][k]-p['frames'][-1]['rotation'][k])**2 for k in 'xyzw')<1e-8
 print(p['name'],len(p['vertices']),'vertices',len(p['frames']),'samples; motion and closed loop verified')
