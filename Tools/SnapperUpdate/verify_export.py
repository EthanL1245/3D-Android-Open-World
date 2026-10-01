"""Blender --background --python verify_export.py -- RED.fbx YELLOW.fbx."""
import bpy,sys
for path in sys.argv[sys.argv.index('--')+1:]:
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.ops.import_scene.fbx(filepath=path)
 arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
 assert set(arm.data.bones.keys())=={'Bone','Bone.001','Bone.002','Bone.003','Bone.004'}
 meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
 assert len(meshes)==1
 mesh=meshes[0]
 assert len(mesh.data.uv_layers)>0 and all(v.groups for v in mesh.data.vertices)
 assert any(m.type=='ARMATURE' and m.object==arm for m in mesh.modifiers)
 start,end=map(int,arm.animation_data.action.frame_range)
 assert end-start==19
 poses=[]
 for frame in range(start,end+1):
  bpy.context.scene.frame_set(frame)
  poses.append((arm.matrix_world@arm.pose.bones['Bone.004'].head).copy())
 assert max((p-poses[0]).length for p in poses)>.001,'Static tail animation'
 assert (poses[-1]-poses[0]).length<.01,'Open swim loop'
 print('PASS',path,': five-bone rig, UVs, full skin weights, moving tail, closed 20-frame loop')
