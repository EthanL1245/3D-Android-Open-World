"""Run with Blender --background --python verify_export.py -- path/to/BlacktipShark.fbx."""
import bpy,sys
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=sys.argv[sys.argv.index('--')+1])
arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
assert set(arm.data.bones.keys())=={'Bone','Bone.001','Bone.002','Bone.003','Bone.004'}
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
assert len(meshes)==2
assert all(len(o.data.uv_layers)>0 for o in meshes)
skinned=[o for o in meshes if any(m.type=='ARMATURE' for m in o.modifiers)]
assert len(skinned)==1 and all(v.groups for v in skinned[0].data.vertices)
rigid=next(o for o in meshes if o not in skinned)
assert rigid.parent==arm and rigid.parent_type=='BONE' and rigid.parent_bone=='Bone'
action=arm.animation_data.action
start,end=map(int,action.frame_range)
assert end-start==19
poses=[]
for frame in range(start,end+1):
 bpy.context.scene.frame_set(frame)
 poses.append((arm.matrix_world@arm.pose.bones['Bone.004'].head).copy())
assert max((p-poses[0]).length for p in poses)>1,'Exported tail animation is static'
assert (poses[-1]-poses[0]).length<.01,'Swim loop does not close'
print('PASS: five-bone rig, both UV meshes, body skin weights, head-attached detail, animated tail and closed 20-frame loop.')
