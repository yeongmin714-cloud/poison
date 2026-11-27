using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [무번호 43-SWEEPB2] 배치 Play 런타임 렌더러 셰이더 전수 스캐너 v2 — 강제 피격 재현 포함.
    /// v1 실측: Test_10 Play 6~7.5s — 렌더러 21개 / 파손 0건, 단 슬라임 공격 미발생(피격 FX 체인 미가동).
    /// v2: 6s 대기 후 슬라임(AnimalAI) TakeDamage 강제 호출(사용자 보고 "몬스터 피격시" 순간 재현) →
    ///   +0.3s(피격 FX 수명 내)·+2.5s(잔존) 2회 스윕 + 전체 렌더러 인벤토리 덤프(결과 파일 append-only — v1 WriteAllText 덮어침 결함 수리).
    /// </summary>
    [InitializeOnLoad]
    public static class MagentaSweepBatchRunner2
    {
        private const string PhaseKey = "MagentaSweep2_Phase";
        private const string StartTimeKey = "MagentaSweep2_StartTime";
        private const string DamageTimeKey = "MagentaSweep2_DamageTime";
        private const string ResultPath = "TestOutput/qa63-playsweepB2-result.txt";

        private static int Phase
        {
            get => SessionState.GetInt(PhaseKey, 0);
            set => SessionState.SetInt(PhaseKey, value);
        }

        static MagentaSweepBatchRunner2()
        {
            if (Phase > 0)
                EditorApplication.update += Tick;
        }

        /// <summary>-executeMethod 진입점.</summary>
        public static void Run()
        {
            if (Phase != 0)
                return;
            Phase = 1;
            EditorApplication.update += Tick;
            Debug.Log("[43-SWEEPB2] Run 진입");
        }

        private static void Tick()
        {
            switch (Phase)
            {
                case 1:   // 씬 로드 → Play 진입
                    if (EditorApplication.isCompiling) return;
                    if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                    if (SceneManager.GetActiveScene().name != "Test_10_TerritoryCombat")
                    {
                        EditorSceneManager.OpenScene(
                            "Assets/Scenes/TestScenes/Test_10_TerritoryCombat.unity", OpenSceneMode.Single);
                        Debug.Log("[43-SWEEPB2] 씬 오픈 완료");
                        return;
                    }
                    Debug.Log("[43-SWEEPB2] EnterPlaymode");
                    EditorApplication.EnterPlaymode();
                    Phase = 2;
                    break;

                case 2:
                    if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
                    SessionState.SetFloat(StartTimeKey, Time.realtimeSinceStartup);
                    Debug.Log("[43-SWEEPB2] Play 진입 확인 — 6s 후 강제 피격");
                    Phase = 3;
                    break;

                case 3:   // 6s 경과 → 슬라임 강제 피격(CombatFXGate 전 체인 재현)
                    if (!EditorApplication.isPlaying) { Phase = 7; return; }
                    if (Time.realtimeSinceStartup - SessionState.GetFloat(StartTimeKey, 0f) < 6f) return;

                    var ai = Object.FindFirstObjectByType<ProjectName.Systems.AnimalAI>();
                    if (ai == null)
                    {
                        Debug.LogWarning("[43-SWEEPB2] AnimalAI 미발견 — 슬라임 미스폰?");
                    }
                    else
                    {
                        Debug.Log($"[43-SWEEPB2] 강제 피격 발화: {ai.gameObject.name} @ {ai.transform.position:F2}");
                        // TakeDamage(float, Vector3, string) — GuardPlaceholder.PerformAttack과 동일 시그니처
                        var mi = typeof(ProjectName.Systems.AnimalAI).GetMethod("TakeDamage",
                            new[] { typeof(float), typeof(Vector3), typeof(string) });
                        if (mi != null)
                            mi.Invoke(ai, new object[] { 12f, Vector3.back, "melee" });
                        else
                            ai.gameObject.SendMessage("TakeDamage", 12f, SendMessageOptions.DontRequireReceiver);
                    }
                    SessionState.SetFloat(DamageTimeKey, Time.realtimeSinceStartup);
                    Phase = 4;
                    break;

                case 4:   // +0.3s — 피격 FX 수명 내 스윕
                    if (!EditorApplication.isPlaying) { Phase = 7; return; }
                    if (Time.realtimeSinceStartup - SessionState.GetFloat(DamageTimeKey, 0f) < 0.3f) return;
                    Sweep("posthit-0.3s", true);
                    Phase = 5;
                    break;

                case 5:   // +2.5s — 잔존 스윕 → 탈출
                    if (!EditorApplication.isPlaying) { Phase = 7; return; }
                    if (Time.realtimeSinceStartup - SessionState.GetFloat(DamageTimeKey, 0f) < 2.5f) return;
                    Sweep("posthit-2.5s", false);
                    Phase = 6;
                    EditorApplication.ExitPlaymode();
                    break;

                case 6:   // 탈출 완료
                    if (EditorApplication.isPlaying || EditorApplication.isCompiling) return;
                    File.AppendAllText(ResultPath, "SWEEP_DONE\n");
                    Debug.Log("[43-SWEEPB2] 완료 — 배치 종료");
                    Phase = 0;
                    EditorApplication.update -= Tick;
                    EditorApplication.Exit(0);
                    break;

                case 7:   // 예외 탈출(플레이 세션 종료됨)
                    if (EditorApplication.isPlaying || EditorApplication.isCompiling) return;
                    File.AppendAllText(ResultPath, "SWEEP_ABORTED\n");
                    Phase = 0;
                    EditorApplication.update -= Tick;
                    EditorApplication.Exit(0);
                    break;
            }
        }

        /// <summary>씬 전체 렌더러 실측(읽기 전용). fullDump=true → 전 렌더러 경로+셰이더 나열.</summary>
        private static void Sweep(string tag, bool fullDump)
        {
            var broken = new List<string>();
            var inventory = new List<string>();
            int count = 0;
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Renderer r in renderers)
            {
                count++;
                Material[] mats = r.sharedMaterials;
                if (mats == null) continue;
                foreach (Material m in mats)
                {
                    string shaderName = (m != null && m.shader != null) ? m.shader.name : (m == null ? "<null material>" : "<null shader>");
                    inventory.Add($"{PathOf(r)} | mat='{(m == null ? "<null>" : m.name)}' shader='{shaderName}'");
                    if (m == null || m.shader == null || m.shader.name == "Hidden/InternalErrorShader")
                        broken.Add($"{PathOf(r)} mat='{(m == null ? "<null>" : m.name)}' shader='{shaderName}' @ {r.transform.position:F2} bounds={r.bounds.size:F2}");
                }
            }

            using (var w = File.AppendText(ResultPath))
            {
                w.WriteLine($"=== SWEEP {tag} — 렌더러 {count}개 / 파손 {broken.Count}건 ===");
                foreach (string b in broken) w.WriteLine("BROKEN: " + b);
                if (fullDump)
                {
                    w.WriteLine("-- 전체 렌더러 인벤토리 --");
                    foreach (string line in inventory) w.WriteLine("  " + line);
                }
                w.WriteLine();
            }

            Debug.Log($"[43-SWEEPB2:{tag}] 렌더러 {count}개 / 파손 {broken.Count}건");
            foreach (string b in broken) Debug.LogWarning($"[43-SWEEPB2:{tag}] 파손: {b}");
        }

        private static string PathOf(Renderer r)
        {
            Transform t = r.transform;
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}
