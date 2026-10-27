"""Run from repository root: blender --background --python Tools/RoughtailStingray/verify_export.py"""
import bpy, os, json
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.abspath('Assets/_Game/Reef/Source/RoughtailStingray.fbx'))
scene=bpy.context.scene
arm=next(o for o in scene.objects if o.type=='ARMATURE')
body=next(o for o in scene.objects if o.type=='MESH')
assert len(arm.data.bones)==29 and body.data.uv_layers
assert len(bpy.data.actions)==2
report={'bones':len(arm.data.bones),'clips':{}}
poses={}
for name in ['Swim','HeldStruggle']:
    action=next(a for a in bpy.data.actions if a.name.endswith('|'+name))
    arm.animation_data.action=action
    arm.animation_data.action_slot=action.slots[0]
    start,end=map(int,action.frame_range)
    samples=[]
    for frame in range(start,end+1):
        scene.frame_set(frame)
        samples.append([v for b in arm.pose.bones for row in b.matrix for v in row])
    seam=max(abs(a-b) for a,b in zip(samples[0],samples[-1]))
    motion=max(abs(a-b) for sample in samples for a,b in zip(samples[0],sample))
    assert seam<.0001 and motion>.01,(name,seam,motion)
    poses[name]=samples
    report['clips'][name]={'frames':[start,end],'loop_error':seam,'motion':motion}
assert max(abs(a-b) for swim,held in zip(poses['Swim'],poses['HeldStruggle']) for a,b in zip(swim,held))>.01
arm.animation_data.action=next(a for a in bpy.data.actions if a.name.endswith('|Swim'))
arm.animation_data.action_slot=arm.animation_data.action.slots[0]
scene.frame_set(2)
evaluated=body.evaluated_get(bpy.context.evaluated_depsgraph_get())
mesh=evaluated.to_mesh();sides=[[],[]]
for loop,uv in zip(mesh.loops,mesh.uv_layers.active.data):
    sides[int(uv.uv.x<.5)].append((body.matrix_world@mesh.vertices[loop.vertex_index].co).z)
assert sum(sides[1])/len(sides[1])>sum(sides[0])/len(sides[0]),'Brown side must face up'
report['brown_side_up']=True
mesh.calc_loop_triangles();report['triangles']=len(mesh.loop_triangles)
evaluated.to_mesh_clear()
print('PASS',json.dumps(report,indent=2))
