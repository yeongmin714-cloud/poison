import bpy, json
SRC = r"C:\Unity\code\Assets\Animations\Mixamo\Quick Roll To Run.fbx"
bpy.ops.wm.read_homefile(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC)
arm = next(o for o in bpy.data.objects if o.type=='ARMATURE')
act = arm.animation_data.action
L = act.layers[0]
info = {"n_strips": len(L.strips)}
strip = L.strips[0]
info["strip_dir"] = [x for x in dir(strip) if not x.startswith('_') and not x.startswith('__')]
cb = strip.channelbag
info["cb_dir"] = [x for x in dir(cb) if not x.startswith('_') and ('channel' in x.lower() or 'data' in x.lower() or 'key' in x.lower() or 'fcurve' in x.lower())]
# Try to enumerate channels
info["cb_has_channels"] = hasattr(cb,"channels")
if hasattr(cb,"channels"):
    info["n_ch"] = len(cb.channels)
    ch0 = cb.channels[0]
    info["ch0_dir"] = [x for x in dir(ch0) if not x.startswith('_') and not x.startswith('__')]
    info["ch0_datapath"] = getattr(ch0,"data_path",None)
    info["ch0_array_index"] = getattr(ch0,"array_index",None)
# try legacy fcurve on channelbag?
for a in ("get_keyframe_points","fcurve","keyframe_points"):
    info[f"cb_{a}"] = hasattr(cb,a)
# print all channel datapaths mentioning rotation_quaternion for Hips
if hasattr(cb,"channels"):
    hips = [(c.data_path, c.array_index, len(getattr(c,'keyframe_points',[])) if hasattr(c,'keyframe_points') else -1)
            for c in cb.channels if 'rotation_quaternion' in (getattr(c,'data_path','') or '') and 'Hips' in (getattr(c,'data_path','') or '')]
    info["hips_rot_channels"] = hips[:8]
    # also expose keyframe_points accessor name on a channel
    if hips:
        c0 = [c for c in cb.channels if c is hips[0]]  # placeholder
print(json.dumps(info, indent=1, ensure_ascii=False))
print("[PROBE-CB] DONE")