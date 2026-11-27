# 51차 계획 — 키 1.8m 통일 / 실내 휠 줌 / Test_14 침공 재구성 / 이동 링 1개+분산

상태: 계획 전용 게이트(소스/테스트 패치 금지). 승인("진행") 후 파트1~4 연속 구현(중간 Play 게이트 없이, 내부 컴파일/EditMode 게이트만 유지 — 일괄 검증 선호 계약).

## 배경 실측 (소스 확정)
- 50차: 병사 키 = 1.0m 고정(TerritoryBuilder.NormalizeSoldierHeightFactor — 씬 플레이어 실측 폐기). **사용자 정정: 목표는 1.0m가 아니라 원래 크기 1.8m.**
- 호출부 5곳(전부 1.0f 리터럴): TerritoryBuilder.cs:1080(CreateGuard FBX) / TestTerritoryCombatSetup.cs:1532·1585(테스트10·14 FBX+GLB 폴백) / SoldierManagementUTK.cs:894(미리보기) / PlayerCastleInteriorPlaytest.cs:165(실내 테스트 플레이어=Heat.fbx).
- 메인 플레이어 비주얼 = 1.0m 치비(ArmorVisualAttachSystem 69차 후속5 — Player_Rigged.fbx 0.89×1.0×0.4). 갑옷 슬롯 목표 크기는 1.0m 플레이어 기준 절대값 캘리브레이션.
- 실내 카메라 = IndoorCameraFollow(피치 65°/거리 11m 고정, 휠 없음). 메인 휠 줌 = CameraZoomControllerRuntime(Cinemachine ThirdPersonFollow, min15/max40/speed5/smoothing10).
- 이동 링 3개 근원 = RTSCommandSystem.TrackMoveMark가 병사별 지속 링 스폰(IssueMoveCommandCore 252행).
- 동일 지점 겹침 근원 2곳: ①IssueSynchronizedMoveCommand(Ctrl+우클릭) = FormationSpread 미적용 전원 동일 좌표 ②실내 = RelayoutAssembly가 "같은 역할"끼리만 슬롯 계산 → 역할이 다르면 전원이 로비 중앙 동일 좌표(슬롯 rank 0).

## 파트1 — 키 1.8m 전 씬 통일
1. 위 5곳 호출부 1.0f → 1.8f (고정계수 방식 유지 — 씬 플레이어 실측 재도입 금지).
2. 플레이어 비주얼도 1.8m: PlayerCastleInteriorPlaytest(Heat.fbx) + 메인 플레이어 비주얼 부착 지점(ArmorVisualAttachSystem) — 동일 bounds 실측 정규화(멱등 — 재측정이므로 이중 적용 안전).
3. ⚠감사: 갑옷 슬롯 목표 크기(투구 0.267m 등)가 1.0m 플레이어 기준 → 플레이어 ×1.8 시 갑옷 목표치 동행 배율 필요 여부 실측 후 적용(부착 순서가 스케일 전/후인지 확인).
4. 유지: lv20+ GetSoldierSizeMultiplier(덩치 배율) / 접지(GroundModelToY) / 로그("h→1.80m"로 자동 표기).
5. 테스트 동행: 키 관련 단정 grep(Tests 내 "1.0"/정규화 계약) → 발견분만 갱신.

## 파트2 — 실내 휠 줌(메인과 동일 체감)
- IndoorCameraFollow에 휠 줌 추가: _targetDistance(초기 11m) ← mouseScrollDelta.y × speed5, 클램프(초안 6~20m — 실내 규모, 튜닝 가능), Lerp smoothing 10(메인과 동일 수치), 피치 65° 불변.
- 셀프감사 완료: 실내 씬 휠 소비처 충돌 없음(휠 소비처 = MinimapUI[IMGUI, 월드]/ShowcaseCameraZoom[쇼케이스 전용] — 실내 무관).

## 파트3 — Test_14 재구성(침공 관찰 씬)
1. 서부 침공군 5명 행진 타겟: southPos → **내 영지(플레이어 성 keep/문 위치, East_01)**. 로그 "서부 영주 공격 명령: 침공군 n명 → 내 영지(플레이어) 행진".
2. 내 문지기 병사 2명 = **제자리 고정**: 스폰 직후 SetHold(15차 H홀드 상태 — 제자리 고정+근접 적 자동 반격 TryHoldEngage). 자동 이동 경로 감사(작업 배정/루틴/분리 push로 움직이지 않는지).
3. 남부(방어자) 병사 4명 = 기본 **제거**(씬 소음 제거 — 50차 몬스터 제거와 동일 취지). 유지 원하면 사용자 정정.
4. 명령 방식 = 기존과 동일(직접 SetCommandTarget 시연 — AIWarSystem 실전 연동은 본 씬 범위 밖, "AI 영주가 명령을 내렸다" 시뮬레이션).

## 파트4 — 이동 링 1개 + 동일 목적지 분산
1. 링 1개: 병사별 TrackMoveMark 폐기 → **명령 지점 1곳에 지속 링 1개**, 전원 도착/사망/명령 취소 시 소멸(RTSCommandSystem.Update 마커 정리 루프에서 그룹 도착 판정). 이동/일제(Ctrl) 양 경로 동일.
2. 분산: IssueSynchronizedMoveCommand에도 FormationSpread.Distribute 적용(결정론 — 링1 1.8m/링2 3.15m, index0=클릭점). ⚠ 함정: Distribute/OffsetFor/테스트 로컬 상수 3곳 동기(0.9).
3. 실내 겹침: RelayoutAssembly 슬롯 풀을 "역할 무관"으로 — 동일 목적지로 명령받은 병사 전원에게 하나의 결정론 슬롯 풀 배분(이름순), 역할별 분리 계산 폐지.

## 검증
- 컴파일 `error CS` 0 → EditMode 전체(baseline-7 집합 동일, 갱신 계약 동행) → Play 게이트(사용자):
  1. 전 씬 플레이어+병사 = 1.8m 체감("병사 크기 정규화 — h→1.80m" 로그)
  2. 실내 휠 줌 동작(확대/축소+스무딩)
  3. Test_14: 문지기 2명 제자리, 서부 침공군이 내 영지로 실제 행진·도달
  4. 병사 3명 이동 명령 = 링 1개, 도착 후 겹침 없음 / Ctrl+우클릭도 분산 / 실내 배치 겹침 없음
- stage/commit/push는 사용자 지시 시만.
