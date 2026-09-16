using UnityEngine;
using System.Text;

/// <summary>[69차 후속5] 리그/장비 실측 배치 스크립트 — 콘솔 anchor 튜닝용 절대 좌표 확보.
/// Unity -batchmode -executeMethod MeasureRig.Measure 로 실행, measure.log 파싱.</summary>
public static class MeasureRig
{
    public static void Measure()
    {
        var sb = new StringBuilder();
        sb.AppendLine("===MEASURE_BEGIN===");

        // ① 플레이어 FBX 실측
        var player = Resources.Load<GameObject>("Models/UserProvided/fbx/Player_Rigged");
        if (player == null) sb.AppendLine("PLAYER_LOAD_FAIL");
        else
        {
            var inst = Object.Instantiate(player);
            inst.transform.position = Vector3.zero;
            var bounds = new Bounds();
            bool any = false;
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            sb.AppendLine(any
                ? $"PLAYER_BOUNDS min={bounds.min:F3} max={bounds.max:F3} size={bounds.size:F3}"
                : "PLAYER_NO_RENDERER");
            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
                if (t.name.ToLowerInvariant().Contains("root") || t.name.ToLowerInvariant().Contains("hips"))
                    sb.AppendLine($"PLAYER_SPECIALBONE {t.name} world={t.position:F3}");
            var anim = inst.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                System.Collections.Generic.Dictionary<HumanBodyBones, string> boneMap =
                    new System.Collections.Generic.Dictionary<HumanBodyBones, string>
                {
                    { HumanBodyBones.Hips, "Hips" }, { HumanBodyBones.Spine, "Spine" },
                    { HumanBodyBones.Chest, "Chest" }, { HumanBodyBones.Head, "Head" },
                    { HumanBodyBones.LeftHand, "LeftHand" }, { HumanBodyBones.RightHand, "RightHand" },
                    { HumanBodyBones.LeftFoot, "LeftFoot" }, { HumanBodyBones.RightFoot, "RightFoot" },
                    { HumanBodyBones.LeftLowerArm, "LeftLowerArm" }, { HumanBodyBones.LeftLowerLeg, "LeftLowerLeg" },
                    { HumanBodyBones.LeftUpperLeg, "LeftUpperLeg" }, { HumanBodyBones.Neck, "Neck" },
                };
                foreach (var kv in boneMap)
                {
                    var b = anim.GetBoneTransform(kv.Key);
                    sb.AppendLine(b != null
                        ? $"PLAYER_BONE {kv.Value}={b.position:F3} name={b.name}"
                        : $"PLAYER_BONE {kv.Value}=NULL");
                }
            }
            else sb.AppendLine("PLAYER_NO_ANIMATOR");
            Object.DestroyImmediate(inst);
        }

        // ② 장비 GLB 실측 (원점 인스턴스 — bounds 크기/중심 오프셋)
        string[] gear = { "wood_helmet", "wood_armor", "wood_shield", "wood_gas_mask", "wood_chemical_pack", "wood_boot_left", "wood_glove_left" };
        foreach (var id in gear)
        {
            var prefab = Resources.Load<GameObject>($"Models/UserProvided/{id}")
                      ?? Resources.Load<GameObject>($"Models/UserProvided/{id}.glb");
            if (prefab == null) { sb.AppendLine($"GEAR {id} LOAD_FAIL"); continue; }
            var gi = Object.Instantiate(prefab);
            gi.transform.position = Vector3.zero;
            var gb = new Bounds(); bool gAny = false;
            foreach (var r in gi.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled) continue;
                if (!gAny) { gb = r.bounds; gAny = true; }
                else gb.Encapsulate(r.bounds);
            }
            sb.AppendLine(gAny
                ? $"GEAR {id} min={gb.min:F3} max={gb.max:F3} size={gb.size:F3} center={gb.center:F3}"
                : $"GEAR {id} NO_RENDERER");
            Object.DestroyImmediate(gi);
        }

        sb.AppendLine("===MEASURE_END===");
        Debug.Log(sb.ToString());
    }
}
