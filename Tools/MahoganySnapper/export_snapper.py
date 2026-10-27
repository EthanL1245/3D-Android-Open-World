"""blender --background "Mahogany Snapper.blend" --python Tools/MahoganySnapper/export_snapper.py -- "<output MahoganySnapper.fbx>" """
import bpy,sys,os
armatures=[o for o in bpy.context.scene.objects if o.type=="ARMATURE"]
meshes=[o for o in bpy.context.scene.objects if o.type=="MESH"]
if not armatures or not meshes: raise RuntimeError("Fish meshes and rig required")
arm=armatures[0]
if arm.animation_data is None or arm.animation_data.action is None:
    raise RuntimeError("Fish has no authored swimming animation")
for obj in bpy.context.scene.objects: obj.select_set(False)
for obj in [arm]+meshes:
    obj.hide_set(False);obj.hide_viewport=False;obj.select_set(True)
    if obj.type=="MESH" and not obj.data.uv_layers:
        raise RuntimeError("Fish mesh UVs required: "+obj.name)
bpy.context.view_layer.objects.active=arm
out=os.path.abspath(sys.argv[sys.argv.index("--")+1])
os.makedirs(os.path.dirname(out),exist_ok=True)
bpy.ops.export_scene.fbx(filepath=out,use_selection=True,object_types={"ARMATURE","MESH"},
    apply_unit_scale=True,apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",axis_up="Y",add_leaf_bones=False,bake_anim=True,
    bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=False,bake_anim_simplify_factor=0,path_mode="AUTO")
print("EXPORTED",out)
