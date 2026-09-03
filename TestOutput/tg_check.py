import bpy
bpy.ops.wm.read_homefile(use_empty=True)
bpy.ops.import_scene.fbx(filepath=r"C:\Unity\code\Assets\Resources\Models\UserProvided\fbx\Player_Rigged.fbx")
for o in bpy.data.objects:
    if o.type == "MESH":
        print("[CHECK] MESH", o.name, "verts=", len(o.data.vertices), "parent=", o.parent.name if o.parent else None, "modifiers=", [m.type for m in o.modifiers])
    elif o.type == "ARMATURE":
        print("[CHECK] ARMATURE", o.name, "bones=", len(o.data.bones), "scale=", tuple(round(v,3) for v in o.scale))
