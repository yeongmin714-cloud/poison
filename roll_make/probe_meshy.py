import bpy, sys
argv = sys.argv[sys.argv.index("--")+1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=argv[0])
for ob in bpy.context.scene.objects:
    print("OBJ", ob.type, ob.name)
    if ob.type == 'ARMATURE':
        names = [b.name for b in ob.data.bones]
        print("BONES(%d):" % len(names), ",".join(names[:40]))
        ad = ob.animation_data
        print("ACTION:", ad.action.name if ad and ad.action else None)
    if ob.type == 'MESH':
        print("MESH_OK", ob.name, "vg:", len(ob.vertex_groups))
