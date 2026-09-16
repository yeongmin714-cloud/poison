using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// [TEST28-69차] 활 idle/run 클립 배선(원샷 에디터 스크립트 — batchmode -executeMethod로 실행).
/// 사용자 제공 클립: Assets/플레이어 애니메이션/Idle_Holding_Bow + Run_Forward_with_Bow.
/// - BowIdle 신규 상태(Idle_Holding_Bow) — 활 장착+정지 시 활 든 대기(장착 시 애니 변화 확보)
/// - BowRunF 신규 상태(Run_Forward_with_Bow) — 활 들고 뛰기
/// - BowAimedF는 활 걷기(Walk_Forward_with_Bow_Aimed) 유지, 67차 BowAimedF→Idle 전이를 BowIdle 목적지로 재지정
/// - 전이: Idle→BowIdle(IsBow+Speed&lt;0.2, 선두) / BowIdle→BowAimedF(IsBow+Speed&gt;0.55) /
///         BowIdle→Idle(IsBow ifNot — 해제 안전) / BowAimedF→BowRunF(Speed&gt;5.0) /
///         BowRunF→BowAimedF(Speed&lt;4.2) / BowRunF→BowIdle(Speed&lt;0.2)
/// 멱등 설계 — 재실행 시 중복 생성하지 않는다.
/// </summary>
public static class BowClipWiring
{
    const string CtrlPath = "Assets/Resources/Animation/Controllers/Player_AC.controller";
    const string IdleFbx = "Assets/플레이어 애니메이션/Meshy_AI_Cartoon_Villager_Boy_biped_Animation_Idle_Holding_Bow_withSkin.fbx";
    const string RunFbx = "Assets/플레이어 애니메이션/Meshy_AI_Cartoon_Villager_Boy_biped_Animation_Run_Forward_with_Bow_withSkin.fbx";
    const float RunEnterSpeed = 5.0f;   // 런 진입(Speed 파라미터 — 걷기 ~3.x, 뛰기 ~6.x, Play 판정 후 조정)
    const float RunExitSpeed = 4.2f;    // 런 이탈(히스테리시스)

    public static void Wire()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath);
        if (ctrl == null) { Debug.LogError("[BowClipWiring] 컨트롤러 로드 실패: " + CtrlPath); Fail(); return; }

        var idleClip = FindClip(IdleFbx, "Idle");
        var runClip = FindClip(RunFbx, "Run");
        if (idleClip == null || runClip == null) { Debug.LogError($"[BowClipWiring] 클립 로드 실패 idle={idleClip} run={runClip}"); Fail(); return; }
        Debug.Log($"[BowClipWiring] 클립 로드: idle='{idleClip.name}' run='{runClip.name}'");

        var sm = ctrl.layers[0].stateMachine;
        var bowAimed = FindState(sm, "BowAimedF");
        var idleState = FindState(sm, "Idle");
        if (bowAimed == null || idleState == null) { Debug.LogError("[BowClipWiring] BowAimedF/Idle 상태 미발견"); Fail(); return; }

        // ① BowIdle 상태(멱등)
        var bowIdle = FindState(sm, "BowIdle");
        if (bowIdle == null)
        {
            bowIdle = sm.AddState("BowIdle", new Vector3(320f, -60f, 0f));
            Debug.Log("[BowClipWiring] BowIdle 상태 생성");
        }
        bowIdle.motion = idleClip;
        bowIdle.writeDefaultValues = true;

        // ② BowRunF 상태(멱등)
        var bowRun = FindState(sm, "BowRunF");
        if (bowRun == null)
        {
            bowRun = sm.AddState("BowRunF", new Vector3(320f, 60f, 0f));
            Debug.Log("[BowClipWiring] BowRunF 상태 생성");
        }
        bowRun.motion = runClip;
        bowRun.writeDefaultValues = true;

        // ③ 67차 BowAimedF→Idle(Speed<0.2) 전이를 BowIdle 목적지로 재지정
        foreach (var t in bowAimed.transitions)
        {
            if (t.destinationState != null && t.destinationState.name == "Idle"
                && HasCondition(t, "Speed", AnimatorConditionMode.Less))
            {
                t.destinationState = bowIdle;
                Debug.Log("[BowClipWiring] BowAimedF→Idle 전이 → BowIdle 목적지로 재지정(정지 시 활 든 대기)");
            }
        }

        // ④ 전이 추가(멱등 — 동일 목적지+조건 존재 시 스킵)
        AddT(sm, bowIdle, bowAimed, ("IsBow", AnimatorConditionMode.If, 0f), ("Speed", AnimatorConditionMode.Greater, 0.55f));
        AddT(sm, bowIdle, idleState, ("IsBow", AnimatorConditionMode.IfNot, 0f));
        AddT(sm, bowAimed, bowRun, ("Speed", AnimatorConditionMode.Greater, RunEnterSpeed));
        AddT(sm, bowRun, bowAimed, ("Speed", AnimatorConditionMode.Less, RunExitSpeed));
        AddT(sm, bowRun, bowIdle, ("Speed", AnimatorConditionMode.Less, 0.2f));

        // ⑤ Idle→BowIdle(IsBow+Speed<0.2) — Idle 전이 목록 선두 삽입(활 장착+정지 즉시 변화)
        var existing = idleState.transitions.FirstOrDefault(t =>
            t.destinationState != null && t.destinationState.name == "BowIdle");
        if (existing == null)
        {
            var tNew = NewTransition(("IsBow", AnimatorConditionMode.If, 0f), ("Speed", AnimatorConditionMode.Less, 0.2f));
            tNew.destinationState = bowIdle;
            var current = idleState.transitions;
            var list = new AnimatorStateTransition[current.Length + 1];
            list[0] = tNew;
            for (int i = 0; i < current.Length; i++) list[i + 1] = current[i];
            idleState.transitions = list;   // 선두 배치 — 활 장착 정지 시 일반 Walk 전이보다 우선
            Debug.Log("[BowClipWiring] Idle→BowIdle 전이 추가(선두)");
        }

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        Debug.Log("[BowClipWiring] ✅ 배선 완료 — BowIdle/BowRunF 상태 + 전이 5종(멱등)");
        AssetDatabase.Refresh();
        EditorApplication.Exit(0);
    }

    static AnimationClip FindClip(string path, string keyword)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var o in all)
        {
            var c = o as AnimationClip;
            if (c != null && c.name.Contains(keyword)) return c;
        }
        return all?.OfType<AnimationClip>().FirstOrDefault();
    }

    static AnimatorState FindState(AnimatorStateMachine sm, string name)
        => sm.states.FirstOrDefault(s => s.state != null && s.state.name == name).state;

    static bool HasCondition(AnimatorStateTransition t, string param, AnimatorConditionMode mode)
        => t.conditions.Any(c => c.parameter == param && c.mode == mode);

    static AnimatorStateTransition NewTransition(params (string param, AnimatorConditionMode mode, float threshold)[] conditions)
    {
        var t = new AnimatorStateTransition
        {
            hasExitTime = false,
            hasFixedDuration = true,
            duration = 0.15f,
            canTransitionToSelf = false,
        };
        foreach (var (param, mode, threshold) in conditions)
            t.AddCondition(mode, threshold, param);
        return t;
    }

    static void AddT(AnimatorStateMachine sm, AnimatorState from, AnimatorState to, params (string param, AnimatorConditionMode mode, float threshold)[] conditions)
    {
        foreach (var t in from.transitions)
        {
            if (t.destinationState != to) continue;
            bool same = conditions.All(c => t.conditions.Any(x => x.parameter == c.param && x.mode == c.mode && Mathf.Approximately(x.threshold, c.threshold)));
            if (same && t.conditions.Length == conditions.Length) return;   // 멱등
        }
        var tNew = NewTransition(conditions);
        tNew.destinationState = to;
        from.AddTransition(tNew);
        Debug.Log($"[BowClipWiring] 전이 추가: {from.name} → {to.name} ({string.Join(", ", conditions.Select(c => $"{c.param}:{c.mode}:{c.threshold}"))})");
    }

    static void Fail() => EditorApplication.Exit(1);
}
