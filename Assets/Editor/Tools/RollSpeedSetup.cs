using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// [135차 P4] 롤 속도 등화 — Roll/RollBack 상태 재생 속도를 롤 이동 지속(0.5s)에 맞춤.
/// 근거: 이동 속도는 전 방향 동일(walkSpeed×3)하나 앞/뒤 롤은 클립 네이티브(Roll_Dodge ≈1.83s) 재생이라
/// 측면 롤(0.5s 스핀)보다 느리게 지각 — 사용자 요청 "옆구르기 속도로 앞과 뒤도 맞춰줘".
/// speed = clip.length / 0.5f (PlayerMovement._rollDuration 직렬 기본값 0.5 — [132차] 롤 지속 계약).
/// 실행: Unity.exe -batchmode -projectPath C:/Unity/code -executeMethod RollSpeedSetup.Run -quit
/// </summary>
public static class RollSpeedSetup
{
    private const float RollDuration = 0.5f;   // [132차] PlayerMovement._rollDuration 기본값 — 롤 이동 지속

    public static void Run()
    {
        const string ctrlPath = "Assets/Resources/Animation/Controllers/Player_AC.controller";
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
        if (ctrl == null) { Debug.Log("[135차 P4] 컨트롤러 로드 실패"); return; }

        int done = 0;
        foreach (var layer in ctrl.layers)
        {
            foreach (var child in layer.stateMachine.states)
            {
                var st = child.state;
                if (st == null || st.motion == null) continue;
                if (st.name != "Roll" && st.name != "RollBack") continue;
                var clip = st.motion as AnimationClip;
                if (clip == null)
                {
                    // 클립이 직접이 아닌 경우(블렌드트리 등) — 스킵 기록
                    Debug.Log($"[135차 P4] {st.name}: motion={st.motion.name}(AnimationClip 아님) 스킵");
                    continue;
                }
                float speed = Mathf.Round(clip.length / RollDuration * 100f) / 100f;
                float old = st.speed;
                st.speed = speed;
                done++;
                Debug.Log($"[135차 P4] {st.name}: clip={clip.name} length={clip.length:F3}s speed {old:F2}→{speed:F2} (0.5s 등화)");
            }
        }
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        Debug.Log($"[135차 P4] DONE {done}건");
    }
}
