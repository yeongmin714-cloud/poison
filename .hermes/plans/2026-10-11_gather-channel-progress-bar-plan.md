# 수집 채널링 진행바(파랑) + 공용 E 상호작용 프롬프트 시스템 (실행 전 계획)

날짜: 2026-10-11
상태: **계획 전용 (plan-only)** — 사용자가 "실행" 명시 전까지 코드 미반영
요청자: 영민 조

---

## 1. Goal

1. **수집 채널링**: E키 즉시 습득 → **자동 채널링**으로 전환, 진행률을 대상 **머리 위 월드 고정 바**로 표시. **바 색 = 파랑(#58A6FF)**.
2. **공용 E 상호작용 프롬프트**: Figma `interaction-panel`(82:45, "E키를 눌러 상호작용") UI를 **모든 E 상호작용**에 표시.
3. **InteractionPanelUTK 정합 확인**: 기존 NPC/병사/영주 다중버튼 패널(240×82)이 **제대로 부착·배선**됐는지 + **Figma 100% 일치**인지 확인.

확정된 사용자 결정:
1. **바 색**: 초록 → **파랑** (#58A6FF — Figma E프롬프트 라벨색·UTKTheme Acc와 통일)
2. **E 프롬프트 범위**: **모든 E 상호작용**(수집+문/자물쇠/낚시/영주실/잠자기) 전부
3. **바 위치**: 대상 머리 위 월드 고정(빌보드, 따라다님)
4. **진행 방식**: E키 후 자동 진행, 이동 시 취소
- **낚시**: 기존 릴 미니게임은 채널링 제외, 단 **낚시 시작 전 "E키" 프롬프트는 공용 프롬프트로 표시**
6. **진행시간**: 전 유형 동일 기본 시간 + **도구 등급 스켈레톤 훅**(나중에 기구 등급↑=시간↓)

## 2. 현재 실측 (read-only)

| 항목 | 파일 | 실측 |
|---|---|---|
| 채집 E→즉시 습득 | `HerbPickup.cs` | `Update()`(L257~259): `_playerNearby && Input.GetKeyDown(KeyCode.E)` → `Harvest()` 직접. private `Harvest()`: Gather 애니 + `LootBasket.Create`+`AddSeedDrop`+`PlayerStats.AddEXP(3)`+리스폰. 병사 `TryAutoGather()` 별도(즉시 — 미변경). |
| 채광/벌목 | `ResourceNode.cs` | `ResourceType{Wood,Stone,IronOre,...}`. 플레이어 E 경로 채널 게이트 필요. 병사 `TryAutoMine()` 즉시(미변경). `TryRollRareBonus()` 잔존. |
| 농경 수확 | `FarmingSystem.cs` | 정적 `Harvest(GameObject plot)`(L181)이 수확 개수 반환 — 채널 완료 최종 경로. |
| 기존 진행바 | `WorkProgressBar.cs` | 월드 2 Quad(배경+채움), 좌정렬 0→1, 빌보드, `p>=1` 자동 Close. `color` 파라미터 존재 → 재사용. |
| 낚시(채널 제외) | `FishingSystem+FishingUTK` | 2바 릴 미니게임 — 내부 동작 불변. "낚시 E 프롬프트"만 공용화. |
| E 프롬프트(신규 대상) | Figma `interaction-panel`(82:45) | 288×52.8, ActionInteract 268.8×40.8, keycap-E + 라벨 "E키를 눌러 상호작용"(13.2px Bold #58A6FF), 배경 #161B22. 변형: `-fishing/-sleep/-lordroom/-lockpick`. |
| 기존 NPC/병사/영주 패널 | `InteractionPanelUTK` | Figma `interaction-panel` 5종(81:4/51/91/134, 82:2) 240×82, ActionGrid 224×66, 2×2버튼. **Phase D 배선(트리거→Open) 컨텍스트상 미완** —— 1번 확인 과제 대상. |
| 대상 분류 | `HoverTargetClassifier` | Enemy/Farm/Mine/Gather/Ally/Terrain + ShopNPC/Lord. E 프롬프트 대상 감지에 재사용. |

## 3. 제안 구조

### 3.1 `GatherChannelSystem` (신규, `Assets/Scripts/Systems/GatherChannelSystem.cs`)
채널 상태 머신(static Instance):
- 상태: `_target`, `_onComplete`, `_onCancel`, `_startTime`, `_startPos`, `_duration`, `_active`
- `bool Begin(Transform targetAnchor, Action onComplete, float baseDuration, float toolFactor=1f)`:
  `_active=true`, 시작 시각·플레이어 위치 캡처, `WorkProgressBar.Show(targetAnchor, duration, ColorBlue)` 초래 진행바.
  `duration = baseDuration * Max(toolFactor, 0.25f)` (기본 1.0)
- `Update()`: 이동감지(XZ 이동 > `CancelMoveThreshold` 0.45m → Cancel), `progress=(Time.time-_start)/_duration`, ≥1 → Complete, 대상 소실/범위 이탈 → Cancel. **파랑색 사용**.
- `Complete()`: `_onComplete?.Invoke()` + 정리. `Cancel()`: 바 Close + 상태 리셋.
- **도구등급 훅(스켈레톤)**: `static Func<float> SpeedFactorProvider = () => 1f;` — 나중에 농작기구 등급 대입. EditMode에서 대체 시 시간 단축 검증.

### 3.2 진행바 (`WorkProgressBar` 확장, 로우 리스크)
- 기본 fill 색 = 병사 유지(현재). **플레이어 채널은 호출부가 파랑 전달**(`Show(…, 0xA6/58, …)`).
- 바 시각: 몬스터 HP바스럽게 얇은 트랙/림 — 기존 2 Quad 유지 + 폭은 HP바보다 짧게. (실측 비교 후 림 추가 판정)

### 3.3 공용 E 상호작용 프롬프트 (`EInteractionPromptUTK` 신규, UI.Toolkit)
- **Figma 82:45 정합**: 288×52.8(등배수 k 적용), ActionInteract 268.8×40.8, keycap-E + 파랑(#58A6FF) Bold 13.2px 라벨, 배경 #161B22(UTKTheme.Panel). GitHub-dark.
- **월드 앵커 HUD**: `HoverTargetClassifier`(또는 각 대상 컴포넌트 프레즌스)로 현재 E 상호작용 가능 대상을 판별 → 대상 머리 위 화면 투영 위치에 표시(몬스터 HP바와 동일 패턴).
- **라벨 데이터주도**: 대상별 문자열 — 기본 "E키를 눌러 상호작용", 특수: 자물쇠/영주실/낚시/잠자기 등 전용 문구(Figma 변형 재사용).
- 표시 조건 = **어떤 E 상호작용이든 가능 상태가 되면 표시**, 불가(범위 이탈/수집 후 고갈) 시 숨김.
- **E 프롬프트 → 진행바 전환**: 수집 대상 E 누르면 프롬프트 숨김 + `GatherChannelSystem` 파랑 진행바 시작. 완료 시 숨김/정리.
- ⚠ 기존 `HerbPickup._promptPrefab`(월드 캔버스 "E키로 채집") → 공용 프롬프트로 **대체/제거** (중복 표시 방지). NPC/병사/영주는 `InteractionPanelUTK` 유지.

### 3.4 상호작용 게이트 (E 즉시 → 채널/반응)
- **HerbPickup L257~259**: E → 채널 Begin, 완료 시 `Harvest()`. 병사 `TryAutoGather` 미변경.
- **ResourceNode (채광/벌목)**: 플레이어 E 경로 추가(실측 후) → 채널 완료 시 마이닝 산출. `TryAutoMine` 미변경.
- **FarmingSystem.Harvest(plot)**: E 게이트 → 채널 완료 시 호출.
- **문/자물쇠/영주실/잠자기**: 공용 프롬프트 표시 + 기존 각 상호작용 트리거와 연결(채널 아님, 즉시 상호작용).

> ⚠ 구현 중 **각 E 게이트의 정확한 입력 라우팅을 먼저 실측** (어느 컴포넌트 Update에서 E 트리거되는지 판별). HerbPickup는 자체 E 확인됨. 채광/농경/문/자물쇠 등은 grep/Editor.log로 확정.

## 4. 단계별 계획

### P0 — InteractionPanelUTK 정합 확인 (확인 과제)
- `InteractionPanelUTK` **부착/배선 상태 전수**: `AddComponent<InteractionPanelUTK>`·`.Open(` 호출부 grep · Figma 렌더 대조. **Figma 100% 일치 검증**(spec 240×82, 버튼 2×2, 절차 데칼, GitHub-dark 색) + 기존 병사 `SoldierInteractUTK` 비회귀 확인.
- 산출: 정합 여부 보고 + 필요 시 수정 범위(계획 갱신).

### P1 — `GatherChannelSystem` 코어 + 파랑 바
- 상태머신, 이동취소, 완료 콜백, 도구등급 훅, `WorkProgressBar` 파랑 초래
- EditMode: Begin/진행/Complete/이동취소/도구훅=1.0

### P2 — `EInteractionPromptUTK` (공용, 모든 상호작용)
- Figma 82:45 정합, 월드 앵커, 라벨 데이터주도, 표시/숨김 조건
- EditMode: 대상 감지 시 표시, 범위 밖 숨김, 수집 E 후 숨김+채널 시작

### P3 — HerbPickup E 게이트
- L257~259 → 채널 Begin, 완료 시 Harvest. `TryAutoGather` 미변경. `_promptPrefab` 대체.

### P4 — ResourceNode(채광/벌목) + Farming E 게이트 + 문/자물쇠 등 연결
- 플레이어 E 마이닝 경로(채널 완료 시 산출·리스폰), `TryAutoMine` 미변경
- `FarmingSystem.Harvest` 채널 완료 호출
- 문/자물쇠/영주실/잠자기 등: 공용 프롬프트 + 기존 상호작용 연결

### P5 — 검증 통합
- 신규 컴파일 `error CS` 0
- 전체 EditMode **baseline(624/617/7) 대비 신규 회귀 0**
- Play: 어떤 E 대상이든 프롬프트 표시 / 수집은 **파랑 진행바** 채우며 습득 / 이동 취소 / 낚시·병사 자동 비회귀 / InteractionPanelUTK 정합

## 5. 테스트 대상 (EditMode)
- `GatherChannelSystemTests.cs` (신규): 진행/완료/이동취소/대상소실/도구등급훅
- `EInteractionPromptUTKTests.cs` (신규): 대상 수 표시, 범위 밖 숨김, 수집 E→숨김+채널시작
- `HerbPickupChannelTests.cs` (신규): E 게이트 리다이렉트, 완료 시 Harvest, 자동경로 비회귀
- `ResourceNodeChannelTests.cs` (신규, P4): 플레이어 마이닝 경로, TryAutoMine 비회귀
- `InteractionPanelParityTests` (신규, P0): InteractionPanelUTK Figma 100% 정합(240×82, 2×2, 색, 부착배선)

## 6. 검증 방법
- 컴파일 `error CS` 0
- 전체 EditMode: `/mnt/c/Unity/qa/harness` config.sh + Unity -runTests -testPlatform EditMode → baseline(624/617/7) 동일 or 신규 실패 시 계약 갱신 후 재검증
- Play: Editor.log 실측 + 캡처(Screenshots/ 순번) — E 프롬프트 / 파랑 진행바 / 이동취소 / InteractionPanelUTK 정합 스샷
- Figma 정합: spec dump 대조(240×82 등) + 원본 figma-file.json(arcData/effects) 대조

## 7. 리스크/트레이드오프/오픈 질문
- **E 핸들러 분산**: 상점NPC/아레나/튜토리얼 등 많은 컴포넌트가 자체 E 가짐. 공용 프롬프트는 "수집+문/자물쇠/영주실/잠자기"만 대상으로 충돌 최소화. 상점NPC/아레나 E는 InteractionPanelUTK/별도 유지.
- **바 색 파랑 확정**: #58A6FF 기본. 원하는 다른 파랑 톤 있으면 실행 전 지정.
- **E 프롬프트 vs InteractionPanelUTK 충돌**: 같은 범위에서 둘 다 뜨면 안 됨 — 판별 우선순위 정의(NPC/병사/영주=패널, 나머지=프롬프트).
- **채광/농경/문 플레이어 E 실측 필요**: P3/P4 시작 전 grep 확정.
- **도구등급 훅**: 스켈레톤 1.0, 실제 기구는 이후 범위.
- **병사 자동 수집**: 즉시 유지(의도), 플레이어 채널과 별개.
- **WorkProgressBar 색 영향**: 병사 파랑→유지의 경우 충돌 — 플레이어와 구분되는 색이 필요하면 조정.

## 8. 완료 조건
- P0 InteractionPanelUTK 정합/배선 확인 보고
- 컴파일 0 + EditMode 신규 회귀 0
- Play: 모든 E 상호작용 프롬프트 표시 / 수집 4종 **파랑 진행바** 완료 습득 / 이동 취소 / 낚시·병사 자동 불변
- QAPROGRESS 기록 + (사용자 요청 시) stage/commit