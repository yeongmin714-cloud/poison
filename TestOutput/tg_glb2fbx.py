import bpy, os

# GLB → FBX 변환 (Humanoid 리타겟용): Player + soldier 3종
SRC = r"C:\Unity\code\Assets\Resources\Models\UserProvided"
DST = r"C:\Unity\code\Assets\Resources\Models\UserProvided\fbx"
os.makedirs(DST, exist_ok=True)

TARGETS = [
    "Player_Rigged.glb",
    "soldier_lv1-20_rigged.glb",
    "soldier_lv20-40_rigged.glb",
    "soldier_lv40-50_rigged.glb",
]

for fname in TARGETS:
    bpy.ops.wm.read_homefile(use_empty=True)
    src = os.path.join(SRC, fname)
    bpy.ops.import_scene.gltf(filepath=src)
    # 쓰레기 오브젝트 제거: 캐릭터 메시(armature modifier 보유)+아머처만 유지
    # (사용자 GLB에 아이코스피어 '공'이 실려 있어 플레이어가 공처럼 보였던 원인)
    for obj in list(bpy.data.objects):
        keep = (obj.type == 'ARMATURE'
                or (obj.type == 'MESH' and any(m.type == 'ARMATURE' for m in obj.modifiers))
                or obj.type == 'EMPTY' and obj.parent is None)
        if not keep:
            print(f"[CONVERT] 제거: {obj.name} ({obj.type})")
            bpy.data.objects.remove(obj, do_unlink=True)
    out = os.path.join(DST, fname.replace(".glb", ".fbx"))
    # 메시+아머처 포함 FBX 내보내기 (클립 없음, 리그 보존)
    bpy.ops.export_scene.fbx(
        filepath=out,
        use_selection=False,
        add_leaf_bones=False,
        bake_anim=False,
        object_types={'ARMATURE', 'MESH'},
        mesh_smooth_type='FACE',
        apply_scale_options='FBX_SCALE_ALL',
        bake_space_transform=True,
    )
    print(f"[CONVERT] {fname} -> {os.path.basename(out)} ({os.path.getsize(out)//1024}KB)")

print("[CONVERT] DONE")
