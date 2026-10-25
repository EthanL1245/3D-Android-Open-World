import bpy,sys,os,json
from mathutils import Vector
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.abspath('Assets/_Game/Reef/Source/WinterFlounder.fbx'))
s=bpy.context.scene;arm=next(o for o in s.objects if o.type=='ARMATURE'); meshes=[o for o in s.objects if o.type=='MESH'];s.frame_set(1)
assert len(arm.data.bones)==6
assert arm.animation_data and arm.animation_data.action
for o in meshes: assert len(o.data.uv_layers)>0
pts=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
lo=Vector([min(p[i] for p in pts) for i in range(3)]);hi=Vector([max(p[i] for p in pts) for i in range(3)]);size=hi-lo;center=(hi+lo)/2
assert size.z<size.x*.4,(size[:])
body=max(meshes,key=lambda o:len(o.data.vertices));side=[[],[]]
for loop,uv in zip(body.data.loops,body.data.uv_layers.active.data):
 p=body.matrix_world@body.data.vertices[loop.vertex_index].co;side[int(uv.uv.y>.5)].append(p.z)
assert sum(side[1])/len(side[1])>sum(side[0])/len(side[0]),'Eyed side must face up'
s.frame_set(1);p1=arm.pose.bones['Bone.005'].matrix.copy();s.frame_set(11);p2=arm.pose.bones['Bone.005'].matrix.copy();assert any(abs(p1[i][j]-p2[i][j])>1e-4 for i in range(4) for j in range(4))
first,last=arm.animation_data.action.frame_range;print('FRAMES',first,last);s.frame_set(int(first),subframe=first-int(first));p1=arm.pose.bones['Bone.005'].matrix.copy();s.frame_set(int(last),subframe=last-int(last));p2=arm.pose.bones['Bone.005'].matrix.copy();assert max(abs(p1[i][j]-p2[i][j]) for i in range(4) for j in range(4))<1e-4
report={'mesh_count':len(meshes),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes),'bones':len(arm.data.bones),'bounds_xyz':list(size),'uv0':True,'dorsal_side_up':True,'animation_motion':True,'loop_seam':True}
open('Tools/WinterFlounder/export-verification.json','w').write(json.dumps(report,indent=2)+'\n');print(report)
