using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;

/// <summary>
/// [126차] 영주 딸 기능 Play 자가검증 드라이버 — 배치모드 전용 (에디터 전용 스크립트, 빌드 미포함).
///
/// 실행: Unity.exe -batchmode -projectPath … -executeMethod LordDaughterPlayVerify.Run -logFile …
/// (단, -nographics 금지 — 렌더 캡처 필요)
///
/// 검증 시퀀스 (실제 런타임 경로):
///   ① SpawnForInvadedTerritory(East_01, LordOwned) — 침략 영주 본경로 → 내부 훅 → 딸 결정론 스폰
///   ② RT 렌더 캡처 #1 (영주+딸 캡슐, 연분홍 확인용)
///   ③ 들꽃 선물 → 호감도 +10 (PlayerInventory 실물 경유)
///   ④ 프로포즈 → 수락 + married + 충성도 +30 + 네임플레이트 ♥
///   ⑤ RT 렌더 캡처 #2 (결혼 후)
/// 판정: "[126PV]" 마커 로그 + 종료코드(0=PASS, 1=FAIL) — 사후 Editor.log grep + vision 판독용.
/// </summary>
public static class LordDaughterPlayVerify
{
    private const string Marker = "[126PV]";

    public static void Run()
    {
        Debug.Log($"{Marker} === START ===");
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        EditorApplication.isPlaying = true;
        EditorApplication.update += WaitPlaying;
    }

    private static void WaitPlaying()
    {
        if (!EditorApplication.isPlaying) return;
        EditorApplication.update -= WaitPlaying;
        var host = new GameObject("126PV_Host");
        host.AddComponent<LordDaughterPlayVerifyRunner>();
    }
}

/// <summary>Play 모드 실행 본체 — 코루틴으로 프레임 흐름을 보장하며 시퀀스를 수행한다.</summary>
public class LordDaughterPlayVerifyRunner : MonoBehaviour
{
    private const string Marker = "[126PV]";
    private const string TerritoryKey = "East_01";   // 결정론 롤 true 실측 키

    private IEnumerator Start()
    {
        // ── 준비: 몇 프레임 안정화 (씬 부팅/조명/인스턴스)
        for (int i = 0; i < 5; i++) yield return null;

        int fails = 0;

        // ── ① 실제 런타임 경로 스폰 (생존 영주 게이트 — 126차 정정 계약)
        var state = TerritoryDatabase.Instance.GetState(TerritoryKey);
        if (state == null) { Fail(ref fails, "1 SPAWN-PREP — East_01 상태 부재"); }
        else
        {
            state.ownership = TerritoryOwnership.LordOwned;   // 생존 영주
            var room = new GameObject("126PV_Room");
            var lord = InvadedLordEncounter.SpawnForInvadedTerritory(room, TerritoryKey);
            if (lord == null) Fail(ref fails, "1 SPAWN — 침략 영주 스폰 실패");
            else
            {
                var daughter = room.GetComponentInChildren<LordDaughterNPC>(true);
                if (daughter == null) Fail(ref fails, "1 SPAWN — 딸 미등장(훅/결정론 롤)");
                else
                {
                    Debug.Log($"{Marker} PASS 1 SPAWN — 영주={lord.LordName}, 딸={daughter.DisplayName}, local={daughter.transform.localPosition:F2}");
                    yield return null;

                    // ── ② 캡처 #1 — 영주+딸 (KeepOctagon 앵커 (10,0,24) / 딸 (11.6,0,24.6))
                    yield return Capture(new Vector3(14.2f, 2.2f, 27.6f), new Vector3(10.8f, 0.95f, 24.2f),
                        "TestOutput/126pv_1_spawn.png");
                    Debug.Log($"{Marker} INFO 1 CAPTURE-DONE");

                    // ── ③ 들꽃 선물 (실물 인벤토리 경유 — Play 모드 Awake 자동 실행)
                    var invGo = new GameObject("126PV_Inventory");
                    var inv = invGo.AddComponent<PlayerInventory>();
                    inv.AddItem(PlayerInventory.Flower_Wild, 2);
                    var gift = daughter.GiftFlower(inv);
                    int affection = TerritoryDatabase.Instance.GetState(TerritoryKey).daughterAffection;
                    if (gift != LordDaughterNPC.GiftResult.Success || affection != LordDaughterNPC.GiftAffectionGain)
                        Fail(ref fails, $"3 GIFT — 결과={gift}, 호감도={affection} (기대 Success/10)");
                    else
                        Debug.Log($"{Marker} PASS 3 GIFT — 호감도 {affection}/100, 잔여 들꽃={inv.GetItemCount(LordDaughterNPC.FlowerItemId)}");

                    // ── ④ 프로포즈 (호감도 10 → NotReady 먼저, 충족 후 수락)
                    var early = daughter.TryPropose();
                    if (early != LordDaughterNPC.ProposeResult.NotReady)
                        Fail(ref fails, $"4 PROPOSE-GATE — 게이트 미충족인데 결과={early}");
                    else
                        Debug.Log($"{Marker} PASS 4 PROPOSE-GATE — 호감도 10 < 80 거부(계약)");

                    TerritoryDatabase.Instance.GetState(TerritoryKey).daughterAffection = LordDaughterNPC.ProposeAffectionThreshold;
                    var s0 = TerritoryDatabase.Instance.GetState(TerritoryKey).loyaltyToPlayer;
                    var propose = daughter.TryPropose();
                    var s1 = TerritoryDatabase.Instance.GetState(TerritoryKey).loyaltyToPlayer;
                    var np = daughter.GetComponent<NameplateDisplay>();
                    bool married = TerritoryDatabase.Instance.GetState(TerritoryKey).daughterMarried;
                    bool plate = np != null && np.DisplayName != null && np.DisplayName.Contains("♥");
                    if (propose != LordDaughterNPC.ProposeResult.Accepted || !married
                        || System.Math.Abs((s1 - s0) - LordDaughterNPC.MarriageLoyaltyBonus) > 0.01f || !plate)
                        Fail(ref fails, $"4 PROPOSE — 결과={propose}, married={married}, 충성도 {s0}→{s1}, 네임플레이트♥={plate}");
                    else
                    {
                        Debug.Log($"{Marker} PASS 4 PROPOSE — 결혼 수락, 충성도 {s0}→{s1}, 네임플레이트='{np.DisplayName}'");
                        yield return Capture(new Vector3(13.4f, 2.0f, 27.2f), new Vector3(10.8f, 0.95f, 24.2f),
                            "TestOutput/126pv_2_married.png");
                        Debug.Log($"{Marker} INFO 4 CAPTURE-DONE");
                    }
                }
            }
        }

        // ── 요약 + 종료
        if (fails == 0)
        {
            Debug.Log($"{Marker} === SUMMARY: PASS ===");
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError($"{Marker} === SUMMARY: FAIL ({fails}건) ===");
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(1);
        }
    }

    private static void Fail(ref int fails, string what)
    {
        Debug.LogError($"{Marker} FAIL {what}");
        fails++;
    }

    /// <summary>RT 렌더 캡처 — 전용 카메라로 실제 GPU 렌더 결과를 PNG로 저장.</summary>
    private static IEnumerator Capture(Vector3 camPos, Vector3 lookAt, string relativeOut)
    {
        var camGo = new GameObject("126PV_CaptureCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 55f;
        cam.transform.position = camPos;
        cam.transform.LookAt(lookAt);
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.cullingMask = ~0;

        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt;
        yield return null;                       // 1프레임 렌더 안정화
        cam.Render();
        RenderTexture.active = rt;

        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        string path = Path.Combine(Application.dataPath, "..", relativeOut);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Debug.Log($"{Marker} CAPTURED {relativeOut} ({new FileInfo(path).Length} bytes)");

        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(camGo);
        yield return null;
    }
}
