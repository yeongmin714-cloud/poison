using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// #10: Weapon_Combo_2 스윙 방향 실측 에디터 분석기 (전역 namespace — QaValidator와 동일).
///
/// 목적: HumanoidClipDriver.FireComboSlash의 하드코딩 스윙 각도(1타 -30° / 2타 +35° / 3타 roll -90°)를
/// 실제 Weapon_Combo_2.fbx 클립의 손/팔 뼈 회전 궤적으로 검증·교체하기 위한 실측 데이터 수집.
/// 여기서는 측정·출력만 하고, 상수 교체는 측정값 확인 후 다음 단계에서 진행한다.
///
/// 무인 실행 (부모 자동화 — Editor.log 불필요, 배치 모드에서 Debug.Log가 stdout으로 출력됨):
///   Unity.exe -quit -batchmode -projectPath &lt;path&gt; -executeMethod WeaponSwingDirectionAnalyzer.AnalyzeWeaponCombo2
/// 수동 실행: 메뉴 Tools → VFX → Analyze Weapon_Combo_2 Swing Direction
///
/// 방법:
///   1) AssetDatabase.LoadAllAssetsAtPath로 FBX 서브에셋에서 AnimationClip 추출.
///   2) AnimationUtility.GetCurveBindings(clip) → propertyName이 m_LocalRotation.x/y/z/w 인 Float 커브만 취함.
///      (FBX 서브에셋 클립은 읽기 전용이라 clip.SetCurve 불가 — GetCurveBindings/GetEditorCurve 읽기 API만 사용.
///       PPtr(ObjectReferenceKeyframe) 커브면 스킵 — 회전 커브는 항상 Float 커브.)
///   3) 뼈 경로 말미가 Hand/LowerArm/UpperArm 계열(휴머노이드 팔 사슬: RightHand/RightLowerArm/RightUpperArm 등)인
///      바인딩만 선별. 매칭 뼈가 0이면 리그 명명 진단용으로 전체 바인딩 경로 일부를 덤프.
///   4) 136프레임 전체(슬라이싱 없음)를 프레임 간격으로 커브 Evaluate → 쿼터니언 → 오일러 변환.
///   5) 스테이지 구간(normT 0~0.331 / 0.331~0.676 / 0.676~1.0 — HumanoidClipDriver.ComboEndNormT와 동일)별로
///      - 순 변화(스테이지 시작→끝 오일러 Δyaw/Δpitch/Δroll, DeltaAngle 360° 언랩)
///      - 프레임 간 최대 회전각(peak)과 시점 프레임/축 분해
///      - 스테이지 내 총 회전 활동량(프레임 간 각도 절대합)
///      를 계산해, 활동량이 가장 큰 뼈를 대표 스윙으로 요약 로그: "[SwingDir] stage1: yaw=-XX° pitch=YY° ...".
/// 커브 접근이 어려운 경우 대체 경로: 같은 Float 커브의 keyframe 시간별 오일러 변화량으로 방향 추정
/// (Evaluate 기반 샘플링과 동일 계산 경로를 공유하므로 별도 분기 없이 커버됨 — keyframe 부재 시 프레임 샘플이 곧 폴백).
/// </summary>
public static class WeaponSwingDirectionAnalyzer
{
    private const string FbxPath = "Assets/Animations/MeshyUser/Weapon_Combo_2.fbx";

    /// <summary>HumanoidClipDriver.ComboEndNormT와 동일한 스테이지 경계 (45f/92f). 3타는 클립 끝(1.0).</summary>
    private static readonly float[] StageEndNormT = { 0.331f, 0.676f, 1f };

    /// <summary>분석 대상 클립 총 프레임 (Weapon_Combo_2: 136f @30fps). 실측값은 clip.length×frameRate로 재계산.</summary>
    private const int ExpectedClipFrames = 136;

    /// <summary>스윙 뼈 판별 키워드 (뼈 경로 말미 leaf 기준, 대소문자 무시).</summary>
    private static readonly string[] SwingBoneKeywords = { "hand", "lowerarm", "upperarm" };

    [MenuItem("Tools/VFX/Analyze Weapon_Combo_2 Swing Direction")]
    public static void AnalyzeWeaponCombo2()
    {
        Debug.Log($"[SwingDir] ────────── Weapon_Combo_2 스윙 방향 실측 시작 ──────────");

        // 1) FBX 서브에셋 → AnimationClip 수집
        var clips = CollectClips();
        if (clips.Count == 0)
        {
            Debug.LogError($"[SwingDir] ❌ AnimationClip 없음: {FbxPath} — FBX 임포트 상태 확인 필요");
            FinishOk();
            return;
        }

        var clipSb = new StringBuilder("[SwingDir] FBX 서브에셋 클립:");
        foreach (var c in clips) clipSb.Append($"  {c.name}(len={c.length:F3}s,rate={c.frameRate:F1})");
        Debug.Log(clipSb.ToString());

        AnimationClip clip = PickTargetClip(clips);
        Debug.Log($"[SwingDir] 분석 대상: {clip.name} (length={clip.length:F3}s, frameRate={clip.frameRate:F1})");

        // 2) 회전 커브 바인딩 수집 — 손/팔 계열 뼈만
        var boneCurves = CollectSwingBoneRotationCurves(clip, out int totalBindingCount);
        Debug.Log($"[SwingDir] 전체 커브 바인딩 중 회전 커브 스캔: 총 {totalBindingCount}개 바인딩 검사 → 스윙 뼈 {boneCurves.Count}개 매칭");
        if (boneCurves.Count == 0)
        {
            Debug.LogWarning("[SwingDir] ⚠️ Hand/LowerArm/UpperArm 계열 회전 커브 미발견 — 리그 명명 진단 덤프 출력");
            DumpBindingPaths(clip);
            FinishOk();
            return;
        }

        // 3) 프레임 전체 샘플링 + 4) 스테이지별 통계
        float frameRate = clip.frameRate > 1f ? clip.frameRate : 30f;
        int totalFrames = Mathf.Max(2, Mathf.RoundToInt(clip.length * frameRate));
        Debug.Log($"[SwingDir] 샘플링: {totalFrames}프레임 (frameRate={frameRate:F1}, 슬라이싱 없음 — 전 구간)");

        var perBone = new Dictionary<string, StageStat[]>();
        var frameTraces = new Dictionary<string, Vector3[]>(); // 뼈별 프레임별 (pitch,yaw,roll) 오일러
        foreach (var kv in boneCurves)
        {
            var eulerPerFrame = new Vector3[totalFrames + 1];
            var quatPerFrame = new Quaternion[totalFrames + 1];
            for (int f = 0; f <= totalFrames; f++)
            {
                float t = Mathf.Min(f / frameRate, clip.length);
                Quaternion q = EvaluateRotation(kv.Value, t);
                quatPerFrame[f] = q;
                eulerPerFrame[f] = q.eulerAngles;   // x=pitch, y=yaw, z=roll
            }
            frameTraces[kv.Key] = eulerPerFrame;

            var stats = new StageStat[3];
            for (int s = 0; s < 3; s++)
                stats[s] = ComputeStageStat(s, quatPerFrame, totalFrames);
            perBone[kv.Key] = stats;
        }

        // 4) 로그 출력
        for (int s = 0; s < 3; s++)
            LogStage(s, perBone, totalFrames);

        DumpPerFrameTraces(perBone, frameTraces, totalFrames);

        Debug.Log("[SwingDir] ────────── 실측 완료 — 위 수치로 HumanoidClipDriver.FireComboSlash/ComboStageDirection 상수 교체는 다음 단계에서 진행 ──────────");
        FinishOk();
    }

    // ────────────────────────────────────────────────────────────────
    // 내부: 클립/바인딩 수집
    // ────────────────────────────────────────────────────────────────

    private static List<AnimationClip> CollectClips()
    {
        var clips = new List<AnimationClip>();
        var all = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
        if (all == null) return clips;
        foreach (var o in all)
        {
            if (o is AnimationClip c && c != null && !string.IsNullOrEmpty(c.name))
                clips.Add(c);
        }
        return clips;
    }

    /// <summary>이름에 Weapon_Combo_2를 포함하는 클립 우선, 없으면 가장 긴 클립.</summary>
    private static AnimationClip PickTargetClip(List<AnimationClip> clips)
    {
        foreach (var c in clips)
            if (c.name.Contains("Weapon_Combo_2"))
                return c;

        AnimationClip longest = clips[0];
        foreach (var c in clips)
            if (c.length > longest.length) longest = c;
        return longest;
    }

    /// <summary>
    /// 회전(m_LocalRotation.*) Float 커브 중 뼈 경로 말미가 Hand/LowerArm/UpperArm 계열인 것만
    /// 뼈 경로별로 그룹핑해 반환. ObjectReference(PPtr) 커브는 스킵.
    /// </summary>
    private static Dictionary<string, Dictionary<string, AnimationCurve>> CollectSwingBoneRotationCurves(
        AnimationClip clip, out int totalBindings)
    {
        var result = new Dictionary<string, Dictionary<string, AnimationCurve>>();
        totalBindings = 0;

        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
        totalBindings = bindings != null ? bindings.Length : 0;
        if (bindings == null) return result;

        foreach (var b in bindings)
        {
            if (b.propertyName == null || !b.propertyName.StartsWith("m_LocalRotation")) continue;
            if (!IsSwingBonePath(b.path)) continue;

            // ObjectReferenceKeyframe(PPtr) 커브 스킵 — 회전은 Float 커브여야 한다
            if (AnimationUtility.GetObjectReferenceCurve(clip, b) != null) continue;

            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, b);
            if (curve == null || curve.keys == null || curve.keys.Length == 0) continue;

            if (!result.TryGetValue(b.path, out var props))
            {
                props = new Dictionary<string, AnimationCurve>();
                result[b.path] = props;
            }
            props[b.propertyName] = curve;
        }

        // x/y/z/w 4성분이 모두 있는 뼈만 유효 판정
        var filtered = new Dictionary<string, Dictionary<string, AnimationCurve>>();
        foreach (var kv in result)
        {
            bool complete = kv.Value.ContainsKey("m_LocalRotation.x") && kv.Value.ContainsKey("m_LocalRotation.y")
                         && kv.Value.ContainsKey("m_LocalRotation.z") && kv.Value.ContainsKey("m_LocalRotation.w");
            if (complete) filtered[kv.Key] = kv.Value;
            else Debug.LogWarning($"[SwingDir] ⚠️ 회전 성분 불완전 스킵: {kv.Key} (성분 {kv.Value.Count}/4)");
        }
        return filtered;
    }

    private static bool IsSwingBonePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        int slash = path.LastIndexOf('/');
        string leaf = slash >= 0 ? path.Substring(slash + 1) : path;
        string n = leaf.ToLowerInvariant();
        for (int i = 0; i < SwingBoneKeywords.Length; i++)
            if (n.Contains(SwingBoneKeywords[i])) return true;
        return false;
    }

    /// <summary>스윙 뼈가 0개일 때 리그 명명 진단용 — 전체 바인딩 경로를 최대 40개 덤프.</summary>
    private static void DumpBindingPaths(AnimationClip clip)
    {
        var bindings = AnimationUtility.GetCurveBindings(clip);
        if (bindings == null || bindings.Length == 0)
        {
            Debug.LogWarning("[SwingDir] 커브 바인딩 자체가 0개 — 클립이 휴머노이드 리타깃 전용이거나 임포트 실패일 수 있음");
            return;
        }

        var paths = new SortedSet<string>();
        foreach (var b in bindings)
            if (!string.IsNullOrEmpty(b.path)) paths.Add(b.path);

        var sb = new StringBuilder($"[SwingDir] 전체 바인딩 경로 {paths.Count}개 중 처음 40개:\n");
        int i = 0;
        foreach (var p in paths)
        {
            if (i >= 40) break;
            sb.AppendLine($"  [{i}] {p}");
            i++;
        }
        Debug.Log(sb.ToString().TrimEnd());
    }

    // ────────────────────────────────────────────────────────────────
    // 내부: 쿼터니언 샘플링 / 스테이지 통계
    // ────────────────────────────────────────────────────────────────

    /// <summary>성분 커브 4개를 time에서 Evaluate해 쿼터니언 재구성 (정규화 포함).</summary>
    private static Quaternion EvaluateRotation(Dictionary<string, AnimationCurve> props, float time)
    {
        float x = props.TryGetValue("m_LocalRotation.x", out var cx) ? cx.Evaluate(time) : 0f;
        float y = props.TryGetValue("m_LocalRotation.y", out var cy) ? cy.Evaluate(time) : 0f;
        float z = props.TryGetValue("m_LocalRotation.z", out var cz) ? cz.Evaluate(time) : 0f;
        float w = props.TryGetValue("m_LocalRotation.w", out var cw) ? cw.Evaluate(time) : 1f;
        var q = new Quaternion(x, y, z, w);
        float sq = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
        return q.w != 0f || sq > 1e-10f ? q.normalized : Quaternion.identity;
    }

    /// <summary>스테이지별 회전 통계 — 순 변화(net)/최대 프레임 각(peak)/총 활동량(total).</summary>
    private struct StageStat
    {
        public int frameFrom;      // 스테이지 시작 프레임(포함)
        public int frameTo;        // 스테이지 끝 프레임(포함)
        public float netPitch;     // 시작→끝 순 오일러 변화 (DeltaAngle 언랩, x성분)
        public float netYaw;       // 〃 y성분
        public float netRoll;      // 〃 z성분
        public float totalAngle;   // 프레임 간 회전각 절대합 — 활동량
        public float peakAngle;    // 프레임 간 최대 회전각
        public int peakFrame;      // peak 스텝의 시작 프레임
        public float peakPitch, peakYaw, peakRoll;   // peak 스텝의 축별 분해
        public bool hasData;
    }

    private static StageStat ComputeStageStat(int stageIndex, Quaternion[] quatPerFrame, int totalFrames)
    {
        var stat = new StageStat { peakFrame = -1 };

        // 스테이지 프레임 범위: normT = frame/totalFrames 기준 (0~0.331 / 0.331~0.676 / 0.676~1.0)
        int from = 0, to = totalFrames;
        if (stageIndex == 0) to = FrameAtNorm(StageEndNormT[0], totalFrames);
        else if (stageIndex == 1) { from = FrameAtNorm(StageEndNormT[0], totalFrames); to = FrameAtNorm(StageEndNormT[1], totalFrames); }
        else from = FrameAtNorm(StageEndNormT[1], totalFrames);
        to = Mathf.Min(to, totalFrames);
        from = Mathf.Min(from, to);
        stat.frameFrom = from;
        stat.frameTo = to;

        if (from >= to || quatPerFrame == null || quatPerFrame.Length <= to)
            return stat;

        Vector3 eStart = quatPerFrame[from].eulerAngles;
        Vector3 eEnd = quatPerFrame[to].eulerAngles;
        stat.netPitch = Mathf.DeltaAngle(eStart.x, eEnd.x);
        stat.netYaw = Mathf.DeltaAngle(eStart.y, eEnd.y);
        stat.netRoll = Mathf.DeltaAngle(eStart.z, eEnd.z);

        for (int f = from; f < to; f++)
        {
            float step = Quaternion.Angle(quatPerFrame[f], quatPerFrame[f + 1]);
            if (step <= 0.0001f) continue;
            stat.totalAngle += step;
            if (step > stat.peakAngle)
            {
                stat.peakAngle = step;
                stat.peakFrame = f;
                Vector3 e1 = quatPerFrame[f].eulerAngles;
                Vector3 e2 = quatPerFrame[f + 1].eulerAngles;
                stat.peakPitch = Mathf.DeltaAngle(e1.x, e2.x);
                stat.peakYaw = Mathf.DeltaAngle(e1.y, e2.y);
                stat.peakRoll = Mathf.DeltaAngle(e1.z, e2.z);
            }
        }
        stat.hasData = true;
        return stat;
    }

    /// <summary>normT(0~1)에 해당하는 프레임 인덱스.</summary>
    private static int FrameAtNorm(float normT, int totalFrames)
    {
        return Mathf.Clamp(Mathf.RoundToInt(normT * totalFrames), 0, totalFrames);
    }

    // ────────────────────────────────────────────────────────────────
    // 내부: 로그 출력
    // ────────────────────────────────────────────────────────────────

    private static void LogStage(int stageIndex, Dictionary<string, StageStat[]> perBone, int totalFrames)
    {
        float normFrom = stageIndex == 0 ? 0f : StageEndNormT[stageIndex - 1];
        float normTo = StageEndNormT[stageIndex];

        var sb = new StringBuilder();
        sb.AppendLine($"[SwingDir] ── stage{stageIndex + 1} (frames {FrameAtNorm(normFrom, totalFrames)}~{FrameAtNorm(StageEndNormT[stageIndex], totalFrames)}, normT {normFrom:F3}~{StageEndNormT[stageIndex]:F3}) ──");

        // 대표 뼈: 스테이지 내 활동량(프레임 간 회전각 합)이 최대인 뼈
        string dominantBone = null;
        StageStat dominant = default;
        foreach (var kv in perBone)
        {
            StageStat s = kv.Value[stageIndex];
            if (!s.hasData) continue;
            if (dominant.peakFrame < 0 || s.totalAngle > dominant.totalAngle)
            {
                dominant = s;
                dominantBone = kv.Key;
            }
        }

        if (!dominant.hasData)
        {
            sb.AppendLine("  회전 변화 없음 (커브 정적 — 스테이지 내 뼈 움직임 미확인)");
            Debug.Log(sb.ToString().TrimEnd());
            return;
        }

        // 요청 형식: [SwingDir] stage1: yaw=-XX° pitch=YY° ...
        sb.AppendLine($"[SwingDir] stage{stageIndex + 1}: yaw={dominant.netYaw:F1}° pitch={dominant.netPitch:F1}° (bone={ShortBoneName(dominantBone)}, roll={dominant.netRoll:F1}°, 순변화 크기={Mathf.Sqrt(dominant.netYaw * dominant.netYaw + dominant.netPitch * dominant.netPitch + dominant.netRoll * dominant.netRoll):F1}°)");

        // 뼈별 상세
        foreach (var kv in perBone)
        {
            StageStat s = kv.Value[stageIndex];
            if (!s.hasData)
            {
                sb.AppendLine($"  {ShortBoneName(kv.Key)}: 변화 없음");
                continue;
            }
            sb.AppendLine($"  {ShortBoneName(kv.Key)}: 순 Δyaw={s.netYaw:F1}° Δpitch={s.netPitch:F1}° Δroll={s.netRoll:F1}° | 활동량={s.totalAngle:F1}° | peak={s.peakAngle:F1}°@f{s.peakFrame}(yaw {s.peakYaw:F1}°/pitch {s.peakPitch:F1}°/roll {s.peakRoll:F1}°) | frames {s.frameFrom}~{s.frameTo}");
        }

        // 팔 사슬 평균(우측 우선) — 뼈 하나의 노이즈를 줄인 방향 후보
        var chain = SelectArmChain(perBone.Keys);
        if (chain.Count > 0)
        {
            float sumYaw = 0f, sumPitch = 0f, sumRoll = 0f;
            foreach (var bone in chain)
            {
                StageStat s = perBone[bone][stageIndex];
                sumYaw += s.netYaw; sumPitch += s.netPitch; sumRoll += s.netRoll;
            }
            sb.AppendLine($"  팔사슬 평균({chain.Count}뼈): yaw={sumYaw / chain.Count:F1}° pitch={sumPitch / chain.Count:F1}° roll={sumRoll / chain.Count:F1}°");
        }

        Debug.Log(sb.ToString().TrimEnd());
    }

    private static string ShortBoneName(string path)
    {
        if (string.IsNullOrEmpty(path)) return "?";
        int slash = path.LastIndexOf('/');
        return slash >= 0 ? path.Substring(slash + 1) : path;
    }

    /// <summary>UpperArm→LowerArm→Hand 우선순위 정렬 후 우측 팔 사슬 선별 (우측 없으면 좌측/기타). 일관 비교자 사용.</summary>
    private static List<string> SelectArmChain(IEnumerable<string> bones)
    {
        var right = new List<string>();
        var other = new List<string>();
        foreach (var b in bones)
        {
            string leaf = ShortBoneName(b).ToLowerInvariant();
            bool arm = leaf.Contains("upperarm") || leaf.Contains("lowerarm") || leaf.Contains("hand");
            if (!arm) continue;
            if (leaf.Contains("right")) right.Add(b);
            else other.Add(b);
        }
        var chain = right.Count > 0 ? right : other;
        // 일관된 비교자: UpperArm 우선, 그 다음 경로 사전순 (비일관 비교로 인한 Sort 예외 방지)
        chain.Sort((a, b) =>
        {
            int rankA = ShortBoneName(a).ToLowerInvariant().Contains("upperarm") ? 0 : 1;
            int rankB = ShortBoneName(b).ToLowerInvariant().Contains("upperarm") ? 0 : 1;
            if (rankA != rankB) return rankA.CompareTo(rankB);
            return string.CompareOrdinal(a, b);
        });
        return chain;
    }

    /// <summary>검증용: 대표 뼈의 프레임별 (yaw/pitch/roll) 전 구간 덤프 — 슬라이싱 없이 136프레임 전체.</summary>
    private static void DumpPerFrameTraces(Dictionary<string, StageStat[]> perBone,
        Dictionary<string, Vector3[]> frameTraces, int totalFrames)
    {
        // 대표 뼈(전체 클립 기준 최다 활동량) 1개만 전 구간 덤프 — 나머지는 스테이지 통계로 충분
        string best = null;
        float bestActivity = -1f;
        foreach (var kv in perBone)
        {
            float act = 0f;
            for (int s = 0; s < 3; s++) act += kv.Value[s].totalAngle;
            if (act > bestActivity) { bestActivity = act; best = kv.Key; }
        }
        if (best == null || !frameTraces.TryGetValue(best, out var trace)) return;

        var sb = new StringBuilder($"[SwingDir] 프레임별 오일러 덤프({ShortBoneName(best)}, yaw/pitch/roll°, 전 {totalFrames}프레임):\n");
        for (int f = 0; f <= totalFrames; f++)
        {
            Vector3 e = trace[f];
            sb.Append($"f{f}:{e.y:F0}/{e.x:F0}/{e.z:F0}  ");
            if ((f + 1) % 6 == 0) sb.Append('\n');
        }
        Debug.Log(sb.ToString().TrimEnd());
    }

    /// <summary>배치 모드에서 정상 종료 통지 (-quit 미사용 실행 대비, 기존 에디터 스크립트 패턴과 동일).</summary>
    private static void FinishOk()
    {
        if (Application.isBatchMode)
            EditorApplication.Exit(0);
    }
}