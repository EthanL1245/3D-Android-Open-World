import bpy, json, os, sys
from mathutils import Matrix
args=sys.argv
out_path=args[args.index('--')+1]
name_map={'Plane':'ReelFootAndBody','Cylinder':'Rotor','Cylinder.001':'ReelHousing','Cylinder.002':'Spool','Cylinder.003':'Handle'}
scene=bpy.context.scene
scene.frame_set(1)
objs={o.name:o for o in scene.objects if o.type=='MESH'}
missing=[n for n in name_map if n not in objs]
if missing:
    raise RuntimeError('Level 3 reel Blend is missing expected mesh object(s): '+', '.join(missing))
basis=Matrix(((1,0,0),(0,-1,0),(0,0,-1))) * 0.04
parts=[]
depsgraph=bpy.context.evaluated_depsgraph_get()
for source_name,target_name in name_map.items():
    obj=objs[source_name]
    eval_obj=obj.evaluated_get(depsgraph)
    mesh=eval_obj.to_mesh(preserve_all_data_layers=True,depsgraph=depsgraph)
    try:
        mesh.calc_loop_triangles()
        rest=obj.matrix_local.copy()
        linear=basis @ rest.to_3x3()
        normal_matrix=linear.inverted().transposed()
        pos=basis @ rest.translation
        uv_layer=mesh.uv_layers.active.data if mesh.uv_layers.active else None
        vertices=[]; normals=[]; uvs=[]; triangles=[]
        for tri in mesh.loop_triangles:
            idx=[]
            for loop_index in tri.loops:
                loop=mesh.loops[loop_index]
                v=mesh.vertices[loop.vertex_index]
                p=linear @ v.co
                n=(normal_matrix @ loop.normal).normalized()
                uv=uv_layer[loop_index].uv if uv_layer else (0.0,0.0)
                idx.append(len(vertices)//3)
                vertices.extend((p.x,p.y,p.z))
                normals.extend((n.x,n.y,n.z))
                uvs.extend((float(uv[0]),float(uv[1])))
            if linear.determinant()<0: idx[1],idx[2]=idx[2],idx[1]
            triangles.extend(idx)
        parts.append({'name':target_name,'position':[pos.x,pos.y,pos.z],'vertices':vertices,'normals':normals,'uv':uvs,'triangles':triangles})
    finally:
        eval_obj.to_mesh_clear()
os.makedirs(os.path.dirname(out_path),exist_ok=True)
with open(out_path,'w',encoding='utf-8') as f: json.dump({'parts':parts},f,separators=(',',':'))