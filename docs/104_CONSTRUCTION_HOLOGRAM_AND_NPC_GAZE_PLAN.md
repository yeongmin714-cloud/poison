# 104차 계획 — 건설 홀로그램 시스템 + U키 정합 + NPC 시선 + 접지 수리

제정: 2026-10-20 / 근거: 마을씬 테스트 14.mp4(69s) + Editor.log([103차 P1] 접지근원 12건) + 사용자 승인(마우스 지면 클릭 배치 방식)

## 배경 (사용자 요구 4건)
1. **애니메이션 여전히 이상** — 영상 13~15s 포즈 동결/끊김, 20s 지하 침하 실측.
2. **NPC 시선** — 카메라 부착은 거의 성공(103차), 남은 것 = NPC가 카메라를 정면으로 바라보도록 **시선만** 전환.
3. **U키 UI 정합** — 여전히 Figma 정합 미달, 가독성 저하.
4. **홀로그램 건설 신규 시스템** — 건설배치 → 홀로그램 프리뷰(위치 선택) → 위치 지정 → 시공 시작 → 완료 시 실물 전환.

## 근거 실측 (감독자)
- **P1 접지 로그(Editor.log, 12건)**: `[103차 P1] 접지근원 — root=Villager_N 최근접=Ground_Village hitY=2.12 footY=5.6~6.27 heightAt=1.12 shift=-3.5~-4.1` — 발 본이 월드 +2.06 유지인데 루트가 -1.52까지 침강([99차 F-D] y=1.980→-1.523 Δ-3.5) = **루트↔가시 신체 3.7m 괴리**. 스냅 대상 루트와 가시 아마추어가 일관되지 않음.
- **건설 현재 흐름**: ConstructionWindowUTK.OnPlaceBlueprint → GetPlacePosition(카메라 forward×5) → TryPlace → 토스트. 위치 선택 단계(홀로그램 프리뷰) 없음.
- **실물 프리빌더**: FacilityPropBuilder(시설 4종 — 프리미티브 조합, CreatePart 재료명 FacilityStone_Mat 등, groundY=TerrainGenerator.GetHeightAt(x,z,BiomeType.Plains,TerrainSeed)+GROUND_BASE) / RestaurantPropBuilder(음식점). StructureCompleted 구독형.
- **검증 순수 함수**: BlueprintData.ValidatePlacement(region.worldPosition, TerritoryRadius, pos, def.footprintRadius, GetFootprints()) — 홀로그램 유효/무효 틴트 판정에 재사용 가능.
- **클릭 충돌**: CommandMarker(마우스 클릭=이동 명령) — 배치 모드 중 억제 게이트 필요.
- **Figma 진실치**: 건설 관리 창 프레임 239:683, 1600×1040 (ConstructionManagementFigmaTests 고정).

## 위임 구성
- **IMPL-A (F1+F4)**: 홀로그램 건설 시스템 + U키 배치 흐름 전환 + Figma 정합 교정.
- **IMPL-B (F2+F3)**: NPC 대화 시선 전환 + 루트↔가시 괴리 수리(접지/애니).
- **QA**: 양측 완료 후 별도 서브에이전트 (컴파일 + EditMode 전수 + 계약 감사).

## F1 — 홀로그램 건설 시스템 (IMPL-A)
- 신규 `Systems/ConstructionHologram.cs`: 배치 모드(팔로우) → 확정(시공중) → 완료(파괴) 3상태.
- 고스트 지오메트리 = 실물 프리빌더 프리미티브 레시피 재사용(홀로그램 재료로 렌더 — 실물 경로 무변경).
- 마우스 레이캐스트 → 지면 팔로우, 지면 클릭 = 확정, Esc = 취소(소비 0). 유효/무효 틴트(cyan/red).
- 클릭 확정 시 ResolveTerritoryAt → TryPlace(기존 검증 전량 유지) → 성공 시 시공 시작, 고스트 "시공중" 유지 → StructureCompleted 시 기존 프리빌더 실물 생성 + 고스트 파괴.
- CommandMarker 클릭 억제 게이트(배치 모드 중 이동 명령 차단).

## F4 — U키 정합 교정 (IMPL-A, 동일 파일)
- Figma 239:683 재실측 대비 소스 상수/레이아웃 감사 → 간격·정렬·여백 교정(가독성).

## F2 — NPC 시선 (IMPL-B)
- DialogueCameraDirector fullBody 경로: Begin 시 NPC yaw를 카메라(플레이어 방향)로 회전, LateUpdate 유지, End 원복. 병사/영주 경로 0변경([103차] 계약).

## F3 — 접지/애니 수리 (IMPL-B)
- 루트↔가시 신체 3.7m 괴리 수리 — 스냅 대상과 가시 아마추어 정렬(P1 로그 근거). shift ≤ 0.5m 목표.
- 발 미끄러짐/포즈 동결: 근원 진단 후 명확하면 수리, 아니면 로그 확장.

## 검증
- 컴파일 error CS 0 / EditMode 전수(현재 1343/1332/11 baseline) / 신규 테스트 GREEN / 계약 무손상 감사.
- Play 확인(승인 게이트): 홀로그램 프리뷰→확정→시공→실물 전환, NPC 정면 시선, 주민 접지/애니, U키 가독성.
