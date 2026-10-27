"""Enforce mathematically mirrored deformation of right fin from left fin in held struggle.
Run from repo root with Blender 5.2 while loading either Stingray .blend.
Only the action named "Stingray Struggling" is modified; all other animations,
mesh, UVs, materials, bones, rest pose and fish transforms remain untouched.
"""
import bpy, os
from mathutils import Matrix
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
assert len(arm.data.bones) == 29, "Unexpected stingray bone count"
assert all('Fin_%02d.%s' % (i,s) in arm.pose.bones for i in range(1,11) for s in ('L','R'))
action = bpy.data.actions.get('Stingray Struggling')
assert action, "Missing held-struggle action"
if arm.animation_data is None: arm.animation_data_create()
arm.animation_data.action = action
arm.data.pose_position = 'POSE'
# The authored fin roots are separated in armature-space Z.
lroot=arm.data.bones['Fin_01.L']
rroot=arm.data.bones['Fin_01.R']
assert abs(lroot.head_local.z-rroot.head_local.z)>.4, "Refuse unknown symmetry axis"
mirror = Matrix.Diagonal((1.0,1.0,-1.0,1.0))
mirror[2][3] = lroot.head_local.z + rroot.head_local.z
frames=range(int(action.frame_range[0]),int(action.frame_range[1])+1)
assert 1<=min(frames) and max(frames)<=120 and len(frames)>=20
right_bones=[arm.pose.bones['Fin_%02d.R'%i] for i in range(1,11)]
left_bones=[arm.pose.bones['Fin_%02d.L'%i] for i in range(1,11)]
rest_right=[bone.bone.matrix_local.copy() for bone in right_bones]
rest_left_inverse=[bone.bone.matrix_local.inverted() for bone in left_bones]
for frame in frames:
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()
    # Sample left side before changing any right-side pose.
    target=[mirror @ (left.matrix.copy() @ inv) @ mirror @ rest
            for left,inv,rest in zip(left_bones,rest_left_inverse,rest_right)]
    # Each wing is a chain. Set and key its WORLD/armature-space pose
    # from root to tip to account for the right wing's actual parent axes.
    for bone,m in zip(right_bones,target):
        bone.rotation_mode='QUATERNION'
        bone.matrix=m
        bpy.context.view_layer.update()
        for channel in ('location','rotation_quaternion','scale'):
            bone.keyframe_insert(data_path=channel, frame=frame, group=bone.name)
max_error=0.0
for frame in frames:
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()
    for left,right,li,rest in zip(left_bones,right_bones,rest_left_inverse,rest_right):
        expected=mirror @ (left.matrix.copy() @ li) @ mirror @ rest
        err=max(abs(expected[i][j]-right.matrix[i][j]) for i in range(4) for j in range(4))
        max_error=max(max_error,err)
assert max_error<.003, "Right wing failed mirror verification: %f"%max_error
scene=bpy.context.scene
scene.frame_set(min(frames))
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
print('PASS MIRRORED HELD WINGS',os.path.basename(bpy.data.filepath),'frames',len(frames),'bones',len(right_bones),'max_error',max_error)
