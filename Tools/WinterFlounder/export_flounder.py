"""Run with Blender --background <source.blend> --python this.py -- <output.fbx>."""
import bpy, sys, os, math
from mathutils import Matrix
scene=bpy.context.scene
arm=next(o for o in scene.objects if o.type=='ARMATURE')
meshes=[o for o in scene.objects if o.type=='MESH']
scene.frame_set(1)
# The supplied UV atlas has the brown/eyed side in its upper half.
# Find which world-X side it occupies before rolling the whole rig flat.
body=max(meshes,key=lambda o:len(o.data.vertices))
dg=bpy.context.evaluated_depsgraph_get()
evalmesh=body.evaluated_get(dg).to_mesh()
side=[[],[]]
for loop,uv in zip(evalmesh.loops,evalmesh.uv_layers.active.data):
    p=body.matrix_world@evalmesh.vertices[loop.vertex_index].co
    side[int(uv.uv.y>.5)].append(p.x)
dark=sum(side[1])/len(side[1]); pale=sum(side[0])/len(side[0])
roll=-math.pi/2 if dark>pale else math.pi/2
print('DORSAL_X',dark,'VENTRAL_X',pale,'ROLL',math.degrees(roll))
body.evaluated_get(dg).to_mesh_clear()
arm.matrix_world=Matrix.Rotation(roll,4,'Y')@arm.matrix_world
# Remove unused modifier slots; retain the actual authored skinning.
for obj in meshes:
    for mod in list(obj.modifiers):
        if mod.type=='ARMATURE' and mod.object is None: obj.modifiers.remove(mod)
    assert obj.data.uv_layers
# Preserve the extra terminal tail bone and all six authored bone channels.
scene.frame_start=1; scene.frame_end=20; scene.frame_set(1)
for obj in scene.objects: obj.select_set(False)
for obj in meshes+[arm]:
    obj.hide_set(False); obj.hide_viewport=False; obj.select_set(True)
bpy.context.view_layer.objects.active=arm
out=os.path.abspath(sys.argv[sys.argv.index('--')+1]);os.makedirs(os.path.dirname(out),exist_ok=True)
bpy.ops.export_scene.fbx(filepath=out,use_selection=True,object_types={'ARMATURE','MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_force_startend_keying=True,bake_anim_simplify_factor=0,path_mode='AUTO')
print('EXPORTED',out)
