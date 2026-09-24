import bpy,json
from pathlib import Path
repo=Path(__file__).resolve().parents[2]
checks={}
for name in ['BigeyeTuna','YellowfinTuna']:
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.ops.import_scene.fbx(filepath=str(repo/'Assets/_Game/Reef/Source'/f'{name}.fbx'))
 scene=bpy.context.scene
 body=next(o for o in scene.objects if o.type=='MESH')
 arm=next(o for o in scene.objects if o.type=='ARMATURE')
 assert len(body.data.uv_layers)>0 and len(arm.data.bones)==5
 assert sum(bool(v.groups) for v in body.data.vertices)==len(body.data.vertices)
 samples=[]
 for frame in [2,6,11,16,21]:
  scene.frame_set(frame);bpy.context.view_layer.update()
  e=body.evaluated_get(bpy.context.evaluated_depsgraph_get());m=e.to_mesh()
  samples.append([list(e.matrix_world@v.co) for v in m.vertices]);e.to_mesh_clear()
 delta=max(abs(a-b) for va,vb in zip(samples[0],samples[2]) for a,b in zip(va,vb))
 assert delta>.01,(name,delta)
 checks[name]={'vertices':len(body.data.vertices),'bones':len(arm.data.bones),'max_motion':delta,'uv_layers':len(body.data.uv_layers),'weighted_vertices':sum(bool(v.groups) for v in body.data.vertices)}
print(json.dumps(checks,indent=2))
(repo/'Tools/TunaUpdate/export-verification.json').write_text(json.dumps(checks,indent=2)+'\n')
