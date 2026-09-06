# fix_hierarchy.py — Player_Rigged_Heat.fbx 골격 위계를 Unity Humanoid 규칙에 맞게 수리
#  - LeftUpperLeg / RightUpperLeg : Spine → Hips 직계
#  - Neck / LeftShoulder / RightShoulder / breast.L / breast.R : Chest2 → UpperChest 직계 (Chest2는 리프로 유지)
#  - armature-space rest 변형 보존(월드 변형 불변): head/tail/roll을 armature-space 값으로 직접 저장 →
#    재부모화 후 변했으면 직접 복원 + 전 본 head/tail/roll 및 data-bone matrix_local 대조 검증
#    (주의: 이 빌드에서 edit_bone.matrix setter는 번역 성분이 유실됨 — 사용 금지, head/tail/roll 직접 할당으로 대체)
#  - export는 rerig_heat.py와 동일 옵션, 원본 경로 덮어쓰기
# 실행: blender.exe --background --python C:/Unity/code/roll_make/fix_hierarchy.py

import bpy, json

SRC = "C:/Unity/code/Assets/Resources/Models/UserProvided/fbx/Player_Rigged_Heat.fbx"
REPORT = "C:/Unity/code/roll_make/fix_hierarchy_report.json"

REQUIRED = ["Hips", "Spine", "Head", "LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand",
            "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand",
            "LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
            "RightUpperLeg", "RightLowerLeg", "RightFoot"]

MOVES = {
    "LeftUpperLeg":  "Hips",
    "RightUpperLeg": "Hips",
    "Neck":          "UpperChest",
    "LeftShoulder":  "UpperChest",
    "RightShoulder": "UpperChest",
    "breast.L":      "UpperChest",
    "breast.R":      "UpperChest",
}

TOL = 1e-5


def vec_close(a, b, tol=TOL):
    return all(abs(x - y) <= tol for x, y in zip(a, b))


def mat_flat(m):
    return [m[r][c] for r in range(4) for c in range(4)]


report = {"src": SRC, "moves": {k: {"to": v} for k, v in MOVES.items()}}

# ---------- a. clean scene + import ----------
bpy.ops.wm.read_homefile(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC)

arm = next((o for o in bpy.data.objects if o.type == 'ARMATURE'), None)
assert arm is not None, "no armature"
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
print("[FIX] armature:", arm.name, "bones:", len(arm.data.bones), "meshes:", [m.name for m in meshes])

report["before"] = {
    "bone_count": len(arm.data.bones),
    "parents": {b.name: (b.parent.name if b.parent else None) for b in arm.data.bones},
    "objects": {o.name: {"type": o.type, "parent": o.parent.name if o.parent else None}
                for o in bpy.data.objects},
}

# 변경 전 ground truth (OBJECT 모드, data bones = armature-space rest 변형)
# 주의: Blender 5.1 data Bone에는 roll 속성이 없음 → head/tail + matrix_local(roll 포함)로 검증
pre = {}
for b in arm.data.bones:
    pre[b.name] = {
        "head": tuple(b.head), "tail": tuple(b.tail),
        "mat": mat_flat(b.matrix_local),
    }

# ---------- c. edit 모드에서 재부모화 (rest 변형 보존) ----------
bpy.context.view_layer.objects.active = arm
arm.select_set(True)
for m in meshes:
    m.select_set(False)
bpy.ops.object.mode_set(mode='EDIT')

ebones = arm.data.edit_bones
missing = [n for n in list(MOVES) + list(MOVES.values()) if n and n not in ebones]
assert not missing, f"missing bones: {missing}"

moved_by_reparent = []
for name, newp in MOVES.items():
    eb = ebones[name]
    report["moves"][name]["from"] = eb.parent.name if eb.parent else None
    assert eb.parent is not None, f"{name} has no parent"
    eb.use_connect = False            # 부모 교체 시 head가 parent tail로 스냅되는 것 방지
    eb.parent = ebones[newp]
    # 재부모화만으로 변형이 변했는지 기록 (edit bone head/tail은 armature-space)
    s = pre[name]
    if not (vec_close(tuple(eb.head), s["head"]) and vec_close(tuple(eb.tail), s["tail"])):
        moved_by_reparent.append(name)

# rest 변형 직접 복원 (edit_bone.matrix setter 사용 금지 — head/tail 직접 할당, roll은 재부모화가 건드리지 않음)
restored = []
for name in moved_by_reparent:
    eb = ebones[name]
    s = pre[name]
    eb.head = s["head"]
    eb.tail = s["tail"]
    restored.append(name)
print("[FIX] moved_by_reparent:", moved_by_reparent)
print("[FIX] restored_head_tail:", restored)

# edit 모드 전체 본 rest 대조 (head/tail; roll은 matrix_local 검증이 담당)
edit_diff = {}
for e in ebones:
    s = pre[e.name]
    d = {}
    if not vec_close(tuple(e.head), s["head"]):
        d["head"] = [list(s["head"]), list(e.head)]
    if not vec_close(tuple(e.tail), s["tail"]):
        d["tail"] = [list(s["tail"]), list(e.tail)]
    if d:
        edit_diff[e.name] = d
rest_preserved_edit = (len(edit_diff) == 0)
print("[FIX] rest preserved in edit mode:", rest_preserved_edit, "diff:", edit_diff)

bpy.ops.object.mode_set(mode='OBJECT')

# data-bone matrix_local 대조 (armature-space rest 행렬, 요소별)
obj_diff = {}
for b in arm.data.bones:
    s = pre[b.name]
    now = mat_flat(b.matrix_local)
    if not vec_close(now, s["mat"], TOL):
        obj_diff[b.name] = {"max_abs_delta": max(abs(a - c) for a, c in zip(now, s["mat"]))}
rest_preserved_obj = (len(obj_diff) == 0)
print("[FIX] rest preserved (data bones matrix_local):", rest_preserved_obj, "diff:", obj_diff)

# ---------- d. 검증 ----------
bones = arm.data.bones
tree = {b.name: (b.parent.name if b.parent else None) for b in bones}


def kids(n):
    return sorted(b.name for b in bones if b.parent and b.parent.name == n)


checks = {}

n_bones = len(bones)
checks["bone_count_27"] = (n_bones == 27, n_bones)

legs_ok = all(tree.get(k) == "Hips" for k in ("LeftUpperLeg", "RightUpperLeg"))
checks["legs_direct_children_of_Hips"] = (legs_ok, {
    "LeftUpperLeg": tree.get("LeftUpperLeg"), "RightUpperLeg": tree.get("RightUpperLeg")})

upper_ok = all(tree.get(k) == "UpperChest" for k in ("Neck", "LeftShoulder", "RightShoulder", "breast.L", "breast.R"))
checks["neck_shoulders_breast_under_UpperChest"] = (upper_ok, {
    k: tree.get(k) for k in ("Neck", "LeftShoulder", "RightShoulder", "breast.L", "breast.R")})

hips_kids = kids("Hips")
checks["children_of_Hips_include_legs_and_Spine"] = (
    set(hips_kids) >= {"LeftUpperLeg", "RightUpperLeg", "Spine"}, hips_kids)

spine_kids = kids("Spine")
checks["pelvisLR_still_under_Spine"] = (
    set(spine_kids) >= {"pelvis.L", "pelvis.R"} and "LeftUpperLeg" not in spine_kids, spine_kids)

c2_kids = kids("Chest2")
checks["Chest2_is_leaf_under_UpperChest"] = (tree.get("Chest2") == "UpperChest" and len(c2_kids) == 0,
                                             {"parent": tree.get("Chest2"), "children": c2_kids})

checks["children_of_UpperChest"] = (True, kids("UpperChest"))

# 메시 버텍스그룹 보존 (본 이름과 동일 → 27개 + required 17 hit)
vg_info, vg_ok = {}, True
for m in meshes:
    vgs = [vg.name for vg in m.vertex_groups]
    hit = sum(1 for r in REQUIRED if r in vgs)
    ok = (len(vgs) == 27) and (hit == len(REQUIRED))
    vg_ok = vg_ok and ok
    vg_info[m.name] = {"count": len(vgs), "required_hit": hit, "ok": ok}
checks["mesh_vgroups_27_and_required17"] = (vg_ok, vg_info)

checks["rest_transform_preserved_editmode"] = (rest_preserved_edit, edit_diff)
checks["rest_transform_preserved_databones"] = (rest_preserved_obj, obj_diff)

report["after"] = {
    "bone_count": n_bones,
    "parents": tree,
    "children_of_Hips": hips_kids,
    "children_of_Spine": spine_kids,
    "children_of_UpperChest": kids("UpperChest"),
    "children_of_Chest2": c2_kids,
    "objects": {o.name: {"type": o.type, "parent": o.parent.name if o.parent else None}
                for o in bpy.data.objects},
}
report["checks"] = {k: {"pass": v[0], "detail": v[1]} for k, v in checks.items()}

all_pass = all(v[0] for v in checks.values())
report["all_pass"] = all_pass
print("[FIX] checks:", json.dumps({k: v[0] for k, v in checks.items()}))
print("[FIX] all_pass:", all_pass)

# ---------- e. export (rerig_heat.py와 동일 옵션, 원본 경로 덮어쓰기) ----------
if all_pass:
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(
        filepath=SRC,
        use_selection=True,
        add_leaf_bones=False,
        bake_anim=False,                    # 모델 전용 — 클립은 Player_AC(팩)이 담당
        object_types={'ARMATURE', 'MESH'},
        mesh_smooth_type='FACE',
        apply_scale_options='FBX_SCALE_ALL',
        path_mode='COPY',
        embed_textures=False,
    )
    report["exported"] = True
    report["export_path"] = SRC
    print("[FIX] exported:", SRC)
else:
    report["exported"] = False
    print("[FIX] NOT exported — checks failed, original file untouched")

with open(REPORT, "w", encoding="utf-8") as f:
    json.dump(report, f, ensure_ascii=False, indent=2)
print("[FIX] report:", REPORT)
print("[FIX] DONE")
