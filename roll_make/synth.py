import bpy, json, math, os
from mathutils import Vector, Euler, Quaternion

SRC = r"C:\Unity\code\Assets\Animations\Mixamo\Quick Roll To Run.fbx"
OUT = r"C:\Unity\code\roll_make\preview"
os.makedirs(OUT, exist_ok=True)
PREV = os.path.join(OUT, "prev")
os.makedirs(PREV, exist_ok=True)

FPS = 30.0
report = {}

def setup_render(sc):
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.render.resolution_x = 320; sc.render.resolution_y = 320
    sc.render.fps = int(FPS)
    sc.display.shading.light = 'STUDIO'
    sc.display.shading.color_type = 'SINGLE'

def add_cam(sc, center, size):
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("C"))
    sc.collection.objects.link(cam)
    dist = max(size*2.6, 2.0)
    cam.location = (center.x, center.y - dist, center.z + size*0.5)
    d = (center - cam.location).to_track_quat('-Z','Y').to_euler()
    cam.rotation_euler = d
    sc.camera = cam
    li = bpy.data.objects.new("L", bpy.data.lights.new("L", type='SUN'))
    li.rotation_euler = (math.radians(50),0,math.radians(30))
    sc.collection.objects.link(li)

def load(path):
    bpy.ops.wm.read_homefile(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    return bpy.context.scene

def top_bone(arm):
    for pb in arm.pose.bones:
        if pb.parent is None:
            return pb
    return arm.pose.bones.get("mixamorig:Hips")

def bake_yaw_into_hip_quats(arm, yaw_deg):
    """Pre-multiply a Z-yaw onto the top bone's rotation_quaternion keyframe values."""
    tb = top_bone(arm)  # name
    act = arm.animation_data.action
    name = tb.name
    prefix = f'pose.bones["{name}"].rotation_quaternion'
    curves = {i: [] for i in range(4)}
    for fc in act.fcurves:
        if fc.data_path == prefix:
            curves[fc.array_index].append(fc)
    # frames where all 4 channels have a key (Mixamo keys aligned)
    frames = sorted({round(k.co[0],3) for i in range(4) for fc in curves[i] for k in fc.keyframe_points})
    yaw = Euler((0,0,math.radians(yaw_deg))).to_quaternion()
    n = 0
    for f in frames:
        vals = {}
        ok = True
        for i in range(4):
            if not curves[i]:
                ok = False; break
            cur = curves[i][0]
            kf = next((k for k in cur.keyframe_points if abs(k.co[0]-f) < 0.01), None)
            if kf is None:
                ok = False; break
            vals[i] = kf.co[1]
        if not ok: continue
        q = Quaternion((vals[0], vals[1], vals[2], vals[3]))
        q2 = yaw @ q   # pre-multiply
        for i in range(4):
            cur = curves[i][0]
            kf = next(k for k in cur.keyframe_points if abs(k.co[0]-f) < 0.01)
            kf.co[1] = q2[i]
            kf.handle_left = (kf.co[0], q2[i])
            kf.handle_right = (kf.co[0], q2[i])
        n += 1
    return name, n, len(frames)

def export(arm, path):
    bpy.ops.export_scene.fbx(filepath=path, use_selection=False, add_leaf_bones=False,
        bake_anim=True, object_types={'ARMATURE','MESH'}, mesh_smooth_type='FACE',
        apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True)

def render_preview(sc, arm, slate):
    act = arm.animation_data.action
    fr0, fr1 = act.frame_range
    meshes = [o for o in bpy.data.objects if o.type=='MESH']
    mn = Vector((1e9,1e9,1e9)); mx = Vector((-1e9,-1e9,-1e9))
    for o in meshes:
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c); mn = Vector((min(mn.x,w.x),min(mn.y,w.y),min(mn.z,w.z))); mx = Vector((max(mx.x,w.x),max(mx.y,w.y),max(mx.z,w.z)))
    center=(mn+mx)/2; size=max((mx-mn).length,1.0)
    add_cam(sc,center,size)
    for i,fr in enumerate([0.2,0.5,0.8]):
        sc.frame_set(int(round(fr0+(fr1-fr0)*fr))); bpy.context.view_layer.update()
        p = os.path.join(PREV, f"{slate}_{i}.png")
        sc.render.filepath = p; bpy.ops.render.render(write_still=True)

# Build 3 variants
variants = [("Roll_Left", -90), ("Roll_Right", 90), ("Roll_Back", 180)]
for name, yaw in variants:
    sc = load(SRC)
    setup_render(sc)
    arm = next(o for o in bpy.data.objects if o.type=='ARMATURE')
    bn, mod, tot = bake_yaw_into_hip_quats(arm, yaw)
    act = arm.animation_data.action
    act.name = name
    out = os.path.join(OUT, f"{name}.fbx")
    export(arm, out)
    # re-import clean to render + verify
    sc2 = load(SRC)  # resets
    bpy.ops.import_scene.fbx(filepath=out)
    arm2 = next(o for o in bpy.data.objects if o.type=='ARMATURE')
    if arm2.animation_data and arm2.animation_data.action:
        a2 = arm2.animation_data.action; a2.name = name
        render_preview(bpy.context.scene, arm2, name)
        report[name] = {"yaw": yaw, "bone": bn, "frames_modified": mod, "total_frames": tot,
                        "reimport_has_action": True, "frames": [round(a2.frame_range[0],1), round(a2.frame_range[1],1)]}
        print(f"[SYNTH] {name} yaw={yaw} modified {mod}/{tot} on bone {bn}")
    else:
        report[name] = {"yaw": yaw, "reimport_has_action": False}
        print(f"[SYNTH] {name} FAILED reimport action")

with open(r"C:\Unity\code\roll_make\synth.json","w") as fp:
    json.dump(report, fp, indent=1, ensure_ascii=False)
print(json.dumps(report, indent=1, ensure_ascii=False))
print("[SYNTH] DONE")