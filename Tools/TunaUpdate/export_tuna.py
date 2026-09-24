import bpy,json,shutil
from pathlib import Path
repo=Path(__file__).resolve().parents[2]
checks={}
for name in ['BigeyeTuna','YellowfinTuna']:
 source=repo/'SourceAssets/Reef'/f'{name}.blend'
 bpy.ops.wm.open_mainfile(filepath=str(source))
 if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
 scene=bpy.context.scene;scene.frame_start=1;scene.frame_end=20
 arm=next(o for o in scene.objects if o.type=='ARMATURE')
 body=next(o for o in scene.objects if o.type=='MESH')
 assert list(arm.data.bones.keys())==['Bone','Bone.001','Bone.002','Bone.003','Bone.004']
 assert len(body.data.uv_layers)>0
 samples=[]
 for frame in [1,5,10,15,20]:
  scene.frame_set(frame);bpy.context.view_layer.update()
  evaluated=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=evaluated.to_mesh()
  samples.append({'frame':frame,'vertices':[list(body.matrix_world@v.co) for v in mesh.vertices]})
  evaluated.to_mesh_clear()
 assert max(abs(a-b) for va,vb in zip(samples[0]['vertices'],samples[2]['vertices']) for a,b in zip(va,vb))>.01
 scene.frame_set(1);bpy.ops.object.select_all(action='DESELECT')
 for o in scene.objects:
  if o.type in {'ARMATURE','MESH','EMPTY'}:o.select_set(True)
 bpy.context.view_layer.objects.active=arm
 out=repo/'Assets/_Game/Reef/Source'/f'{name}.fbx'
 bpy.ops.export_scene.fbx(filepath=str(out),use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,bake_anim_force_startend_keying=True,use_mesh_modifiers=True,path_mode='AUTO')
 checks[name]={'frames':[s['frame'] for s in samples],'max_motion':max(abs(a-b) for va,vb in zip(samples[0]['vertices'],samples[2]['vertices']) for a,b in zip(va,vb)),'fps':scene.render.fps,'source_vertices':len(body.data.vertices)}
(repo/'Tools/TunaUpdate/animation-check.json').write_text(json.dumps(checks))
print('Exported both textured five-bone tuna rigs; authored motion confirmed.')
