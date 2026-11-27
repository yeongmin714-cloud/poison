using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [139차 P2] 캐릭터 크기 통일 진단 프로브 — 측정 전용(수리 없음). 단일 진입 RunAll().
/// 배경: 139차 전수 프로브 — 병사(CreateGuard→NormalizeSoldierHeight 후) 3.289m / 주민 1.81 ✓ /
///   플레이어 원시 1.156. 계약 = 병사/플레이어/주민 전 씬 1.8m 동일(TerritoryBuilder.SoldierTargetHeight).
/// 결정적 선행 증거(139차 unity 로그 L2202): "[TerritoryBuilder] 병사 크기 정규화 — T139Soldier 높이 1.18m → 1.80m (scale ×1.52)"
///   → localBounds 모드가 lv1-20 병사 FBX를 1.18m로 과소측정했고 ×1.52 스케일 후 월드 실측 3.289m.
/// 시퀀스:
///  ① 병사 원시 실측 — FBX 3구간(lv1-20/20-40/40-50) + GLB 2구간(refill 경로 소스) Instantiate 직후
///     MeasureStandingHeight 두 모드(skinnedLocalBounds true/false) 병기 + 렌더러별 기여 덤프.
///  ② 정규화 모드 대조 — 동일 FBX를 localBounds 모드(현행 CreateGuard 경로) / 월드 모드(수리 후보)로
///     각각 NormalizeSoldierHeight → 4실측(모드×전/후) + 멱등성 재적용 검증.
///  ③ 실경로 재현 — SpawnDeployedGuard(lv5, 139 프로브 동일 조건) 후 전/후·모드별 + anim.Update(스테일 판별)
///     + 레이블/장비 렌더러 기여 분리(SMR만 측정 병행).
///  ④ 배율 판별 — GetSoldierSizeMultiplier 값 + 사용처 전수 grep(소스 스캔) + lv25 재충원 수식 재현
///     (NormalizeSoldierHeightFactor × 배율 → 월드 실측 → 1.8 계약 이탈률 정량).
///  ⑤ 플레이어 — 원시 실측(1.156 재현) + 인게임 캘리브레이션 경로(PlayerPlaceholder.TryLoadGLBModel 순서:
///     Instantiate → NormalizeSoldierHeight(1.8, skinnedLocalBounds:false)) 적용치 실측.
/// 산출: TestOutput/npc139_heights_diag.txt
/// 실행: Unity.exe -batchmode -projectPath C:/Unity/code -executeMethod Npc139HeightProbe.RunAll -quit
/// </summary>
public static class Npc139HeightProbe
{
    private const string OutTxt = "TestOutput/npc139_heights_diag.txt";

    private static readonly string[] FbxKeys =
    {
        "Models/UserProvided/fbx/soldier_lv1-20_rigged",
        "Models/UserProvided/fbx/soldier_lv20-40_rigged",
        "Models/UserProvided/fbx/soldier_lv40-50_rigged",
    };
    private static readonly string[] GlbKeys =
    {
        "Models/UserProvided/Soldier_Lv1-20_Rigged",
        "Models/UserProvided/Soldier_Lv20-40_Rigged",
    };

    private readonly static List<UnityEngine.Object> _trash = new List<UnityEngine.Object>();

    public static void RunAll()
    {
        var log = new StringBuilder();
        log.AppendLine("[139차 P2 진단 프로브] RunAll 시작 — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        EnsureDir();

        // ── ① 병사 원시 실측(FBX 3구간 + GLB 2구간) — 정규화 없음 ──
        log.AppendLine();
        log.AppendLine("[139차 P2 ①] 병사 원시 Instantiate 직후 — MeasureStandingHeight 두 모드 병기(스케일/수리 없음)");
        foreach (var fbxKey in FbxKeys) MeasureRaw(fbxKey, log);
        foreach (var glbKey in GlbKeys) MeasureRaw(glbKey, log);

        // ── ② 정규화 모드 대조(lv1-20 FBX 대표 — 139차 3.289 재현 층) ──
        log.AppendLine();
        log.AppendLine("[139차 P2 ②] NormalizeSoldierHeight 모드 대조 — localBounds(현행 기본) vs 월드(수리 후보) + 멱등성");
        NormalizeModeCompare(FbxKeys[0], "FBX_lv1-20", log);
        NormalizeModeCompare(FbxKeys[1], "FBX_lv20-40", log);
        NormalizeModeCompare(FbxKeys[2], "FBX_lv40-50", log);
        NormalizeModeCompare(GlbKeys[0], "GLB_lv1-20", log);
        NormalizeModeCompare(GlbKeys[1], "GLB_lv20-40", log);

        // ── ③ 실경로 재현 — SpawnDeployedGuard(lv5) = 139 프로브 조건 ──
        log.AppendLine();
        log.AppendLine("[139차 P2 ③] 실경로 SpawnDeployedGuard(lv5) — 139 프로브 3.289 재현 + 렌더러 기여 분리 + anim.Update 스테일 판별");
        try
        {
            var soldier = TerritoryBuilder.SpawnDeployedGuard("T139P2Soldier", 5, NationType.West, new Vector3(46f, 0f, 4f));
            if (soldier != null)
            {
                _trash.Add(soldier);
                DumpScaleChain(soldier, "실경로 병사", log);
                DumpRenderers(soldier, "실경로 병사(레이블 포함 전체)", log);
                float hLoc = TerritoryBuilder.MeasureStandingHeight(soldier, true);
                float hWor = TerritoryBuilder.MeasureStandingHeight(soldier, false);
                float hSmr = MeasureSmrOnly(soldier);
                log.AppendLine($"  실경로 스폰 직후: h_local={hLoc:F3}m h_world={hWor:F3}m h_smrOnly={hSmr:F3}m rootScale={soldier.transform.localScale.x:F4} (139차 실측 3.289m 대조)");
                // anim.Update — 에디터 배치 스테일 판별(스키닝 갱신 후 재측정)
                var anim = soldier.GetComponentInChildren<Animator>(true);
                if (anim == null) anim = soldier.AddComponent<Animator>();
                try
                {
                    anim.Update(0.05f);
                    float hWor2 = TerritoryBuilder.MeasureStandingHeight(soldier, false);
                    log.AppendLine($"  anim.Update(0.05) 후: h_world={hWor2:F3}m (전 {hWor:F3}m, Δ={hWor2 - hWor:+0.000;-0.000}) — Δ≈0이면 스테일 아님(월드 bounds 즉시 신뢰 가능)");
                }
                catch (Exception e) { log.AppendLine($"  anim.Update 실패(기록만) {e.GetType().Name}: {e.Message}"); }
                // NormalizeSoldierHeight 재적용(현행 모드) — 실경로 결과물에 대한 멱등성
                TerritoryBuilder.NormalizeSoldierHeight(soldier, TerritoryBuilder.SoldierTargetHeight);
                float hLocAfter = TerritoryBuilder.MeasureStandingHeight(soldier, true);
                float hWorAfter = TerritoryBuilder.MeasureStandingHeight(soldier, false);
                log.AppendLine($"  NormalizeSoldierHeight(현행 localBounds 모드) 재적용 후: h_local={hLocAfter:F3}m h_world={hWorAfter:F3}m rootScale={soldier.transform.localScale.x:F4}");
            }
            else log.AppendLine("  실경로 스폰 실패(null)");
        }
        catch (Exception e) { log.AppendLine($"  실경로 스폰 예외 {e.GetType().Name}: {e.Message}"); }

        // ── ④ 배율 판별 — GetSoldierSizeMultiplier 값 + 사용처 전수 + lv25 재충원 수식 재현 ──
        log.AppendLine();
        log.AppendLine("[139차 P2 ④] GetSoldierSizeMultiplier — 값/사용처 전수 grep/재충원 수식 재현");
        var lvSamples = new[] { 1, 19, 20, 25, 50 };
        log.AppendLine("  값: " + string.Join(", ", lvSamples.Select(lv => $"lv{lv}={GuardManager.GetSoldierSizeMultiplier(lv):F2}")));
        GrepMultiplierUsages(log);
        ReproduceRefillFormula(GlbKeys[1], 25, log);   // lv20-40 GLB — refill lv25 소스

        // ── ⑤ 플레이어 — 원시 + 인게임 캘리브레이션 경로 적용치 ──
        log.AppendLine();
        log.AppendLine("[139차 P2 ⑤] 플레이어 — 원시 실측(1.156 재현) + 인게임 캘리브레이션 경로(PlayerPlaceholder 순서) 적용치");
        try
        {
            if (RuntimeModelLoader.TryGetModel("player", out var playerPrefab) && playerPrefab != null)
            {
                var raw = UnityEngine.Object.Instantiate(playerPrefab);
                raw.name = "T139P2PlayerRaw";
                raw.transform.position = new Vector3(48f, 0f, 4f);
                _trash.Add(raw);
                float rawLoc = TerritoryBuilder.MeasureStandingHeight(raw, true);
                float rawWor = TerritoryBuilder.MeasureStandingHeight(raw, false);
                log.AppendLine($"  원시: h_local={rawLoc:F3}m h_world={rawWor:F3}m (139차 원시 1.156m=월드 모드 대조)");

                var cal = UnityEngine.Object.Instantiate(playerPrefab);
                cal.name = "T139P2PlayerCal";
                cal.transform.position = new Vector3(50f, 0f, 4f);
                _trash.Add(cal);
                // 인게임 캘리브레이션 경로 재현 — PlayerPlaceholder.TryLoadGLBModel L150 순서
                // (ModelAnimatorAssigner 부착은 에디터 배치에서 Awake 미실행으로 bounds 무영향 — 측정 경로에서 생략 없음 명시)
                TerritoryBuilder.NormalizeSoldierHeight(cal, TerritoryBuilder.SoldierTargetHeight, skinnedLocalBounds: false);
                float calLoc = TerritoryBuilder.MeasureStandingHeight(cal, true);
                float calWor = TerritoryBuilder.MeasureStandingHeight(cal, false);
                float calSmr = MeasureSmrOnly(cal);
                log.AppendLine($"  캘리브레이션 적용 후: h_world={calWor:F3}m h_local={calLoc:F3}m h_smrOnly={calSmr:F3}m scale={cal.transform.localScale.x:F4} → 1.8±0.05 판정={(Math.Abs(calWor - 1.8f) <= 0.05f ? "✓PASS" : "✗FAIL")}");
                DumpRenderers(cal, "플레이어(캘리브레이션 후)", log);
            }
            else log.AppendLine("  플레이어 프리팹 로드 실패");
        }
        catch (Exception e) { log.AppendLine($"  플레이어 예외 {e.GetType().Name}: {e.Message}"); }

        log.AppendLine();
        log.AppendLine("[139차 P2 진단 프로브] DONE — 판정/수리 없음(측정 전용). 수치는 상위 보고에서 판정.");
        File.WriteAllText(OutTxt, log.ToString());
        Debug.Log($"[139차 P2 진단 프로브] DONE — {OutTxt}");

        foreach (var o in _trash)
            if (o != null) UnityEngine.Object.DestroyImmediate(o);
        _trash.Clear();
    }

    /// <summary>① 원시 실측 — Instantiate 직후 두 모드 + 렌더러/스케일 체인 덤프.</summary>
    private static void MeasureRaw(string resKey, StringBuilder log)
    {
        try
        {
            var prefab = Resources.Load<GameObject>(resKey);
            if (prefab == null) { log.AppendLine($"  [{resKey}] 프리팹 로드 실패"); return; }
            var go = UnityEngine.Object.Instantiate(prefab);
            go.name = "Raw_" + Path.GetFileName(resKey);
            go.transform.position = new Vector3(44f, 0.05f, 0f);
            _trash.Add(go);
            float hLoc = TerritoryBuilder.MeasureStandingHeight(go, true);
            float hWor = TerritoryBuilder.MeasureStandingHeight(go, false);
            float hSmr = MeasureSmrOnly(go);
            log.AppendLine($"  [{resKey}] 원시: h_local={hLoc:F3}m h_world={hWor:F3}m h_smrOnly={hSmr:F3}m rootScale={go.transform.localScale.x:F4} local/world비={(hLoc > 0.01f ? (hWor / hLoc).ToString("F3") : "n/a")}");
            DumpScaleChain(go, resKey, log);
            DumpRenderers(go, resKey + "(원시)", log);
        }
        catch (Exception e)
        {
            log.AppendLine($"  [{resKey}] 예외 {e.GetType().Name}: {e.Message}");
        }
    }

    /// <summary>② 모드 대조 — 동일 소스를 localBounds 모드/월드 모드로 각각 정규화(서로 다른 인스턴스) + 멱등성.</summary>
    private static void NormalizeModeCompare(string resKey, string tag, StringBuilder log)
    {
        try
        {
            var prefab = Resources.Load<GameObject>(resKey);
            if (prefab == null) { log.AppendLine($"  [{tag}] 프리팹 로드 실패"); return; }

            // (a) 현행 경로 — localBounds 모드(139차 CreateGuard 조건)
            var a = UnityEngine.Object.Instantiate(prefab);
            a.name = "NormLocal_" + tag;
            a.transform.position = new Vector3(44f, 0.05f, 0f);
            _trash.Add(a);
            TerritoryBuilder.NormalizeSoldierHeight(a, TerritoryBuilder.SoldierTargetHeight, skinnedLocalBounds: true);
            float aLoc = TerritoryBuilder.MeasureStandingHeight(a, true);
            float aWor = TerritoryBuilder.MeasureStandingHeight(a, false);
            float aSmr = MeasureSmrOnly(a);
            log.AppendLine($"  [{tag}] localBounds 모드 정규화: scale={a.transform.localScale.x:F4} → h_local={aLoc:F3}m h_world={aWor:F3}m h_smrOnly={aSmr:F3}m 계약1.8±0.05 월드판정={(Math.Abs(aWor - 1.8f) <= 0.05f ? "✓PASS" : "✗FAIL")}");
            // 멱등성 — 같은 모드 재적용
            TerritoryBuilder.NormalizeSoldierHeight(a, TerritoryBuilder.SoldierTargetHeight, skinnedLocalBounds: true);
            float aLoc2 = TerritoryBuilder.MeasureStandingHeight(a, true);
            log.AppendLine($"  [{tag}] localBounds 재적용(멱등): h_local={aLoc2:F3}m scale={a.transform.localScale.x:F4} (Δscale={a.transform.localScale.x - (aLoc > 0 ? 0 : 0):F4})");

            // (b) 수리 후보 — 월드 모드
            var b = UnityEngine.Object.Instantiate(prefab);
            b.name = "NormWorld_" + tag;
            b.transform.position = new Vector3(46f, 0.05f, 0f);
            _trash.Add(b);
            TerritoryBuilder.NormalizeSoldierHeight(b, TerritoryBuilder.SoldierTargetHeight, skinnedLocalBounds: false);
            float bLoc = TerritoryBuilder.MeasureStandingHeight(b, true);
            float bWor = TerritoryBuilder.MeasureStandingHeight(b, false);
            float bSmr = MeasureSmrOnly(b);
            log.AppendLine($"  [{tag}] 월드 모드 정규화: scale={b.transform.localScale.x:F4} → h_local={bLoc:F3}m h_world={bWor:F3}m h_smrOnly={bSmr:F3}m 계약1.8±0.05 월드판정={(Math.Abs(bWor - 1.8f) <= 0.05f ? "✓PASS" : "✗FAIL")}");
            // 멱등성 — 월드 모드 재적용
            float bScaleBefore = b.transform.localScale.x;
            TerritoryBuilder.NormalizeSoldierHeight(b, TerritoryBuilder.SoldierTargetHeight, skinnedLocalBounds: false);
            float bWor2 = TerritoryBuilder.MeasureStandingHeight(b, false);
            log.AppendLine($"  [{tag}] 월드 재적용(멱등): h_world={bWor2:F3}m scale {bScaleBefore:F4}→{b.transform.localScale.x:F4}");
        }
        catch (Exception e)
        {
            log.AppendLine($"  [{tag}] 예외 {e.GetType().Name}: {e.Message}");
        }
    }

    /// <summary>④ lv25 재충원 수식 재현 — GuardManager.TryRefillTerritory L371-373 동일식: Factor(현행) × 배율.</summary>
    private static void ReproduceRefillFormula(string glbKey, int level, StringBuilder log)
    {
        try
        {
            var prefab = Resources.Load<GameObject>(glbKey);
            if (prefab == null) { log.AppendLine($"  [재충원수식 {glbKey}] 프리팹 로드 실패"); return; }
            var go = UnityEngine.Object.Instantiate(prefab);
            go.name = "RefillFormula_lv" + level;
            go.transform.position = new Vector3(52f, 0.05f, 0f);
            _trash.Add(go);

            // TryRefillTerritory와 동일 순서: Factor 산출 → 배율 곱 → localScale 대입
            float s = TerritoryBuilder.NormalizeSoldierHeightFactor(go, TerritoryBuilder.SoldierTargetHeight);
            float mult = GuardManager.GetSoldierSizeMultiplier(level);
            s *= mult;
            go.transform.localScale = Vector3.one * s;
            float hWor = TerritoryBuilder.MeasureStandingHeight(go, false);
            float hSmr = MeasureSmrOnly(go);
            log.AppendLine($"  [재충원수식 lv{level}] Factor(현행 localBounds 모드)×배율{mult:F2} → scale={s:F4} h_world={hWor:F3}m h_smrOnly={hSmr:F3}m → 1.8 계약 이탈률={hWor / 1.8f:F3}× (계약 {(Math.Abs(hWor - 1.8f) <= 0.05f ? "✓PASS" : "✗FAIL — 배율/측정모드가 계약을 깬다")})");
        }
        catch (Exception e)
        {
            log.AppendLine($"  [재충원수식] 예외 {e.GetType().Name}: {e.Message}");
        }
    }

    /// <summary>④ 사용처 전수 grep — Assets/**/*.cs 소스 스캔(에디터 IO — 배치 1회).</summary>
    private static void GrepMultiplierUsages(StringBuilder log)
    {
        try
        {
            var files = Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories);
            int hits = 0;
            foreach (var f in files.OrderBy(f => f))
            {
                string[] lines = File.ReadAllLines(f);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Contains("GetSoldierSizeMultiplier"))
                    {
                        hits++;
                        log.AppendLine($"    {f.Replace('\\', '/')}:{i + 1}: {lines[i].Trim()}");
                    }
                }
            }
            log.AppendLine($"  사용처 총 {hits}라인 — lv20+ 배율 1.8가 정규화(1.8m) 위에 곱해지는 호출부 = 1.8 계약 위반 후보.");
        }
        catch (Exception e)
        {
            log.AppendLine($"  소스 스캔 예외 {e.GetType().Name}: {e.Message}");
        }
    }

    /// <summary>SMR(캐릭터 몸) 전용 측정 — 레이블/장비/이펙트 렌더러 기여 분리용(프로브 로컬).</summary>
    private static float MeasureSmrOnly(GameObject go)
    {
        float minY = float.MaxValue, maxY = float.MinValue;
        bool any = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (!(r is SkinnedMeshRenderer smr) || smr.sharedMesh == null) continue;
            var lb = smr.localBounds;
            var m = smr.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                var corner = lb.center + new Vector3(((i & 1) == 0 ? 1 : -1) * lb.extents.x,
                                                     ((i & 2) == 0 ? 1 : -1) * lb.extents.y,
                                                     ((i & 4) == 0 ? 1 : -1) * lb.extents.z);
                var w = m.MultiplyPoint3x4(corner);
                if (w.y < minY) minY = w.y;
                if (w.y > maxY) maxY = w.y;
            }
            any = true;
        }
        return any ? Mathf.Max(0f, maxY - minY) : 0f;
    }

    /// <summary>렌더러별 기여 덤프 — 3.289 원인 렌더러 특정(월드 min/max + SMR localBounds/mesh AABB 병기).</summary>
    private static void DumpRenderers(GameObject go, string title, StringBuilder log)
    {
        var rends = go.GetComponentsInChildren<Renderer>(true);
        log.AppendLine($"    [렌더러 덤프 {title}] 총 {rends.Length}개");
        foreach (var r in rends)
        {
            string type = r.GetType().Name;
            var b = r.bounds;
            string extra = "";
            if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
            {
                var lb = smr.localBounds;
                var mb = smr.sharedMesh.bounds;
                extra = $" localBounds.y=[{lb.min.y:F3}~{lb.max.y:F3}] meshBounds.y=[{mb.min.y:F3}~{mb.max.y:F3}] rootBone={(smr.rootBone != null ? smr.rootBone.name : "null")}";
            }
            log.AppendLine($"      {type} \"{r.name}\" world.y=[{b.min.y:F3}~{b.max.y:F3}] h={b.size.y:F3}{extra}");
        }
    }

    /// <summary>스케일 체인 덤프 — 루트/1차 자식/SMR 조상 체인 localScale·lossyScale(골격 스케일 구조 판별).</summary>
    private static void DumpScaleChain(GameObject go, string title, StringBuilder log)
    {
        log.AppendLine($"    [스케일 체인 {title}] root \"{go.name}\" localScale={V3(go.transform.localScale)} lossyScale={V3(go.transform.lossyScale)}");
        foreach (Transform child in go.transform)
            log.AppendLine($"      ├ \"{child.name}\" localScale={V3(child.localScale)} lossyScale={V3(child.lossyScale)}");
        var smr = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (smr != null)
        {
            var chain = new StringBuilder();
            var t = smr.transform;
            while (t != null) { chain.Insert(0, $" ← \"{t.name}\"(ls={V3(t.localScale)})"); t = t.parent; }
            log.AppendLine($"      └ SMR \"{smr.name}\" 체인:{chain}");
        }
    }

    private static void EnsureDir()
    {
        if (!Directory.Exists("TestOutput")) Directory.CreateDirectory("TestOutput");
    }

    private static string V3(Vector3 v) => $"({v.x:F3},{v.y:F3},{v.z:F3})";
}
