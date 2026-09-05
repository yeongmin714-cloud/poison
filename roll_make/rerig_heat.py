# Rigify(Blender) 리그 → Unity Humanoid(믹사모 히트 계열) 리그 변환
# Player_Rigged.glb → Player_Rigged_Heat.fbx (본 리네임만, 메시/스킨 보존)
# 실행: blender-3.6.0-linux-x64/blender --background --python rerig_heat.py
# 검증: 뼈 이름 매핑률 + Hips/Head/HandL/R 존재 + 메시 버텍스 그룹 보존 수

import bpy, json, sys

SRC = "/mnt/c/Unity/code/Assets/Resources/Models/UserProvided/Player_Rigged.glb"
OUT = "/mnt/c/Unity/code/roll_make/Player_Rigged_Heat.fbx"
REPORT = "/mnt/c/Unity/code/roll_make/rerig_report.json"

# ---------- Clean scene ----------
bpy.ops.wm.read_factory_settings(use_empty=True)

# ---------- Import GLB ----------
bpy.ops.import_scene.gltf(filepath=SRC)
objs = bpy.context.scene.objects
arm = None
for o in objs:
    if o.type == 'ARMATURE':
        arm = o
        break
assert arm is not None, "no armature"
bpy.context.view_layer.objects.active = arm
print("[RIG] armature:", arm.name, "bones:", len(arm.data.bones))

# ---------- Rename map (Rigify/GLB → Unity-Heat(믹사모 계열)) ----------
# GLB joint 27: Root, spine~spine.005, shoulder.L/R, upper_arm.L/R, forearm.L/R, hand.L/R,
#               breast.L/R, pelvis.L/R, thigh.L/R, shin.L/R, foot.L/R, toe.L/R
# Unity Humanoid 필수: Hips, Spine, Chest, Head, LeftShoulder, LeftUpperArm, LeftLowerArm,
#                      LeftHand, LeftUpperLeg, LeftLowerLeg, LeftFoot, (+Right 동일)
# 목표 매핑(안전판: Unity가 자동 매핑하는 표준 히트 이름 사용):
#   Root → Hips
#   spine → Spine, spine.001 → Chest, spine.002 → UpperChest
#   spine.003/004/005 → Neck 방향 후보지만 목/머리 본이 없다 → spine.003을 Chest2로 두고
#     Head는 신규 생성 불가(메시 없음) → 대안: shoulder.L/R 위 상위가 spine.003 → Unity가
#     Head 없이도 매핑 가능(Head/Trees는 옵션) → spine.003은 UpperChest2 대신 'Neck'으로 지정해
#     Head 부재를 완화. 단 정확히는: Unity 필수는 Hips+Spine+Head+Legs+Arms. Head 필수!
#     → 해결: spine.005(목 위 끝뼈)를 Head로 매핑. 그러면 목-머리 없이도 Head 역할을 함.
#   shoulder.L → LeftShoulder, upper_arm.L → LeftUpperArm, forearm.L → LeftLowerArm, hand.L → LeftHand
#   thigh.L → LeftUpperLeg, shin.L → LeftLowerLeg, foot.L → LeftFoot, toe.L → LeftToes
#   breast.L/R → 무매핑 버림(이름 유지, Humanoid 매핑 제외)
#   pelvis.L/R → Unity 필수 아님(무매핑 유지) — Hips는 Root로부터.
MAP = {
    "Root": "Hips",
    "spine": "Spine",
    "spine.001": "Chest",
    "spine.002": "UpperChest",
    "spine.003": "Chest2",          # Humanoid 무매핑 잔여(상부 흉추)
    "spine.004": "Neck",            # 자동 매핑 후보
    "spine.005": "Head",            # 상부 끝뼈를 Head로
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
KEEP = {"breast.L", "breast.R", "pelvis.L", "pelvis.R"}  # 무매핑 유지

# ---------- Apply rename (edit-bone level) ----------
bpy.context.view_layer.objects.active = arm
renamed, missing = [], []
for old, new in MAP.items():
    if old in arm.pose.bones:
        pb = arm.pose.bones[old]
        pb.name = new
        renamed.append((old, new))
    else:
        missing.append(old)
# 메시 버텍스 그룹 자동 추적: 뼈 이름이 바뀌면 blend 데이터는 참조 유지(pose bone rename은 mesh vgroup 이름을 따라가지 않음!)
# → 메시 vgroup도 함께 리네임
for ob in bpy.context.scene.objects:
    if ob.type == 'MESH':
        for old, new in renamed:
            vg = ob.vertex_groups.get(old)
            if vg:
                vg.name = new

# ---------- Verify ----------
print("[RIG] renamed:", len(renamed), "missing:", missing)
names = [b.name for b in arm.data.bones]
required = ["Hips", "Spine", "Head", "LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand",
            "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand",
            "LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
            "RightUpperLeg", "RightLowerLeg", "RightFoot"]
missing_req = [r for r in required if r not in names]
print("[RIG] missing_required:", missing_req if (missing_req:=missing_req) else missing_req) if False else None
print("[RIG] missing_required:", missing_req) if False else None

mesh_ok = True
for ob in bpy.context.scene.objects:
    if ob.type == 'MESH':
        vgs = {vg.name for vg in ob.vertex_groups}
        hit = sum(1 for r in required if r in vgs)
        print(f"[RIG] mesh {ob.name}: vertex_groups={len(ob.vertex_groups)} required_hit={hit}/{len(required)}")
        if hit < 10:
            mesh_ok = False

# ---------- Export FBX (armature+mesh, no anim — this is the model, not a clip) ----------
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(
    filepath=OUT,
    use_selection=True,
    add_leaf_bones=False,
    bake_anim=False,             # 모델 전용 — 클립은 Player_AC(팩)이 담당
    object_types={'ARMATURE', 'MESH'},
    mesh_smooth_type='FACE',
    apply_scale_options='FBX_SCALE_ALL',
    path_mode='COPY',
    embed_textures=False,
)
print("[RIG] exported:", OUT)

json.dump({
    "renamed": renamed, "missing_in_source": missing,
    "missing_required": missing_req if 'missing_req' in dir() else [],
    "mesh_ok": mesh_ok, "bones_final": names,
}, open(REPORT, "w"), ensure_ascii=False, indent=2)
print("[RIG] report:", REPORT)
