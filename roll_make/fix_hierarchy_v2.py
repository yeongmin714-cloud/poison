import bpy, json, math

SRC = "C:/Unity/code/Assets/Resources/Models/UserProvided/fbx/Player_Rigged_Heat.fbx"
TMP = "C:/Unity/code/roll_make/Player_Rigged_Heat_reparent.fbx"
REQUIRED = ["Hips", "Spine", "Chest", "UpperChest", "Neck", "Head",
            "LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand",
            "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand",
            "LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
            "RightUpperLeg", "RightLowerLeg", "RightFoot"]
# (LeftToes/RightToes는 필수 아님 — 팩 17본 기준 toes 제외 목록과 정합, 있으면 더 좋음)

def snapshot():
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    bones = {}
    for b in arm.data.bones:
        bones[b.name] = [tuple(b.head_local), tuple(b.tail_local)]
    mesh = next(o for o in bpy.data.objects if o.type == 'MESH')
    vgs = [vg.name for vg in mesh.vertex_groups]
    return arm, bones, vgs, mesh

bpy.ops.wm.read_homefile(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC)
arm0, rest0, vgs0, mesh0 = snapshot()
rep = {"import_vgroups": len(vgs0),
       "missing_required_vgroups": [r for r in REQUIRED if r not in vgs0]}

def parents(a):
    return {b.name: (b.parent.name if b.parent else None) for b in a.data.bones}
rep["before_parents"] = parents(arm0)

# ── 재부모화 (edit 모드, use_connect 해제 + parent 할당만 — head/tail 무손상) ──
bpy.context.view_layer.objects.active = arm0
bpy.ops.object.mode_set(mode='EDIT')
eb = {b.name: b for b in arm0.data.edit_bones}
MOVES = {"LeftUpperLeg": "Hips", "RightUpperLeg": "Hips",
         "Neck": "UpperChest", "LeftShoulder": "UpperChest",
         "RightShoulder": "UpperChest", "breast.L": "UpperChest", "breast.R": "UpperChest"}
for name, newp in MOVES.items():
    eb[name].use_connect = False
    eb[name].parent = eb[newp]
bpy.ops.object.mode_set(mode='OBJECT')

arm1, rest1, vgs1, mesh1 = snapshot()
rest_identical = all(rest0[k] == rest1[k] for k in rest0)
rep["rest_head_tail_identical"] = rest_identical
rep["vgroups_unchanged"] = (vgs0 == vgs1)
rep["after_parents"] = parents(arm1)
topo_ok = (arm1.data.bones["LeftUpperLeg"].parent.name == "Hips"
           and arm1.data.bones["RightUpperLeg"].parent.name == "Hips"
           and arm1.data.bones["Neck"].parent.name == "UpperChest"
           and arm1.data.bones["Chest2"].parent.name == "UpperChest"
           and len(arm1.data.bones) == 27)
rep["topology_ok"] = topo_ok
print("[V2] rest identical:", rest_identical, "| vgroups unchanged:", vgs0 == vgs1,
      "| missing required:", rep["missing_required_vgroups"])

if not (rest_identical and vgs0 == vgs1 and topo_ok):
    rep["abort"] = "pre-export verification failed"
    json.dump(rep, open("C:/Unity/code/roll_make/fix2_report.json", "w"), indent=1)
    print("[V2] ABORT")
    raise SystemExit(1)

# ── 임시 경로 export (원본 보호) ──
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=TMP, use_selection=True, add_leaf_bones=False,
                         bake_anim=False, object_types={'ARMATURE', 'MESH'},
                         mesh_smooth_type='FACE', apply_scale_options='FBX_SCALE_ALL',
                         path_mode='COPY', embed_textures=False)

# ── roundtrip 검증: export본 재임포트 ──
bpy.ops.wm.read_homefile(use_empty=True)
bpy.ops.import_scene.fbx(filepath=TMP)
arm2, rest2, vgs2, mesh2 = snapshot()
rep["rt_parents"] = parents(arm2)
rep["rt_bone_count"] = len(arm2.data.bones)
rep["rt_vgroups"] = len(vgs2)
rep["rt_vgroups_missing_required"] = [r for r in REQUIRED if r not in vgs2]
def maxdelta(a, b):
    m = 0.0
    for k in a:
        if k in b:
            for (h0, t0), (h1, t1) in [(a[k], b[k])]:
                m = max(m, max(abs(x - y) for x, y in zip(h0, h1)),
                        max(abs(x - y) for x, y in zip(t0, t1)))
    return m
rep["rt_max_head_tail_delta"] = round(maxdelta(rest1, rest2), 6)
rt_ok = (rep["rt_bone_count"] == 27
         and arm2.data.bones["LeftUpperLeg"].parent.name == "Hips"
         and arm2.data.bones["Neck"].parent.name == "UpperChest"
         and not rep["rt_vgroups_missing_required"]
         and rep["rt_max_head_tail_delta"] < 0.001)
rep["roundtrip_ok"] = rt_ok
json.dump(rep, open("C:/Unity/code/roll_make/fix2_report.json", "w"), indent=1)
print("[V2] ROUNDTRIP", "PASS" if rt_ok else "FAIL",
      "| bones:", rep["rt_bone_count"], "| vgroups:", rep["rt_vgroups"],
      "| delta:", rep["rt_max_head_tail_delta"],
      "| missing_required:", rep["rt_vgroups_missing_required"])
print("[V2] DONE")
