"""Blender regression check: compare exported reel transforms to original actions.
blender -b --python verify_export.py -- SOURCE_DIR OUTPUT_JSON
"""
import bpy,json,sys,math
from pathlib import Path
from mathutils import Matrix,Vector,Quaternion
source, exported=map(Path,sys.argv[sys.argv.index('--')+1:])
data=json.loads(exported.read_text())
assert len(data['parts'])==6
for p in data['parts']:
    assert len(p['vertices'])==len(p['normals'])
    assert len(p['uv'])==len(p['vertices'])//3*2
    assert min(p['triangles'])>=0 and max(p['triangles'])<len(p['vertices'])//3
    assert all(math.isfinite(v) for field in ('vertices','normals','uv','position') for v in p[field])
bpy.ops.wm.open_mainfile(filepath=str(source/'Reel.blend'))
lookup={'Rotor':'Cylinder','Spool':'Cylinder.002','Handle':'Cylinder.003'}
basis=Matrix(((1,0,0),(0,-1,0),(0,0,-1)))*.04
max_error=0
for p in data['parts']:
    if not p['frames']:continue
    obj=bpy.data.objects[lookup[p['name']]]
    scene=bpy.context.scene
    scene.frame_set(1)
    rest=obj.matrix_world.copy()
    action=obj.animation_data.action
    lo,hi=action.frame_range
    # Test all original mesh vertices, not just pivot/rotation keys.
    for frame in p['frames']:
        time=lo+(hi-lo)*frame['time']/data['duration']
        scene.frame_set(int(time),subframe=time-int(time))
        x,y,z,w=frame['rotation'];rotation=Quaternion((w,x,y,z))
        position=Vector(frame['position'])
        for vertex in obj.data.vertices:
            authored=basis@(obj.matrix_world@vertex.co)
            baked=rotation@(basis@(rest.to_3x3()@vertex.co))+position
            max_error=max(max_error,(authored-baked).length)
    a,b=p['frames'][0],p['frames'][-1]
    assert (Vector(a['position'])-Vector(b['position'])).length<1e-5
    assert abs(abs(sum(x*y for x,y in zip(a['rotation'],b['rotation'])))-1)<1e-5
    print('PASS',p['name'],len(p['frames']),'authored samples, closed loop')
assert max_error<1e-5,max_error
print('PASS: six meshes; UV/index checks; maximum authored vertex error',max_error,'metres')
