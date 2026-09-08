import bpy, sys
argv = sys.argv[sys.argv.index("--")+1:]
src = argv[0]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
print("[PROBE] objects:")
for ob in bpy.context.scene.objects:
    print("  OBJ", ob.type, ob.name)
    if ob.type == 'ARMATURE':
        names = [b.name for b in ob.data.bones]
        print("  BONES(%d):" % len(names), names)
        ad = ob.animation_data
        print("  ACTION:", ad.action.name if ad and ad.action else None)
        if ad and ad.nla_tracks:
            for t in ad.nla_tracks:
                for s in t.strips:
                    print("  NLA strip:", s.name, "action:", s.action.name if s.action else None)
