"""Apply ONLY updated Stingray animation curves in Blender 5.2.3."""
import bpy, base64, zlib, json, os, hashlib
source = os.path.join(os.getcwd(), "Tools/RoughtailStingray/motion_payload.b64")
payload = json.loads(zlib.decompress(base64.b64decode(open(source).read())))
channels = [tuple(x) for x in payload["channels"]]
assert len(channels) == 290 and len(set(channels)) == 290
assert set(payload["actions"]) == {"Stingray Swimming", "Stingray Struggling"}
assert all(len(a) == 290 for a in payload["actions"].values())
def static_signature():
    obs = sorted(bpy.data.objects,key=lambda o:o.name)
    sig=[]
    for obj in obs:
        sig.append((obj.name,obj.type,tuple(round(v,8) for row in obj.matrix_world for v in row)))
        if obj.type == "MESH":
            m=obj.data
            sig.append(("mesh",obj.name,len(m.vertices),len(m.polygons),
                tuple(tuple(round(x,8) for x in v.co) for v in m.vertices),
                tuple(tuple(p.vertices) for p in m.polygons),
                tuple((layer.name,tuple(tuple(round(x,8) for x in d.uv) for d in layer.data)) for layer in m.uv_layers)))
        elif obj.type == "ARMATURE":
            sig.append(("rig",obj.name,tuple((bone.name,bone.parent.name if bone.parent else "",
                tuple(round(x,8) for row in bone.matrix_local for x in row)) for bone in obj.data.bones)))
    return hashlib.sha256(repr(sig).encode()).hexdigest()
before=static_signature()
def curves_for(action):
    fcs=[]
    for layer in action.layers:
        for strip in layer.strips:
            if strip.type != "KEYFRAME": continue
            for slot in action.slots:
                bag=strip.channelbag(slot,ensure=False)
                if bag is not None:
                    fcs.extend(list(bag.fcurves))
    if not fcs:
        raise RuntimeError("No Blender 5.2 layered curves in "+action.name)
    return fcs
for name,motion in payload["actions"].items():
    act=bpy.data.actions[name]
    curves={(fc.data_path,int(fc.array_index)):fc for fc in curves_for(act)}
    if set(curves)!=set(channels):
        raise RuntimeError(name+" bone/channel mismatch; refusing static changes")
    for (path,index),keys in zip(channels,motion):
        fc=curves[(path,index)]
        if len(keys)==1:
            value=float(keys[0])
            for kp in fc.keyframe_points:
                kp.co.y=value
                kp.handle_left.y=value
                kp.handle_right.y=value
        else:
            while fc.keyframe_points:
                fc.keyframe_points.remove(fc.keyframe_points[-1],fast=True)
            fc.keyframe_points.add(len(keys))
            for kp,key in zip(fc.keyframe_points,keys):
                x,y,hx,hy,rx,ry=key
                kp.interpolation="BEZIER"
                kp.handle_left_type="FREE"
                kp.handle_right_type="FREE"
                kp.co=(x,y)
                kp.handle_left=(hx,hy)
                kp.handle_right=(rx,ry)
        fc.update()
    print("UPDATED_ACTION",name,len(curves))
assert static_signature()==before,"Static mesh/rig/UV/transforms changed!"
bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath("ArtSources/RoughtailStingray.blend"))
print("PASS: updated swimming and held struggling motion only")
