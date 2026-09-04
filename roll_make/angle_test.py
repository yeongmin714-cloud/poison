import bpy, json, math, os
from mathutils import Vector

SRC = r"C:\Unity\code\Assets\Animations\Mixamo\Quick Roll To Run.fbx"
OUT = r"C:\Unity\code\roll_make"

report = {}

def fwd_of(m):  # approximate local +Y (forward) of a bone from its world matrix
    return Vector((m[0][1], m[1][1], m[2][1]))

def arm_mid_forward():
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    pb = arm.pose.bones.get("mixamorig:Hips")
    scene = bpy.context.scene
    act = arm.animation_data.action
    scene.frame_set(int(round((act.frame_range[0]+act.frame_range[1])/2)))
    bpy.context.view_layer.update()
    return pb.matrix, fwd_of(pb.matrix)

def import_and_midfwd(path):
    bpy.ops.wm.read_homefile(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    return arm_mid_forward()

def export(path, out, yaw_deg, bake):
    bpy.ops.wm.read_homefile(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    # yaw about vertical (Z) in armature's OWN local frame: add to existing Z rotation
    e = list(arm.rotation_euler)
    e[2] += math.radians(yaw_deg)
    arm.rotation_euler = e
    bpy.context.view_layer.update()
    bpy.ops.export_scene.fbx(
        filepath=out, use_selection=False, add_leaf_bones=False,
        bake_anim=bake, object_types={'ARMATURE','MESH'},
        mesh_smooth_type='FACE', apply_scale_options='FBX_SCALE_ALL',
        bake_space_transform=True,
    )

bpy.ops.wm.read_homefile(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
act = arm.animation_data.action
fr0, fr1 = act.frame_range
report["src_frames"] = [round(fr0,2), round(fr1,2)]

m0, f0 = arm_mid_forward()
report["src_mid_fwd"] = [round(c,3) for c in f0]

# Test yaw via 2 export flags; reimport and measure hip world-forward angle vs src
for yaw in (90, -90):
    row = {"yaw": yaw}
    for bake in (False, True):
        outp = os.path.join(OUT, f"t_{yaw}_{'b' if bake else 'nb'}.fbx")
        export(SRC, outp, yaw, bake)
        _, f = import_and_midfwd(outp)
        ang = math.degrees(f0.angle(Vector(f)))
        # normalize signed angle in the horizontal plane x-z
        row[f"bake_{bake}_angle"] = round(ang,1)
    report.setdefault("yaw_tests", []).append(row)

with open(os.path.join(OUT,"angle_test.json"),"w") as fp:
    json.dump(report, fp, indent=1, ensure_ascii=False)
print(json.dumps(report, indent=1, ensure_ascii=False))
print("[ANGLE-TEST] DONE")