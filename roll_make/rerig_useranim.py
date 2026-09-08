import bpy, os, sys
# Rigify 리그 애니 FBX 8개 → 본명 표준화(Humanoid) + 클립 전용 FBX 출력
# 원본: Assets/플레이어 애니메이션/*.fbx  →  출력: Assets/Animations/MixamoUser/*.fbx
# MAP은 roll_make/rerig_heat.py와 동일(Heat 모델과 같은 리그 계열)

MAP = {
    "Root": "Hips",
    "spine": "Spine",
    "spine.001": "Chest",
    "spine.002": "UpperChest",
    "spine.003": "Chest2",
    "spine.004": "Neck",
    "spine.005": "Head",
    "shoulder.L": "LeftShoulder",
    "upper_arm.L": "LeftUpperArm",
    "forearm.L": "LeftLowerArm",
    "hand.L": "LeftHand",
    "shoulder.R": "RightShoulder",
    "upper_arm.R": "RightUpperArm",
    "forearm.R": "RightLowerArm",
    "hand.R": "RightHand",
    "thigh.L": "LeftUpperLeg",
    "shin.L": "LeftLowerLeg",
    "foot.L": "LeftFoot",
    "toe.L": "LeftToes",
    "thigh.R": "RightUpperLeg",
    "shin.R": "RightLowerLeg",
    "foot.R": "RightFoot",
    "toe.R": "RightToes",
}
REQUIRED = ["Hips", "Spine", "Head", "LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand",
            "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand",
            "LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
            "RightUpperLeg", "RightLowerLeg", "RightFoot"]

SRC_DIR = r"C:/Unity/code/Assets/플레이어 애니메이션"
OUT_DIR = r"C:/Unity/code/Assets/Animations/MixamoUser"
os.makedirs(OUT_DIR, exist_ok=True)

files = sorted(f for f in os.listdir(SRC_DIR) if f.lower().endswith(".fbx"))
print("[BATCH] files:", files)
ok_all = True

for fname in files:
    stem = os.path.splitext(fname)[0].replace(" ", "_")
    src = os.path.join(SRC_DIR, fname)
    out = os.path.join(OUT_DIR, stem + ".fbx")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=src)

    arm = next((o for o in bpy.context.scene.objects if o.type == 'ARMATURE'), None)
    if arm is None:
        print("[FAIL] no armature:", fname); ok_all = False; continue

    # 본 리네임(fcurve data path는 Blender가 자동 리맵)
    renamed, missing = [], []
    for old, new in MAP.items():
        if old in arm.pose.bones:
            arm.pose.bones[old].name = new
            renamed.append(old)
        else:
            missing.append(old)

    # 메시 제거(클립 전용) + 액션명 정리
    for ob in list(bpy.context.scene.objects):
        if ob.type == 'MESH':
            bpy.data.objects.remove(ob, do_unlink=True)
    ad = arm.animation_data
    ncurves = 0
    if ad and ad.action:
        ad.action.name = stem
        try:
            ncurves = len(ad.action.fcurves)
        except AttributeError:
            ncurves = -1  # Blender 5.x slotted action — 액션 존재 자체가 애니 존재 증거
    if ad is None or ad.action is None:
        print("[FAIL] no action:", fname); ok_all = False; continue

    # 검증
    names = [b.name for b in arm.data.bones]
    miss_req = [r for r in REQUIRED if r not in names]

    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=out,
        use_selection=True,
        add_leaf_bones=False,
        bake_anim=True,
        object_types={'ARMATURE'},
    )

    status = "OK" if (not miss_req and ncurves > 0) else "WARN"
    if status != "OK": ok_all = False
    print(f"[{status}] {fname} -> {os.path.basename(out)} renamed={len(renamed)} missing_map={missing} missing_req={miss_req} fcurves={ncurves} size={os.path.getsize(out)}")

print("[BATCH] all_ok:", ok_all)
