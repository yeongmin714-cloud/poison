using UnityEngine;
using System.Reflection;
using System.IO;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.UI;

/// <summary>
/// [210차] 모루 아이콘 밑동(stump) 제외 — 베이크 프로브(before는 209차-E 산출물 재사용, 본 프로브는 after만 생성).
/// - RunAnvilNoStump : ItemHoldManager 은닉 레지스트리("anvil-1286cb" → "stump") 경유 재베이크 → anvil_nostump.png
///   ① 레지스트리 조회 실측(GetHideNodeNames) ② bounds 실측(은닉 전/후 — stump 렌더러 제외·프레이밍 확대 판정)
///   ③ SyncBakeItem GLB 직결 1회 호출(165b차 은닉 규약 통과 — TryMount/SyncBakeFromPrefab 동일).
/// 월드 모델 GLB 원형 유지(파일 미수정 — 아이콘 렌더에서만 은닉). AO 확대 없음(209차 곱셈 상태 유지).
/// 런타임/게임 코드 수정 없음(에디터 프로브 신설만). 커밋 금지 세션 산출물.
/// 사용: Unity.exe -batchmode -projectPath C:\Unity\code -executeMethod IconCompare210.RunAnvilNoStump -logFile ... -quit
/// </summary>
public static class IconCompare210
{
    private const string OutDir = "TestOutput/icon_compare210";
    private const string Id = "polyfork_anvil";     // 캐시/노드 명명용 — 아이템 테이블 비의존(209차-E 동일)
    private const string Key = "anvil-1286cb";      // modelKey — [210차] ItemHoldManager 은닉 레지스트리 등록 키
    private const string Glb = "Assets/Resources/Models/Polyfork/anvil-1286cb.glb";

    public static void RunAnvilNoStump()
    {
        Directory.CreateDirectory(OutDir);
        var log = new System.Text.StringBuilder();
        log.AppendLine($"RESOLVED: id={Id} key={Key} glb={Glb}");

        // ── ① 은닉 레지스트리 실측 — [210차] 등록 키 정합(미등록이면 이후 전부 무의미) ──
        var hide = ItemHoldManager.GetHideNodeNames(Key);
        log.AppendLine(hide != null && hide.Length > 0
            ? $"REGISTRY: key={Key} hide=[{string.Join(",", hide)}]"
            : $"REGISTRY: key={Key} hide=null (미등록 — 210차 등록 누락!)");

        // ── ② bounds 실측 — 은닉 전/후 수집(GetComponentsInChildren 기본 includeInactive=false 재현) ──
        GameObject probe = null;
        try
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(Glb);
            if (prefab != null)
            {
                probe = Object.Instantiate(prefab);
                probe.name = "IconCompare210_BoundsProbe";
                probe.transform.position = new Vector3(10000f, 1000f, 10000f);

                var rendsAll = probe.GetComponentsInChildren<Renderer>();   // 은닉 전(밑동 포함)
                Bounds bAll = rendsAll.Length > 0 ? rendsAll[0].bounds : new Bounds();
                for (int i = 1; i < rendsAll.Length; i++) bAll.Encapsulate(rendsAll[i].bounds);
                log.AppendLine($"BOUNDS_BEFORE: rends={rendsAll.Length} size=({bAll.size.x:F3},{bAll.size.y:F3},{bAll.size.z:F3}) maxDim={Mathf.Max(bAll.size.x, Mathf.Max(bAll.size.y, bAll.size.z)):F3}");

                // 165b차 규약 동일 — GetHideNodeNames → FindHideNode → SetActive(false)
                var stump = ItemHoldManager.FindHideNode(probe.transform, "stump");
                if (stump != null) stump.gameObject.SetActive(false);
                log.AppendLine($"HIDE: stump_found={stump != null} stump_active={(stump != null ? stump.gameObject.activeSelf.ToString() : "n/a")}");

                var rendsHide = probe.GetComponentsInChildren<Renderer>();  // 은닉 후(기본=false — 비활성 렌더러 미수집)
                Bounds bHide = rendsHide.Length > 0 ? rendsHide[0].bounds : new Bounds();
                for (int i = 1; i < rendsHide.Length; i++) bHide.Encapsulate(rendsHide[i].bounds);
                float maxAll = Mathf.Max(bAll.size.x, Mathf.Max(bAll.size.y, bAll.size.z));
                float maxHide = Mathf.Max(bHide.size.x, Mathf.Max(bHide.size.y, bHide.size.z));
                log.AppendLine($"BOUNDS_AFTER: rends={rendsHide.Length} size=({bHide.size.x:F3},{bHide.size.y:F3},{bHide.size.z:F3}) maxDim={maxHide:F3}");
                log.AppendLine($"FRAMING: maxDim {maxAll:F3}→{maxHide:F3} scale_gain={maxAll / Mathf.Max(0.0001f, maxHide):F3} (1.8/maxDim 핏 — >1이면 프레이밍 확대)");
                log.AppendLine($"BOUNDS_ORDER: 은닉(SetActive false) 선행 → 수집 제외 판정 rends {rendsAll.Length}→{rendsHide.Length} (감소=stump bounds 제외 확정)");
            }
            else log.AppendLine("BOUNDS: prefab 로드 실패(AssetDatabase) — 실측 스킵");
        }
        catch (System.Exception e) { log.AppendLine($"EXC(bounds): {e.GetType().Name}: {e.Message}"); }
        finally { if (probe != null) Object.DestroyImmediate(probe); }

        // ── ③ 실제 베이크 — SyncBakeItem GLB 직결(209차-E 동일 패턴, 은닉 체계 경유) ──
        var miSync = typeof(GblItemIconRenderer).GetMethod("SyncBakeItem",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (miSync == null)
        {
            log.AppendLine("FAIL: SyncBakeItem reflection null");
            File.WriteAllText(Path.Combine(OutDir, "facts_210_anvil_nostump.log"), log.ToString());
            return;
        }

        Texture2D tex = null;
        try { tex = (Texture2D)miSync.Invoke(null, new object[] { Id, Key, Glb }); }
        catch (System.Exception e) { log.AppendLine($"EXC(glb): {e.GetType().Name}: {e.Message}"); }

        if (tex == null)
        {
            log.AppendLine("MISS: anvil bake=null");
            log.AppendLine("DONE");
            File.WriteAllText(Path.Combine(OutDir, "facts_210_anvil_nostump.log"), log.ToString());
            return;
        }

        string png = Path.Combine(OutDir, "anvil_nostump.png");
        File.WriteAllBytes(png, tex.EncodeToPNG());

        Color sum = Color.clear; int n = 0;
        for (int y = tex.height / 3; y < tex.height * 2 / 3; y++)
            for (int x = tex.width / 3; x < tex.width * 2 / 3; x++)
            { sum += tex.GetPixel(x, y); n++; }
        Color avg = sum / n;
        log.AppendLine($"OK: {Id} key={Key} size={tex.width}x{tex.height} avgRGB=({avg.r:F2},{avg.g:F2},{avg.b:F2}) png={png}");
        Object.DestroyImmediate(tex);

        log.AppendLine("DONE");
        File.WriteAllText(Path.Combine(OutDir, "facts_210_anvil_nostump.log"), log.ToString());
        Debug.Log($"[IconCompare210:anvil_nostump] 완료 — {png}");
    }
}
