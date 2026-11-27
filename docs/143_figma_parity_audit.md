# 143차 P0 — Figma ↔ 게임 UI 매핑 감사표 (창 전수 1차)

작성: 2026-10-30. Figma 파일 `02vPXOGzFJUUYTaEPSVz3R` 실측(TestOutput/figma_tree.json — Page 1, 75노드/FRAME 56). ⭐모든 수정 전 프레임 노드 재실측 필수(캐시 금지).

## §1 Figma FRAME → 현재 구현 매핑 (56 FRAME)

| Figma FRAME | 현재 구현(클래스) | 초기 판정 | 비고 |
|---|---|---|---|
| fluent-game-inventory-ui | InventoryWindowUTK + InventoryClusterFigmaLayout | 정합 계약 존재 — 재실측 필요 | 136차 스냅샷 대조 |
| equipment-inventory-panel | EquipmentWindowUTK | 미확인 | 장비창(인벤 임베드 계약) |
| game-shop-ui ×2 | ShopWindowUTK | **미정합 보고** — "나의 가방" 그리드 미표현(헤더만 존재 L458) | 우선 순위 1 |
| crafting-panel ×3 | CraftingWindowUTK / CookingBenchUTK / CraftBenchBaseUTK 계열 | 미확인 | 3变형(제작/요리/제련 추정) — 노드 재실측으로 구분 |
| loot-panel | LootWindowUTK | 미확인 | 5열 그리드 계약 존재 |
| fishing-panel / fishing-result-ui | FishingUTK / HarvestResultUTK(낚시 분기) | 미확인 | |
| gathering-result-ui / mining-result-ui | HarvestResultUTK(채집/채광 분기) | 미확인 | |
| interaction-panel ×8 | InteractionPanelUTK(상태보기/작용 버튼 그리드) | 142차 B' 원복 — **피그마 스타일 유지가 사용자 판정** | 8변형 = 대상별 버튼 조합 |
| interaction-panel-fishing/sleep/lordroom/lockpick | FishingUTK / SleepPanelUTK / LordAudienceUTK / LockpickingUTK | 미확인 | |
| npc-dialogue-panel ×2 | NPCDialoguePanelUTK | 미확인 | bg #151B21/45% (74차 Figma 진실치) — 142차 기준 유지 |
| character-status-panel | StatusWindowUTK / GuardStatusWindowUTK | 미확인 | |
| potion-status-panel | PotionStatusUTK | 미확인 | |
| HUD_Canvas | HUDUTK + HotbarUIUTK + QuestChoiceUTK | 미확인 | 하단 앵커 계약(HudBottomAnchorTests) |
| empty-circular-minimap | MinimapUTK + MinimapRouteOverlay | 미확인 | 월드맵 패널@0.80 계약 별도 |
| monster-health-bar / reference-monster-health-bar | MonsterInfoUTK | 미확인 | |
| selection-ring-system / selection-ring-red | SelectionRingController(월드) | 시각 시스템 — UI 아님 | 참조용 |
| fluent-gauge-bar | UTKGaugeStripArc / WorkProgressBar | 게이지 어휘 — 벡터메시 금지 계약(84차) | |
| quest-window-ui / daily-combat-log | QuestWindowUTK / QuestJournalUTK | 미확인 | |
| medieval-world-map-ui | 월드맵 패널(패널@0.80+현재지핀 계약) | 미확인 | |
| 투기장 승리/패배 결과 | ArenaResultWindowUTK | 미확인 | |
| 투기장 베팅 / 대화·베팅 상호작용 패널 | ArenaWindowUTK / ArenaBattleUTK / ArenaMenuUTK | 미확인 | 어느 프레임이 어느 창인지 재실측 구분 |
| 훈련소 병사 훈련 관리 | GuardInfoUTK / 훈련소 창 | 미확인 | |
| 이벤트 발생 영상 패널 | DynamicEventUTK | 미확인 | |
| 단일 영지 세금 관리 / 영지별 주간 세금 | 세금 창(TerritoryTax 계열) | 미확인 | 클래스명 확인 필요 |
| 전초기지 건설 관리 | ConstructionWindowUTK | 미확인 | |
| 음식점 판매 관리 | RestaurantWindowUTK | 미확인 | |
| 수면 시스템 패널 | SleepPanelUTK | 미확인 | |
| 고용비 알림 패널 | MercenaryPaymentNoticeUTK | 미확인 | |
| 공격조 전투 배치 | AttackFormationUTK | 미확인 | |
| soldier-management-ui | GuardSquadHotbarUTK / GuardStatusWindowUTK | 미확인 | |
| 지역 마커·현재지/요새/공업지구, 지역 탐색 리스트, 선택 지역정보 | 월드맵/패스트트래블(FastTravelUTK) 오버레이 요소 | 미확인 | |
| 3dicons-* 19종 | 아이콘 에셋(3D 아이콘 참조) | — | **아이콘 잘림 이슈(§3)와 연관** |

## §2 신규 확정된 신규 미정합/결함 (142차 영상·코드 실측)

> **[142차 후속3 수리 상태 갱신 10-30]** — 아래 2번(아이콘 핏)·3-②(획득 연출 정면 카메라)·§1 상점 가방 그리드는 **수리 완료**(컴파일 0/베이스라인 유지 실측). 3-①(GLB 매핑 폴백)만 잔존 — AcquisitionItemVisualResolver 치유초 등 소비성 매핑 추가 필요(다음 사이클).

1. **환호 연출(AcquisitionCinema) 2결함** — 사용자 영상(환호.mp4 f_020) 실측:
   - ①획득 GLB 미표출: 오른손 본 부착이 `AcquisitionItemVisualResolver` 매핑 실패 시 **조용히 생략**됨(소스 L11/99/143 계약) — 치유초 등 소비성 아이템이 매핑 누락 추정.
   - ②플레이어 **뒷모습**만 노출: 연출 카메라가 플레이어 정면으로 오비트하지 않음(추적 카메라 뒤 유지 실측).
   - 수리 방향: 카메라 = 플레이어 정면 프레이밍(대화카메라 정면 서보 선례), GLB 매핑 실패 시 텍스트+아이콘 폴백(생략 대신).
2. **아이콘 잘림(GblItemIconRenderer)** — 아이콘 베이크 카메라가 **높이(halfH) 기준 30° FOV 핏**만 수행(L389~394) → 가로가 긴 모델(창·활 등) 수평 절단. 수리: 가로/세로 동시 핏(max(halfH/tanV, halfW/tanH)) + 여유 패딩.
3. 상호작용 패널 8변형·Figma 재실측 시 버튼 구성 대조 필요(현재 소스 하드코딩 조합).

## §3 진행 규칙 (docs/143_FIGMA_UI_PARITY_PLAN.md §2~§4 준용)

- 창당 1사이클: Figma 노드 재실측 → 현재 소스 열수 → 5축(그리드/배열/여백/플루언트/폰트) 차분 → 수정 → 컴파일 0 + 베이스라인(병행 WIP 제외) → 스크린샷 픽셀 비교 → 사용자 확인.
- 우선순위: ①상점 가방 그리드(버그) ②인벤 재실측 ③아이템 설명창 ④환호 연출 2결함 ⑤아이콘 핏 ⑥나머지 창 전수.
