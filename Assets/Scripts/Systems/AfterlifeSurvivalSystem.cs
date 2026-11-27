using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// [A2] 이계(冥界) 생존 시스템 — 사망 → 이계 웨이브 버티기 → 귀환/퍼머데스 (AFTERLIFE_SURVIVAL_PLAN).
    ///
    /// 흐름: PlayerHealth.Die → TryInterceptDeath(가로채기) → 이계 아레나 진입(월드 시간 동결) →
    ///   2분마다 웨이브 강화 → 타이머 완주 → 귀환 문 개방 → 접촉 → 정산 → 기존 위치 부활(HP 50%) → 시간 복원
    ///   이계 HP 0 = 완전 게임오버(퍼머데스) — 현재 슬롯 세이브 삭제 → 타이틀 복귀.
    ///
    /// - 타이머/웨이브 = Time.unscaledDeltaTime (불릿타임 무영향 — RespawnCoroutine 선례)
    /// - 지속시간 = 난이도별: Easy 5분 / Normal 10분 / Hard 15분
    /// - 웨이브 = 결정론 (사망 영지 링 + 웨이브 인덱스 → 수량/스탯 — 무작위 배치만 유한 랜덤)
    /// - 승률 우선 가드: 정예 상한/웨이브 수량 상한 — 파멸은 월드 판단 실수에서 와야 함
    /// </summary>
    public static class AfterlifeSurvivalSystem
    {
        // ===== 상수 =====
        public const int WAVE_INTERVAL_SEC = 120;          // 2분마다 웨이브 강화
        public const float REVIVE_HP_PERCENT = 0.5f;       // 이계 진입/귀환 HP 50%
        public const int MAX_ALIVE_ENEMIES = 12;           // 승률 우선 가드 — 동시 적 상한
        public const int RUN_GOLD_PER_KILL = 5;            // 처치당 수확 골드 기준 — [A3] 오브 드랍 총합의 기준값
        public const float TRIBUTE_RATIO = 0.2f;           // [A5] 봉헌(탈주) — 소유 골드 20% (가격 동결: 계획서 A5-2)
        public const float SURRENDER_HP_PERCENT = 0.10f;   // [A5] 봉헌 귀환 HP 10%
        public const float SURRENDER_HARVEST_RATIO = 0.5f; // [A5] 봉헌 — 수확 50% (실패의 회수감)

        // ===== 상태 =====
        public static bool UseAfterlifeRespawn { get; set; } = true; // 설정 — false면 기존 즉시 부활
        public static bool IsActive { get; private set; }
        public static float Elapsed { get; private set; }
        public static int WaveIndex { get; private set; }
        public static int RunGold { get; private set; }
        public static int RunXP { get; private set; }       // [A3] 오브 흡수 누적 — 귀환 시 AddEXP 이관
        public static int Kills { get; private set; }
        /// <summary>사망 직전 위치 — 귀환 시 Respawn 위치 로직 참고(현행 우선순위는 유지, 로그용).</summary>
        public static Vector3 DeathPosition { get; private set; }

        public static event System.Action<float> OnElapsedChanged;   // (elapsed sec)
        public static event System.Action<int> OnWaveStarted;         // (waveIndex 1..)
        public static event System.Action<bool> OnRunEnded;           // (true=귀환 성공 / false=퍼머데스)

        private static float _targetSec = 600f;
        /// <summary>[155차 P2] 테스트 훅 — 테스트 부트 전용 목표 시간 오버라이드(초).
        ///   null이면 기본 경로(GetTargetSecondsForDifficulty) — 프로덕션 기본 동작 무변경.
        ///   EnterAfterlife에서 1회만 읽는다. 내부 조립(Systems 어셈블리)에서만 접근 가능.</summary>
        internal static float? TestTargetSecondsOverride { get; set; }
        private static float _nextWaveAt;
        private static readonly List<AfterlifeEnemy> _aliveEnemies = new List<AfterlifeEnemy>();
        /// <summary>[156차 P5] 생존 적 읽기 전용 뷰 — 스킬 AoE 판정용(물리 쿼리 0 — 결정론/EditMode 안전).</summary>
        public static IReadOnlyList<AfterlifeEnemy> AliveEnemies => _aliveEnemies;

        /// <summary>[156차 P5] 테스트 전용 적 등록 — SpawnWave 미경유 EditMode 경로(프로덕션 무호출).</summary>
        internal static void TestRegisterEnemy(AfterlifeEnemy enemy)
        {
            if (enemy != null && !_aliveEnemies.Contains(enemy)) _aliveEnemies.Add(enemy);
        }
        private static Driver _driver;
        private static int _rngSeed;
        private static int _deathRing = 2;           // [A3] 이계 티어 — 사망 위치 최근접 영지 링 (1~4)
        private static int _deathTerritoryIndex;     // 결정론 시드 재료 — 영지 인덱스(1~20, 폴백 0)

        /// <summary>난이도별 이계 지속 시간 (초) — Easy 300 / Normal 600 / Hard 900.</summary>
        public static float GetTargetSecondsForDifficulty(int difficulty)
        {
            switch (difficulty)
            {
                case 0: return 300f;  // Easy
                case 2: return 900f;  // Hard
                default: return 600f; // Normal
            }
        }

        // ===== 사망 가로채기 (PlayerHealth 리플렉션 진입점) =====

        /// <summary>
        /// 사망 가로채기 판정. true 반환 시 PlayerHealth가 기존 리스폰을 시작하지 않는다.
        /// 이계 중 재사망 → 퍼머데스로 처리(재진입 아님).
        /// </summary>
        public static bool TryInterceptDeath()
        {
            if (!UseAfterlifeRespawn) return false;

            if (IsActive)
            {
                // 이계 중 사망 = 퍼머데스 (판정 확정 후 1회 삭제 — A5 원칙)
                ExecutePermadeath();
                return true;
            }

            DeathPosition = PlayerHealth.Instance != null ? PlayerHealth.Instance.transform.position : Vector3.zero;
            EnterAfterlife();
            return true;
        }

        // ===== 이계 진입 =====

        private static void EnterAfterlife()
        {
            if (IsActive) return;
            IsActive = true;
            Elapsed = 0f;
            WaveIndex = 0;
            RunGold = 0;
            Kills = 0;
            _aliveEnemies.Clear();

            // [155차 P2] 테스트 훅 — 부트가 설정한 오버라이드가 있으면 난이도 계산 대신 사용
            //   (null = 프로덕션 기본 — GetTargetSecondsForDifficulty 그대로)
            _targetSec = TestTargetSecondsOverride ?? GetTargetSecondsForDifficulty(GameManager.CurrentDifficulty);

            // 월드 시간 동결 — 급료/공물/배신/주야 전부 정지 (사용자 지시: 게임 시간 변동 없음)
            TimeManager.IsTimeFrozen = true;

            // 현장 부활 — 살아있는 상태로 이계 진입 (HP 50%)
            PlayerHealth.Instance?.ReviveInPlace(REVIVE_HP_PERCENT);

            // [A3] 오브/증강 런 초기화 + 이계 티어(사망 링) 확정 — 웨이브 스탯/오퍼 시드/드랍 기준
            (_deathRing, _deathTerritoryIndex) = ResolveDeathTier();
            _rngSeed = _deathRing * 7919 + _deathTerritoryIndex * 131 + GameManager.CurrentDifficulty * 17;
            AfterlifeOrb.BeginRun(PlayerHealth.Instance != null ? PlayerHealth.Instance.transform : null);
            AfterlifeAugmentSystem.BeginRun(_deathRing, _deathTerritoryIndex);
            AfterlifeCardSystem.BeginRun(_deathRing);  // [156차 P5] 카드 제시기 시작(진입 무기 캡처 포함)
            AfterlifeSkillSystem.ResetState();          // [156차 P5] 스킬 쿨/상태 초기화
            Debug.Log($"[AfterlifeSurvival] 🩸 이계 티어 — 링 {_deathRing} (스탯 배율 ×{AfterlifeAugmentSystem.GetTierFactor(_deathRing):F2})");

            // 이계 아레나 진입 — 기존 실내 Additive 흐름 재사용 (월드 언로드+플레이어 이동 포함)
            BuildingEvents.RequestEnterBuilding("afterlife", null, false, null);

            EnsureDriver();
            _nextWaveAt = WAVE_INTERVAL_SEC;

            Debug.Log($"[AfterlifeSurvival] 🔮 이계 진입 — 목표 {_targetSec / 60f:F0}분, 웨이브 {WAVE_INTERVAL_SEC / 60f}분 간격 (난이도 {(DifficultyMode)GameManager.CurrentDifficulty})");
            CombatLog.AddEntry("💀 이계에 떨어졌다 — 목숨을 걸고 목표 시간을 버텨라", LogType.Warning);
        }

        /// <summary>드라이버 보장 — unscaled 타이머/웨이브 판정 루프 (DDOL — 씬 언로드 생존).</summary>
        private static void EnsureDriver()
        {
            if (_driver != null) return;
            var go = new GameObject("[AfterlifeDriver]");
            if (Application.isPlaying)
                UnityEngine.Object.DontDestroyOnLoad(go); // 편집 모드 DDOL 금지 — TimeManager QA 수리 동일 패턴
            _driver = go.AddComponent<Driver>();
        }

        // ===== 웨이브 =====

        private static void CheckWave()
        {
            if (Elapsed < _nextWaveAt) return;
            _nextWaveAt += AfterlifeCardSystem.WaveIntervalSeconds; // [156차 P5] 시간의 모래 — 120→135
            WaveIndex++;
            if (AfterlifeCardSystem.ConsumeWavePardon()) // [156차 P5] 웨이브 유예 — 스폰만 생략(인덱스는 진행)
            {
                CombatLog.AddEntry("🕳️ 웨이브 유예 — 이번 웨이브는 오지 않았다", LogType.Warning);
                return;
            }
            SpawnWave(WaveIndex);
        }

        /// <summary>
        /// 웨이브 스폰 — 결정론 수량/스탯(웨이브 인덱스 기반) + 균등 링 배치(유한 랜덤 — 시드 고정).
        /// 승률 우선 가드: 동시 적 MAX_ALIVE_ENEMIES 상한.
        /// </summary>
        private static void SpawnWave(int wave)
        {
            int count = Mathf.Min(3 + wave * 2, MAX_ALIVE_ENEMIES);
            // 스탯 스케일 — 웨이브 1당 +15% HP/+10% 공격 (상한에서 정체 → 승률 가드)
            float hpScale = Mathf.Min(1f + (wave - 1) * 0.15f, 2.5f);
            float dmgScale = Mathf.Min(1f + (wave - 1) * 0.10f, 2.0f);
            float speedScale = Mathf.Min(1f + (wave - 1) * 0.04f, 1.4f);
            int level = 1 + wave * 2;

            // [A3] 이계 티어 배율 — 사망 영지 링 (Ring1=외곽 쉬움 → Ring4=중앙 어려움, 승률 우선 완만)
            float tierFactor = AfterlifeAugmentSystem.GetTierFactor(_deathRing);
            hpScale *= tierFactor;
            dmgScale *= tierFactor;

            // 결정론 시드 — 사망 영지 링+웨이브 해시 → 동일 사망 조건 = 동일 웨이브 (결정론 원칙)
            var rng = new System.Random(_rngSeed + wave * 7919);
            float spawnR = AfterlifeInteriorBuilder.ArenaRadius * 0.65f;

            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                if (_aliveEnemies.Count >= MAX_ALIVE_ENEMIES) break; // 승률 가드 — 남은 몫은 다음 웨이브로

                double angle = rng.NextDouble() * Mathf.PI * 2f;
                var pos = new Vector3(
                    (float)(Mathf.Cos((float)angle) * spawnR),
                    0.5f,
                    (float)(Mathf.Sin((float)angle) * spawnR));
                // 월드 격리 좌표 — 이계 Room 기준 (room.position 5000,5000)
                pos += AfterlifeInteriorRoomOffset();

                var enemyGo = new GameObject($"AfterlifeEnemy_W{wave}_{i:00}");
                enemyGo.transform.position = pos;
                var enemy = enemyGo.AddComponent<AfterlifeEnemy>();
                enemy.Configure(60f * hpScale, 12f * dmgScale, 2.6f * speedScale, level);
                _aliveEnemies.Add(enemy);
                spawned++;
            }

            Debug.Log($"[AfterlifeSurvival] 👹 웨이브 {wave} 시작 — 적 {spawned}마리 (HP×{hpScale:F2} 공격×{dmgScale:F2})");
            CombatLog.AddEntry($"👹 이계 웨이브 {wave} — 망자 {spawned}마리", LogType.Warning);
            OnWaveStarted?.Invoke(wave);
        }

        private static Vector3 AfterlifeInteriorRoomOffset()
        {
            // 이계 Room은 (5000, 0, 5000) — 웨이브 스폰도 그 중심 기준
            return new Vector3(5000f, 0f, 5000f);
        }

        /// <summary>
        /// [A3] 이계 티어 판정 — 사망 위치 최근접 영지(TerritoryId.index 1~20 → 링 (index-1)/5+1).
        /// 소유 무관 전체 영지 대상(사망 장소의 링 난이도 — 계획서 A3-3). 판정 불가 시 링 2 폴백.
        /// </summary>
        private static (int ring, int index) ResolveDeathTier()
        {
            var db = TerritoryDatabase.Instance;
            if (db != null && DeathPosition != Vector3.zero)
            {
                TerritoryDefinition best = default;
                float bestDist = float.MaxValue;
                foreach (var def in db.GetAllDefinitions())
                {
                    float d = (def.worldPosition - DeathPosition).sqrMagnitude;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = def;
                    }
                }
                int idx = best.id.index;
                if (idx > 0) return (((idx - 1) / 5) + 1, idx);
            }
            return (2, 0);
        }

        /// <summary>적 사망 통보 — [A3] 오브 드랍(골드/XP — 흡수 시 집계) + 적 목록 정리.</summary>
        public static void NotifyEnemyKilled(AfterlifeEnemy enemy)
        {
            _aliveEnemies.Remove(enemy);
            Kills++;
            AfterlifeOrb.DropLoot(enemy.transform.position, enemy.Level);

            // [156차 P5] 뱀파이어 송곳니 처치 힐 + 해골 반지 연쇄 갱신
            if (AfterlifeCardSystem.KillHealPerStack > 0f)
            {
                var hp = PlayerHealth.Instance;
                if (hp != null) hp.Heal(AfterlifeCardSystem.KillHealPerStack);
            }
            if (AfterlifeCardSystem.NotifyEnemyKilledForSkullRing())
                Debug.Log("[AfterlifeSurvival] 💀 해골 반지 10연쇄 — 다음 오브 2배");
        }

        /// <summary>오브 흡수 통보 — 골드는 원값 집계, XP는 증강 배율 적용 후 집계.</summary>
        public static void NotifyOrbAbsorbed(AfterlifeOrb.OrbType type, int baseValue)
        {
            float orbMult = AfterlifeCardSystem.OrbValueMult; // [156차 P5] 영혼 항아리 ×1.2
            if (AfterlifeCardSystem.ConsumeSkullRingBonus()) orbMult *= 2f; // [156차 P5] 해골 반지 예약 소비
            if (type == AfterlifeOrb.OrbType.Gold) RunGold += Mathf.RoundToInt(baseValue * orbMult);
            else RunXP += AfterlifeAugmentSystem.RoundXPGain(Mathf.RoundToInt(baseValue * orbMult));
        }

        // ===== 완주/귀환 =====

        /// <summary>귀환 문 접촉(문 컴포넌트) → 정산 → 월드 복귀 부활.</summary>
        public static void CompleteRun()
        {
            if (!IsActive) return;
            FinishRun(success: true);
        }

        /// <summary>
        /// [A5] 봉헌(탈주) — 언제든: 소유 골드 20% 봉헌 → 수확 50% + HP 10% 즉시 귀환(증강 소멸).
        /// 퍼머데스 도입으로 탈주는 "보험" 가치 — 가격 동결 유지(계획서 A5-2).
        /// 0골드면 봉헌 0 — 항상 탈주 가능(소프트락 없음).
        /// </summary>
        public static void Surrender()
        {
            if (!IsActive) return;

            // 1. 봉헌 — 소유 골드 20% (SpendGold — 부족/0이면 생략)
            var stats = PlayerStats.Instance;
            if (stats != null)
            {
                int tribute = Mathf.FloorToInt(stats.Gold * TRIBUTE_RATIO);
                if (tribute > 0 && stats.SpendGold(tribute, "afterlife_tribute"))
                    Debug.Log($"[AfterlifeSurvival] 🕯️ 봉헌 {tribute}G — 이계의 어둠에 바쳤다");
            }

            // 2. 수확 — [156차 P5] 봉헌 영수증 보유 시 1.0(기본 0.5) — 미흡수 오브는 소멸(즉시 귀환)
            float harvest = AfterlifeCardSystem.SurrenderHarvestRatio;
            RunGold = Mathf.FloorToInt(RunGold * harvest);
            RunXP = Mathf.FloorToInt(RunXP * harvest);

            CombatLog.AddEntry("🕊️ 봉헌으로 귀환 — 수확의 절반만 챙긴다", LogType.Warning);
            Debug.Log($"[AfterlifeSurvival] 🕊️ 탈주(봉헌) — 귀환 수확 {RunGold}G / {RunXP}XP");

            // 3. 즉시 귀환 — HP 10%
            FinishRun(success: true, hpPercent: SURRENDER_HP_PERCENT);
        }

        private static void FinishRun(bool success, float hpPercent = REVIVE_HP_PERCENT)
        {
            IsActive = false;
            TimeManager.IsTimeFrozen = false; // 월드 시간 복원 — 변동 없음(동결 중 진행 안 됨)

            // [156차 P5] 영지의 문 — 조기 귀환 감면(보유 시 0.5 배율 적용 후 정산/로그 전파)
            float realmMult = AfterlifeCardSystem.ConsumeRealmGate();
            if (realmMult > 0f && (RunGold > 0 || RunXP > 0))
            {
                RunGold = Mathf.RoundToInt(RunGold * realmMult);
                RunXP = Mathf.RoundToInt(RunXP * realmMult);
                CombatLog.AddEntry("🚪 영지의 문 — 조기 귀환의 대가로 수확이 줄어든다", LogType.Warning);
            }

            // [A3] 증강 원복(최대HP/이속 복원) — 부활 HP 계산 전에 복원되어야 한다 + 잔존 오브 정리
            AfterlifeAugmentSystem.EndRun();
            AfterlifeOrb.ClearAll();
            AfterlifeCardSystem.EndRun();       // [156차 P5] 카드 소멸 + 무기 원복
            AfterlifeSkillSystem.ResetState();  // [156차 P5] 스킬 상태 소멸

            // 이계 정리 — 잔존 적/문 소멸은 씬 언로드가 담당(ExitBuilding → IndoorScene 언로드)
            _aliveEnemies.Clear();
            if (_driver != null)
            {
                Object.Destroy(_driver.gameObject);
                _driver = null;
            }

            // 정산 — [A3] 골드 + XP 오브 흡수 누적 이관 (귀환 성공 시에만)
            var player = PlayerStats.Instance;
            if (success && player != null && RunGold > 0)
            {
                player.AddGold(RunGold, "afterlife_survival");
                Debug.Log($"[AfterlifeSurvival] 🏆 이계 수확 정산 — {RunGold}G (처치 {Kills})");
            }
            if (success && player != null && RunXP > 0)
            {
                player.AddEXP(RunXP);
                Debug.Log($"[AfterlifeSurvival] 📈 이계 경험치 정산 — +{RunXP}XP");
            }

            if (success)
            {
                // 귀환 — 기존 위치 우선순위(스폰핀 > 자기 영지) + HP 50% (이계 귀환 보상)
                CombatLog.AddEntry($"🌟 이계 귀환! 생존 보상 {RunGold}G +{RunXP}XP", LogType.Heal);
                Debug.Log($"[AfterlifeSurvival] 🌟 귀환 성공 — {_targetSec / 60f:F0}분 생존, 처치 {Kills}, 수확 {RunGold}G / {RunXP}XP");
                IndoorSceneTransitionProxyExit(hpPercent);
            }

            RunGold = 0;
            RunXP = 0;
            OnRunEnded?.Invoke(success);
        }

        /// <summary>월드 복귀 — 실내 퇴출 흐름 재사용(Systems→UI 리플렉션) 후 정식 부활.</summary>
        private static void IndoorSceneTransitionProxyExit(float hpPercent)
        {
            try
            {
                if (Application.isPlaying)
                {
                    // 씬 조작은 Play 전용 — 편집 모드(테스트)에서 LoadSceneAsync 시도 방지(75차 안전 가드 패턴)
                    var t = System.Type.GetType("ProjectName.UI.IndoorSceneTransition, ProjectName.UI");
                    if (t != null)
                    {
                        var exit = t.GetMethod("ExitBuilding", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        exit?.Invoke(null, null);
                    }
                }

                // 복귀 후 정식 부활 — 위치 우선순위+페이드 인+HP (Instance 없으면 무해 스킵)
                PlayerHealth.Instance?.RespawnPublic(hpPercent);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AfterlifeSurvival] 귀환 복귀 실패(폴백 — 직접 부활): {e.Message}");
                PlayerHealth.Instance?.RespawnPublic(hpPercent);
            }
        }

        // ===== 퍼머데스 =====

        /// <summary>
        /// 완전 게임오버 — 현재 플레이 슬롯 세이브 삭제 → 타이틀 복귀.
        /// 판정 확정 후 1회 삭제 (씬 언로드/크래시 도중 조기 삭제 사고 방지 — 여기 단일 지점에서만 호출).
        /// </summary>
        private static void ExecutePermadeath()
        {
            // [156차 P5] 두 번째 목숨 — 퍼머데스 1회 방어: 소비 시 사망 무효 + HP 50% 부활 + 런 지속
            if (AfterlifeCardSystem.SecondLifeAvailable && AfterlifeCardSystem.ConsumeSecondLife() > 0f)
            {
                IsActive = true; // 사망 무효 — 생존 런 계속(타이머/웨이브 유지)
                var hp = PlayerHealth.Instance;
                if (hp != null) hp.ReviveAtRatio(0.5f);
                CombatLog.AddEntry("🕯️ 두 번째 목숨 — 이계가 나를 다시 세웠다", LogType.Heal);
                return;
            }

            IsActive = false;
            TimeManager.IsTimeFrozen = false;

            _aliveEnemies.Clear();
            AfterlifeAugmentSystem.EndRun(); // [A3] 증강 원복 + 오브 정리 — 퍼머데스에도 월드 오염 없음
            AfterlifeOrb.ClearAll();
            AfterlifeCardSystem.EndRun();       // [156차 P5] 카드 소멸 + 무기 원복(부활 경유 시 미도달 — 소유 유지)
            AfterlifeSkillSystem.ResetState();  // [156차 P5]
            if (_driver != null)
            {
                Object.Destroy(_driver.gameObject);
                _driver = null;
            }

            int slot = SaveManager.Instance != null ? SaveManager.Instance.CurrentSaveSlot : -1;
            if (slot >= 0)
            {
                SaveManager.Instance.DeleteSlot(slot);
                Debug.LogError($"[AfterlifeSurvival] ☠️ 완전 게임오버 — 슬롯 {slot} 세이브 삭제. 새로운 시작이 필요합니다.");
            }
            else
            {
                Debug.LogError("[AfterlifeSurvival] ☠️ 완전 게임오버 — 로드된 세이브 없음(신규 게임). 삭제 대상 없음.");
            }

            CombatLog.AddEntry("☠️ 이계에서 쓰러졌다 — 모든 것이 끝났다", LogType.Warning);
            OnRunEnded?.Invoke(false);

            // 타이틀 복귀 — GameEndingManager 선례("MainMenu")
            try
            {
                var t = System.Type.GetType("ProjectName.UI.IndoorSceneTransition, ProjectName.UI");
                if (t != null)
                {
                    // 실내 씬일 수 있으나 타이틀은 전체 씬 교체 — IndoorSceneTransition 정리 건드리지 않음
                }
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AfterlifeSurvival] 타이틀 복귀 실패: {e.Message}");
            }
        }

        // ===== 리셋 (테스트) =====
        public static void ResetAll()
        {
            IsActive = false;
            Elapsed = 0f;
            WaveIndex = 0;
            RunGold = 0;
            RunXP = 0;
            Kills = 0;
            DeathPosition = Vector3.zero;
            _targetSec = 600f;
            _nextWaveAt = WAVE_INTERVAL_SEC;
            _rngSeed = 0;
            _deathRing = 2;
            _deathTerritoryIndex = 0;
            _aliveEnemies.Clear();
            AfterlifeAugmentSystem.EndRun(); // [A3] 배율/캡처 원복
            AfterlifeCardSystem.EndRun();       // [156차 P5] 카드 소멸 + 무기 원복
            AfterlifeSkillSystem.ResetState();  // [156차 P5]
            AfterlifeOrb.ClearAll();
            TimeManager.IsTimeFrozen = false;
            if (_driver != null)
            {
                Object.Destroy(_driver.gameObject);
                _driver = null;
            }
            Debug.Log("[AfterlifeSurvival] 모든 상태 초기화 완료");
        }

        /// <summary>unscaled 타이머/웨이브 드라이버 — 씬 언로드 생존(DDOL), 이계 중에만 활동.</summary>
        private class Driver : MonoBehaviour
        {
            private void Update()
            {
                if (!AfterlifeSurvivalSystem.IsActive) return;

                // [156차 P5] 카드/스킬 틱 — 증강 정지 게이트 앞(카드 폴백 타이머는 자체 unscaled 누적)
                AfterlifeCardSystem.Tick(Elapsed, _targetSec, _rngSeed, _deathRing, Time.unscaledDeltaTime);
                AfterlifeSkillSystem.Tick();

                if (AfterlifeAugmentSystem.IsPending) return; // [A3] 3택1 대기 — 아레나 전체 정지(VS식 — 타이머 포함)
                if (AfterlifeCardSystem.IsPending) return; // [156차 P5] 카드 3택 대기 — 동일 정지 계약

                Elapsed += Time.unscaledDeltaTime; // 불릿타임 무영향
                OnElapsedChanged?.Invoke(Elapsed);

                // [A3] 증강 마크 오퍼/재생/만료 폴백
                AfterlifeAugmentSystem.Tick(Elapsed, _targetSec, _rngSeed, Time.unscaledDeltaTime);

                CheckWave();

                if (Elapsed >= AfterlifeSurvivalSystem._targetSec)
                {
                    // 완주 — 귀환 문 개방 (시스템 완주 판정 후 문 접촉으로 귀환)
                    AfterlifeSurvivalSystem.OpenReturnDoor();
                    // 완주 후 드라이버는 대기(문 접촉 → CompleteRun) — 중복 개방 방지로 Elapsed 고정
                    AfterlifeSurvivalSystem._targetSec = float.MaxValue;
                }
            }
        }

        /// <summary>[156차 P5] 영지의 문(sp_realm_gate) — 즉시 귀환 문 개방(감면은 FinishRun의 ConsumeRealmGate 적용).</summary>
        public static void ForceReturnDoor()
        {
            if (!IsActive) return;
            OpenReturnDoor();
            _targetSec = float.MaxValue; // 완주 경로 동일 — 타이머 고정(중복 개방 방지)
        }

        private static void OpenReturnDoor()
        {
            var door = Object.FindAnyObjectByType<AfterlifeExitDoor>();
            if (door != null)
            {
                door.ActivateDoor();
                Debug.Log("[AfterlifeSurvival] 🌟 목표 시간 버팀 — 귀환 문이 개방되었다!");
                CombatLog.AddEntry("🌟 귀환 문 개방 — 문으로 들어가라", LogType.Heal);
            }
            else
            {
                Debug.LogWarning("[AfterlifeSurvival] 귀환 문 미발견 — 즉시 귀환 폴백");
                CompleteRun();
            }
        }
    }
}
