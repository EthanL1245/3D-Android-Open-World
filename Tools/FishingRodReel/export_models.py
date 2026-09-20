"""Run with Blender: blender -b --python export_models.py -- SOURCE_DIR OUTPUT_JSON.
SOURCE_DIR contains Rod.blend and Reel.blend (original user files).
Exports evaluated geometry/UVs and the three original reel actions into Unity space.
No Blender dependency is needed by Unity; use the committed JSON and textures.
"""
import bpy, json, sys, math
from pathlib import Path
from mathutils import Matrix, Vector
source, output = map(Path, sys.argv[sys.argv.index('--') + 1:])
parts = []
steps = 120
names = {'Plane': 'ReelFootAndBody', 'Cylinder': 'Rotor',
         'Cylinder.001': 'ReelHousing', 'Cylinder.002': 'Spool', 'Cylinder.003': 'Handle'}
def numbers(v): return [round(float(x), 7) for x in v]
for kind in ('Rod', 'Reel'):
    bpy.ops.wm.open_mainfile(filepath=str(source / (kind + '.blend')))
    scene = bpy.context.scene
    scene.frame_set(1)
    basis = (Matrix(((0,1,0),(-1,0,0),(0,0,1))) * .01 if kind == 'Rod'
             else Matrix(((1,0,0),(0,-1,0),(0,0,-1))) * .04)
    offset = Vector((0,.01,0)) if kind == 'Rod' else Vector((0,0,0))
    for obj in list(scene.objects):
        if obj.type != 'MESH': continue
        scene.frame_set(1)
        deps = bpy.context.evaluated_depsgraph_get()
        evaluated = obj.evaluated_get(deps)
        mesh = evaluated.to_mesh()
        mesh.calc_loop_triangles()
        rest = obj.matrix_world.copy()
        linear = basis @ rest.to_3x3()
        normals = linear.inverted().transposed()
        part = dict(name='RodBlank' if kind=='Rod' else names[obj.name], group=kind,
                    position=numbers(basis @ rest.translation + offset), vertices=[], normals=[], uv=[], triangles=[], frames=[])
        # Split corners so authored UV seams and split normals are retained.
        for tri in mesh.loop_triangles:
            indices=[]
            for li in tri.loops:
                indices.append(len(part['vertices'])//3)
                part['vertices'] += numbers(linear @ mesh.vertices[mesh.loops[li].vertex_index].co)
                part['normals'] += numbers((normals @ mesh.corner_normals[li].vector).normalized())
                part['uv'] += numbers(mesh.uv_layers.active.data[li].uv)
            part['triangles'] += indices if linear.determinant()>0 else indices[::-1]
        evaluated.to_mesh_clear()
        action = obj.animation_data.action if obj.animation_data else None
        if action:
            lo, hi = action.frame_range
            previous=None
            for i in range(steps+1):
                frame=lo+(hi-lo)*i/steps
                scene.frame_set(int(frame), subframe=frame-int(frame))
                current=obj.matrix_world.copy()
                delta=basis @ current.to_3x3() @ rest.to_3x3().inverted() @ basis.inverted()
                q=delta.to_quaternion().normalized()
                if previous and q.dot(previous)<0:q.negate()
                previous=q.copy()
                part['frames'].append(dict(time=round(1.25*i/steps,7),
                    position=numbers(basis @ current.translation + offset),
                    rotation=numbers((q.x,q.y,q.z,q.w))))
            print('BAKED', part['name'], action.name, [lo,hi], 'into',len(part['frames']),'samples')
        parts.append(part)
    scene.frame_set(1)
data=dict(version=1,duration=1.25,tip=[0,1.80232,0],reelMount=[0,.17,.1035],parts=parts)
output.parent.mkdir(parents=True,exist_ok=True)
output.write_text(json.dumps(data,separators=(',',':')))
print('EXPORTED',output,len(parts),'parts',sum(len(p['triangles'])//3 for p in parts),'triangles')
