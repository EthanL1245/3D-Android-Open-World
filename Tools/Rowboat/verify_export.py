import bpy,json,sys,math
from pathlib import Path
from mathutils import Vector,Matrix,Quaternion
src,out=sys.argv[sys.argv.index('--')+1:]
bpy.ops.wm.open_mainfile(filepath=src)
if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
d=json.loads(Path(out).read_text());C=Matrix(((0,-1,0),(0,0,1),(1,0,0)));center=Vector((-.2761713,1.0005022,2.38198624))
def v(d):return Vector((d['x'],d['y'],d['z']))
def q(d):return Quaternion((d['w'],d['x'],d['y'],d['z']))
error=0
for part in d['parts']:
 obj=bpy.data.objects[part['name']];obj.data.calc_loop_triangles()
 # the first exported vertex corresponds to the first triangle's first loop.
 vert=obj.data.vertices[obj.data.loops[obj.data.loop_triangles[0].loops[0]].vertex_index].co
 for i in range(144):
  f=1+(i+.5)/4;bpy.context.scene.frame_set(int(f),subframe=f-int(f))
  expected=C@(obj.matrix_world@vert-center)
  actual=q(part['rotations'][i]).slerp(q(part['rotations'][i+1]),.5)@v(part['vertices'][0])+v(part['positions'][i]).lerp(v(part['positions'][i+1]),.5)
  error=max(error,(actual-expected).length)
 assert (v(part['positions'][0])-v(part['positions'][-1])).length<1e-4
 assert abs(q(part['rotations'][0]).dot(q(part['rotations'][-1])))>.99999
print('VERIFY: seamless endpoints; interpolation maximum first-vertex error',error,'metres')
assert error<.002
