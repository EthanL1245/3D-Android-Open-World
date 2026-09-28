import bpy,json,pathlib,shutil,sys
from mathutils import Vector
args=sys.argv[sys.argv.index('--')+1:];source=pathlib.Path(args[0]);out=pathlib.Path(args[1])
parts=[]
for file in ['Dock','Dock Post']:
 bpy.ops.wm.open_mainfile(filepath=str(source/(file+'.blend')))
 if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
 for ob in bpy.context.scene.objects:
  if ob.type!='MESH':continue
  me=ob.data;me.calc_loop_triangles();uvs=me.uv_layers.active.data
  vv=[ob.matrix_world@v.co for v in me.vertices]
  center=(Vector(tuple(min(v[i] for v in vv) for i in range(3)))+Vector(tuple(max(v[i] for v in vv) for i in range(3))))*.5
  verts=[];uv=[];tri=[]
  for t in me.loop_triangles:
   indices=[]
   for li in t.loops:
    p=vv[me.loops[li].vertex_index]
    if file=='Dock':q=(p.x-.015556693,p.z-2.751240969+.15,p.y+.022861958+1)
    else:q=((p.x-center.x)/2,(p.z-center.z)/20.93287468,(p.y-center.y)/2)
    indices.append(len(verts)//3);verts.extend(q);uv.extend(uvs[li].uv)
   tri.extend([indices[0],indices[2],indices[1]]) # Blender Z-up -> Unity Y-up reflection
  parts.append({'name':'Post' if file=='Dock Post' else 'Deck' if ob.name=='Cube' else 'EdgeTrim','vertices':verts,'uv':uv,'triangles':tri})
json.dump({'parts':parts},open(out/'Dock.json','w'),separators=(',',':'))
shutil.copy(source/'Dock Texture.jpg',out/'DockTexture.jpg')
