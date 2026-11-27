     1|# 135차 수정계획 — 롤 속도 등화 + NPC 대화 정면/피부 + C2-2 철회 (계획 전용 — 승인 대기)
     2|
     3|작성: 2026-10-30 (135차). 근거 영상: Screenshots/구르기 3.mp4 (37.7s, 02:16 녹화).
     4|상태: **계획만 — 구현/컴파일/테스트 미실행. 사용자 승인 후 Phase별 진행.**
     5|
     6|## §1 판정 근거 (실측)
     7|
     8|1. **옆구르기 성공 확인**: 영상+사용자 확인 — E6(측면 롤 진행 방향 주시 스냅)+E5(스핀+딥웅크림) 조합으로 측면 구르기 정상 판독. 잔존 이슈 = 속도 체감만.
     9|2. **롤 속도 차이의 기구적 원인**:
    10|   - 이동 속도는 전 방향 동일(PlayerMovement L1083 `_rollDirection × walkSpeed × _rollSpeedMultiplier(3)`) — **이동은 차이 없음**.
    11|   - 차이는 **애니메이션 재생 속도**: 측면 롤 = 스핀 0.5s(롤 지속시간에 맞춰 동작), 앞/뒤 롤 = Roll/RollBack **클립 네이티브 재생(m_Speed: 1)**. Roll_Dodge = lastFrame 55(≈30fps 기준 1.83s) — 롤 이동(0.5s)은 끝났는데 클립이 1.8s짜리라 몸동작이 느리게 이어짐 → "옆구르기보다 느리다"로 지각.
    12|   - 롤클립 자산: Roll=Assets/Animations/MeshyUser/Roll_Dodge.fbx, RollBack=Roll_Back.fbx (Player_AC 상태 m_Speed: 1 확인).
    13|3. **NPC 대화 뒷모습 — C2-2(Hips 플립)는 무효+유해**:
    14|   - Editor.log: `[134차 C2-2] 비주얼 180° 교정 — Villager_N visual=Hips (rootBone-fallback)` — **플립 대상이 뼈대(Hips)**. 의도는 "비주얼 루트"였으나 폴백이 뼈대를 잡음.
    15|   - Hips는 애니메이터가 매 프레임 재구동 → 플립 즉시 덮어씀 = **정면 교정 효과 없음**.
    16|   - 대화 카메라는 플레이어 측에 배치(center − bearing×dist, [96차 F1] 측면 오프셋 있음) — NPC가 플레이어(=카메라 방향)를 바라보면 정면이 보여야 정상. 뒷모습이 계속 보인다는 것 = **골격 forward와 메시 시각 forward가 180° 어긋남(A3-era 가설) 또는 서보/플립 충돌** — 재실측 필요.
    17|4. **NPC 피부 "깨짐" = 대화 클로즈업 국소 현상**:
    18|   - 영상 1차 모자이크(마을 전경 포함): 피부 파손 **없음**. 파손은 대화 클로즈업 구간(영상 12~14초대)에만 관측 — NPC가 화면을 덮치듯 비정상 확대 + 15~17초대 팔/하체 각도 이상.
    19|   - A4 프로브(모든 슬롯): uvBounds 정상범위, badWeights=0, 셰이더/바인딩 완벽 — **에셋 레벨 결함 아님** 확정 유지.
    20|   - 유력 원인: 대화 카메라 근접 프레이밍(dist 2.92m+상반신 모드)에서 **근평면이 메시에 침입(클리핑)** + 주민 루트 지하 침하(y≈-1.7, [96차 F1] 기록)와 프레이밍 기준 불일치. Hips 플립 기여 가능성도 배제 불가(철회로 소거).
    21|
    22|## §2 Phase 계획
    23|
    24|### Phase 1 — C2-2 철회 (Hips 플립 제거) [최우선]
    25|- 파일: Assets/Scripts/Systems/VillageNpcSpawner.cs
    26|- 내용: L330 호출 제거 + FixNpcVisualOrientation/NpcVisualFlipMarker 정리(메서드·마커 클래스 삭제 또는 noop 화 — 계약테스트 참조 유무 확인 후 결정).
    27|- 근거: 애니메이터가 Hips를 매 프레임 재구동해 효과 없음 + 서보([104차 F2] Hips yaw)와 충돌 소지 + 피부 파손 후보 소거.
    28|- 검증: 컴파일 0, 마을 NPC 스폰 정상(스폰 로그), 피부 파손 소거 확인.
    29|
    30|### Phase 2 — NPC 정면/피부 진단 프로브 (배치모드 1회, 수리 없음)
    31|- 신규: Assets/Editor/Tools/NpcFacingProbe.cs (134차 슬롯프로브 선례)
    32|- 덤프 항목(스폰 NPC 전원):
    33|  1. 계층 구조: 루트 → 모델 루트(SMR 포함 계층 최상위) → Hips 경로와 각 world rotation
    34|  2. 골격 forward vs 모델 루트 forward vs SMR bounds 중심 방향 (3점 비교 — 메시 시각 forward 추정)
    35|  3. 대화 세션 중: 서보 적용 후 Hips world yaw vs 카메라 베어링 dot(전/후 비교)
    36|  4. 대화 카메라 최종 위치 dist + near clip + NPC bounds 크기 → 근평면 침입 여부 산술 판정
    37|- 산출: TestOutput/npc_facing_probe.txt — Phase 3 수리 방향을 이 수치로 확정(확정 전 수리 금지 원칙).
    38|
    39|### Phase 3 — NPC 정면 수리 (Phase 2 판정 분기)
    40|- **분기 A — 메시 forward 반대 확정 시**: 모델 루트(SMR 포함 계층 루트, 뼈대 제외 아님 — 계층째)에 180° 플립 + 세이보 yaw 계산 보정. Hips는 건드리지 않음(플립 대상 교정이 본 의도). 서보가 world 기준이면 자체 수렴, local 기준이면 오프셋 상수 보정.
    41|- **분기 B — 골격은 정면, 카메라 프레이밍 문제 확정 시**: DialogueCameraDirector fullBody 프레이밍 수리 — ①측면 오프셋 증폭(어깨 너머 구도 — NPC 얼굴이 플레이어 실루엣에 가려지지 않게) ②근평면 안전 dist 하한(예: 2.92m→3.2m+) 또는 near clip 조정 ③center 높이에 루트 침하(y≈-1.7) 보정 반영.
    42|- 공통: 병사/영주(fullBody=false) 경로 0변경, [92~104차] 카메라 계약 문서화 유지.
    43|- 검증: 프로브 재실행 수치 + 사용자 재Play(대화 정면/피부).
    44|
    45|### Phase 4 — 롤 속도 등화 (측면 기준)
    46|- 원칙: **측면 롤(0.5s 스핀)을 기준으로 앞/뒤 클립 재생을 맞춤** — 이동 로직 무수정(이동은 이미 동일 3×).
    47|- 파일: Assets/Resources/Animation/Controllers/Player_AC.controller (배치모드 스크립트 — 129차 PlayerActionsAnimSetup 선례)
    48|- 내용: Roll/RollBack 상태 m_Speed = clipLength / RollDuration(0.5s). 사전 실측: 배치모드에서 clip.length 직독(Roll_Dodge 메타 lastFrame 55 → ≈1.83s 예상 → speed ≈3.67; Roll_Back는 구조 달라 meta 미확인 — 실측 후 산정). 반올림 규칙: speed는 소수 2자리.
    49|- 전이 정합: Roll→Idle ExitTime 0.92는 정규화 시간이라 speed 무관 자동 단축 ✓ (구현 시 실측 확인).
    50|- 계약: PlayerRollRunRebindTests 등 m_Speed/클립 관련 소스계약 영향 사전 grep — 있으면 계약 갱신(94차 규칙).
    51|- 검증: 배치모드 세팅 로그(상태별 m_Speed 실측치), 컴파일 0, 재Play 체감(앞/뒤 롤이 측면과 동일한 0.5s 리듬).
    52|
    53|### Phase 5 — 통합 검증
    54|- 컴파일 error CS 0 + EditMode 1721/1710/11 = baseline 1:1(신규 0).
    55|- QAPROGRESS 135차 기록(상단 프리펜드).
    56|- 재Play 승인 후 사용자 판정: ①앞/뒤 롤 속도 = 옆구르기 ②NPC 대화 정면 ③대화 클로즈업 피부 정상.
    57|
    58|## §3 승인 게이트
    59|- 구현 착수 승인(전체 또는 Phase별) → 컴파일/테스트 → **Play는 사용자 전용** → 커밋은 별도 승인(128~134 누적 적체 포함).
    60|