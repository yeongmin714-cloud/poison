import bpy, json, math
from mathutils import Vector

D = "/mnt/c/Unity/code/roll_make"
BONE = "mixamorig:Hips"

def measure_midfwd(fbxpath):
    bpy.ops.wm.read_homefile(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbxpath)
    arm = next(o for o in bpy.data.objects if o.type=='ARMATURE')
    pb = arm.pose.bones.get(BONE)
    act = arm.animation_data.action
    sc = bpy.context.scene
    fr = int(round((act.frame_range[0]+act.frame_range[1])/2))
    sc.frame_set(fr); bpy.context.view_layer.update()
    m = pb.matrix
    # local +Y (forward approx) -> world
    v = Vector((m[0][1], m[1][1], m[2][1]))
    return v, fr

# base
v0, fr0 = measure_midfwd(f"{D}/Assets_X_FB") if False else (None,None)
bpy.ops.wm.read_homefile(use_empty=True)
bpy.ops.import_scene.fbx(filepath=f"/mnt/c/Unity/code/Assets/Animations/Mixamo/Quick Roll To Run.fbx")
arm0 = next(o for o in bpy.data.objects if o.type=='ARMATURE')
pb0 = arm0.pose.bones.get(BONE); act0 = arm0.animation_data.action
sc = bpy.context.scene
fr0 = int(round((act0.frame_range[0]+act0.frame_range[1])/2))
sc.frame_set(fr0); bpy.context.view_layer.update()
m0 = pb0.matrix
v0 = Vector((m0[0][1], m0[1][1], m0[2][1]))

report = {"base_mid": fr0, "base_fwd": [round(c,3) for c in v0]}
for name in ["Roll_Left","Roll_Right","Roll_Back"]:
    v, fr = measure_midfwd(f"{D}/{name}.fbx")
    # signed angle in horizontal plane
    ang = math.degrees(math.atan2(v0.y*v.x - v0.x*v.y, v0.x*v.x + v0.y*v.y))
    report[name] = {"mid_frame": fr, "fwd": [round(c,3) for c in v],
                    "signed_horiz_angle_deg": round(ang,1)}
    print(f"{name}: horiz_angle={round(ang,1)}deg")

with open(f"{D}/verify36.json","w") as fp:
    json.dump(report, fp, indent=1, ensure_ascii=False)
print(json.dumps(report, indent=1, ensure_ascii=False))
print("[VERIFY36] DONE")