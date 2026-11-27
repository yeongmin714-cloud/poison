# 농경시스템 업그레이드 계획 (모여라 동물의 숲 스타일)

> 제정: 2026-10-26 / 승인: 사용자(계획 3차 정정 확정) / 물뿌리개 저장량 20회 확정
> 감독자 정책: `docs/AGENT_DELEGATION_POLICY.md` 준수 — 구현/QA는 서브에이전트 위임 필수

## 0. 확정 요구사항 (사용자 발화 요약)

1. **굉이질로 땅을 판 곳은 구멍(웅덩이)이 생긴다** — 고품질 시각
2. **파종하면 구멍이 메워지고 평평한 "흙길"이 된다** — 잔디와 다른 갈색 흙 텍스처
3. **하루마다 물을 줘야 한다** — 수분도는 하루(게임 1일)에 걸쳐 드레인, 바닥이면 농산물 말라죽음
4. **수확 시기 = 3일**, Ready 경과 **7일 초과 시 부패** — 부패 농작물은 정리 후 흙땅 복귀
5. **수확 후 땅은 기본 흙땅으로 복귀** — 흙땅에서 다시 굉이질 → 파종 사이클 무한 반복
6. **모든 농경 상호작용은 채널링 게이트** (채광/벌목과 동일 스타일) — 즉시 실행 ❌, E채널(파랑 진행바) 채워야 다음 단계
7. **수분바 UI = 몬스터 체력바와 동일한 가로 바, 푸른색** (`NameplateOverlayUTK` 스타일 계승)
8. **성장바 = 수분바 바로 위에 붙은 2행 가로 바** (위=성장 초록, 아래=수분 파랑)
9. **HUD 원형 게이지는 무변경** — 플레이어 HP/스태미나 전용. 농경 게이지는 월드 가로바로만
10. **물뿌리개 = 상점 판매, 내구도 없음, 물 저장량 20회** — 저장량 0이면 물주기 불가
11. **호수 물가에서 물뿌리개 물담기 상호작용** — 기존 낚시 상호작용에 추가 (물뿌리개 소지 시). UI 배치는 사용자가 차후 수정
12. **병사 농경 역할 배치 = 전 과정 완전 자동** (굉이질→파종→매일 물주기→수확, 물뿌리개/충전 개념 없음). **플레이어 = 모든 단계 직접 수행**

## 1. 상태머신

```
Grass(잔디밭, 초기) ──굉이질 채널──▶ Tilled(구멍)
Dirt(흙땅, 수확/정리 후) ──굉이질 채널──▶ Tilled
Tilled ──파종 채널(씨앗 1 소비)──▶ Planted(평평한 흙길, 수분 100 시작)
Planted(파종 직후) → Growing(성장, 3일) → Ready(수확 가능)
Growing/Ready ──수분 0 도달──▶ Withered(시듦) ──정리 채널──▶ Dirt
Ready ──Ready 경과 7일 초과──▶ Rotted(부패) ──정리 채널──▶ Dirt
Growing/Ready ──수확 채널──▶ Dirt(기본 흙땅)
```

- 수분: Planted 시 100% 시작, 게임 1일(86400s) 리니어 드레인. 물주기 채널 완료 시 100% 충전.
- 성장 진행은 수분 > 0일 때만. 수분 0 → 즉시 Withered(작물 데이터 소실, 재배치 필요).
- 부패 판정: Ready 도달 시각에서 7일(게임) 경과 → Rotted.
- 성장 일수: 기존 2일 → **3일** (`TestTerritoryCombatSetup.SetupFarm` 하드코딩 갱신 포함).

## 2. 채널링 규약 (`GatherChannelSystem` 재사용)

| 행위 | 채널 시간 | 비고 |
|---|---|---|
| 굉이질 | 2s | Grass/Dirt → Tilled |
| 파종 | 1s | 씨앗 1 소비, Tilled → Planted |
| 물주기 | 1.5s | 물뿌리개 저장량 1 소비, 수분 100% 충전 |
| 수확 | 2s | Ready → 수확물+씨앗(기존 HerbPickup 경로 유지) |
| 정리 | 1.5s | Withered/Rotted → Dirt |
| 물담기 | 2s | 호수 물가, 물뿌리개 저장량 20/20 충전 |

- 이동 취소/단일 채널 계약은 `GatherChannelSystem` 기존 것 그대로.
- 병사는 채널 게이트 없이 워커 연출 시간 사용(기존 채집 2s/채굴 3s 패턴 계승).

## 3. Phase 분해

### Phase 1 — FarmPlot 상태머신 개편
- 파일: `Assets/Scripts/Systems/FarmPlot.cs`, 신규 `Assets/Scripts/Tests/EditMode/FarmPlotLifecycleTests.cs`
- 내용: §1 상태머신 전환(Grass/Dirt/Tilled/Planted/Growing/Ready/Withered/Rotted), 수분 필드+드레인+물주기 API, 3일 성장, 7일 부패, 모든 플레이어 상호작용을 `GatherChannelSystem.Begin` 경유 게이트, Ready→Dirt 시 HerbPickup 기존 수확 경로 유지
- 검증 범위: EditMode 신규(상태전이/수분드레인/물주기/부패/시듦/정리) + 기존 11건 1:1 유지

### Phase 2 — 고품질 밭 시각
- 파일: `Assets/Scripts/Systems/FarmPlot.cs`(시각부) 또는 신규 `Assets/Scripts/Systems/FarmPlotVisuals.cs`
- 내용: ①구멍=Tilled 상태 프로시저럴 오목 메시(중앙 concave+주변 흙둑 rim+어두운 내벽, 탑다운 음영 판독) ②흙길=Planted 이후 평평한 갈색 경작 텍스처(잔디와 명확 구분) ③Dirt=기본 흙땅 ④시든 작물(갈색 처짐)/부패 작물(검붉은) 시각. 탑다운 계약(천장/존매트 금지=해당없음, 바닥 원본 유지) 준수
- 검증: 컴파일 + Play 시각 확인(Play 승인 게이트 별도)

### Phase 3 — 농장 플레이트 UI (2행 가로바)
- 파일: 신규 `Assets/Scripts/UI/Toolkit/FarmPlateOverlayUTK.cs` (+ `UIToolkitBootstrap` 배선)
- 내용: 밭 위 월드스페이스 가로바 2행 — 위=성장도(초록 그라디언트), 아래=수분도(푸른색 #58A6FF 계열). 디자인 언어는 `NameplateOverlayUTK` 몬스터 체력바 계승(다크 트로프 #451212 계열 트랙+세로 그라디언트 채움+베벨+78px 바폭/PlateW 통일 계약). 표시 조건: 플레이어 근접(~8m)+Planted 이후. 카메라 뒤/화면 밖 스킵 계약 동일. `pickingMode=Ignore`. **HUD 원형 게이지 무변경**
- 기존 FarmPlot OnGUI 프롬프트는 E 프롬프트 UTK(`EInteractionPromptUTK`) 경로로 이관 검토

### Phase 4 — 물뿌리개 + 호수 물담기
- 파일: `Assets/Scripts/Core/PlayerInventory.cs`, `Assets/Scripts/UI/Toolkit/ShopWindowUTK.cs`, `Assets/Scripts/Systems/FishingSystem.cs`
- 내용: ①`tool_watering_can` 아이템(Tool 계열, 내구도 없음) + 물 저장량 20회 필드(세이버 연동 검토) ②상점 등록 ③물주기 채널 완료 시 저장량 1 소비, 0이면 "물뿌리개에 물이 없습니다 — 호수에서 충전하세요" 피드백 ④`FishingSystem` 물가 근접 판정(`IsNearWater`)에서 물뿌리개 소지 시 물담기 상호작용 추가(채널 2s → 20/20 충전). UI 배치는 사용자 차후 수정 — 최소 동작 연결만
- 검증: 컴파일 + EditMode(저장량 소비/충전 단위 테스트)

### Phase 5 — 병사 완전 자동 농경
- 파일: `Assets/Scripts/Systems/GuardTaskSystem.cs`
- 내용: 농경 역할 병사 워커 — 플롯 탐색 → Grass/Dirt면 굉이질 → Tilled면 파종 → Planted/Growing이면 수분 부족 시 물주기(병사는 저장량 무제한) → Ready면 수확 → 반복. 기존 일일 원정 사이클 워커 패턴 계승. 병사 배정 밭은 부패/시듦 리스크 없음
- 검증: 컴파일 + EditMode(워커 상태전이) + Play(승인 게이트 별도)

### Phase 6 — 통합 QA
- EditMode 전체 회귀(기존 11건 1:1 포함) + 컴파일. Play 영상 검증(프레임+vision)은 **별도 승인 게이트** 이후.

## 4. 셀프 감사 기록 (충돌/중복)

- E키 충돌 없음 — 기존 FarmPlot도 E 단일 컨텍스트, 상태별 분기만 추가
- HP바 중복 없음 — HUD 원형 무변경, 농장 플레이트는 별도 월드 오버레이
- `GatherChannelSystem` 단일 인스턴스 — 밭 E채널이 낚시/채집/채굴 채널과 동시 진행 불가(기존 시스템 동일 계약, 통일성 유지)
- AutoMissionManager 자동수확은 Phase 5 병사 완전자동으로 대체 — 배정 밭은 병사가, 미배정 밭은 플레이어가(기존 자동수확 경로와 충돌 여부 Phase 1에서 실측)
- 물뿌리개 저장량 20회 — 사용자 확정(2026-10-26)
