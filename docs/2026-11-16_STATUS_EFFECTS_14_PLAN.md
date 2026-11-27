# [무번호 63-B] 플레이어 상태이상 14종 + 상태이상 UI 계획서 (2026-11-16)

> 사용자 확정: 14종 전부 채택(기존 6 + 추가 8). UI = 현재 시간 UI 밑, 원형 게이지(체력/스태미너 동일 계열),
> 중앙 아이콘, 남은 시간 시계방향 소모, 호버 시 남은 시간, 여러 개면 일정 여백 가로 배열.
> 효과치는 기본값 — Play 체감 후 트림 대상(상수만 수정).

---

## §1 시스템 스펙 — PlayerStatusEffectSystem (신규)

**파일**: `Assets/Scripts/Systems/PlayerStatusEffectSystem.cs` (신규, 단일 파일)
**네임스페이스**: `ProjectName.Systems`

```csharp
public enum StatusEffectId
{
    Bleeding, Poisoning, Slowness, Exhaustion, Fracture, Daze,
    Hunger, Burn, Hypothermia, Terror, Infection, Weaken,
    Drunkenness, BrittleDefense
}

public class PlayerStatusEffectSystem : MonoBehaviour
{
    public static PlayerStatusEffectSystem Instance { get; }

    // ── 핵심 API (UI와 공유하는 교차 시그니처 — 이 시그니처 불변) ──
    public static void AddEffect(StatusEffectId id, float duration);      // 멱등 — 이미 활성이면 remaining=duration 갱신(중첩 아님)
    public static void RemoveEffect(StatusEffectId id);                    // 수동 해제(치료)
    public static bool HasEffect(StatusEffectId id);
    public static float GetRemainingSeconds(StatusEffectId id);            // 비활성 시 0
    public static float GetDurationSeconds(StatusEffectId id);             // 현재 구간 전체 지속(UI 분모)
    public static IReadOnlyList<StatusEffectId> GetActiveEffects();        // UI 표시 순서 — 추가 순
    public static event Action OnChanged;                                  // 추가/제거/만료 시 1회 발화(UI 폴링 보조)

    // 해제 수단 (치료 채널 — ConsumableSystem/수면/환경에서 호출)
    public static void CureBleeding();        // 붕대 — Bleeding 제거
    public static void CurePoisoning();       // 해독제
    public static void CureSleep();           // 수면 — Exhaustion/Fracture/Hypothermia/Terror/Infection 제거 + Hunger remaining 60%로 절반
    public static void CureMeal();            // 식사 — Hunger 제거
    public static void CureWarm();            // 실내/모닥불 — Hypothermia 제거
    public static void CureCalm();            // 진정제 — Terror 제거
    public static void Extinguish();          // 물 접촉(수영) — Burn 즉시 제거
}
```

**동작 규약**
- 자가부트: `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` 멱등 Ensure — GameObject "PlayerStatusEffectSystem" + DontDestroyOnLoad (BuffManager 선례).
- Update에서 remaining 감소(Time.deltaTime) — 0 도달 시 자동 해제 + OnChanged.
- DOT 틱: Bleeding 2 HP/s · Poisoning 1.5 HP/s · Burn 5 HP/s · Infection 1 HP/s — `PlayerHealth.Instance.TakeDamage(float)` 사용(순수 대미지 — 피격 훅 재진입 금지: 이 DOT 호출에는 상태이상 신규 발동 확률 판정을 태우지 않을 것, §4 참조).
- 스탯 수정: 적용 시 1회 `PlayerStats` 직접 가감 + 해제 시 역가감(BuffManager Apply/ReverseBuff 동일 산식 — BuffManager 자체는 무수정). 스탯 연관:
  - Slowness: MoveSpeedBase −(적용값) [기존 몬스터 값 −0.5 유지]
  - Fracture: MoveSpeedBase −20% (−0.2×기본 이동속도 아님 — 적용 시점 MoveSpeedBase에 −0.2 고정치 금지, **기본값 기준 비율**: `_stats.MoveSpeedBase` 기본 10 기준 −2.0 → 상수 FRACTURE_SPEED_PENALTY = 2f)
  - Exhaustion: 스태미너 회복률 −50% → PlayerMovement의 회복 로직에 시스템 조회 훅(§4) — 스탯 가감 아님
  - Daze: 스태미너 회복 −30% + 조준 분산(활/투사체에 훅 — §4, 미연동 시 1차는 스태미너만)
  - Hunger: 스태미너 최대치 −30% + 힐 정지(Heal 무시 가드) — PlayerMovement MaxStamina/PlayerHealth.Heal 조회 훅
  - Hypothermia: MoveSpeedBase −1.5 + 스태미너 회복 −40%
  - Weaken: AttackDamageBase −30% (기본값 기준 — 기본 공격력 상수 실측 후 적용, 기본값 기준 비율 산식 주석 필수)
  - Drunkenness: AttackDamageBase +10% / MoveSpeedBase −1.0
  - BrittleDefense: DefenseBase −30% (기본값 기준)
  - Terror: 스탯 없음 — 콤보/대쉬 훅(§4)
- **행동 잠금 조회 API** (PlayerMovement 등이 읽음):
  ```csharp
  public static bool BlocksDash();    // Exhaustion || Fracture
  public static bool BlocksRoll();    // Fracture
  public static bool BlocksHeal();    // Poisoning(회복효율×0.5 — 정지 아님) → Heal 절반 경로: ApplyHealMultiplier()
  public static float HealMultiplier();  // Poisoning 0.5, Infection 0(정지), Hunger 0(자연회복만 정지) — 다중 활성 시 최소값
  public static float StaminaRegenMultiplier(); // Exhaustion 0.5, Daze 0.7, Hypothermia 0.6 — 다중 최소값
  public static float ComboMissChance();        // Terror 0.3, Daze 0.15, Drunkenness 0.1 — 합산 clamp 0.8
  ```
- Infection 연쇄: Bleeding 또는 Fracture가 활성 상태로 30s 경과(효과 자체 remaining이 아닌 "치료 안 된 채 경과" 추적 — _infectionTimer) → AddEffect(Infection, 30). 출혈 치료 시 타이머 리셋.
- Hunger: 게임시간 기준 — TimeManager.GameTime 마지막 식사 시각 대비 24h(게임초 86400) 경과 시 AddEffect(Hunger, 600). 식사(CureMeal)로 해제 + 마지막 식사 시각 갱신. 세이브/로드 미연동(세션 내 휘발성 — 1차).
- 접근성: 씬 전환에도 유지(DDOL). Play 종료 시 자동 소멸.

## §2 14종 정의 표 (기본값 — Play 후 트림)

| ID | 발동원 | 효과 | 지속 | 해제 |
|---|---|---|---|---|
| Bleeding | 근접/화살 피격 25% | 2 HP/s DOT | 10s | 붕대·시간 |
| Poisoning | 독 가스·독 피격 | 1.5 HP/s + 회복×0.5 | 12s | 해독제·시간 |
| Slowness | 몬스터 약화 스킬(기존) | 이속 −50% | 5s | 시간 |
| Exhaustion | 스태미너 0 도달·밤샘 | 대쉬불가 + 스태 회복 −50% | 20s | 수면·시간 |
| Fracture | 낙하 피해·1타 ≥HP30% | 구르기불가 + 이속 −20% | 90s | 부목(붕대 재사용)·수면·시간 |
| Daze | 가스 노출(마스크 면역) | 스태 회복 −30% + 콤보 누락 15% | 8s | 이탈·시간 |
| Hunger | 24h 미식 | 스태 최대 −30% + 힐 정지 | 600s(갱신) | 식사 |
| Burn | Molotov 화염 접촉 | 5 HP/s 강틱 | 4s | 수영(물)·시간 |
| Hypothermia | 밤+비+젖음 | 이속 −1.5 + 스태 회복 −40% | 15s | 실내/모닥불·수면 |
| Terror | 보스급 근접 3s | 콤보 누락 30% | 6s | 이탈·진정제 |
| Infection | 출혈/골절 미치료 30s | 1 HP/s + 힐 정지 | 30s | 붕대·수면 |
| Weaken | 몬스터 스킬 | 공격력 −30% | 8s | 시간 |
| Drunkenness | 술 소비 | 공격 +10%/이속 −1.0/콤보 누락 10% | 25s | 시간 |
| BrittleDefense | 1타 ≥HP20% | 방어력 −30% | 10s | 시간 |

## §3 UI 스펙 — StatusEffectBarUTK (신규)

**파일**: `Assets/Scripts/UI/Toolkit/StatusEffectBarUTK.cs` (신규, 단일 파일)
**네임스페이스**: `ProjectName.UI.Toolkit`

```csharp
public class StatusEffectBarUTK : VisualElement
{
    public static void Ensure();   // 멱등 — UIRoot 부착. UTKWireUp.Wire()에서 호출(1줄 추가)
}
```

**배치**
- UIRoot 절대 배치 `left=14` — 시계(TimeClockGlassUTK: left=14/top=12/width=166/height=GlyphHeight+30) **바로 아래**, 상단 간격 8px.
  - `style.top = 12 + <시계 높이 실측값> + 8` — 시계 소스의 GlyphHeight 실측 후 상수 산출(하드코딩 금지 — 소스 참조 주석).
- 표시: `GetActiveEffects()` 순서대 가로 배치(Row), **아이콘 44×44**, 아이콘 간 여백 **6px 고정**. 개수 가변 — 활성 0개면 바 표시 영역 투명/비활성.
- 개별 아이콘 = **UTKGaugeStripArc 재사용**(HP/스태미너 게이지 동일 계열 — HUDUTK BuildHotbarGauges 선례):
  - 원형 아크 = 남은 시간 비율(`GetRemainingSeconds/GetDurationSeconds`) — **시계방향 소모**(UTKGaugeStripArc 내장 규약, 12시 시작).
  - 아이콘별 색: 상태별 고정 색(§2 표 매핑 — 출혈 #C0392B, 중독 #27AE60, 둔화 #7F8C8D, 탈진 #2980B9, 골절 #B95C50, 혼탁 #8E9AA6, 허기 #E67E22, 화상 #E74C3C, 저체온 #5DADE2, 공포 #9B59B6, 감염 #6B8E23, 약화 #D4AC0D, 취기 #F1C40F, 방어취약 #95A5A6).
  - 중앙 글리프: **절차적 VisualElement 조합**(SleepPanelUTK BuildMoonIcon/BuildBedGlyph/BuildSaveGlyph 선례 — rect/circle/rotate 조합). **벡터메시 금지**(84차 계약). PNG 부재 — 절차 근사 14종:
    - 출혈=물방울(rect 회전+circle), 중독=두개 단순(circle+2사각), 둔화=달팽이 대각선+원, 탈진=번개(다이아+사각), 골절=뼈(2원+사각), 혼탁=나선 대신 물결 사각 3단, 허기= Drumstick(원+사각), 화상=불꽃(다이아+사각), 저체온=눈(십자+대각), 공포=눈(타원+원), 감염=세균(circle+돌기 4), 약화=아래화살, 취기=잔(사각+원), 방어취약=방패(테두리+금 사각)
    - 정밀 그림 불요 — 44px에서 식별 가능 수준, 각 글리프는 독립 메서드 Build<Name>Glyph() (선례 스타일).
- **호버 툴팁**: 아이콘 `pickingMode=Position` + MouseEnterEvent/MouseLeaveEvent — 진입 시 아이콘 위 96×24 라벨(이름+남은 초 "골절 — 37초") 표시, 이탈 시 숨김. 컨테이너/바 자체는 `pickingMode=Ignore`(HUD 클릭 흡수 금지 규약 — TimeClockGlassUTK P23 선례).
- 폴링: `schedule.Execute(Refresh).Every(250ms)` — HUD 폴링 선례. OnChanged 이벤트 수신 시 즉시 Refresh(리프레시 타이머+아이콘 수).
- 자가부트: `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` 멱등 Ensure — UIRoot 미준비 시 조용히 스킵(선례). UTKWireUp.Wire()에서 Ensure() 호출 1줄 추가(흐름상 중복 무해 — 멱등).
- HUD 계약 준수: 시계/HUD와 겹침 금지, 창박스 등배수 계약 미적용(HUD 앵커 요소라 raw px).

## §4 발동/해제 훅 (Phase C — 기존 파일 증분 patch 목록)

| # | 파일 | 위치 | 훅 내용 |
|---|---|---|---|
| 1 | Core/PlayerHealth.cs | TakeDamage(float, Vector3, string weaponType) 216행 본문 말미 | 피격 발동: weaponType 근접/화살 → 25% Bleeding(10s); 단발 피해 ≥ MaxHP×0.3 → Fracture(90s); ≥ MaxHP×0.2 → BrittleDefense(10s). **DOT 자기재귀 가드**: PlayerStatusEffectSystem이 뿌린 DOT 대미지는 훅 미발동 — `weaponType=="status"` 문자열로 구분(TakeDamage 3인자 오버로드의 weaponType에 "status" 전달) |
| 2 | Systems/MonsterSkillSystem.cs | ExecuteDebuff 641행 | 기존 `BuffManager.Instance.AddBuff("Slowness", 0.5f, 5f)` → `PlayerStatusEffectSystem.AddEffect(Slowness, 5f)` 치환(**이중 적용 금지 — BuffManager 경로 삭제**). 동일 함수에 Weaken 추가 발동(8s) |
| 3 | Systems/PlayerMovement.cs | 대쉬/구르기 진입 게이트 + 스태미너 0 도달 지점 + 낙하 피해 지점 + 스태미너 회복 가산점 + HandleSwimming | ①대쉬·구르기 진입 조건에 `!PlayerStatusEffectSystem.BlocksDash()/BlocksRoll()` ②스태미너 0 도달 → AddEffect(Exhaustion,20) 1회(재진입 가드 — 이미 HasEffect면 스킵) ③낙하 피해 적용점 → Fracture 발동 조건(§2) ④스태미너 회복량 × StaminaRegenMultiplier() ⑤수영 진입 → Extinguish()(화상 해제)+저체온 젖음 플래그 ⑥대쉬 방향 계산에 Terror/Drunkenness 분산 — 1차는 콤보 누락만(PlayerCombat 훅이 없으면 이동 쪽 대쉬만) |
| 4 | Systems/GasCloudField.cs | 플레이어 구름 내부 판정 지점(파일 내 플레이어 거리/Overlap 체크 — 구현 서브가 실측) | 노출 시 AddEffect(Poisoning,12) — 가스면 착용(GasMaskController 상태 조회) 중이면 Poisoning/Daze 면역 + AddEffect(Daze,8)은 마스크 미착용 시 |
| 5 | Systems/Bomb.cs | Molotov 화염 영역(131행 주변 — 화염 피해 적용점) | 화염 접촉 → AddEffect(Burn,4) |
| 6 | Systems/TimeManager.cs | 게임시간 진행 지점(Update/틱) | ①Hunger: 마지막 식사 대비 24h 경과 → AddEffect(Hunger,600) 갱신형 ②Hypothermia: IsNight && 비 && 젖음(수영 훅 플래그) → AddEffect(Hypothermia,15) 갱신형 |
| 7 | Core/Data/ConsumableSystem.cs | ApplyEffect 147행 + 264행 "상태이상 방지" | ①"붕대" 아이템 → CureBleeding()+Infection 제거 ②"해독제" → CurePoisoning() ③"진정제" 기존 로직 유지 + CureCalm() 추가 ④"상태이상 방지" → 전체 활성 디버프 제거(예방은 유지 시간 정책 1차 생략 — 즉시 제거만) ⑤술 아이템(이름 실측 — 없으면 소비 시 효과 문자열 "취기"/"술" 매칭) → AddEffect(Drunkenness,25) ⑥모든 음식 섭취 → CureMeal() |
| 8 | Systems/StealthAssassination.cs | 무수정 — 기존 Bleeding(보스 대상)은 그대로. 플레이어 피격 출혈은 #1 훅에서만 |

**PlayerCombat 훅(콤보 누락)**: PlayerCombat의 공격 입력 진입점에 `Random.value < ComboMissChance()` 가드 — 파일 실측 후 적용(무번호61차 ParryGauge 작업 파일 — 더티 주의, 증분 1지점만).

## §5 테스트 (EditMode)

1. `StatusEffectSystemTests` (신규): ①AddEffect 멱등 갱신(중복 Add → duration 리셋, 목록 1개) ②만료 자동 해제(remaining 수동 감소 — Update 우회 순수 경로: `Tick(float dt)` internal 테스트 훅) ③RemoveEffect/Cure* 매핑 ④HasEffect/GetRemaining/GetActive 순서 ⑤OnChanged 발화 카운트 ⑥HealMultiplier/StaminaRegenMultiplier/ComboMissChance 다중 최소값·합산 ⑦14종 enum 무결성 ⑧DOT 틱 — Tick(dt) 경유 PlayerHealth 더블 가드(status weaponType 미재진입 — 소스 어서션)
2. `StatusEffectBarTests` (신규): 소스 어서션 — ①`StatusEffectBarUTK` 존재+Ensure 멱등 ②left=14/시계 하단 배치 근거 주석 ③UTKGaugeStripArc 사용(동그란 계열) ④여백 상수(ICON_GAP=6) ⑤MouseEnterEvent/남은 초 라벨 ⑥"Mesh" 벡터메시 문자열 부재 ⑦PlayerStatusEffectSystem API 소비(GetActiveEffects/GetRemainingSeconds)

**EditMode 제약 준수**: Time.deltaTime=0 — 시스템은 `internal void Tick(float dt)` 순수 훅 제공(Update에서만 호출), AddComponent해도 Awake 미호됨 — Instance 의존 테스트는 static 경로만(Tick은 인스턴스 무관 정적 상태 사용) 또는 테스트 주입 훅.

## §6 화이트리스트 / 검증 기준

**신규**: Systems/PlayerStatusEffectSystem.cs · UI/Toolkit/StatusEffectBarUTK.cs · Tests/EditMode/StatusEffectSystemTests.cs · Tests/EditMode/StatusEffectBarTests.cs
**수정(증분만)**: MonsterSkillSystem.cs(#2) · PlayerHealth.cs(#1) · PlayerMovement.cs(#3) · GasCloudField.cs(#4) · Bomb.cs(#5) · TimeManager.cs(#6) · ConsumableSystem.cs(#7) · UTKWireUp.cs(UI Ensure 1줄) · PlayerCombat.cs(콤보 가드 1지점)
**금지**: BuffManager.cs 무수정(Slowness 마이그레이션은 MonsterSkillSystem 호출부 치환으로) · .meta 수기 작성 금지(삭제 후 Unity 임포트 재생성) · SleepPanelUTK/UTKWireUp 기존 63차 미커밋분 보존
**검증**: 컴파일 error CS=0 → EditMode 전수(baseline 10건 1:1, 신규 회귀 0) → Play 승인 후 Test_10 피격/가스/화염 3소스 발동 확인
**더티 보호**: 워킹트리 미커밋 다수(144·63차) — 범위 파일 최소 증분 patch만, revert/stash 금지
