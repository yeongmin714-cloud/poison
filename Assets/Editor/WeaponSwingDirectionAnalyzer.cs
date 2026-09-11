using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// #10: Weapon_Combo_2 스윙 방향 실측 에디터 분석기 v2 (전역 namespace — QaValidator와 동일).
///
/// 목적: HumanoidClipDriver.FireComboSlash의 하드코딩 스윙 각도(1타 -30° / 2타 +35° / 3타 roll -90°)를
/// 실제 Weapon_Combo_2.fbx 클립의 손 궤적으로 검증·교체하기 위한 실측 데이터 수집.
///
/// v1의 AnimationUtility.GetCurveBindings 경로는 휴머노이드 클립(머슬 커브)에서 회전 바인딩 0개로 실패함(compile.log 실측).
/// v2는 PlayableGraph 휴머노이드 샘플링으로 전환 — 배치모드에서도 Animator CPU 평가만으로 동작(렌더링 불필요):
///   1) LoadAllAssetsAtPath(FBX) → AnimationClip(이름 contains "Weapon_Combo_2", __preview__ 제외) + Avatar.
///   2) FBX 루트 GameObject를 임시 인스턴스화("SwingSampler") — 뼈 계층이 있어야 GetBoneTransform이 유효.
///      HideFlags.HideAndDontSave + cullingMode=AlwaysAnimate(카메라 없는 배치에서도 평가 보장), applyRootMotion=false.
///   3) PlayableGraph(Manual) + AnimationClipPlayable → SetTime(t) 후 graph.Evaluate(0)로 정확 시간 샘플링.
///   4) 136프레임(30fps, 4.533s) 전체를 1프레임 간격으로 RightHand world pos/rot 기록
///      (null이면 RightLowerArm → LeftHand 폴백. GetBoneTransform은 휴머노이드 매핑 기반 리타깃 자세 반환).
///   5) 스테이지별(normT 0~0.331 / 0.331~0.676 / 0.676~1.0) 임팩트 후보(normT 0.18/0.53/0.84) 중심 ±0.15s 구간의
///      인접 샘플 (p2-p1) 정규화 접선 벡터 누적·정규화 → 캐릭터 로컬(yaw=Atan2(t.x,t.z), pitch=Asin(t.y)).
///      캐릭터 forward=+Z, 임시 GO 회전 identity → world == character local.
///   6) 고정 포맷 RESULT 라인 출력: "[SwingDir] RESULT stage1 yaw=XX.X pitch=YY.Y ..." (배치 로그 파싱용).
///
/// 무인 실행 (부모 자동화 — 배치 모드에서 Debug.Log가 stdout으로 출력됨):
///   Unity.exe -quit -batchmode -projectPath &lt;path&gt; -executeMethod WeaponSwingDirectionAnalyzer.AnalyzeWeaponCombo2
/// 수동 실행: 메뉴 Tools → VFX → Analyze Weapon_Combo_2 Swing Direction
/// </summary>
public static class WeaponSwingDirectionAnalyzer
{
    private const string FbxPath = "Assets/Animations/MeshyUser/Weapon_Combo_2.fbx";

    /// <summary>HumanoidClipDriver.ComboEndNormT와 동일한 스테이지 경계 (45f/92f). 3타는 클립 끝(1.0).</summary>
    private static readonly float[] StageEndNormT = { 0.331f, 0.676f, 1f };

    /// <summary>스테이지별 임팩트 후보 normT (스윙 방향 접선 측정 중심).</summary>
    private static readonly float[] ImpactNormT = { 0.18f, 0.53f, 0.84f };

    /// <summary>임팩트 중심 ± 윈도우 (초).</summary>
    private const float ImpactWindowSec = 0.15f;

    [MenuItem("Tools/VFX/Analyze Weapon_Combo_2 Swing Direction")]
    public static void AnalyzeWeaponCombo2()
    {
        Debug.Log("[SwingDir] ────────── Weapon_Combo_2 스윙 방향 실측 시작 (v2 PlayableGraph 휴머노이드 샘플링) ──────────");

        // 1) FBX 서브에셋 → AnimationClip + Avatar
        AnimationClip clip = null;
        Avatar avatar = null;
        GameObject fbxRoot = null;
        var all = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
        if (all == null || all.Length == 0)
        {
            Debug.LogError($"[SwingDir] ❌ 에셋 없음: {FbxPath} — 경로/임포트 상태 확인 필요");
            FinishOk();
            return;
        }

        foreach (var o in all)
        {
            if (o is AnimationClip c && c != null && !c.name.Contains("__preview__") && c.name.Contains("Weapon_Combo_2"))
                clip = clip == null || c.length > clip.length ? c : clip;
            if (o is Avatar av && av != null && avatar == null) avatar = av;
            if (o is GameObject go && fbxRoot == null) fbxRoot = go;
        }

        if (clip == null)
        {
            // 이름 매칭 실패 시 가장 긴 클립 폴백
            foreach (var o in all)
                if (o is AnimationClip c && c != null && !c.name.Contains("__preview__"))
                    clip = clip == null || c.length > clip.length ? c : clip;
        }
        if (clip == null)
        {
            Debug.LogError($"[SwingDir] ❌ AnimationClip 없음: {FbxPath}");
            FinishOk();
            return;
        }
        if (avatar == null || fbxRoot == null)
        {
            Debug.LogError($"[SwingDir] ❌ Avatar/FBX 루트 GameObject 없음 (avatar={(avatar == null ? "null" : avatar.name)}, root={(fbxRoot == null ? "null" : fbxRoot.name)}) — FBX 임포트 Animation Type 확인 필요");
            FinishOk();
            return;
        }

        float fps = clip.frameRate > 1f ? clip.frameRate : 30f;
        int totalFrames = Mathf.Max(2, Mathf.RoundToInt(clip.length * fps));
        Debug.Log($"[SwingDir] 대상 클립: {clip.name} (length={clip.length:F3}s, frameRate={fps:F1}, frames={totalFrames}) | 아바타: {avatar.name} (isHuman={avatar.isHuman})");

        // 2) 임시 캐릭터 인스턴스 (FBX 루트 = 전체 뼈 계층 포함)
        var sampler = Object.Instantiate(fbxRoot);
        sampler.name = "SwingSampler";
        sampler.hideFlags = HideFlags.HideAndDontSave;

        var anim = sampler.GetComponent<Animator>();
        if (anim == null) anim = sampler.AddComponent<Animator>();
        anim.avatar = avatar;
        anim.runtimeAnimatorController = null;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        anim.updateMode = AnimatorUpdateMode.Normal;
        anim.applyRootMotion = false;   // 루트 고정 — 손 궤적을 캐릭터 기준 자세 변화로 측정
        anim.enabled = true;

        if (!anim.isHuman || !avatar.isHuman)
        {
            Debug.LogError($"[SwingDir] ❌ 휴머노이드 아바타 아님 (anim.isHuman={anim.isHuman}) — Humanoid 리타깃 샘플링 불가");
            Object.DestroyImmediate(sampler);
            FinishOk();
            return;
        }

        // 뼈 폴백 순서: RightHand → RightLowerArm → LeftHand
        HumanBodyBones[] boneOrder = { HumanBodyBones.RightHand, HumanBodyBones.RightLowerArm, HumanBodyBones.LeftHand };
        Transform bone = null;
        string boneName = "?";
        foreach (var b in boneOrder)
        {
            var t = anim.GetBoneTransform(b);
            if (t != null)
            {
                bone = t;
                boneName = $"{b}({t.name})";
                break;
            }
        }
        if (bone == null)
        {
            Debug.LogError("[SwingDir] ❌ 휴머노이드 뼈 매핑 실패 (RightHand/RightLowerArm/LeftHand 전부 null) — 아바타 보네임 매핑 확인 필요");
            Object.DestroyImmediate(sampler);
            FinishOk();
            return;
        }
        Debug.Log($"[SwingDir] 샘플링 뼈: {boneName} | 임시 캐릭터 pos={sampler.transform.position} rot={sampler.transform.eulerAngles} (identity — world == character local, forward=+Z)");

        // 3) PlayableGraph 구성 (Manual 모드 — CPU 애니 평가만, 렌더링 불필요)
        var graph = PlayableGraph.Create("WeaponSwingDirectionAnalyzer");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        var output = AnimationPlayableOutput.Create(graph, "out", anim);
        output.SetWeight(1f);
        var clipPlayable = AnimationClipPlayable.Create(graph, clip);
        clipPlayable.SetApplyFootIK(false);
        clipPlayable.SetApplyPlayableIK(false);
        output.SetSourcePlayable(clipPlayable);

        var positions = new Vector3[totalFrames + 1];
        try
        {
            for (int f = 0; f <= totalFrames; f++)
            {
                float t = Mathf.Min(f / fps, clip.length);
                clipPlayable.SetTime(t);
                graph.Evaluate(0);   // Manual 모드 — SetTime한 시점의 정확한 포즈 평가
                positions[f] = bone.position;
            }

            // 샘플링 유효성 진단: 전 프레임 동일 위치면 평가 실패 의심
            bool allSame = true;
            for (int f = 1; f <= totalFrames && allSame; f++)
                if ((positions[f] - positions[0]).sqrMagnitude > 1e-10f) allSame = false;
            if (allSame)
                Debug.LogWarning($"[SwingDir] ⚠️ 전 프레임 손 위치 동일({positions[0]}) — 그래프 평가가 반영되지 않았을 수 있음. 결과 신뢰도 낮음");
        }
        finally
        {
            if (graph.IsValid()) graph.Destroy();
            Object.DestroyImmediate(sampler);
        }

        // 4) 스테이지별 접선 방향 산출
        for (int s = 0; s < 3; s++)
            LogStageResult(s, positions, totalFrames, fps, clip.length, boneName);

        // 5) 전 프레임 궤적 1줄 요약 (중심점 변화량)
        LogTrajectorySummary(positions);

        Debug.Log("[SwingDir] ────────── 실측 완료 — 위 RESULT 수치로 HumanoidClipDriver.FireComboSlash/ComboStageDirection 상수 교체는 다음 단계에서 진행 ──────────");
        FinishOk();
    }

    // ────────────────────────────────────────────────────────────────
    // 내부: 스테이지 접선 → yaw/pitch
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 스테이지 s의 임팩트(normT ImpactNormT[s]) 중심 ±0.15s 구간 샘플로 접선 평균:
    /// 인접 샘플 쌍 (p2-p1) 정규화 벡터 누적 후 정규화. yaw=Atan2(t.x,t.z), pitch=Asin(t.y) (도).
    /// 신뢰도 = 구간 내 아크 길이(경로 길이, m). RESULT 고정 포맷 유지.
    /// </summary>
    private static void LogStageResult(int stageIndex, Vector3[] positions, int totalFrames, float fps, float clipLength, string boneName)
    {
        float normFrom = stageIndex == 0 ? 0f : StageEndNormT[stageIndex - 1];
        float normTo = StageEndNormT[stageIndex];
        float centerT = Mathf.Clamp(ImpactNormT[stageIndex] * clipLength, 0f, clipLength);
        float from = Mathf.Max(0f, centerT - ImpactWindowSec);
        float to = Mathf.Min(clipLength, centerT + ImpactWindowSec);

        Vector3 tangentSum = Vector3.zero;
        float arcLength = 0f;
        int segmentCount = 0;

        for (int f = 0; f < totalFrames; f++)
        {
            float t0 = Mathf.Min(f / fps, clipLength);
            float t1 = Mathf.Min((f + 1) / fps, clipLength);
            if (t0 < from || t1 > to) continue;

            Vector3 d = positions[f + 1] - positions[f];
            float dist = d.magnitude;
            arcLength += dist;
            if (dist > 1e-6f)
            {
                tangentSum += d / dist;
                segmentCount++;
            }
        }

        Vector3 tangent = Vector3.zero;
        bool hasTangent = segmentCount > 0 && tangentSum.sqrMagnitude > 1e-6f;
        if (hasTangent) tangent = tangentSum.normalized;

        float yaw = hasTangent ? Mathf.Atan2(tangent.x, tangent.z) * Mathf.Rad2Deg : 0f;
        float pitch = hasTangent ? Mathf.Asin(Mathf.Clamp(tangent.y, -1f, 1f)) * Mathf.Rad2Deg : 0f;

        // RESULT 고정 포맷 — 배치 로그 파싱용 (접미사 추가는 파싱에 영향 없음)
        string confidence = hasTangent ? $"confidence=arc{arcLength:F3}m" : "confidence=LOW(무접선)";
        Debug.Log($"[SwingDir] RESULT stage{stageIndex + 1} yaw={yaw:F1} pitch={pitch:F1} bone={boneName} {confidence} segments={segmentCount} window={from:F3}s~{to:F3}s tangent=({tangent.x:F3},{tangent.y:F3},{tangent.z:F3})");

        Debug.Log($"[SwingDir] stage{stageIndex + 1} 상세: normT {normFrom:F3}~{normTo:F3}, 임팩트 후보 normT={ImpactNormT[stageIndex]} (t={centerT:F3}s) 중심 ±{ImpactWindowSec:F2}s, 아크길이={arcLength:F3}m, 접선벡터크기={tangentSum.magnitude:F3}/{(segmentCount > 0 ? segmentCount : 1)} (1에 가까울수록 일관 방향)");
    }

    /// <summary>전 프레임 손 위치 자표 덤프의 1줄 요약 — 시작/끝/중심점(centroid)/총경로/중심 이동량.</summary>
    private static void LogTrajectorySummary(Vector3[] positions)
    {
        Vector3 centroid = Vector3.zero;
        float pathLength = 0f;
        for (int i = 0; i < positions.Length; i++)
        {
            centroid += positions[i];
            if (i > 0) pathLength += Vector3.Distance(positions[i - 1], positions[i]);
        }
        centroid /= positions.Length;

        Vector3 start = positions[0];
        Vector3 end = positions[positions.Length - 1];
        float centroidShift = Vector3.Distance(start, centroid);

        Debug.Log($"[SwingDir] 궤적 요약(1줄): start=({start.x:F3},{start.y:F3},{start.z:F3}) end=({end.x:F3},{end.y:F3},{end.z:F3}) centroid=({centroid.x:F3},{centroid.y:F3},{centroid.z:F3}) 중심이동={centroidShift:F3}m 총경로={pathLength:F3}m 최대이동={(end - start).magnitude:F3}m (프레임 {positions.Length}개)");
    }

    /// <summary>배치 모드에서 정상 종료 통지 (-quit 미사용 실행 대비, 기존 에디터 스크립트 패턴과 동일).</summary>
    private static void FinishOk()
    {
        if (Application.isBatchMode)
            EditorApplication.Exit(0);
    }
}
