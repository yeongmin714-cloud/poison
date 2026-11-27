// 2026-10-23 (121차): 아레나 관전 베팅 시스템 — 피그마 255:647(투기장 베팅) 계약.
// 플로우: 로비 관리인 NPC → 상호판넬(대화하기/베팅하기) → ArenaWindowUTK(선수 선택+금액) →
// 확정 시 골드 즉시 차감 → 로비→타원 투기장 자동 전환 → 선수 2명 배치구역(서/동) 스폰 →
// 라운드제 자동 전투(AI 대 AI) → 생존 1명 승리 → 정산(승=금액×배율 수령, 패=원금 상실) → 로비 복귀.
// 기록(승·패)은 세션 누적(세이브 영속은 후속 — 사용자 확정 기본값).
using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>베팅 대상 격투사 — 능력치 + 세션 누적 기록.</summary>
    public class ArenaBetFighter
    {
        public string id;
        public string name;
        public string origin;     // 출신 설명(피그마 "북부 용병단 출신의 쌍검 검사…")
        public string tagline;    // 태그 라인("쌍검 검사 · 경갑 · 근접 공격형 · 2인 난투")
        public float attack;
        public float defense;
        public float agility;
        public float maxHP;
        public int wins;
        public int losses;

        public float WinRate => (wins + losses) > 0 ? (float)wins / (wins + losses) : 0.5f;
        public float Power => attack + defense + agility;
        public string RecordText => $"{wins}승 {losses}패 · 승률 {WinRate * 100f:0}%";
    }

    /// <summary>베팅 가능 선수 쌍 + 배율(베팅 시점 고정).</summary>
    public class ArenaBetPair
    {
        public ArenaBetFighter west;    // 서 배치구역(120차 설계도)
        public ArenaBetFighter east;    // 동 배치구역
        public float oddsWest;
        public float oddsEast;
    }

    /// <summary>라운드 판정 로그(전투 로그/토스트용).</summary>
    public class ArenaDuelRoundLog
    {
        public int round;
        public string attackerName;
        public string defenderName;
        public float damage;
        public bool dodged;
        public float westHP;
        public float eastHP;
        public bool ended;
        public string winnerName;
    }

    /// <summary>[125차 후속30 K6] 결과 개방 페이로드 — 승패/정산/전적 스냅숏(결과창 바인딩용).
    /// 전적(wins/losses)은 OnDuelSettled 시점에 이미 갱신 완료 실측(L319-320 승패 판정 직후 증감).</summary>
    public class ArenaResultPayload
    {
        public ArenaBetFighter winner, loser;
        public string winnerName, loserName;
        public bool playerWon, winnerIsWest;
        public int betAmount;
        public float odds;
        public int payout;
        public int finalGold;
    }

    /// <summary>
    /// 아레나 관전 베팅 — 정적 시스템(런너 MonoBehaviour 자가부트, DialogueCameraDirector.Ensure 선례).
    /// 배율 산식: p = power/(powerA+powerB), odds = clamp(0.85/p, 1.2, 3.0)(하우스 엣지 15%) — 0.05 반올림.
    /// 라운드 데미지: max(1, atk − def×0.5) × 분산(0.85~1.15) — ArenaSystem.SimulateBattle 산식 동일 계열.
    /// 민첩: 회피 확률 = clamp((agi_방어자 − agi_공격자) × 0.25%, 0~25%).
    /// </summary>
    public static class ArenaBettingSystem
    {
        public const float MinOdds = 1.2f;
        public const float MaxOdds = 3.0f;
        public const float HouseEdgeFactor = 0.85f;
        public const int MaxRounds = 12;
        public const float RoundIntervalSeconds = 1.8f;

        private static readonly Dictionary<string, ArenaBetFighter> _registry =
            new Dictionary<string, ArenaBetFighter>();
        private static ArenaBetPair _currentPair;
        private static (ArenaBetFighter fighter, int amount, float odds)? _pendingBet;
        private static bool _duelRunning;
        private static string _nationStyle = "Western";

        /// <summary>베팅 관련 이벤트 — ArenaWindowUTK/테스트 구독.</summary>
        public static event Action<ArenaBetPair> OnPairOffered;
        public static event Action<ArenaDuelRoundLog> OnRoundEnd;
        public static event Action<string, int, bool> OnDuelSettled;   // (승자명, 배당금, 플레이어 승리여부)
        public static event Action<string> OnBetRejected;              // 거부 사유
        /// <summary>[125차 후속30 K6] 결과 개방 신호 — 로비 복귀 완료 후 1회 발화. ArenaResultWindowUTK(UI)가 구독.</summary>
        public static event Action<ArenaResultPayload> OnResultReady;

        /// <summary>현재 제공 쌍(개방 시 갱신).</summary>
        public static ArenaBetPair CurrentPair => _currentPair;
        public static bool DuelRunning => _duelRunning;

        // ─────────────────────────────────────────────────────────────
        // 선수 생성/쌍 제공
        // ─────────────────────────────────────────────────────────────
        private static readonly string[] NamePool =
        {
            "칼날의 카르", "북풍의 라그나", "철벽 돌가", "붉은 여우 마라", "무리의 가론", "침묵의 토르",
            "모래의 개울", "검은 골 힐데", "쌍달 글라디우스", "폭풍 아이자", "짐승 발 브록", "은빛 셀마",
        };
        private static readonly string[] OriginPool =
        {
            "북부 용병단 출신의 쌍검 검사. 빠른 연격으로 상대를 압박한다.",
            "남부 사막 투기장 출신의 창술사. 거리 유지와 견제에 능하다.",
            "동부 초원 출신의 유목 전사. 승마 단련된 하체로 균형이 강하다.",
            "서부 화산 광산 출신의 광부 출신 격투가. 조악한 판금 갑옷을 두른다.",
            "황제국 근위 퇴역병. 정형화된 검술 훈련을 거쳤다.",
            "낙민 항구 출신의 선원 출신 난투꾼. 그물과 단검을 섞어 쓴다.",
        };
        private static readonly string[] TaglinePool =
        {
            "쌍검 검사 · 경갑 · 근접 공격형 · 2인 난투",
            "창술사 · 중갑 · 거리 유지형 · 2인 난투",
            "유목 전사 · 피혁 · 균형형 · 2인 난투",
            "격투가 · 판금 · 방어 특화 · 2인 난투",
            "퇴역 근위 · 정갑 · 균형형 · 2인 난투",
            "난투꾼 · 경갑 · 민첩 특화 · 2인 난투",
        };

        private static int _freshIndex;

        /// <summary>새 선수 생성(능력치 랜덤 — 세션 흐름용; 테스트는 MakeFighterForTest 사용).</summary>
        public static ArenaBetFighter CreateFreshFighter()
        {
            var f = new ArenaBetFighter
            {
                id = $"arena_fighter_{_freshIndex++}",
                name = NamePool[_freshIndex % NamePool.Length],
                origin = OriginPool[_freshIndex % OriginPool.Length],
                tagline = TaglinePool[_freshIndex % TaglinePool.Length],
                attack = UnityEngine.Random.Range(40f, 95f),
                defense = UnityEngine.Random.Range(25f, 70f),
                agility = UnityEngine.Random.Range(30f, 90f),
                maxHP = UnityEngine.Random.Range(90f, 130f),
            };
            _registry[f.id] = f;
            return f;
        }

        /// <summary>베팅 쌍 제공 — 기존 선수 2명(판수 최소 로테이션) 우선, 부족 시 신규 생성.</summary>
        public static ArenaBetPair OfferPair()
        {
            var candidates = new List<ArenaBetFighter>(_registry.Values);
            candidates.Sort((x, y) => (x.wins + x.losses).CompareTo(y.wins + y.losses));   // 판수 적은 순
            ArenaBetFighter west = candidates.Count > 0 ? candidates[0] : CreateFreshFighter();
            ArenaBetFighter east = candidates.Count > 1 ? candidates[1] : CreateFreshFighter();
            if (ReferenceEquals(west, east)) east = CreateFreshFighter();

            _currentPair = new ArenaBetPair
            {
                west = west,
                east = east,
                oddsWest = ComputeOdds(west.Power, east.Power),
                oddsEast = ComputeOdds(east.Power, west.Power),
            };
            OnPairOffered?.Invoke(_currentPair);
            Debug.Log($"[ArenaBetting] 베팅 쌍 제공 — {west.name}(×{_currentPair.oddsWest:0.00}) vs {east.name}(×{_currentPair.oddsEast:0.00})");
            return _currentPair;
        }

        /// <summary>배율 산식(순수 — 테스트 대상): p=power/sum, odds=clamp(0.85/p, 1.2, 3.0) 0.05 반올림.</summary>
        public static float ComputeOdds(float powerSelf, float powerOpponent)
        {
            float sum = powerSelf + powerOpponent;
            if (sum <= 0.0001f) return MinOdds;
            float p = powerSelf / sum;
            float odds = HouseEdgeFactor / Mathf.Max(0.05f, p);
            odds = Mathf.Clamp(odds, MinOdds, MaxOdds);
            return Mathf.Round(odds * 20f) / 20f;   // 0.05 스텝
        }

        // ─────────────────────────────────────────────────────────────
        // 베팅 확정 → 자동 전환 → 듀엘
        // ─────────────────────────────────────────────────────────────
        /// <summary>
        /// 베팅 확정 단일 진입 — 골드 즉시 차감(취소 불가) → 로비→투기장 자동 전환 → 듀엔 개시.
        /// ArenaWindowUTK "베팅하기" 버튼의 유일 호출점. 실패 시 false + error 사유.
        /// </summary>
        public static bool PlaceBetAndStart(ArenaBetFighter fighter, int amount, string nationStyle, out string error)
        {
            error = null;
            if (_duelRunning) { error = "이미 진행 중인 투기가 있습니다."; return false; }
            if (fighter == null) { error = "선수를 선택하세요."; return false; }
            if (_currentPair == null || (_currentPair.west != fighter && _currentPair.east != fighter))
            { error = "제공된 선수만 베팅할 수 있습니다."; return false; }
            if (amount <= 0) { error = "베팅 금액은 0보다 커야 합니다."; return false; }
            int gold = PlayerStats.Instance?.Gold ?? 0;
            if (gold < amount) { error = $"보유 골드가 부족합니다 (보유 {gold:N0} G)."; return false; }

            float odds = _currentPair.west == fighter ? _currentPair.oddsWest : _currentPair.oddsEast;
            if (!PlayerStats.Instance.SpendGold(amount, "arena_bet"))
            { error = "골드 차감에 실패했습니다."; return false; }

            _pendingBet = (fighter, amount, odds);
            _nationStyle = string.IsNullOrEmpty(nationStyle) ? "Western" : nationStyle;
            _duelRunning = true;
            Debug.Log($"[ArenaBetting] 베팅 확정 — {fighter.name} {amount:N0} G ×{odds:0.00} (잔액 {GetGold():N0} G) → 투기장 전환");

            // 로비 → 타원 투기장 자동 전환 — Systems↔UI 어셈블리 경계(asmdef)로 리플렉션 호출
            // (TestTerritoryCombatSetup 크로스 어셈블리 선례).
            EnterBuildingViaReflection(IndoorTransitionSetup.TYPE_ARENA_PIT, _nationStyle);

            EnsureRunner();
            _runner.StartCoroutine(RunDuelCoroutine(fighter, amount, odds));
            return true;
        }

        /// <summary>ProjectName.UI.IndoorSceneTransition.EnterBuilding 리플렉션 호출 — 어셈블리 탐색형(이름 불변).</summary>
        private static void EnterBuildingViaReflection(string buildingType, string nationStyle)
        {
            Type t = FindCrossAssemblyType("ProjectName.UI.IndoorSceneTransition");
            if (t == null)
            {
                Debug.LogError("[ArenaBetting] IndoorSceneTransition 미발견 — 전환 실패(베팅금 환불 필요 시 Play 확인).");
                return;
            }
            t.GetMethod("EnterBuilding", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                ?.Invoke(null, new object[] { buildingType, nationStyle, false, null });
        }

        /// <summary>ProjectName.UI.Toolkit.UTKToastService.Show 리플렉션 호출 — 실패 시 콘솔 로그 폴백.</summary>
        private static void ShowToastViaReflection(string text, float duration)
        {
            Type t = FindCrossAssemblyType("ProjectName.UI.Toolkit.UTKToastService");
            var m = t?.GetMethod("Show", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null, new[] { typeof(string), typeof(float) }, null);
            if (m != null) m.Invoke(null, new object[] { text, duration });
            else Debug.Log($"[토스트] {text}");
        }

        private static Type FindCrossAssemblyType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        private static int GetGold() => PlayerStats.Instance != null ? PlayerStats.Instance.Gold : 0;

        private static ArenaBettingRunner _runner;
        // [125차 후속31 M2] 결과 흐름 상태 — 투기장 내 결과창 표시 → 확인 → 로비 복귀(자동 복귀 폐기)
        private static Transform _lastWestGO, _lastEastGO;
        private static string _lastNationStyle = "Western";
        private static bool _resultPending, _resultShown;

        private static void EnsureRunner()
        {
            if (_runner != null) return;
            var go = new GameObject("ArenaBettingRunner");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<ArenaBettingRunner>();
        }

        private class ArenaBettingRunner : MonoBehaviour { }

        /// <summary>듀엔 코루틴 — 배치구역 스폰 → 라운드 반복 → 정산 → 로비 복귀.</summary>
        private static System.Collections.IEnumerator RunDuelCoroutine(ArenaBetFighter playerPick, int amount, float odds)
        {
            // [125차 후속11] 전환 정착 폴링 — 실내→실내 전환은 언로드→재로드→빌더 3단계(OnPreviousIndoorUnloaded
            //   재로드 체인)라 완성까지 0.8s를 초과할 수 있다. Play 실측: 베팅 3회 연속 "ArenaPitRoom 미발견 — 환불".
            //   고정 대기+1회 Find를 폴링(0.1s 간격, 최대 15s)으로 교체 — 방 완성을 기다린 뒤 듀엔 개시.
            GameObject pitRoom = null;
            const float PitWaitTimeout = 15f;
            float waited = 0f;
            while (pitRoom == null && waited < PitWaitTimeout)
            {
                pitRoom = GameObject.Find("ArenaPitRoom");
                if (pitRoom == null)
                {
                    yield return new WaitForSeconds(0.1f);
                    waited += 0.1f;
                }
            }
            if (pitRoom == null)
            {
                Debug.LogError($"[ArenaBetting] [125차 후속11] ArenaPitRoom {PitWaitTimeout:F0}s 폴링 후에도 미발견 — 전환 실패 판정. 베팅금 환불. (실내→실내 전환 체계 점검 필요)");
                PlayerStats.Instance?.AddGold(amount, "arena_bet_refund");
                _duelRunning = false;
                _pendingBet = null;
                yield break;
            }
            Debug.Log($"[ArenaBetting] [125차 후속11] ArenaPitRoom 발견 — {waited:F1}s 대기 후 듀엔 개시");

            ArenaBetFighter west = _currentPair.west;
            ArenaBetFighter east = _currentPair.east;
            // [125차 후속13] 선수 근접 배치(±12→±6m) — 관전 카메라(orthoSize 12)에서 전투가 눈에 잘 들어오게
            //   (사용자 지시). 배치구역 데코(±12m)와 무관 — 스폰 좌표만 조정.
            // [125차 후속16] 스폰 ±6→±5m — 관전 뷰(orthoSize 8, ±8m)에서 병사가 화면 안쪽 60%에 안착
            // [125차 후속29 G1] 스폰 y 0.15→0.6 — 스폰이 모래 톱(0.15)과 정확히 겹치면 접지 판정이
            //   콜라이더 표면에서 시작해 실패 → RequireComponent(Rigidbody) 자동부착 RB의 유니티 중력으로
            //   자유낙하(pit_late 실측 y=-27.93/-29.35). 0.6 스폰은 G2 박스 콜라이더(톱 0.14)를 위에서
            //   인지하는 접지 성공 경로 — 캡슐 폴백은 proc 컨트롤러가 착지 자체 수행(구조 스왑 금지 계약),
            //   표준 병사 경로(후속29 후속2)는 bounds 결정론 접지로 스폰 즉시 고정.
            Transform westGO = SpawnFighterVisual(pitRoom.transform, west, "ArenaDuelFighter_West", new Vector3(-5f, 0.6f, 0f), new Color(0.29f, 0.47f, 0.76f), west.maxHP, playerPick == west);
            Transform eastGO = SpawnFighterVisual(pitRoom.transform, east, "ArenaDuelFighter_East", new Vector3(5f, 0.6f, 0f), new Color(0.76f, 0.29f, 0.29f), east.maxHP, playerPick == east);
            // [125차 후속25 R5] 이름표 HP 동기화 핸들 — 라운드마다 기존 병사형 이름표의 HP바에 반영
            var westTag = westGO != null ? westGO.GetComponent<ArenaDuelFighterTag>() : null;
            var eastTag = eastGO != null ? eastGO.GetComponent<ArenaDuelFighterTag>() : null;

            // [125차 후속30 K3] HP 런타임 홀더 — 타격 순간 판정(데미지 적용/HP바 동기가 접촉 시점)용 뮤터블 상태.
            var duel = new DuelRuntime { WestHP = west.maxHP, EastHP = east.maxHP, WestMax = west.maxHP, EastMax = east.maxHP, WestTag = westTag, EastTag = eastTag };
            ShowToastViaReflection($"⚔ {west.name} vs {east.name} — 2인 난투 시작!", 2.5f);
            yield return new WaitForSeconds(1.0f);   // [125차 후속13] 첫 라운드 즉시 개시(사용자: 진입시 바로 전투)
            // [125차 후속29 후속] 접근 보행 — 제자리 원거리 공격 폐기(사용자 지시 "실제로 움직여서 서로를 공격").
            //   양 선수가 서로를 향해 실제 이동으로 근접(격투 거리까지) — 트랜스폼 구동이므로
            //   TransformVelocityProvider가 프레임 변위를 보행 속도로 공급해 보행 애니가 자동 재생된다.
            yield return DuelEngage(westGO, eastGO);

            ArenaDuelRoundLog finalLog = null;
            for (int round = 1; round <= MaxRounds; round++)
            {
                // 서→동 → 동→서 교대 공격 — [125차 후속30 K1~K4] 접촉 시점 판정(이펙트/티어 반응/넉백/HP바 동기)
                yield return StrikeRound(duel, west, east, round, westGO, eastGO, defenderIsWest: false);
                if (duel.EastHP <= 0f) break;
                yield return new WaitForSeconds(RoundIntervalSeconds * 0.5f);
                yield return StrikeRound(duel, east, west, round, eastGO, westGO, defenderIsWest: true);
                if (duel.WestHP <= 0f) break;

                var log = new ArenaDuelRoundLog { round = round, westHP = duel.WestHP, eastHP = duel.EastHP };
                OnRoundEnd?.Invoke(log);
                yield return new WaitForSeconds(RoundIntervalSeconds);
            }

            // 승패 판정 — 생존 1명(라운드 소진 시 HP 우세)
            bool westWon;
            if (duel.WestHP <= 0f && duel.EastHP <= 0f) westWon = duel.WestHP > duel.EastHP;   // 동시 사망 — 잔여 HP 우세
            else if (duel.WestHP <= 0f) westWon = false;
            else if (duel.EastHP <= 0f) westWon = true;
            else westWon = duel.WestHP >= duel.EastHP;

            ArenaBetFighter winner = westWon ? west : east;
            ArenaBetFighter loser = westWon ? east : west;
            winner.wins++;
            loser.losses++;

            bool playerWon = playerPick == winner;
            int payout = playerWon ? Mathf.RoundToInt(amount * odds) : 0;
            if (playerWon && PlayerStats.Instance != null)
                PlayerStats.Instance.AddGold(payout, "arena_bet_payout");

            finalLog = new ArenaDuelRoundLog { round = MaxRounds, westHP = duel.WestHP, eastHP = duel.EastHP, ended = true, winnerName = winner.name };
            OnRoundEnd?.Invoke(finalLog);
            OnDuelSettled?.Invoke(winner.name, payout, playerWon);

            if (playerWon) ShowToastViaReflection($"🏆 베팅 승리! {winner.name} 승리 — {payout:N0} G 수령", 3.5f);
            else ShowToastViaReflection($"💀 베팅 실패 — {winner.name} 승리, {amount:N0} G 상실", 3.5f);
            Debug.Log($"[ArenaBetting] 듀엔 종료 — 승자 {winner.name} / 베팅 {(playerWon ? "적중" : "실패")} / 정산 {payout:N0} G");

            yield return new WaitForSeconds(2.5f);   // 결과 관람 유예 — [125차 후속31 M2] 이 구간에 패자 다운이 보인다

            // [125차 후속31 M2] 결과 흐름 재구성 — 자동 로비 복귀 폐기(사용자 지시). 투기장에서 결과창을 표시하고
            //   확인(버튼/Enter/Esc) 입력 시에만 로비로 복귀. 선수 파괴/상태 정리/EnterBuilding은
            //   ReturnToLobbyAfterResult(창 확인 콜백)가 소유 — 이 코루틴은 발화 후 종료.
            _lastWestGO = westGO;
            _lastEastGO = eastGO;
            _lastNationStyle = _nationStyle;
            var result = new ArenaResultPayload
            {
                winner = winner,
                loser = loser,
                winnerName = winner.name,
                loserName = loser.name,
                playerWon = playerWon,
                winnerIsWest = westWon,
                betAmount = amount,
                odds = odds,
                payout = payout,
                finalGold = GetGold(),
            };
            _resultShown = false;
            _resultPending = true;   // _duelRunning=true 유지 — 결과 표시 중 베팅창 재개방 차단(확인 시 false)
            // [125차 후속31 M3] 발화 로그를 Invoke 이전으로 — 구독자 예외가 종료 로그를 삼키지 않게(후속30 실측 교훈)
            Debug.Log($"[ArenaBetting] [125차 후속31 M2] 결과 개방 신호 발화(투기장 내) — 승자 {winner.name} / 정산 {payout:N0} G / 잔액 {result.finalGold:N0} G");
            try { OnResultReady?.Invoke(result); }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ArenaBetting] [125차 후속31 M3] OnResultReady 구독자 예외 흡수 — 폴백 복귀 대기: {ex.GetType().Name}: {ex.Message}");
            }
            _runner.StartCoroutine(ResultFallbackWatch(12f));   // [125차 후속31 M2] 표시 실패 시 강제 복귀(고립 금지)
        }

        /// <summary>[125차 후속31 M2] 결과 표시 완료 표식 — ArenaResultWindowUTK.Open 성공 시 호출.
        ///   폴백 감시(무표시 강제 복귀)의 해제 조건.</summary>
        public static void MarkResultShown() => _resultShown = true;

        /// <summary>[125차 후속31 M2] 로비 복귀 — 결과창 확인(버튼/Enter/Esc)에서 호출. 선수 파괴+상태 정리+
        ///   로비 전환(기존 RunDuelCoroutine 말미 로직 이전 — 후속30 K6 로비 폴링 경로 폐기).</summary>
        public static void ReturnToLobbyAfterResult()
        {
            if (!_resultPending) return;   // 이중 호출 가드
            _resultPending = false;
            if (_lastWestGO != null) UnityEngine.Object.Destroy(_lastWestGO.gameObject);
            if (_lastEastGO != null) UnityEngine.Object.Destroy(_lastEastGO.gameObject);
            _lastWestGO = null;
            _lastEastGO = null;
            _pendingBet = null;
            _duelRunning = false;
            EnterBuildingViaReflection(IndoorTransitionSetup.TYPE_ARENA, _lastNationStyle);   // 로비 복귀(리플렉션 — 어셈블리 경계)
            Debug.Log("[ArenaBetting] [125차 후속31 M2] 결과 확인 — 로비 복귀 실행");
        }

        /// <summary>[125차 후속31 M2] 폴백 감시 — 결과창이 timeout 내 표시되지 않으면 강제 로비 복귀(플레이어 고립 금지).</summary>
        private static System.Collections.IEnumerator ResultFallbackWatch(float timeout)
        {
            float waited = 0f;
            while (waited < timeout && !_resultShown)
            {
                yield return new WaitForSeconds(0.5f);
                waited += 0.5f;
            }
            if (!_resultShown)
            {
                Debug.LogWarning($"[ArenaBetting] [125차 후속31 M2] 결과창 {timeout:0}s 미표시 — 강제 로비 복귀(폴백)");
                ReturnToLobbyAfterResult();
            }
        }

        /// <summary>[125차 후속30 K3] 듀엔 런타임 HP 홀더 — 접촉 시점 판정(StrikeRound 내부 변이)용.</summary>
        private class DuelRuntime
        {
            public float WestHP, EastHP, WestMax, EastMax;
            public ArenaDuelFighterTag WestTag, EastTag;
            public int AttackCount;   // [무번호 87] 맨손 공격 클립 결정론 사이클 카운터
        }

        /// <summary>[125차 후속30 K1~K4] 1회 공격 — 접촉 시점 판정. 데미지 산식/회피 확률은 기존 AttackRound와
        /// 동일(수치 불변) — 적용 시점만 돌진 접촉(0.25s)으로 이동. 접촉 순서: 데미지 확정 → CombatFX(본씬 표준 경로)
        /// → 티어 반응 애니(≥40 다운/≥25 스턴/그 외 경직 — ProceduralAnimStateMachine L248 선례) → 트랜스폼 넉백
        /// → HP바 동기 → 라운드 로그. 최후 타격(HP 0)은 항상 다운 유지(K4 — 관람 유예와 정합).</summary>
        private static System.Collections.IEnumerator StrikeRound(DuelRuntime duel, ArenaBetFighter attacker, ArenaBetFighter defender,
            int round, Transform atkGO, Transform defGO, bool defenderIsWest)
        {
            if (atkGO == null || defGO == null) yield break;
            float dodgeChance = Mathf.Clamp((defender.agility - attacker.agility) * 0.0025f, 0f, 0.25f);
            bool dodged = UnityEngine.Random.value < dodgeChance;
            float damage = 0f;
            if (!dodged)
            {
                float raw = Mathf.Max(1f, attacker.attack - defender.defense * 0.5f);
                damage = raw * UnityEngine.Random.Range(0.85f, 1.15f);
            }

            // 공격 애니 — [무번호 87] 맨손 결투: 강타(≥25)=PunchKick 콤보, 그 외 4종 결정론 사이클.
            //   ReplayAction(상태명 Play) 경로 — 드라이버 무수정. 데미지 산식/회피/정산 수치 불변(애니 레이어만 교체).
            //   + 돌진 접근(0.25s)
            var atkDriver = atkGO.GetComponent<HumanoidClipDriver>();
            if (atkDriver != null)
            {
                duel.AttackCount++;
                string attackState = damage >= 25f ? FistComboState : FistAttackCycle[duel.AttackCount % FistAttackCycle.Length];
                atkDriver.ReplayAction(attackState);
            }
            Vector3 start = atkGO.position;
            Vector3 dir = (defGO.position - start); dir.y = 0f;
            float dist = Mathf.Min(1.2f, dir.magnitude * 0.35f);
            if (dir.sqrMagnitude > 0.0001f) dir.Normalize();
            float t = 0f;
            while (t < 0.5f)
            {
                t += Time.deltaTime * 4f;   // 0.25s 전진
                if (atkGO == null) yield break;
                atkGO.position = start + dir * (dist * Mathf.Min(1f, t / 0.5f));
                yield return null;
            }

            // ── 접촉 순간 [125차 후속30 K1~K3] ──
            float defenderHP = defenderIsWest ? duel.WestHP : duel.EastHP;
            if (!dodged)
            {
                defenderHP = Mathf.Max(0f, defenderHP - damage);
                // K1 피격 이펙트 — 본씬 표준 경로(CombatFXGate: 플래시/스파크/임팩트/출혈/데미지넘버 일괄).
                //   hitPos 정밀 오버로드(46차 후속 — 피벗 y≈2.9 공중 부양 폐기): 흉부 높이(y+1.0) 발화.
                Vector3 hitPos = defGO.position + Vector3.up * 1.0f;
                CombatFXGate.PlayHitFX(defGO.gameObject, hitPos, dir, CombatHitType.Organic, false, damage, Color.white);
                // K2 피격 반응 애니 — 데미지 티어 계약. 트리거가 SoldierShield_AC에 없으면 SetTrigger 무해(미정의 무시).
                var defDriver = defGO.GetComponent<HumanoidClipDriver>();
                if (defDriver != null)
                {
                    // [무번호 87] 수비 반응 개편 — 구 SoldierShield_AC 미정의 no-op(Knockdown/Stun)을 SoldierFist_AC 배선으로 가시화
                    if (defenderHP <= 0f || damage >= 40f) defDriver.TriggerDeath();   // KO — 쓰러짐 유지(패자 관람 계약)
                    else if (damage >= 25f) defDriver.TriggerHitLight();               // 강타 — Hit 경직(기존 믹사모 클립 승계)
                    else defDriver.TriggerBlock();                                     // 약타 — 막기(병사 신규 FBX)
                }
                // K2 넉백 — 트랜스폼 구동(물리 금지 — G3 kinematic 계약). 티어별 거리: 다운 0.7/스턴 0.4/경직 0.25m.
                float kbDist = defenderHP <= 0f || damage >= 40f ? 0.7f : damage >= 25f ? 0.4f : 0.25f;
                _runner.StartCoroutine(KnockbackPush(defGO, dir, kbDist));
            }

            // HP 반영 + 이름표 동기 — 접촉 시점([125차 후속30 K3])
            if (defenderIsWest) duel.WestHP = defenderHP; else duel.EastHP = defenderHP;
            duel.WestTag?.SetHP(duel.WestHP, duel.WestMax);
            duel.EastTag?.SetHP(duel.EastHP, duel.EastMax);

            var log = new ArenaDuelRoundLog
            {
                round = round,
                attackerName = attacker.name,
                defenderName = defender.name,
                damage = damage,
                dodged = dodged,
                ended = defenderHP <= 0f,
                winnerName = defenderHP <= 0f ? attacker.name : null,
            };
            OnRoundEnd?.Invoke(log);
            if (defenderHP <= 0f)
                Debug.Log($"[ArenaBetting] 라운드 {round} — {attacker.name}가 {defender.name} 격파 (누적 데미지)");
            else if (dodged)
                Debug.Log($"[ArenaBetting] 라운드 {round} — {defender.name} 회피! ({attacker.name} 공격 빗나감)");
            else
                Debug.Log($"[ArenaBetting] 라운드 {round} — {attacker.name} → {defender.name} {damage:0} 피해");

            // 복귀 — 최후 타격(사망) 시 공격자 제자리 유지(패자 다운 관람 — K4)
            if (defenderHP <= 0f) yield break;
            t = 0f;
            while (t < 0.5f)
            {
                t += Time.deltaTime * 3f;   // 0.33s 복귀
                if (atkGO == null) yield break;
                atkGO.position = Vector3.Lerp(atkGO.position, start, Mathf.Min(1f, t / 0.5f));
                yield return null;
            }
            if (atkGO != null) atkGO.position = start;
        }

        /// <summary>[125차 후속30 K2] 넉백 — 트랜스폼 구동(물리 금지 — G3 kinematic 계약). 0.12s 밀림+0.2s 복귀 —
        /// 격투 거리 1.6m 계약 유지(다음 라운드 돌진이 피격자 현재 위치를 향함).</summary>
        private static System.Collections.IEnumerator KnockbackPush(Transform defGO, Vector3 dir, float dist)
        {
            if (defGO == null) yield break;
            Vector3 origin = defGO.position;
            Vector3 target = origin + dir * dist;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.12f;
                if (defGO == null) yield break;
                defGO.position = Vector3.Lerp(origin, target, Mathf.Min(1f, t));
                yield return null;
            }
            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.2f;
                if (defGO == null) yield break;
                defGO.position = Vector3.Lerp(target, origin, Mathf.Min(1f, t));
                yield return null;
            }
            if (defGO != null) defGO.position = origin;
        }

        // [125차 후속31 M2] 구 로비 준비 폴링(WaitForLobbyReady) 폐기 — 결과 개방이 투기장 내 발화로 이동(자동 복귀 삭제)

        // [125차 후속29 후속] 접근 상수 — 격투 거리(서로 멈추는 간격)와 접근 보행 속도.
        private const float EngageGap = 1.6f;      // 격투 거리 — 라운드 돌진(LungeVisual)이 타격거리로 읽히는 간격
        private const float EngageSpeed = 3.2f;    // 접근 보행 속도(m/s) — CurrentSpeed 공급 → 보행 애니 자동

        /// <summary>[125차 후속29 후속] 결투 개시 접근 — 양 선수가 서로를 향해 실제 보행으로 근접.
        /// 제자리 원거리 공격(스폰 ±5m 간격 + 1.2m 돌진) 폐기. 트랜스폼 구동(G3 kinematic 계약 유지 —
        /// 물리 이동 없음), 이동 중 서로 마주보게 조향, 도착 후 정면 고정.</summary>
        private static System.Collections.IEnumerator DuelEngage(Transform west, Transform east)
        {
            if (west == null || east == null) yield break;
            float timeout = 8f;   // 안전 상한 — 어떤 경우에도 라운드 진행 보장
            while (timeout > 0f)
            {
                timeout -= Time.deltaTime;
                if (west == null || east == null) yield break;
                Vector3 wa = west.position, ea = east.position;
                Vector3 toEast = ea - wa; toEast.y = 0f;
                float gap = toEast.magnitude;
                if (gap <= EngageGap) break;
                Vector3 dirW = toEast / gap;
                float step = EngageSpeed * Time.deltaTime;
                float remain = gap - EngageGap;
                float move = Mathf.Min(step, remain * 0.5f);   // 양측 절반씩 — 대칭 근접
                west.position = wa + dirW * move;
                east.position = ea - dirW * move;
                // 이동 방향을 바라보게 조향 — 보행 애니가 전진으로 재생되게
                Quaternion faceW = Quaternion.LookRotation(dirW, Vector3.up);
                Quaternion faceE = Quaternion.LookRotation(-dirW, Vector3.up);
                west.rotation = Quaternion.Slerp(west.rotation, faceW, Mathf.Min(1f, Time.deltaTime * 10f));
                east.rotation = Quaternion.Slerp(east.rotation, faceE, Mathf.Min(1f, Time.deltaTime * 10f));
                yield return null;
            }
            if (west == null || east == null) yield break;
            // 도착 후 정면 고정 — 라운드 돌진(전진 방향)과 이름표 방향 정합
            Vector3 toEastFinal = east.position - west.position; toEastFinal.y = 0f;
            if (toEastFinal.sqrMagnitude > 0.0001f)
            {
                Vector3 dir = toEastFinal.normalized;
                west.rotation = Quaternion.LookRotation(dir, Vector3.up);
                east.rotation = Quaternion.LookRotation(-dir, Vector3.up);
            }
            Debug.Log($"[ArenaBetting] [125차 후속29 후속] 결투 접근 완료 — 간격 {Vector3.Distance(west.position, east.position):F2}m");
        }

        // [125차 후속29 후속2] 병사 표준 애니 경로 리소스 — 본씬 병사 계약(TestTerritoryCombatSetup 1938~1999행
        //   검증 패턴 / TerritoryNPCSpawner 180~187행)과 동일 조합. lv1 공통(결투선수는 레벨 구분 없음).
        private const string SoldierFbxKey = "Models/UserProvided/fbx/soldier_lv1-20_rigged";   // Humanoid avatar 포함
        private const string SoldierGlbMatKey = "Models/UserProvided/Soldier_Lv1-20_Rigged";    // 재질 이식 원본 GLB
        private const string SoldierAcKey = "Animation/Controllers/SoldierShield_AC";           // 믹사모 클립 컨트롤러
        private const string FistAcKey = "Animation/Controllers/SoldierFist_AC";                // [무번호 87] 맨손 결투 컨트롤러(병사 신규 FBX 5종 — Humanoid 리타깃)
        private static readonly string[] FistAttackCycle = { "Punch", "Kick", "Punch", "Headbutt" }; // [무번호 87] 기본 공격 결정론 사이클
        private const string FistComboState = "PunchKick";   // [무번호 87] 강타(피해 ≥25) — 펀치 후 돌려차기 콤보

        /// <summary>[125차 후속29 후속2] 결투선수 표준 병사 비주얼 — 본씬 병사 계약 통일. 검증 패턴 이식:
        /// ①병사 Humanoid FBX 본체(GLB는 Humanoid avatar 부재로 클립 바인딩 불가 — 재질 소스로만 사용)
        /// ②1.8m 전 씬 키 계약 정규화(51차 SoldierTargetHeight, MeasureStandingHeight 실측)
        /// ③FBX 자식 콜라이더 제거(충돌=G2 모래 박스 단일 — 트랜스폼 구동 계약) ④Animator+SoldierShield_AC
        /// ⑤HumanoidClipDriver(Soldier) 루트 부착(Speed=transform 델타 자체 계산 — 후속11 속도공급 폐기 대체)
        /// ⑥GLB 재질 이식(Blender FBX 텍스처 유실 대책) ⑦결정론 접지(bounds.min.y=모래 보행 박스 톱 0.14 —
        /// G2 보행면; 물리 미사용 계약이라 proc 접지 폐기 대체).
        /// 성공 시 true+root 반환, FBX 미로드 시 false(호출부 캡슐 폴백).</summary>
        private static bool TrySpawnStandardSoldierVisual(Transform parent, string goName, Vector3 pos, string fighterName, out GameObject root)
        {
            root = null;
            var fbxPrefab = Resources.Load<GameObject>(SoldierFbxKey);
            if (fbxPrefab == null)
            {
                Debug.LogWarning($"[ArenaBetting] 병사 Humanoid FBX 미로드 — 캡슐 폴백: {SoldierFbxKey}");
                return false;
            }

            root = new GameObject(goName);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = pos;   // [125차 후속17] 룸 기준 로컬 배치 정합
            root.transform.localRotation = Quaternion.identity;

            var body = UnityEngine.Object.Instantiate(fbxPrefab, root.transform);
            body.name = $"{goName}_Body";
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = Vector3.one;

            // 1.8m 전 씬 키 계약(51차) — 검증 측정 경로 재사용(TerritoryBuilder 단일 소스)
            float factor = TerritoryBuilder.NormalizeSoldierHeightFactor(body, TerritoryBuilder.SoldierTargetHeight);
            body.transform.localScale = Vector3.one * factor;

            // FBX 자식 콜라이더 제거 — 병사 자체 충돌 불필요(이동=트랜스폼 구동, 보행면=G2 박스)
            foreach (var col in body.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.Destroy(col);

            // Animator + SoldierShield_AC(믹사모 클립) — applyRootMotion=false(트랜스폼 구동 계약)
            var anim = body.GetComponent<Animator>();
            if (anim == null) anim = body.AddComponent<Animator>();
            var ctrl = Resources.Load<RuntimeAnimatorController>(FistAcKey);   // [무번호 87] 맨손 결투 우선 — 로드 실패 시 기존 경로 폴백
            if (ctrl == null) ctrl = Resources.Load<RuntimeAnimatorController>(SoldierAcKey);
            if (ctrl != null) anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (anim.avatar == null || !anim.avatar.isValid || !anim.avatar.isHuman)
                Debug.LogWarning($"[ArenaBetting] ⚠️ {goName} Humanoid avatar 무효 — 클립 재생 불가(T포즈) 가능성");

            // 병사 모드 드라이버(루트) — Speed=transform 델타, 공격=AttackRound→TriggerAttack(GuardCombatAI 선례)
            var driver = root.AddComponent<HumanoidClipDriver>();
            driver.mode = HumanoidClipDriver.DriveMode.Soldier;

            // GLB 재질 이식 — Blender FBX 텍스처 유실 대책(본씬 동일 계약)
            HumanoidClipDriver.CopyMaterialsFromGlb(body, SoldierGlbMatKey);

            // 결정론 접지 — 렌더러 bounds 최하단 = 모래 보행 박스 톱(G2, y=0.14=SAND_TOP_Y-0.01).
            //   물리 미사용 계약(G3 kinematic)이므로 스폰 후 즉시 고정 — 낙하/부유 없음.
            var rends = root.GetComponentsInChildren<Renderer>();
            if (rends != null && rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                root.transform.position -= Vector3.up * (b.min.y - (ArenaPitInteriorBuilder.SAND_TOP_Y - 0.01f));
            }
            Debug.Log($"[ArenaBetting] [125차 후속29 후속2] 선수 시각 — 표준 병사 경로(FBX+SoldierShield_AC+드라이버+재질 이식) ({fighterName})");
            return true;
        }

        /// <summary>투기장 선수 시각 — 표준 병사 경로 우선(병사 FBX+SoldierShield_AC+드라이버), 캡슐 폴백(팀 색).</summary>
        private static Transform SpawnFighterVisual(Transform parent, ArenaBetFighter fighter, string goName, Vector3 pos, Color fallbackColor, float maxHP, bool isPlayerPick = false)
        {
            // [125차 후속29 후속2] 병사 표준 애니 경로 — 본씬 병사 계약(병사 Humanoid FBX+SoldierShield_AC+
            //   HumanoidClipDriver(Soldier)) 통일(사용자 지시 "모든 테스트씬에서 병사/몬스터/플레이어 정상 애니").
            //   GLB는 Humanoid avatar 부재로 클립 바인딩 불가(Phase 1 스캔 실측) — 구 GLB+ForceBiped 절차 경로 폐기,
            //   후속11 속도공급 배선도 함께 폐기(드라이버 Soldier 모드가 transform 델타로 Speed 자체 계산).
            GameObject go = null;
            TrySpawnStandardSoldierVisual(parent, goName, pos, fighter.name, out go);
            if (go == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = goName;
                go.transform.SetParent(parent, false);
                go.transform.localPosition = pos;   // [125차 후속17] 로컬 배치 정합
                go.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);   // 높이 1.8m
                var rend = go.GetComponent<MeshRenderer>();
                if (rend != null) rend.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
                { name = $"ArenaDuelMat_{goName}", color = fallbackColor };
                Debug.Log($"[ArenaBetting] 선수 시각 — 캡슐 폴백 ({fighter.name})");
            }
            // [125차 후속29 G3] 병사 물리 안전망 — 이동 계약=LungeVisual 트랜스폼 구동(물리 이동 불필요).
            //   ProceduralAnimationController RequireComponent(Rigidbody) 자동부착 RB(useGravity 기본 on)가
            //   유니티 중력 낙하의 실제 원천(pit_late y=-28 실측) — kinematic+useGravity off로 제거.
            //   proc 컨트롤러는 속도공급자 존재 시 AddForce 경로가 비활성(L722 조기반환) — 무영향.
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                Debug.Log($"[ArenaBetting] [125차 후속29 G3] 병사 물리 안전망 — Rigidbody kinematic ({go.name}/{rb.name})");
            }
            foreach (var cc in go.GetComponentsInChildren<CharacterController>(true))
            {
                cc.enabled = false;
                Debug.Log($"[ArenaBetting] [125차 후속29 G3] 병사 CharacterController disable ({go.name}/{cc.name})");
            }
            // [125차 후속25 R5] 커스텀 접지 마커 폐기 — 식별은 기존 병사형 이름표(이름+HP바)가 담당(사용자 계약).
            // [125차 후속25 R5] 이름표 바인딩 태그 — NameplateOverlayUTK 스캔 대상(병사형 이름표+HP바 자동 표시).
            var tag = go.GetComponent<ArenaDuelFighterTag>() ?? go.AddComponent<ArenaDuelFighterTag>();
            tag.DisplayName = fighter.name;
            tag.SetHP(maxHP, maxHP);
            return go.transform;
        }

        private static void NormalizeHeight(GameObject go, float targetHeight)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            if (b.size.y > 0.01f)
            {
                float k = targetHeight / b.size.y;
                go.transform.localScale *= k;
                var bounds2 = go.GetComponentInChildren<Renderer>().bounds;
                go.transform.position += Vector3.down * bounds2.min.y + Vector3.up * 0.02f;   // 접지
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 테스트 훅 (EditMode)
        // ─────────────────────────────────────────────────────────────
        internal static void ClearForTest()
        {
            _registry.Clear();
            _currentPair = null;
            _pendingBet = null;
            _duelRunning = false;
        }

        internal static ArenaBetFighter MakeFighterForTest(string id, string name, float atk, float def, float agi, int wins, int losses)
        {
            var f = new ArenaBetFighter
            {
                id = id, name = name,
                origin = "테스트 출신", tagline = "테스트 · 2인 난투",
                attack = atk, defense = def, agility = agi, maxHP = 100f,
                wins = wins, losses = losses,
            };
            _registry[id] = f;
            return f;
        }

        internal static (ArenaBetFighter fighter, int amount, float odds)? PendingBetForTest => _pendingBet;
        internal static void SettleForTest(ArenaBetFighter winner, ArenaBetFighter loser)
        {
            winner.wins++;
            loser.losses++;
        }
    }
}
