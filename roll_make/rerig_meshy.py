import bpy, os
# Meshy biped 애니 FBX 전량 → 본명 표준화(Humanoid) + 클립 전용 FBX 출력
# 원본: Assets/플레이어 애니메이션/Meshy_*.fbx  →  출력: Assets/Animations/MeshyUser/*.fbx
# Meshy 리그: Hips/Spine/Spine01/Spine02/neck/Head + LeftUpLeg/LeftLeg/LeftArm/LeftForeArm 계열

MAP = {
    "Spine": "Spine",
    "Spine01": "Chest",
    "Spine02": "UpperChest",
    "neck": "Neck",
    "Head": "Head",
    "LeftUpLeg": "LeftUpperLeg",
    "LeftLeg": "LeftLowerLeg",
    "LeftFoot": "LeftFoot",
    "LeftToeBase": "LeftToes",
    "RightUpLeg": "RightUpperLeg",
    "RightLeg": "RightLowerLeg",
    "RightFoot": "RightFoot",
    "RightToeBase": "RightToes",
    "LeftShoulder": "LeftShoulder",
    "LeftArm": "LeftUpperArm",
    "LeftForeArm": "LeftLowerArm",
    "LeftHand": "LeftHand",
    "RightShoulder": "RightShoulder",
    "RightArm": "RightUpperArm",
    "RightForeArm": "RightLowerArm",
    "RightHand": "RightHand",
}
REQUIRED = ["Hips", "Spine", "Head", "LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand",
            "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand",
            "LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
            "RightUpperLeg", "RightLowerLeg", "RightFoot"]

PREFIX = "Meshy_AI_Cartoon_Villager_Boy_biped_Animation_"
SRC_DIR = r"C:/Unity/code/Assets/플레이어 애니메이션"
OUT_DIR = r"C:/Unity/code/Assets/Animations/MeshyUser"
os.makedirs(OUT_DIR, exist_ok=True)

LOOP_KEYS = ["walk", "run_", "running", "swim", "crawl", "carry", "sneaky", "spear", "idle_turn"]
NO_LOOP_KEYS = ["transition", "toss", "pitching"]

files = sorted(f for f in os.listdir(SRC_DIR) if f.lower().endswith(".fbx") and f.startswith(PREFIX))
print("[BATCH] files:", len(files))
ok_all = True

for fname in files:
    stem = fname[len(PREFIX):-4]
    if stem.endswith("_withSkin"):
        stem = stem[:-len("_withSkin")]  # 접미사 제거(빌더 참조명 일치)
    src = os.path.join(SRC_DIR, fname)
    out = os.path.join(OUT_DIR, stem + ".fbx")
    if os.path.exists(out):
        print("[SKIP]", stem)
        continue

    bpy.ops.wm.read_factory_settings(use_empty=True)
    try:
        bpy.ops.import_scene.fbx(filepath=src)
    except Exception as e:
        print("[FAIL] import", fname, e); ok_all = False; continue

    arm = next((o for o in bpy.context.scene.objects if o.type == 'ARMATURE'), None)
    if arm is None:
        print("[FAIL] no armature:", fname); ok_all = False; continue

    renamed, missing = [], []
    for old, new in MAP.items():
        if old in arm.pose.bones:
            arm.pose.bones[old].name = new
            renamed.append(old)
        else:
            missing.append(old)

    for ob in list(bpy.context.scene.objects):
        if ob.type == 'MESH':
            bpy.data.objects.remove(ob, do_unlink=True)

    ad = arm.animation_data
    if ad is None or ad.action is None:
        print("[FAIL] no action:", fname); ok_all = False; continue
    ad.action.name = stem

    names = [b.name for b in arm.data.bones]
    miss_req = [r for r in REQUIRED if r not in names]

    low = fname.lower()
    loop = any(k in low for k in LOOP_KEYS) and not any(k in low for k in NO_LOOP_KEYS)

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
    status = "OK" if not miss_req else "WARN"
    if miss_req: ok_all = False
    print(f"[{status}] {stem} renamed={len(renamed)} missing_map={len(missing)} miss_req={len(miss_req)} loop={loop} size={os.path.getsize(out)}")

print("[BATCH] all_ok:", ok_all)
