import bpy, json, math
from mathutils import Vector, Euler

SRC = r"C:\Unity\code\Assets\Animations\Mixamo\Quick Roll To Run.fbx"
PROBE = r"C:\Unity\code\roll_make"

result = {}

bpy.ops.wm.read_homefile(use_empty=True)
scene = bpy.context.scene
bpy.ops.import_scene.fbx(filepath=SRC)

arm = next((o for o in bpy.data.objects if o.type == 'ARMATURE'), None)
result["armature"] = arm.name if arm else None
if arm is None:
    print(json.dumps(result)); raise SystemExit(0)

result["bones"] = len(arm.pose.bones)
# list first 12 bone names to find root
names = [pb.name for pb in arm.pose.bones]
result["bone_names"] = names[:14]
# find the top-level bone (no parent in rigify/mixamo = 'mixamorig:Root' or 'mixamorig:Hips')
result["root_candidates"] = [n for n in names if any(k in n.lower() for k in ("root","hips","base"))]

ad = arm.animation_data
act = ad.action if ad and ad.action else None
result["action"] = act.name if act else None
if act:
    result["frame_range"] = [round(act.frame_range[0],2), round(act.frame_range[1],2)]
    result["frames"] = round(act.frame_range[1]-act.frame_range[0],2)
    result["duration_s"] = round((act.frame_range[1]-act.frame_range[0])/30.0,2)

# Which bones animate most (motion signature) at mid roll — top 5 world-orientation delta
if act:
    fr0, fr1 = act.frame_range
    scene.frame_set(int(round(fr0)))
    p0 = {pb.name: pb.rotation_quaternion.copy() for pb in arm.pose.bones}
    scene.frame_set(int(round((fr0+fr1)/2)))
    deltas = sorted(
        ((math.degrees(p0[n].rotation_difference(pb.rotation_quaternion).angle), n)
         for pb in arm.pose.bones if (n:=pb.name) in p0), reverse=True)
    result["top_animated_bones"] = deltas[:6]

# YAW TEST: rotate armature +90deg about vertical (Z in Blender), confirm bones' world orientation yaws ~90
if arm.animation_data and arm.animation_data.action:
    a = arm.animation_data.action
    fr0, fr1 = a.frame_range
    mid = int(round((fr0+fr1)/2))
    scene.frame_set(mid)
    b0 = {pb.name: pb.matrix.copy() for pb in arm.pose.bones}  # world matrix
    
    # apply yaw to armature rotation
    base_rot = arm.rotation_euler.copy()
    arm.rotation_euler = (base_rot[0], base_rot[1], base_rot[2] + math.radians(90))
    scene.frame_set(mid)
    # Re-evaluate? Blender may need context update
    bpy.context.view_layer.update()
    b1 = {pb.name: pb.matrix.copy() for pb in arm.pose.bones}
    # measure yaw between world orientations of a stable bone (e.g. a thigh)
    # simplest: compare forward vector of a bone
    def fwd(m):
        return Vector((m[0][1], m[1][1], m[2][1]))  # +Y column = local forward approx
    probe_bone = next(n for n in names if any(k in n.lower() for k in ("thigh","leg","hip")))
    v0 = fwd(b0[probe_bone]); v1 = fwd(b1[probe_bone])
    ang = math.degrees(v0.angle(v1))
    result["yaw_test"] = { "bone": probe_bone, "angle_between_world_fwd": round(ang,1) }
    result["yaw_ok"] = abs(ang - 90) < 25
    arm.rotation_euler = base_rot  # restore

with open(r"C:\Unity\code\roll_make\probe.json","w") as fp:
    json.dump(result, fp, ensure_ascii=False, indent=1)
print(json.dumps(result, ensure_ascii=False, indent=1))
print("[PROBE] DONE")