using System.Collections.Generic;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [41차] 출동 배지(배치조 → 실제 명령 수행) — 실내↔월드 씬 경계 병사 운반.
    ///
    /// IndoorScene은 월드 언로드/재로드 구조(IndoorSceneTransition P19)라 실내에서 생성된
    /// 병사는 씬 언로드와 함께 사라진다. 이 시스템이 그 간극을 메운다:
    ///   1) CaptureFromIndoor — 성 퇴출 시 역할(배치조)이 부여된 병사만 스냅샷(명부/레벨/국가/역할)
    ///   2) SpawnAllInWorld  — 월드 복귀 직후 성문 밖(플레이어 입장 위치=문밖)에 재스폰하고
    ///                         GuardTaskSystem.AssignTask로 역할을 재부여 → 0.4s 루틴이 자동 실행
    ///                         (Attack=플레이어 추종 / Defend=영지 안 수비 / Farm/Gather/Mine=주변
    ///                          노드 탐색 후 작업 / Hunt=몬스터 사냥 / Envoy=첩보)
    /// 역할 미부여 병사는 실내에 남아 침대 휴식(다음 입장 시 재배치) — 배치조 보내기 = 출동의 의미.
    /// </summary>
    public static class AttackSquadDeployment
    {
        private sealed class PendingGuard
        {
            public string Name;
            public int Level;
            public NationType Nation;
            public GuardTaskSystem.GuardTask Task;
            public Vector3 GatePos;
        }

        private static readonly List<PendingGuard> _pending = new();
        private static int _spawnSeq;

        public static int PendingCount => _pending.Count;

        /// <summary>성 퇴출 직전 호출 — 역할이 부여된 실내 병사를 출동 스냅샷으로 포획.</summary>
        public static void CaptureFromIndoor(Vector3 gateWorldPos)
        {
            var gm = GuardManager.Instance;
            var gts = GuardTaskSystem.Instance;
            if (gm == null) return;

            int captured = 0;
            foreach (var g in gm.GetAllPlayerGuards())
            {
                if (g == null) continue;
                var task = gts != null ? gts.GetTask(g) : GuardTaskSystem.GuardTask.None;
                if (task == GuardTaskSystem.GuardTask.None) continue;   // 역할 없음 — 실내 잔류(침대 휴식)

                _pending.Add(new PendingGuard
                {
                    Name = g.GuardName,
                    Level = g.Level,
                    Nation = ParseNation(g.Nation),
                    Task = task,
                    GatePos = gateWorldPos,
                });
                captured++;
                Debug.Log($"[AttackSquadDeployment] 📦 출동 대기 — {g.GuardName} ({task}) → 문밖 배치 예정");
            }
            if (captured > 0)
                Debug.Log($"[AttackSquadDeployment] 출동 포획 {captured}명 — 월드 복귀 시 문밖 스폰 예정");
        }

        /// <summary>월드 복귀 직후 호출 — 문밖에 출동 병사 재스폰 + 역할 재부여(루틴 자동 실행).</summary>
        public static void SpawnAllInWorld()
        {
            if (_pending.Count == 0) return;

            int spawned = 0;
            foreach (var p in _pending)
            {
                Vector3 pos = p.GatePos + new Vector3((_spawnSeq % 5 - 2) * 1.3f, 0f, -1.5f - (_spawnSeq % 3));
                _spawnSeq++;
                var go = TerritoryBuilder.SpawnDeployedGuard(p.Name + "(출동)", p.Level, p.Nation, pos);
                if (go == null) continue;

                var ph = go.GetComponent<GuardPlaceholder>();
                if (ph != null)
                {
                    GuardManager.Instance?.RegisterStandalone(ph);
                    var gts = GuardTaskSystem.Ensure();
                    gts.AssignTask(ph, p.Task);   // 역할 루틴 개시 — Update 루프가 명령을 수행
                }
                spawned++;
            }
            Debug.Log($"[AttackSquadDeployment] ⚔️ 출동 병사 월드 배치 — {spawned}/{_pending.Count}명 (문밖 → 역할 루틴 실행)");
            _pending.Clear();
        }

        private static NationType ParseNation(string nationName)
        {
            if (string.IsNullOrEmpty(nationName)) return NationType.Empire;
            string s = nationName.Trim();
            if (s.Contains("황제")) return NationType.Empire;
            if (s.Contains("동")) return NationType.East;
            if (s.Contains("서")) return NationType.West;
            if (s.Contains("남")) return NationType.South;
            if (s.Contains("북")) return NationType.North;
            return NationType.Empire;
        }
    }
}
