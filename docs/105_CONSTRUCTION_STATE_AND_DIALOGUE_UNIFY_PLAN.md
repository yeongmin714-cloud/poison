# 105차 계획 — 건설 상태 수리 + 대화 카메라 전 대상 일반화 + U키 Figma 재정합

제정: 2026-10-20 / 근거: 건설배치 ui.PNG 실측 + ConstructionHologram.cs 전수 실측 + 사용자 승인("진행")

## 사용자 요구 3건
1. **건설 상태 버그** — 음식점이 안 지어지거나, 1건 건설 후 다른 건물이 안 지어짐.
2. **대화 카메라 일반화** — NPC 시선은 완벽(104차). 병사/영주 등 **모든 대상**에 동일한 카메라/시선 처리.
3. **U키 UI Figma 정합** — 여전히 어긋남(스크린샷 실측: matSub 우측 클리핑, 목록 패딩, 스크롤바, 표 헤더 대비, 버튼 스타일 불일치).

## 확정 근원 (감독자 실측 — ConstructionHologram.cs 288행 전수)
- **[결함1] L19**: `IsPlacementActive => _mode != Inactive` — Confirmed 모드가 시공 완료까지 유지 → CommandMarker.Spawn(L33 게이트) + RTSCommandSystem.IssueRightClickCommand 게이트가 **이동 명령 전면 차단** (시공 45~150s 동안). 1건 건설 후 다른 건물 배치가 막힌 체감의 직접 원인.
- **[결함2] L234**: OnStructureCompletedForDestroy가 entry 무시·무조건 DestroyHologram → 시공 중 2번째 배치(Following) 홀로그램이 1번째 완료 시 소멸.
- **[결함3] L228**: 실패 토스트 "배치할 수 없는 위치입니다" 고정 — TryConfirmPlacement L257의 실제 result("이미 음식점이 있는 마을"/"마을 반경 밖 (마을 중심 반경+5m)") 폐기 → 음식점 실패 사유 비노출.
- **[결함4] L93-107**: CreatePrimitive 콜라이더 방치 — LateUpdate 레이캐스트 자기 히트 + Confirmed 고스트가 클릭 가로챔.
- **[결함5] ConstructionManager.Demolish L549-568**: 구조물 제거 시 VillageDrugState.restaurantBuilt 미리셋 + RestaurantPropBuilder/FacilityPropBuilder 실물 프롭 미파괴(사유: 해체 후 해당 마을 영구 잠금).
- **[결함6] L197-201**: Esc가 Confirmed 모드(시공 중)에도 CancelPlacement — 시공 중 고스트가 사라져 완료 전 실물 부재 창 발생.

## 위임 구성 (3병렬 → QA)
- **IMPL-A (결함1~6)**: 건설 상태 수리 — ConstructionHologram/ConstructionManager/RestaurantPropBuilder/FacilityPropBuilder + 테스트 갱신.
- **IMPL-B (요구2)**: DialogueCameraDirector fullBody 경로 전 대상 일반화 (병사/영주 → 주민과 동일 프레이밍+시선+플레이어 숨김). 패널 종료 동작([97차 F2])은 범위 외.
- **IMPL-C (요구3)**: Figma 239:683 재실측(세션 간 개정 규칙) + U키 정합 교정 + FigmaTests 갱신.
- **QA**: 전부 완료 후 별도 서브에이전트 — 컴파일 0 + EditMode 전수 + baseline 11건 1:1 대조.

## IMPL-A 스펙 (근거 라인 그대로 인도)
1. L19 → `IsPlacementActive => Instance != null && Instance._mode == PlacementMode.Following` (게이트 = 배치 중에만).
2. Confirm 성공 시 `_confirmedBlueprintId`/`_confirmedPos` 기록 → StructureCompleted 핸들러는 entry.blueprintId+pos 일치 시만 파괴(ε 0.5m), 불일치 무시 → 2번째 Following 홀로그램 보호.
3. TryConfirmPlacement가 TryPlace result 문자열을 반환(out/필드) → 실패 토스트 = 실제 사유 (L228 고정 문구 폐기).
4. BuildHologramPrimitives/StartPlacementInternal 완료 후 GetComponentsInChildren<Collider>() 전부 disable (Following/Confirmed 공통).
5. Update L197: Esc는 Following에서만 취소; Confirmed 무시(+로그 [105차 A]).
6. Demolish: RemoveAt 후 bp_restaurant이면 TryFindRestaurantVillage(s.pos) → VillageDrugState.SetRestaurantBuilt(n, idx, false); RestaurantPropBuilder.TryDestroyProp(structureId)/FacilityPropBuilder.TryDestroyProp(structureId) 신규 public static(런타임만, EditMode no-op).
7. 테스트: ConstructionHologramTests 갱신(상태 의도 보존) + Demolish 플래그 리셋 신규. 변경 라인이 어서션 대상인지 테스트 파일 먼저 grep.

## IMPL-B 스펙
- Begin(Component,string,bool fullBody) 내부: **전 대상 fullBody 동작 강제**(병사 Begin(GuardPlaceholder) L223-226 → fullBody:true 전환 포함, 전 call sites grep — Assets/ 본문만, Tests 제외).
- 시선 yaw: 주민은 기존 DialogueYawControlled 게이트 유지; 비주민 대상은 Begin에서 yaw 기록 → LateUpdate 매 틱 적용 → End 원복(기존 _gazeSavedYaw 기구 재사용).
- [97차 F2] 패널 수명/ESC 분기, 병사/영주 대화 UI 자체는 0변경 — 범위 = 카메라 프레이밍+시선+플레이어 숨김.
- 테스트: DialogueFeedbackTests/DialogueGaze104Tests 소스 어서션("fullBody: false" 등) 실측 후 갱신(의도 보존).

## IMPL-C 스펙 (스크린샷 실측 근거)
- Figma 재실측 필수(토큰 ~/.figma_token, 키 02vPXOGzFJUUYTaEPSVz3R, 프레임 239:683, 1600×1040).
- 교정 목표: ①matSub '공용 창고 기준 · 수량 단위: 개' 우측 클리핑 → 우측 24px 여백 정렬(y local 349.5) ②좌 목록 항목 내부 패딩(재료 수치가 하단 경계에 붙음) ③스크롤바(굵은 흰색 기본 → 디자인 스펙 얇은 다크) ④상태 아이콘(체크/느낌표 과대) ⑤재료 표 헤더 대비/크기 ⑥필터탭 vs 실행버튼 색/코너 불일치 통일 ⑦패널 사이 간격 배경 노출 ⑧닫기(X) 헤더 수직 정렬 ⑨FACILITY 플레이스홀더 박스(검정 사각) Figma 스펙 적용.
- 보존 계약: WinW/LeftX/RightX/HeaderY/HelpY/FilterTabY=88f/FacilityListY=168f/SummaryBoxY=748f/SummaryCellY=40f/ExecuteY=718f/색 hex/ApplyCanvasFit/OnPlaceBlueprint/UTKToastService — 변경 시 ConstructionManagementFigmaTests 의도 보존 갱신.

## 검증
- 컴파일 error CS 0 / EditMode 전수(104차 기준 1368 total / 1357 pass / 11 fail baseline 1:1) / 신규 테스트 GREEN / diff-105.md 증거.
- Play 확인(승인 게이트): ①건설 1확정 후 즉시 이동 가능+2번째 배치 가능 ②시공 중 고스트 잔존→완료 시 실물+홀로그램 소멸 ③음식점 실패 시 실제 사유 토스트 ④병사/영주 대화 = 주민과 동일 정면 전신+시선 ⑤U키 스크린샷 대조.
