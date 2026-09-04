import bpy, json, os, math
from mathutils import Vector, Euler, Quaternion

SRC = "/mnt/c/Unity/code/Assets/Animations/Mixamo/Quick Roll To Run.fbx"
OUT = "/mnt/c/Unity/code/roll_make"
PREV = os.path.join(OUT, "prev36")
os.makedirs(PREV, exist_ok=True)
FPS = 30.0

report = {}

def fresh():
    bpy.ops.wm.read_homefile(use_empty=True)
    return bpy.context.scene

def bake_yaw_into_hips(act, yaw_deg):
    """Pre-multiply Z-yaw onto mixamorig:Hips rotation_quaternion keyframes."""
    path = 'pose.bones["mixamorig:Hips"].rotation_quaternion'
    curves = {i: [] for i in range(4)}
    for fc in act.fcurves:
        if fc.data_path == path:
            curves[fc.array_index].append(fc)
    for i in range(4):
        if not curves[i]:
            return None
    frames = sorted({round(k.co[0],3) for i in range(4) for fc in curves[i] for k in fc.keyframe_points})
    yaw = Euler((0.0,0.0,math.radians(yaw_deg))).to_quaternion()
    n = 0
    for f in frames:
        kfs = {}
        ok = True
        for i in range(4):
            cur = curves[i][0]
            k = next((kk for kk in cur.keyframe_points if abs(kk.co[0]-f) < 0.01), None)
            if k is None: ok=False; break
            kfs[i] = k
        if not ok: continue
        q = Quaternion((kfs[0].co[1], kfs[1].co[1], kfs[2].co[1], kfs[3].co[1]))
        q2 = yaw @ q
        for i in range(4):
            kfs[i].co[1] = q2[i]
            if kfs[i].handle_left_type == 'FREE':
                kfs[i].handle_left = (kfs[i].co[0], q2[i])
            if kfs[i].handle_right_type == 'FREE':
                kfs[i].handle_right = (kfs[i].co[0], q2[i])
        n += 1
    return n, len(frames)

def export_fbx(arm, path):
    bpy.ops.export_scene.fbx(filepath=path, use_selection=False, add_leaf_bones=False,
        bake_anim=True, object_types={'ARMATURE','MESH'}, mesh_smooth_type='FACE',
        apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True)

def setup_render(sc):
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.render.resolution_x = 320; sc.render.resolution_y = 320
    sc.render.fps = int(FPS)
    sc.render.film_transparent = False

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

def render_preview(arm, slate):
    sc = bpy.context.scene
    act = arm.animation_data.action
    fr0, fr1 = act.frame_range
    meshes = [o for o in bpy.data.objects if o.type=='MESH']
    mn = Vector((1e9,1e9,1e9)); mx = Vector((-1e9,-1e9,-1e9))
    for o in meshes:
        mw = o.matrix_world
        for c in o.bound_box:
            w = mw @ Vector(c)
            mn = Vector((min(mn.x,w.x),min(mn.y,w.y),min(mn.z,w.z)))
            mx = Vector((max(mx.x,w.x),max(mx.y,w.y),max(mx.z,w.z)))
    center=(mn+mx)/2; size=max((mx-mn).length,1.0)
    add_cam(sc,center,size)
    for i,fr in enumerate([0.18,0.5,0.82]):
        sc.frame_set(int(round(fr0+(fr1-fr0)*fr)))
        bpy.context.view_layer.update()
        p = os.path.join(PREV, f"{slate}_{i}.png")
        sc.render.filepath = p
        bpy.ops.render.render(write_still=True)

# baseline preview
scene = fresh(); setup_render(scene)
bpy.ops.import_scene.fbx(filepath=SRC)
arm0 = next(o for o in bpy.data.objects if o.type=='ARMATURE')
render_preview(arm0, "base")

# 3 variants
variants = [("Roll_Left", -90), ("Roll_Right", 90), ("Roll_Back", 180)]
for name, yaw in variants:
    scene = fresh(); setup_render(scene)
    bpy.ops.import_scene.fbx(filepath=SRC)
    arm = next(o for o in bpy.data.objects if o.type=='ARMATURE')
    act = arm.animation_data.action
    res = bake_yaw_into_hips(act, yaw)
    if res is None:
        report[name] = {"error": "no hips rot channels"}; continue
    n_mod, n_frames = res
    act.name = name
    out = os.path.join(OUT, f"{name}.fbx")
    export_fbx(arm, out)
    # fresh reimport to verify + render
    scene = fresh(); setup_render(scene)
    bpy.ops.import_scene.fbx(filepath=out)
    arm2 = next(o for o in bpy.data.objects if o.type=='ARMATURE')
    a2 = arm2.animation_data.action
    if a2:
        a2.name = name
        render_preview(arm2, name)
        report[name] = {"yaw": yaw, "frames_modified": n_mod, "total_frames": n_frames,
                        "reimport_has_action": True,
                        "frame_range": [round(a2.frame_range[0],1), round(a2.frame_range[1],1)]}
    else:
        report[name] = {"yaw": yaw, "reimport_has_action": False}
    print(f"[SYNTH] {name} yaw={yaw} modified {n_mod}/{n_frames}")

with open(os.path.join(OUT,"synth36.json"),"w") as fp:
    json.dump(report, fp, indent=1, ensure_ascii=False)
print(json.dumps(report, indent=1, ensure_ascii=False))
print("[SYNTH36] DONE")