using System.Collections.Generic;
using System.Text;
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
/// v4는 플레이어 리그(Heat) 인스턴스 + PlayableGraph 휴머노이드 리타깃 샘플링 — 배치모드에서도 Animator CPU 평가만으로 동작(렌더링 불필요):
///   1) LoadAllAssetsAtPath(Meshy FBX) → AnimationClip(이름 contains "Weapon_Combo_2", __preview__ 제외).
///      클립(Armature|Weapon_Combo_2_withSkin)은 머슬 커브 — 리그 무관 리타깃 재생 가능(런타임 Player_AC와 동일 원리).
///   2) 샘플러 = 플레이어 실제 리그 Player_Rigged_Heat.fbx 루트 GameObject 인스턴스화("SwingSampler", HideAndDontSave).
///      swingdir.log 실측: Meshy Weapon_Combo_2.fbx를 인스턴스화하면 스켈레톤 뼈가 4개뿐(SwingSampler/Head/head_end/
///      headfront — 스킨 본이 메시에 베이크된 내보내기) → 뼈 샘플링 불가. Heat 리그는 스켈레톤 계층 보유 +
///      런타임 애니 정상 실증(TestPlayerAnimatorBoot.cs 62행 선례). 인스턴스 Animator에는 같은 FBX 서브에셋의
///      imported Avatar(OfType<Avatar>)를 할당(인스턴스 내장 avatar 우선). cullingMode=AlwaysAnimate(카메라 없는
///      배치에서도 평가 보장), applyRootMotion=false.
///   3) PlayableGraph(Manual) + AnimationClipPlayable → SetTime(t) 후 graph.Evaluate(0)로 정확 시간 샘플링.
///   4) 뼈 선택 폴백 체인 — (1) GetBoneTransform(RightHand) 휴머노이드 매핑 우선, null/이동 0이면
///      (2) 이름 기반 폴백: 계층 전체를 순회해 이름에 hand/arm/wrist 포함 뼈를 후보 수집
///      (우선순위: RightHand > RightLowerArm/RightForeArm > 기타 hand), 전 프레임(136+1 샘플) world position
///      이동 경로 길이가 최대인 후보를 자동 선택 — 머슬 평가는 보네임 매핑과 무관하게 스켈레톤 Transform을
///      실제로 움직이므로 이름 기반 직접 샘플링으로 우회 가능.
///      검증 로그: 인스턴스 뼈 수(4개 이하면 실패 경고), GetBoneTransform(RightHand), 매핑 본수,
///      avatar.humanDescription.human 길이. 실패 시 전체 뼈 이동 경로길이 상위 5개 진단 로그.
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

    /// <summary>샘플러 리그: 플레이어 실제 휴머노이드 FBX(스켈레톤 계층 보유) — Meshy 머슬 클립 리타깃 샘플링 타깃.</summary>
    private const string HeatFbxPath = "Assets/Resources/Models/UserProvided/fbx/Player_Rigged_Heat.fbx";

    /// <summary>Heat 리그 Resources.Load 경로(TestPlayerAnimatorBoot.cs 62행 선례 — 에디터 에셋 로드 실패 시 폴백).</summary>
    private const string HeatResourcePath = "Models/UserProvided/fbx/Player_Rigged_Heat";

    /// <summary>HumanoidClipDriver.ComboEndNormT와 동일한 스테이지 경계 (45f/92f). 3타는 클립 끝(1.0).</summary>
    private static readonly float[] StageEndNormT = { 0.331f, 0.676f, 1f };

    /// <summary>스테이지별 임팩트 후보 normT (스윙 방향 접선 측정 중심).</summary>
    private static readonly float[] ImpactNormT = { 0.18f, 0.53f, 0.84f };

    /// <summary>임팩트 중심 ± 윈도우 (초).</summary>
    private const float ImpactWindowSec = 0.15f;

    [MenuItem("Tools/VFX/Analyze Weapon_Combo_2 Swing Direction")]
    public static void AnalyzeWeaponCombo2()
    {
        Debug.Log("[SwingDir] ────────── Weapon_Combo_2 스윙 방향 실측 시작 (v4 Heat 리그 리타깃 샘플링) ──────────");

        // 1) Meshy FBX 서브에셋 → AnimationClip (머슬 커브 — 휴머노이드 리타깃으로 리그 무관 재생)
        AnimationClip clip = null;
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

        // 2) 플레이어 리그(Heat) 로드 — 샘플러용 실제 스켈레톤 계층 보유 리그.
        //    근거(swingdir.log 실측): Meshy Weapon_Combo_2.fbx 인스턴스는 뼈 4개뿐(SwingSampler/Head/head_end/headfront
        //    — 스킨 본이 메시에 베이크된 내보내기, avatar.humanDescription.human=23으로 매핑은 있으나 계층이 없어
        //    GetBoneTransform 전부 null) → 뼈 샘플링 불가. 런타임에서 Player_AC가 Meshy 클립을 휴머노이드 리타깃으로
        //    재생하는 것과 동일 원리로, Heat 리그 인스턴스에서 클립을 평가해 뼈 궤적을 확보한다.
        GameObject heatAsset = AssetDatabase.LoadAssetAtPath<GameObject>(HeatFbxPath);
        if (heatAsset == null) heatAsset = Resources.Load<GameObject>(HeatResourcePath);
        if (heatAsset == null)
        {
            Debug.LogError($"[SwingDir] ❌ 플레이어 리그 로드 실패: {HeatFbxPath} 및 Resources \"{HeatResourcePath}\"");
            FinishOk();
            return;
        }

        // Heat FBX 서브에셋의 imported Avatar (인스턴스 Animator에 내장 avatar가 없을 때 할당용)
        Avatar heatAvatar = null;
        var heatSubs = AssetDatabase.LoadAllAssetsAtPath(HeatFbxPath);
        if (heatSubs != null)
            foreach (var o in heatSubs)
                if (o is Avatar av && av != null && heatAvatar == null) heatAvatar = av;

        float fps = clip.frameRate > 1f ? clip.frameRate : 30f;
        int totalFrames = Mathf.Max(2, Mathf.RoundToInt(clip.length * fps));
        Debug.Log($"[SwingDir] 대상 클립: {clip.name} (length={clip.length:F3}s, frameRate={fps:F1}, frames={totalFrames}) | 샘플러 리그: {heatAsset.name} ({HeatFbxPath})");

        // 3) Heat 리그 인스턴스화 (HideAndDontSave) — 이 리그에는 스켈레톤 계층이 있음
        var sampler = Object.Instantiate(heatAsset);
        sampler.name = "SwingSampler";
        sampler.hideFlags = HideFlags.HideAndDontSave;
        sampler.transform.position = Vector3.zero;
        sampler.transform.rotation = Quaternion.identity;   // identity — world == character local, forward=+Z

        var anim = sampler.GetComponent<Animator>();
        if (anim == null) anim = sampler.AddComponent<Animator>();
        if (anim.avatar == null && heatAvatar != null) anim.avatar = heatAvatar;
        else if (anim.avatar != null) heatAvatar = anim.avatar;   // 인스턴스 내장 avatar(FBX imported) 우선
        anim.runtimeAnimatorController = null;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        anim.updateMode = AnimatorUpdateMode.Normal;
        anim.applyRootMotion = false;   // 루트 고정 — 손 궤적을 캐릭터 기준 자세 변화로 측정
        anim.enabled = true;

        if (anim.avatar == null || !anim.isHuman || !anim.avatar.isHuman)
        {
            Debug.LogError($"[SwingDir] ❌ Heat 리그 휴머노이드 아바타 없음/비휴머노이드 (avatar={(anim.avatar == null ? "null" : anim.avatar.name)}, anim.isHuman={anim.isHuman}) — 휴머노이드 리타깃 샘플링 불가");
            Object.DestroyImmediate(sampler);
            FinishOk();
            return;
        }

        // ── 검증 로그: 인스턴스 뼈 수 / GetBoneTransform(RightHand) / 휴머노이드 매핑 본수 ──
        var skeleton = sampler.GetComponentsInChildren<Transform>(true);
        var rightHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
        int mappedBones = 0;
        foreach (HumanBodyBones b in System.Enum.GetValues(typeof(HumanBodyBones)))
            if (b != HumanBodyBones.LastBone && anim.GetBoneTransform(b) != null) mappedBones++;
        var heatHuman = anim.avatar.humanDescription.human;

        // Meshy 베이크 케이스(swingdir.log: 인스턴스 뼈 4개)와 동일 상태면 실패 경고
        if (skeleton.Length <= 4)
            Debug.LogWarning($"[SwingDir] ⚠️ Heat 인스턴스 뼈 수={skeleton.Length} — 스킨 본이 메시에 베이크된 리그로 보임(실패 예상). FBX 임포트 Rig 설정 확인 필요");

        Debug.Log($"[SwingDir] Heat 리그 검증: 인스턴스 뼈 수={skeleton.Length} | GetBoneTransform(RightHand)={(rightHand == null ? "null → 이름 기반 폴백 사용" : rightHand.name)} | 휴머노이드 매핑 본수={mappedBones} | avatar.humanDescription.human 길이={(heatHuman != null ? heatHuman.Length : 0)}");

        // ── 폴백 체인 (2): 이름 기반(hand/arm/wrist) 후보 수집 — (1)단계 GetBoneTransform(RightHand)이 null/이동 0일 때 사용 ──
        // 머슬 평가는 보네임 매핑과 무관하게 스켈레톤 Transform을 실제로 움직이므로,
        // 계층 순회 + 이름 매칭으로 후보를 수집하고 움직임이 가장 큰 뼈를 자동 선택한다.
        var candidates = new List<Candidate>();
        for (int i = 0; i < skeleton.Length; i++)
        {
            string n = skeleton[i].name.ToLowerInvariant();
            if (!n.Contains("hand") && !n.Contains("arm") && !n.Contains("wrist")) continue;
            candidates.Add(new Candidate { t = skeleton[i], idx = i, pri = BonePriority(n) });
        }

        // 후보 나열 (우선순위 그룹 → 이름순)
        candidates.Sort((a, b) => a.pri != b.pri ? a.pri.CompareTo(b.pri) : string.CompareOrdinal(a.t.name, b.t.name));
        var candLog = new StringBuilder();
        foreach (var c in candidates)
            candLog.Append($"  [{c.pri}] {c.t.name} (path={TransformPath(c.t)})\n");
        Debug.Log($"[SwingDir] 이름 기반 뼈 후보 {candidates.Count}개 (hand/arm/wrist 매칭, [n]=우선순위 0=RightHand 1=RightLowerArm/ForeArm 2=기타 hand 3=wrist/arm):\n{candLog}");

        if (candidates.Count == 0)
        {
            var allNames = new StringBuilder();
            for (int i = 0; i < skeleton.Length && i < 60; i++) allNames.Append($"  {skeleton[i].name}\n");
            Debug.LogWarning($"[SwingDir] ⚠️ hand/arm/wrist 이름 매칭 후보 0개 — 스켈레톤 뼈 이름(최대 60개):\n{allNames} → 샘플링 후 전체 뼈 이동 상위 진단으로 계속");
        }

        // 4) PlayableGraph 구성 (Manual 모드 — CPU 애니 평가만, 렌더링 불필요)
        var graph = PlayableGraph.Create("WeaponSwingDirectionAnalyzer");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        var output = AnimationPlayableOutput.Create(graph, "out", anim);
        output.SetWeight(1f);
        var clipPlayable = AnimationClipPlayable.Create(graph, clip);
        clipPlayable.SetApplyFootIK(false);
        clipPlayable.SetApplyPlayableIK(false);
        output.SetSourcePlayable(clipPlayable);

        // 전체 뼈 샘플링 — 후보 자동 선택과 실패 시 전체 뼈 이동 진단에 모두 사용
        int boneCount = skeleton.Length;
        var samples = new Vector3[boneCount][];
        for (int i = 0; i < boneCount; i++) samples[i] = new Vector3[totalFrames + 1];

        Vector3[] positions = null;
        string boneName = null;
        try
        {
            for (int f = 0; f <= totalFrames; f++)
            {
                float t = Mathf.Min(f / fps, clip.length);
                clipPlayable.SetTime(t);
                graph.Evaluate(0);   // Manual 모드 — SetTime한 시점의 정확한 포즈 평가
                for (int i = 0; i < boneCount; i++) samples[i][f] = skeleton[i].position;
            }

            // 뼈별 world position 이동 경로 길이 (전 프레임 누적)
            var pathLen = new float[boneCount];
            for (int i = 0; i < boneCount; i++)
            {
                float len = 0f;
                for (int f = 1; f <= totalFrames; f++) len += Vector3.Distance(samples[i][f - 1], samples[i][f]);
                pathLen[i] = len;
            }

            // ── 폴백 체인 뼈 선택: (1) GetBoneTransform(RightHand) 휴머노이드 매핑 → (2) 이름 매칭+이동 최대 뼈 ──
            int sel = -1;
            float bestLen = 0f;
            int bestPri = int.MaxValue;
            string selSrc = null;

            if (rightHand != null)
            {
                int hi = -1;
                for (int i = 0; i < boneCount; i++)
                    if (skeleton[i] == rightHand) { hi = i; break; }
                if (hi >= 0 && pathLen[hi] > 1e-5f)
                {
                    sel = hi;
                    bestLen = pathLen[hi];
                    selSrc = "humanoid-mapping(GetBoneTransform)";
                }
                else
                    Debug.LogWarning("[SwingDir] ⚠️ GetBoneTransform(RightHand) 매핑 존재하나 전 프레임 이동 0 — 이름 기반 폴백으로 계속");
            }

            if (sel < 0)
            {
                // (2) 이름 기반 폴백 — 후보 중 "휘두름이 가장 큰 뼈" 자동 선택 (이동 0 제외, 근접 동률은 우선순위 그룹으로 결정)
                foreach (var c in candidates)
                {
                    if (pathLen[c.idx] <= 1e-5f) continue;   // 이동 0 뼈 제외
                    if (pathLen[c.idx] > bestLen + 1e-5f || (pathLen[c.idx] > bestLen - 1e-5f && c.pri < bestPri))
                    {
                        sel = c.idx;
                        bestLen = pathLen[c.idx];
                        bestPri = c.pri;
                        selSrc = $"name-fallback(pri={c.pri})";
                    }
                }
            }

            if (sel < 0)
            {
                // 진단 강화: 스켈레톤 전체 뼈 중 이동 경로 길이 상위 5개 (디버깅용)
                var order = new List<int>();
                for (int i = 0; i < boneCount; i++) order.Add(i);
                order.Sort((a, b) => pathLen[b].CompareTo(pathLen[a]));
                var top = new StringBuilder();
                for (int k = 0; k < order.Count && k < 5; k++)
                    top.Append($"  {skeleton[order[k]].name} (path={TransformPath(skeleton[order[k]])}): {pathLen[order[k]]:F4}m\n");
                Debug.LogError($"[SwingDir] ❌ 샘플링 뼈 선택 실패 (GetBoneTransform(RightHand)={(rightHand == null ? "null" : "매핑됐으나 이동 0")}, hand/arm/wrist 이름 후보 {candidates.Count}개 전부 이동 0 또는 매칭 없음) — 전체 뼈 이동 경로길이 상위 5:\n{top}");
                return;   // finally에서 graph/sampler 정리
            }

            positions = samples[sel];
            boneName = skeleton[sel].name;
            Debug.Log($"[SwingDir] 샘플링 뼈(선택): {boneName} (path={TransformPath(skeleton[sel])}, 선택경로={selSrc}, 전체 이동 경로길이={bestLen:F3}m) | 임시 캐릭터 pos={sampler.transform.position} rot={sampler.transform.eulerAngles} scale={sampler.transform.lossyScale} (identity — world == character local, forward=+Z)");

            // 샘플링 유효성 진단: 전 프레임 동일 위치면 평가 실패 의심
            bool allSame = true;
            for (int f = 1; f <= totalFrames && allSame; f++)
                if ((positions[f] - positions[0]).sqrMagnitude > 1e-10f) allSame = false;
            if (allSame)
                Debug.LogWarning($"[SwingDir] ⚠️ 전 프레임 {boneName} 위치 동일({positions[0]}) — 그래프 평가가 반영되지 않았을 수 있음. 결과 신뢰도 낮음");
        }
        finally
        {
            if (graph.IsValid()) graph.Destroy();
            Object.DestroyImmediate(sampler);
        }

        if (positions == null)
        {
            FinishOk();   // 실패 진단 로그는 위에서 이미 출력됨
            return;
        }

        // 5) 스테이지별 접선 방향 산출
        for (int s = 0; s < 3; s++)
            LogStageResult(s, positions, totalFrames, fps, clip.length, boneName);

        // 6) 전 프레임 궤적 1줄 요약 (중심점 변화량)
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

    // ────────────────────────────────────────────────────────────────
    // 내부: 이름 기반 뼈 후보 (이름 매칭 폴백)
    // ────────────────────────────────────────────────────────────────

    /// <summary>이름 매칭 뼈 후보 (skeleton 배열 인덱스 + 우선순위 그룹).</summary>
    private struct Candidate
    {
        public Transform t;
        public int idx;
        public int pri;
    }

    /// <summary>
    /// 이름 기반 뼈 후보 우선순위: 0=오른손("righthand" 또는 right+hand), 1=오른팔 하완("lowerarm"+right / "rightforearm"),
    /// 2=기타 hand, 3=wrist/arm 기타. right 판정은 "right" 포함 외에 Meshy 계열 접미사(_r/.r/공백 r)도 허용.
    /// </summary>
    private static int BonePriority(string n)
    {
        bool right = n.Contains("right") || n.EndsWith("_r") || n.EndsWith(".r") || n.EndsWith(" r");
        bool hand = n.Contains("hand");
        if (n.Contains("righthand") || (right && hand)) return 0;
        if ((n.Contains("lowerarm") && right) || n.Contains("rightforearm")) return 1;
        if (hand) return 2;
        return 3;
    }

    /// <summary>계층 전체 경로(루트→뼈) 문자열 — 로그 진단용.</summary>
    private static string TransformPath(Transform t)
    {
        var sb = new StringBuilder(t.name);
        var p = t.parent;
        while (p != null)
        {
            sb.Insert(0, p.name + "/");
            p = p.parent;
        }
        return sb.ToString();
    }

    /// <summary>배치 모드에서 정상 종료 통지 (-quit 미사용 실행 대비, 기존 에디터 스크립트 패턴과 동일).</summary>
    private static void FinishOk()
    {
        if (Application.isBatchMode)
            EditorApplication.Exit(0);
    }
}
