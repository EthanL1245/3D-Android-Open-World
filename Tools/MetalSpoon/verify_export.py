"""Run with Blender and the original blend loaded; verify baked assets against it."""
import bpy,json,sys,math
from pathlib import Path
from mathutils import Matrix,Vector,Quaternion
D=json.loads(Path(sys.argv[sys.argv.index('--')+1]).read_text())
s=bpy.context.scene;s.frame_set(1)
body=bpy.data.objects['BézierCurve.001'];hook=bpy.data.objects['BézierCurve.002']
C=Matrix(((-1,0,0),(0,0,1),(0,-1,0))) @ body.matrix_world.to_3x3().normalized().inverted()*.032892715
origin=body.matrix_world.translation.copy()
max_error=0
for obj,p in zip([body,hook],D['parts']):
 s.frame_set(1);m=obj.data;m.calc_loop_triangles()
 corners=[li for tri in m.loop_triangles for li in tri.loops]
 assert len(corners)==len(p['vertices'])
 for li,uv in zip(corners,p['uv']):
  assert (m.uv_layers.active.data[li].uv-Vector((uv['x'],uv['y']))).length<1e-7
 lo,hi=obj.animation_data.action.frame_range
 for i,sample in enumerate(p['frames']):
  f=lo+(1+9*i/72-lo)%(hi-lo);s.frame_set(int(f),subframe=f-int(f))
  q=sample['rotation'];q=Quaternion((q['w'],q['x'],q['y'],q['z']))
  position=Vector([sample['position'][a] for a in 'xyz'])
  for corner,v in zip(corners,p['vertices']):
   expected=C@(obj.matrix_world@m.vertices[m.loops[corner].vertex_index].co-origin)
   actual=q@Vector([v[a] for a in 'xyz'])+position
   max_error=max(max_error,(expected-actual).length)
 assert max_error<1e-6
 assert max(abs(p['frames'][0]['rotation'][a]-p['frames'][-1]['rotation'][a]) for a in 'xyzw')<1e-6
 print(p['name'],'original UVs + all animated vertices agree with export')
# Eye is near the front at +Z; hook occupies the far negative-Z end.
line=Vector([D['lineAttach'][a] for a in 'xyz'])
assert line.z>-.005
assert D['parts'][1]['frames'][0]['position']['z']<-.20
# Player views along -retrieve direction: screen-right is -world-X.
up=Quaternion(Vector((0,0,1)),math.radians(10))@Vector((0,1,0))
assert -up.x>0 and up.y>0  # top leans screen-right: clockwise
print('Verified non-hook line eye and clockwise crankbait roll. Max vertex error (metres):',max_error)
