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
    out = os.path.join(DST, fname.replace(".glb", ".fbx"))
    # 메시+아머처 포함 FBX 내보내기 (클립 없음, 리그 보존)
    bpy.ops.export_scene.fbx(
        filepath=out,
        use_selection=False,
        add_leaf_bones=False,
        bake_anim=False,
        object_types={'ARMATURE', 'MESH'},
        mesh_smooth_type='FACE',
    )
    print(f"[CONVERT] {fname} -> {os.path.basename(out)} ({os.path.getsize(out)//1024}KB)")

print("[CONVERT] DONE")
