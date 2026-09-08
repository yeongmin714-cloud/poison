import bpy, traceback
try:
    props = bpy.ops.export_scene.fbx.get_rna_type().properties
    print("[PROPS]", sorted(p.identifier for p in props if any(k in p.identifier for k in ('anim', 'version', 'bake'))))
except Exception:
    traceback.print_exc()
try:
    import io_scene_fbx
    print("[MOD]", io_scene_fbx.__file__)
except Exception:
    traceback.print_exc()
