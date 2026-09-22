import bpy,json,math,os
from pathlib import Path
repo=Path(__file__).resolve().parents[2]
from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath=str(repo/'SourceAssets/Reef/SeaBass.blend'))
scene=bpy.context.scene;scene.frame_start=1;scene.frame_end=20
arm=next(o for o in scene.objects if o.type=='ARMATURE');body=next(o for o in scene.objects if o.type=='MESH')
info={'frames':[],'vertices':len(body.data.vertices),'uv_layers':len(body.data.uv_layers),'modifiers':[(m.name,m.type) for m in body.modifiers]}
for f in [1,5,10,15,20]:
 scene.frame_set(f);bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get();ev=body.evaluated_get(dg);mesh=ev.to_mesh()
 info['frames'].append({'frame':f,'tail':list(arm.matrix_world@arm.pose.bones['Bone.004'].tail),'vertex0':list(body.matrix_world@mesh.vertices[0].co),'bone_rotations':[list(b.matrix.to_quaternion()) for b in arm.pose.bones]});ev.to_mesh_clear()
assert len(body.data.uv_layers)>0
assert any(sum(abs(a-b) for a,b in zip(info['frames'][0]['bone_rotations'][i],info['frames'][2]['bone_rotations'][i]))>.01 for i in range(5)), 'No authored bone motion decoded'
scene.frame_set(1)
bpy.ops.object.select_all(action='DESELECT')
for o in scene.objects:
 if o.type in {'ARMATURE','MESH','EMPTY'}:o.select_set(True)
bpy.context.view_layer.objects.active=arm
out=str(repo/'Assets/_Game/Reef/Source/SeaBass.fbx')
bpy.ops.export_scene.fbx(filepath=out,use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,bake_anim_force_startend_keying=True,use_mesh_modifiers=True,path_mode='AUTO')
open(str(repo/'Tools/SuncrestReef/seabass-export-check.json'),'w').write(json.dumps(info,indent=2))
print('EXPORTED',os.path.getsize(out),info)
