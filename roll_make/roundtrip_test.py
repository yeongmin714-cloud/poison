import bpy, json, math, os
from mathutils import Vector

SRC = r"C:\Unity\code\Assets\Animations\Mixamo\Quick Roll To Run.fbx"
OUT = r"C:\Unity\code\roll_make"

def fwd_of(m):
    return Vector((m[0][1], m[1][1], m[2][1]))

def mid_frame(arm):
    act = arm.animation_data.action
    return int(round((act.frame_range[0]+act.frame_range[1])/2))

def load(path):
    bpy.ops.wm.read_homefile(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    return arm

def hip_world_fwd(arm):
    sc = bpy.context.scene
    sc.frame_set(mid_frame(arm)); bpy.context.view_layer.update()
    pb = arm.pose.bones.get("mixamorig:Hips")
    return fwd_of(pb.matrix)

# baseline
arm = load(SRC); f0 = hip_world_fwd(arm)
report = {"src_mid_fwd":[round(c,3) for c in f0], "src_action": arm.animation_data.action.name if (arm.animation_data and arm.animation_data.action) else None}

# bake_anim=True roundtrip with +90 yaw
for bake in (True,):
    for yaw in (90, -90, 180):
        path = os.path.join(OUT, f"wt_{yaw}.fbx")
        a = load(SRC)
        e = list(a.rotation_euler); e[2] += math.radians(yaw); a.rotation_euler = e
        bpy.context.view_layer.update()
        bpy.ops.export_scene.fbx(filepath=path, use_selection=False, add_leaf_bones=False,
            bake_anim=bake, object_types={'ARMATURE','MESH'}, mesh_smooth_type='FACE',
            apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True)
        b = load(path)
        has_act = bool(b.animation_data and b.animation_data.action)
        ang = None
        if has_act:
            ang = round(math.degrees(f0.angle(hip_world_fwd(b))),1)
        report[str(yaw)] = {"has_action": has_act, "ibs": len(b.pose.bones), "hip_fwd_angle_vs_src": ang}
        print(f"yaw={yaw} bake={bake} -> has_action={has_act} angle={ang} bones={len(b.pose.bones)}")

with open(os.path.join(OUT,"roundtrip.json"),"w") as fp:
    json.dump(report, fp, indent=1, ensure_ascii=False)
print(json.dumps(report, indent=1, ensure_ascii=False))
print("[RT] DONE")