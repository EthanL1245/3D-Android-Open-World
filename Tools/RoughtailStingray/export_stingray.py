"""Blender --background source.blend --python this.py -- output.fbx.
Preserves both authored motions while attaching wing roots to the shared spine.
"""
import bpy, sys, os, math, json
from mathutils import Matrix
scene=bpy.context.scene
arm=next(o for o in scene.objects if o.type=='ARMATURE')
meshes=[o for o in scene.objects if o.type=='MESH']
assert len(meshes)==1 and meshes[0].data.uv_layers
source_names={'Swim':'Stingray Swimming','HeldStruggle':'Stingray Struggling'}
# Capture original armature-space matrices BEFORE changing any hierarchy.
samples={}
for name,source in source_names.items():
    arm.animation_data.action=bpy.data.actions[source]
    frames=range(int(arm.animation_data.action.frame_range[0]),int(arm.animation_data.action.frame_range[1])+1)
    samples[name]={}
    for f in frames:
        scene.frame_set(f)
        samples[name][f]={b.name:b.matrix.copy() for b in arm.pose.bones}
arm.animation_data.action=None
arm.data.pose_position='REST';scene.frame_set(1);bpy.context.view_layer.update()
body=meshes[0];dg=bpy.context.evaluated_depsgraph_get();evaluated=body.evaluated_get(dg);mesh=evaluated.to_mesh();side=[[],[]]
for loop,uv in zip(mesh.loops,mesh.uv_layers.active.data):
    p=body.matrix_world@mesh.vertices[loop.vertex_index].co
    side[int(uv.uv.x<.5)].append(p.x)
dark=sum(side[1])/len(side[1]);pale=sum(side[0])/len(side[0]);evaluated.to_mesh_clear()
roll=-math.pi/2 if dark>pale else math.pi/2
# Ray wings have independent roots in the source. Parent them to the nearest
# spine bone so shared aquarium trail bends carry the wings with the body.
# Re-bake all local transforms below to retain the original animation exactly.
bpy.context.view_layer.objects.active=arm;arm.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
spine=[arm.data.edit_bones['Bone.%03d'%i] for i in range(10,19)]
for b in arm.data.edit_bones:
    if b.name.startswith('Fin_') and b.parent is None:
        b.parent=min(spine[:4],key=lambda c:(b.head-c.head).length)
        b.use_connect=False
bpy.ops.object.mode_set(mode='OBJECT')
rename={'Bone.%03d'%(10+i):('Bone' if i==0 else 'Bone.%03d'%i) for i in range(9)}
for old,new in rename.items():arm.data.bones[old].name=new
arm.data.pose_position='POSE'
def depth(b):return 0 if b.parent is None else 1+depth(b.parent)
bones=sorted(arm.pose.bones,key=depth)
# Remove old actions after sampling so FBX contains only the two final takes.
for action in list(bpy.data.actions):bpy.data.actions.remove(action)
max_error=0
for name,frames in samples.items():
    action=bpy.data.actions.new(name);arm.animation_data.action=action
    for f,poses in frames.items():
        scene.frame_set(f)
        targets={rename.get(n,n):m for n,m in poses.items()}
        for b in bones:
            b.rotation_mode='QUATERNION';b.matrix=targets[b.name]
            bpy.context.view_layer.update()
            b.keyframe_insert('location',frame=f)
            b.keyframe_insert('rotation_quaternion',frame=f)
            b.keyframe_insert('scale',frame=f)
        bpy.context.view_layer.update()
        max_error=max(max_error,max(abs(b.matrix[i][j]-targets[b.name][i][j]) for b in bones for i in range(4) for j in range(4)))
    action.use_fake_user=True
assert max_error<.0001,max_error
arm.matrix_world=Matrix.Rotation(roll,4,'Y')@arm.matrix_world
arm.animation_data.action=bpy.data.actions['Swim']
scene.frame_start=1;scene.frame_end=30;scene.frame_set(1)
for o in scene.objects:o.select_set(False)
for o in meshes+[arm]:o.hide_set(False);o.hide_viewport=False;o.select_set(True)
out=os.path.abspath(sys.argv[sys.argv.index('--')+1]);os.makedirs(os.path.dirname(out),exist_ok=True)
bpy.ops.export_scene.fbx(filepath=out,use_selection=True,object_types={'ARMATURE','MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=True,bake_anim_force_startend_keying=True,bake_anim_simplify_factor=0,path_mode='AUTO')
print('EXPORT_CHECK',json.dumps({'roll_degrees':math.degrees(roll),'max_pose_matrix_error':max_error,'bones':len(bones),'actions':list(source_names)}))
