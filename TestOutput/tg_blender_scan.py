import bpy, json, os, math
from mathutils import Vector

SRC = r"C:\Unity\code\Assets\Animations\Mixamo"
OUT = r"C:\Unity\code\TestOutput\tganalysis"
PREV = os.path.join(OUT, "prev")
os.makedirs(PREV, exist_ok=True)

FPS = 30.0
RES = 320

def clear_scene():
    bpy.ops.wm.read_homefile(use_empty=True)

def setup_render(scene):
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.render.resolution_x = RES
    scene.render.resolution_y = RES
    scene.render.fps = int(FPS)
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'SINGLE'
    scene.display.shading.show_backface_culling = False

def add_camera_light(scene, center, size):
    cam_data = bpy.data.cameras.new("Cam")
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    dist = max(size * 2.6, 2.0)
    cam.location = (center.x, center.y - dist, center.z + size * 0.5)
    direction = Vector((center.x, center.y, center.z)) - cam.location
    cam.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
    scene.camera = cam
    light_data = bpy.data.lights.new("L", type='SUN')
    light_data.energy = 3.0
    light = bpy.data.objects.new("L", light_data)
    scene.collection.objects.link(light)
    light.rotation_euler = (math.radians(50), 0, math.radians(30))
    return cam

def render_frames(scene, arm, fname_safe, fr0, fr1):
    paths = []
    span = fr1 - fr0
    for i, frac in enumerate([0.0, 0.25, 0.5, 0.75]):
        scene.frame_set(int(round(fr0 + span * frac)))
        p = os.path.join(PREV, f"{fname_safe}_{i}.png")
        scene.render.filepath = p
        bpy.ops.render.render(write_still=True)
        paths.append(p)
    return paths

clear_scene()
files = sorted(f for f in os.listdir(SRC) if f.lower().endswith(".fbx"))
report = []

for fname in files:
    clear_scene()
    scene = bpy.context.scene
    setup_render(scene)
    entry = {"file": fname}
    try:
        bpy.ops.import_scene.fbx(filepath=os.path.join(SRC, fname))
        arm = next((o for o in bpy.data.objects if o.type == 'ARMATURE'), None)
        if arm is None:
            entry["error"] = "no armature"
            report.append(entry)
            continue
        entry["bones"] = len(arm.pose.bones)
        ad = arm.animation_data
        act = ad.action if ad and ad.action else None
        if act is None:
            entry["error"] = "no action"
            report.append(entry)
            continue
        fr0, fr1 = act.frame_range
        entry["clip"] = act.name
        entry["frames"] = [round(fr0, 1), round(fr1, 1)]
        entry["duration_s"] = round((fr1 - fr0) / FPS, 2)

        # 본별 최대 회전 변화량 (첫 프레임 대비) — 모션 시그니처
        def pose_at(f):
            scene.frame_set(int(round(f)))
            return {pb.name: pb.rotation_quaternion.copy() for pb in arm.pose.bones}

        p0 = pose_at(fr0)
        maxdelta = {}
        steps = max(4, min(24, int(fr1 - fr0)))
        for i in range(steps + 1):
            f = fr0 + (fr1 - fr0) * i / steps
            for name, q in pose_at(f).items():
                if name in p0:
                    d = math.degrees(q.rotation_difference(p0[name]).angle)
                    if d > maxdelta.get(name, 0):
                        maxdelta[name] = round(d, 1)
        top = sorted(maxdelta.items(), key=lambda x: -x[1])[:6]
        entry["top_bones_deg"] = top

        # 렌더: 카메라를 캐릭터에 맞춰 배치
        meshes = [o for o in bpy.data.objects if o.type == 'MESH']
        mn = Vector((1e9, 1e9, 1e9)); mx = Vector((-1e9, -1e9, -1e9))
        for o in meshes:
            for corner in o.bound_box:
                wc = o.matrix_world @ Vector(corner)
                mn = Vector((min(mn.x, wc.x), min(mn.y, wc.y), min(mn.z, wc.z)))
                mx = Vector((max(mx.x, wc.x), max(mx.y, wc.y), max(mx.z, wc.z)))
        center = (mn + mx) / 2
        size = max((mx - mn).length, 1.0)
        add_camera_light(scene, center, size)
        safe = "".join(ch if ch.isalnum() else "_" for ch in fname[:-4])[:40]
        entry["preview"] = render_frames(scene, arm, safe, fr0, fr1)
        entry["ok"] = True
    except Exception as e:
        entry["error"] = str(e)[:200]
    report.append(entry)
    print(f"[SCAN] {fname}: {entry.get('error') or 'ok ' + str(entry.get('top_bones_deg', [])[:3])}")

with open(os.path.join(OUT, "report.json"), "w", encoding="utf-8") as fp:
    json.dump(report, fp, ensure_ascii=False, indent=1)
print(f"[SCAN] DONE: {len(report)} files -> report.json")
