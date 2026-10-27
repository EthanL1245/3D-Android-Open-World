"""Compare runtime FBX static contents before/after animation update."""
import bpy,os,sys
paths=sys.argv[sys.argv.index("--")+1:]
assert len(paths)==2
def static(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.abspath(path),use_anim=False)
    objects=sorted((o for o in bpy.data.objects if o.type in ("MESH","ARMATURE")),key=lambda o:o.name)
    meshes=[o for o in objects if o.type=="MESH"];rigs=[o for o in objects if o.type=="ARMATURE"]
    assert len(meshes)==len(rigs)==1
    mesh=meshes[0];rig=rigs[0]
    def mat(m):return tuple(round(float(v),5) for row in m for v in row)
    def vec(v):return tuple(round(float(x),5) for x in v)
    return (
        tuple((o.name,o.type,mat(o.matrix_world),o.parent.name if o.parent else None) for o in objects),
        tuple(vec(v.co) for v in mesh.data.vertices),
        tuple(tuple(poly.vertices) for poly in mesh.data.polygons),
        tuple((layer.name,tuple(vec(v.uv) for v in layer.data)) for layer in mesh.data.uv_layers),
        tuple((v.index,tuple(sorted((mesh.vertex_groups[g.group].name,round(g.weight,5)) for g in v.groups))) for v in mesh.data.vertices),
        tuple((bone.name,bone.parent.name if bone.parent else None,mat(bone.matrix_local),bone.use_deform) for bone in rig.data.bones),
        tuple(material.name if material else None for material in mesh.data.materials))
old,new=map(static,paths)
labels=("scene transforms","mesh geometry","faces","UVs","skin weights","bone names/parents/rest poses","materials")
for label,a,b in zip(labels,old,new):
    assert a==b,"Changed non-animation content: "+label
print("PASS: model, UV, weights, bones, transforms, materials unchanged")
