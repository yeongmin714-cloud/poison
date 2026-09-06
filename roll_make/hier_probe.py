import bpy, json
SRC = "C:/Unity/code/Assets/Resources/Models/UserProvided/fbx/Player_Rigged_Heat.fbx"
bpy.ops.wm.read_homefile(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC)
arm = next((o for o in bpy.data.objects if o.type == 'ARMATURE'), None)
out = {"armature": arm.name if arm else None}
if arm:
    bones = arm.data.bones
    out["bone_count"] = len(bones)
    # 전체 위계: name -> parent
    tree = {}
    for b in bones:
        tree[b.name] = b.parent.name if b.parent else None
    out["parents"] = tree
    # 핵심 판정: Hips/Spine의 자식, 다리의 부모
    def kids(n):
        return [b.name for b in bones if (b.parent and b.parent.name == n)]
    out["children_of_Hips"] = kids("Hips")
    out["children_of_Spine"] = kids("Spine")
    for key in ["Hips", "Spine", "LeftUpperLeg", "RightUpperLeg", "LeftFoot", "Chest"]:
        p = tree.get(key, "ABSENT")
        out[f"parent_of_{key}"] = p
    # 오브젝트 레벨: 메시/아머처 부모
    out["objects"] = [{"name": o.name, "type": o.type,
                       "parent": o.parent.name if o.parent else None} for o in bpy.data.objects]
    # rest pose 샘플(바인드포즈 판정용): LeftUpperLeg 로컬 회전
    def rpy(bname):
        b = bones.get(bname)
        if not b:
            return None
        import math
        r = b.matrix_local.to_euler('XYZ')
        return [round(math.degrees(a), 1) for a in r]
    out["rest_euler"] = {k: rpy(k) for k in ["Hips", "Spine", "LeftUpperLeg", "LeftUpperArm", "LeftFoot"]}
print(json.dumps(out, indent=1, ensure_ascii=False))
print("[HIER] DONE")
