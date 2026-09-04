import bpy, os, math
from mathutils import Vector

SRC = r"C:\Unity\code\roll_make"
PREV = os.path.join(SRC, "prev51")
os.makedirs(PREV, exist_ok=True)
FPS = 30.0

files = [r"C:\Unity\code\Assets\Animations\Mixamo\Quick Roll To Run.fbx", "Roll_Left.fbx", "Roll_Right.fbx", "Roll_Back.fbx"]
flags = ["base", "Roll_Left", "Roll_Right", "Roll_Back"]

def clear_scene():
    bpy.ops.wm.read_homefile(use_empty=True)

def setup_render(scene):
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.render.resolution_x = 320
    scene.render.resolution_y = 320
    scene.render.fps = int(FPS)
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'SINGLE'

def add_cam(scene, center, size):
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("C"))
    scene.collection.objects.link(cam)
    dist = max(size*2.6, 2.0)
    cam.location = (center.x, center.y - dist, center.z + size*0.5)
    direction = (center - cam.location).to_track_quat('-Z','Y').to_euler()
    cam.rotation_euler = direction
    scene.camera = cam
    ld = bpy.data.lights.new("L", type='SUN')
    ld.energy = 3.0
    li = bpy.data.objects.new("L", ld)
    li.rotation_euler = (math.radians(50),0,math.radians(30))
    scene.collection.objects.link(li)

def render_one(fbx, slate):
    clear_scene()
    scene = bpy.context.scene
    setup_render(scene)
    bpy.ops.import_scene.fbx(filepath=os.path.join(SRC, fbx))
    arm = next((o for o in bpy.data.objects if o.type=='ARMATURE'), None)
    if arm is None: print(f"[R]{slate} no arm"); return
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
    add_cam(scene,center,size)
    for i,fr in enumerate([0.18,0.5,0.82]):
        scene.frame_set(int(round(fr0+(fr1-fr0)*fr)))
        p = os.path.join(PREV, f"{slate}_{i}.png")
        scene.render.filepath = p
        bpy.ops.render.render(write_still=True)
    print(f"[R] {slate} done")

for fbx, slate in zip(files, flags):
    try:
        render_one(fbx, slate)
    except Exception as e:
        print(f"[R] {slate} ERROR {str(e)[:120]}")
print("[R5.1] DONE")