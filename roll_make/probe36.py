import bpy, json
SRC = "/mnt/c/Unity/code/Assets/Animations/Mixamo/Quick Roll To Run.fbx"
bpy.ops.wm.read_homefile(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC)
arm = next((o for o in bpy.data.objects if o.type=='ARMATURE'), None)
info = {"arm": arm.name if arm else None}
if arm is None: print(json.dumps(info)); raise SystemExit(0)
act = arm.animation_data.action if (arm.animation_data and arm.animation_data.action) else None
info["action"] = act.name if act else None
info["has_fcurves"] = hasattr(act, "fcurves") if act else False
if act and hasattr(act,'fcurves'):
    info["n_fcurves"] = len(act.fcurves)
    # Hips rotation quaternion channels
    hips = [ (fc.data_path, fc.array_index, len(fc.keyframe_points), [round(k.co[0],1) for k in fc.keyframe_points[:5]]) 
             for fc in act.fcurves if 'rotation_quaternion' in fc.data_path and 'Hips' in fc.data_path ]
    info["hips_rot_channels"] = hips
    # any quaternion channels sample
    q = [a for a in hips if a[3]]
    info["frame_range"] = [round(act.frame_range[0],1), round(act.frame_range[1],1)]
print(json.dumps(info, indent=1, ensure_ascii=False))
print("[P36] DONE")