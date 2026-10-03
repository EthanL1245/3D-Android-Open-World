import bpy, os, sys, math, traceback
args=sys.argv
out_path=args[args.index('--')+1]

armatures=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
if not armatures or not meshes:
    raise RuntimeError('Expected an armature and at least one mesh in the fish Blend.')

# Use the armature actually bound to the fish mesh whenever possible. This is
# more reliable than simply taking the first armature in files containing helpers.
arm=None
for mesh in meshes:
    for modifier in mesh.modifiers:
        if modifier.type=='ARMATURE' and modifier.object is not None:
            arm=modifier.object
            break
    if arm is not None:
        break
if arm is None:
    arm=armatures[0]

if arm.animation_data is None:
    arm.animation_data_create()

action=arm.animation_data.action
actions=list(bpy.data.actions)

# Blender 4.4+/5.x can store layered/slotted Actions. Action.fcurves is not
# available for every Action type anymore, so never inspect fcurves here.
# Prefer the action already assigned to the fish armature, then a clearly named
# swim/armature action, then the longest authored action by frame range.
if action is None:
    for candidate in actions:
        lower=candidate.name.lower()
        if 'swim' in lower or 'armatureaction' in lower or lower=='armature':
            action=candidate
            break
if action is None and actions:
    def span(candidate):
        try:
            r=candidate.frame_range
            return float(r[1]-r[0])
        except Exception:
            return 0.0
    action=max(actions,key=span)
if action is None:
    raise RuntimeError('Fish Blend has no authored animation action.')

arm.animation_data.action=action
# Normalize authored .R names for the shared five-bone presentation contract.
# Blender updates vertex groups and action paths along with bone names.
for bone in arm.data.bones:
    if bone.name.endswith('.R'): bone.name=bone.name[:-2]
# Mako sources include a two-bone fin branch before the four-bone spine.
# Match the shared presentation contract without deleting any authored fin bones.
if all(name in arm.data.bones for name in ['Bone.005','Bone.006']):
    mapping={'Bone.001':'DorsalFin.001','Bone.002':'DorsalFin.002',
             'Bone.003':'Bone.001','Bone.004':'Bone.002',
             'Bone.005':'Bone.003','Bone.006':'Bone.004'}
    saved=[(arm.data.bones[old],new) for old,new in mapping.items()]
    for i,(bone,new) in enumerate(saved): bone.name='ExportTemp'+str(i)
    for bone,new in saved: bone.name=new
    # Blender updates both bound vertex groups and bone-parented fin objects.
scene=bpy.context.scene
try:
    scene.frame_start=int(math.floor(action.frame_range[0]))
    scene.frame_end=int(math.ceil(action.frame_range[1]))
except Exception:
    scene.frame_start=1
    scene.frame_end=max(scene.frame_end,2)
scene.frame_set(scene.frame_start)

# Avoid context-sensitive select_all in background mode. Explicitly unhide and
# select the mesh + bound armature, matching the proven Red Snapper importer.
for obj in bpy.context.view_layer.objects:
    try:
        obj.select_set(False)
    except Exception:
        pass
for obj in meshes+[arm]:
    obj.hide_viewport=False
    obj.hide_render=False
    try:
        obj.hide_set(False)
    except Exception:
        pass
    try:
        obj.select_set(True)
    except Exception:
        pass
try:
    bpy.context.view_layer.objects.active=arm
except Exception:
    pass

os.makedirs(os.path.dirname(out_path),exist_ok=True)
if os.path.exists(out_path):
    os.remove(out_path)

bpy.ops.export_scene.fbx(
    filepath=out_path,
    use_selection=True,
    object_types={'ARMATURE','MESH'},
    apply_unit_scale=True,
    bake_space_transform=False,
    axis_forward='-Z',
    axis_up='Y',
    add_leaf_bones=False,
    bake_anim=True,
    bake_anim_use_all_bones=True,
    bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=False,
    bake_anim_force_startend_keying=True,
    bake_anim_step=1.0,
    bake_anim_simplify_factor=0.0,
    path_mode='AUTO')
if not os.path.exists(out_path):
    raise RuntimeError('Blender did not create the FBX.')
print('FISH_ARMATURE='+arm.name)
print('FISH_ACTION='+action.name)
print('FISH_FRAMES='+str(scene.frame_start)+':'+str(scene.frame_end))
