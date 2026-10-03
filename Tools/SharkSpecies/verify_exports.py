import bpy,sys,json
from pathlib import Path
root=Path(__file__).resolve().parents[2]
from mathutils import Vector
for key in ['BlacktipReefShark','MakoShark','BattleScarredMakoShark']:
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.ops.import_scene.fbx(filepath=str(root/'Assets/_Game/Reef/Source'/(key+'.fbx')))
 arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
 assert all(n in arm.data.bones for n in ['Bone','Bone.001','Bone.002','Bone.003','Bone.004'])
 for i in range(1,5): assert arm.data.bones['Bone.%03d'%i].parent.name==('Bone' if i==1 else 'Bone.%03d'%(i-1))
 meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
 print([(o.name,o.parent.name if o.parent else None,o.parent_type,o.parent_bone) for o in meshes]);assert all(len(o.data.uv_layers)>0 and (all(len(v.groups)>0 for v in o.data.vertices) or o.parent_type=='BONE') for o in meshes)
 poses=[]
 for f in [1,5,10,15,20]:
  bpy.context.scene.frame_set(f);poses.append([tuple(b.matrix.translation) for b in arm.pose.bones])
 assert poses[0]!=poses[2],key+' animation is static'
 print('VERIFIED',key,'bones',len(arm.data.bones),'vertices',sum(len(o.data.vertices) for o in meshes),'animated frames',len(poses))
