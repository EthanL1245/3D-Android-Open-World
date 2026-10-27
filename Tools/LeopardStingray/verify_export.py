"""Check export has the two required distinct actions and all Leopard UV data."""
import bpy,os,json
bpy.ops.wm.read_factory_settings(use_empty=True)
path=os.path.abspath('Assets/_Game/Reef/Source/LeopardStingray.fbx')
bpy.ops.import_scene.fbx(filepath=path)
scene=bpy.context.scene
arms=[o for o in scene.objects if o.type=='ARMATURE']
meshes=[o for o in scene.objects if o.type=='MESH']
assert len(arms)==len(meshes)==1
arm=arms[0];body=meshes[0]
assert len(arm.data.bones)==29
assert body.data.uv_layers and len(body.data.vertices)>100
names={a.name.split('|')[-1]:a for a in bpy.data.actions}
assert 'Swim' in names and 'HeldStruggle' in names,list(names)
result={}
for name in ('Swim','HeldStruggle'):
  action=names[name]
  arm.animation_data.action=action
  if hasattr(arm.animation_data,'action_slot') and len(action.slots):
      arm.animation_data.action_slot=action.slots[0]
  low,high=map(int,action.frame_range)
  assert high-low>=15,(name,low,high)
  poses=[]
  for frame in (low,(low+high)//2,high):
      scene.frame_set(frame)
      poses.append([float(x) for bone in arm.pose.bones for row in bone.matrix for x in row])
  motion=max(abs(x-y) for x,y in zip(poses[0],poses[1]))
  seam=max(abs(x-y) for x,y in zip(poses[0],poses[2]))
  assert motion>.01,(name,'missing motion')
  assert seam<.02,(name,'animation not looped',seam)
  result[name]={'frames':[low,high],'movement':motion,'seam':seam}
print('PASS LEOPARD ANIMATIONS',json.dumps(result))
