import bpy, json, os, sys
args=sys.argv
out_path=args[args.index('--')+1]
deps=bpy.context.evaluated_depsgraph_get()
best=None; best_count=-1
for obj in bpy.context.scene.objects:
    if obj.type!='MESH': continue
    eo=obj.evaluated_get(deps); me=eo.to_mesh(preserve_all_data_layers=True,depsgraph=deps)
    try:
        me.calc_loop_triangles(); count=len(me.loop_triangles)
        if count>best_count: best=obj; best_count=count
    finally: eo.to_mesh_clear()
if best is None: raise RuntimeError('No mesh object exists in the Level 3 rod Blend.')
eo=best.evaluated_get(deps); me=eo.to_mesh(preserve_all_data_layers=True,depsgraph=deps)
try:
    me.calc_loop_triangles(); uv_layer=me.uv_layers.active.data if me.uv_layers.active else None
    verts=[]; uvs=[]; tris=[]; world=best.matrix_world; flip=world.to_3x3().determinant()<0
    for tri in me.loop_triangles:
        ids=[]
        for li in tri.loops:
            loop=me.loops[li]; p=world @ me.vertices[loop.vertex_index].co; uv=uv_layer[li].uv if uv_layer else (0.0,0.0)
            ids.append(len(verts)//3); verts.extend((p.x,p.y,p.z)); uvs.extend((float(uv[0]),float(uv[1])))
        if flip: ids[1],ids[2]=ids[2],ids[1]
        tris.extend(ids)
    os.makedirs(os.path.dirname(out_path),exist_ok=True)
    with open(out_path,'w',encoding='utf-8') as f: json.dump({'vertices':verts,'uv':uvs,'triangles':tris},f,separators=(',',':'))
finally: eo.to_mesh_clear()