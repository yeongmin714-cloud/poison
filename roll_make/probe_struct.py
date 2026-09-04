import bpy, json
SRC = r"C:\Unity\code\Assets\Animations\Mixamo\Quick Roll To Run.fbx"
bpy.ops.wm.read_homefile(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC)
arm = next(o for o in bpy.data.objects if o.type=='ARMATURE')
act = arm.animation_data.action
info = {"action_name": act.name}
info["action_dir"] = [x for x in dir(act) if not x.startswith('_') and ('fcurve' in x or 'layer' in x or 'slot' in x or 'channel' in x or 'strip' in x)]
# print top-level via hasattr
info["has_fcurves"] = hasattr(act, "fcurves")
info["has_layers"] = hasattr(act, "layers")
if hasattr(act,"layers"):
    L = act.layers
    info["n_layers"] = len(L)
    info["layer0_dir"] = [x for x in dir(L[0]) if not x.startswith('_') and any(k in x.lower() for k in ('fcurve','channel','strip','key'))] if len(L) else []
    if len(L):
        l0 = L[0]
        info["layer0_has_fcurves"] = hasattr(l0,"fcurves")
        st = l0.strips[0] if hasattr(l0,"strips") and l0.strips else None
        if st is not None:
            info["strip0_dir"] = [x for x in dir(st) if not x.startswith('_') and any(k in x.lower() for k in ('channel','fcurve','key'))]
            ch = st.channels[0] if hasattr(st,"channels") and st.channels else None
            if ch is not None:
                info["channel0_dir"] = [x for x in dir(ch) if not x.startswith('_') and not x.startswith('__')]
                info["channel0_datapath"] = getattr(ch,"data_path",None)
print(json.dumps(info, indent=1, ensure_ascii=False))
print("[PROBE-STRUCT] DONE")