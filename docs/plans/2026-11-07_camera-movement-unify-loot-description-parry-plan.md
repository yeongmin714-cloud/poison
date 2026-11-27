# 계획 — 카메라·조작 전 씬 통일(Test_10 기준) + 각도 실질 적용 + 전리품 좌클릭 설명창 + Test_10 패링 발화 수리

> 발행: 2026-11-07. 사용자 지시 4건: ①"여전히 각도 변경이 없어" ②Test_10과 동일한 카메라·움직임(점프/구르기/커서 시선)을 **모든 씬**에 ③전리품 좌클릭 → 가운데 아이템 설명창 미표시 수리 ④Test_10 패링 액션 고품질화가 "전혀 실행된 게 없다" — 진단·수리.

## 진단 (소스 실측 근거)

### D-1 각도 무변화의 근본 원인 — 카메라 기록자가 2개였다
- **실게임 카메라 = PlayerMovement.ApplyFollowCamera** (PlayerMovement.cs L1530, LateUpdate "최종 적용" 계약). 피치원 = `_camPitch = 65f` (L1454). Test_10을 포함한 PlayerMovement 씬 전반이 이 값을 쓴다 — **v2에서 57°로 바꾼 대상은 IndoorCameraFollow(투어/실내 전용)뿐** → 실게임 각도 무변화가 정상이었다(수리 대상 착오).
- IndoorCameraFollow = 직교(ortho) 카메라 + 고정 피치. 65→57은 직교 투영에서 타일 세로 스쿼시 0.906→0.839(약 7%)라 육안 체감 미미.
- **이중 기록자 잠복 결함**: CaveInteriorTestSetup(L278)·ArenaTestSetup(L249)·AfterlifeCemeteryTestSetup(L303)·LordPeaceVisitTestSetup(L383)은 PlayerMovement도 붙고 IndoorCameraFollow도 붙음(억제 게이트 없음) → LateUpdate 실행 순서에 따라 카메라 기록자가 갈려 씬마다 보이는 각도/투영이 제각각이었을 것.
- 실내 진입(IndoorSceneTransition L541)은 `CameraFollowSuppressed=true`로 PM 카메라를 누르고 IndoorCameraFollow(직교)가 소유 — 실내는 의도적 직교였으나 "Test_10과 동일" 요구와 상충.

### D-2 조작 불일치의 원인 — 단순 이동 컨트롤러 3 씬
- PlayerCastleInteriorTestMovement(WASD만 — 점프/구르기/더블탭 달리기/커서 조준 전무, L40~70)를 쓰는 씬: **PlayerCastleInteriorPlaytest(L139/L179), LordInteractionTestSetup(L369), IndoorWarTestSetup(L452)**.
- Test_10 = PlayerMovement: WASD + 더블탭 달리기 + Shift+방향 구르기(4방향) + Space 점프 + **커서 지면 조준 strafe(Diablo/Hades, L859)** + 휠 줌. E/F 상호작용·넉백 구독도 동일 계약 보유(69차가 PM 선례 복제).

### D-3 전리품 좌클릭 설명 미표시 원인
- LootWindowUTK 슬롯 바인딩(L556): `UTKDragDrop.MakeDraggable(slot, payload, null, () => TakeLootRow(idx))` — 4번째 인자=**우클릭 즉시 획득**만 연결, **좌클릭 핸들러 부재** → ItemDescriptionWindowUTK.ShowItem이 호출될 경로가 없음.
- 인벤 선례 존재: InventoryWindowUTK L1353 `ItemDescriptionWindowUTK.ShowItem(slotData.item, slotData.count)` (ShowItem(PlayerInventory.ItemData, int) 시그니처 확인 L516).
- **의도적 차단 계약이 원인 일부**: UTKWireUp L62~65 — "상세창(672,175.2,576,729.6)과 전리품(708,324,504,432)이 Figma 좌표상 겹쳐 전리품을 가린다 → 전리품 라우트에서는 상세창 개방 금지". 사용자가 이번에 좌클릭 설명 표시를 명시했으므로 **계약 변경**(겹침 수용, 상세창 최상단 표시).

### D-4 Test_10 패망 고품질 "없다"의 판단
- 구현 자체는 존재: 95차 F1~F4(PlayerCombat L93~108 — PERFECT/GOOD 판정·반격·히스톱·FOV 펀치·방향성 스파크) + 159차 P3(히트스톱·지면 링 2겹·스파클 버스트·아우터 링). QAPROGRESS 159차 기록 확인.
- 즉 **"구현 미실행"이 아니라 Test_10에서 발화 체인이 끊겨 있을 공산** — 의심 고리: Test_10 몬스터가 공격을 안 하거나(관성화 L1852/Aggro 미등록 L115 이력), 텔레그래프(95차 F5 AttackTelegraphIndicator)가 Test_10 몬스터에 미부착이거나, 피해 경로가 패링 반영 경로(PlayerHealth.TakeDamage → TryParryReflection → PlayerCombat.TryParry)를 우회. → **진단 선행 후 끊긴 고리만 수리**(기존 FX 구현 원칙 무수정).

---

## Phase A — 카메라 통일 + 각도 실질 적용
- **A1** PlayerMovement.cs `_camPitch` 65→**57f**(L1454, 주석 갱신) — 전 씬 실질 각도. 커서 상하 피치 팬은 기존 비활성(고정 오빗) 유지. IndoorCameraFollow의 스테일 주석("기본 65° 경로 보존") 정합화.
- **A2** 실내/특수 셋업 6종에서 **IndoorCameraFollow 부착 제거**(일반 플레이 카메라 = PM 단일 기록자): CaveInteriorTestSetup L278, AfterlifeCemeteryTestSetup L303, LordPeaceVisitTestSetup L383, LordInteractionTestSetup L437, IndoorWarTestSetup L520, PlayerCastleInteriorPlaytest L216. 각 셋업 카메라 생성부의 직교 설정(orthographic=true/orthoSize)도 원근(PM 오빗)으로 전환.
- **A2-제외(사용자 지시 2026-11-07)**: **아레나(ArenaTestSetup)는 완전 제외** — 외부 타원 결투장+로비+피트 통합 씬의 기존 직교 카메라+IndoorCameraFollow+듀엔 관전 계약을 그대로 유지(카메라 GO가 외부/실내 공유라 부분 전환 불가). 아레나는 PlayerMovement 이미 부착(L230)이라 점프/구르기/커서 조준은 기존 발동 — Phase B 변경 불요.
- **A3** 아레나 관전 보존: A2-제외로 불요 — 기존 세션 계약 무손상(ArenaSpectatorCameraLock/IndoorCameraFollow 기존 경로).
- **A4** IndoorSceneTransition L541 억제 제거 + 실내 진입 후 PM 원근 카메라 추적(지형 클램프 스킵 L1599~1611 기존 유지). 전환부의 직교 블렌드/IndoorCameraFollow 개입 경로도 원근 단일 경로로 정리. 휠 줌 = PM 거리 줌(CamDistanceMin/Max 실내 적정성 실측).
- **A5** Test_23 관람 모드는 예외 유지(조작 없는 관람 전용 — IndoorCameraFollow + ResolvePitch 57 기존).

## Phase B — 이동·조작 통일 (Test_10 전 기능)
- **B1** PlayerCastleInteriorTestMovement 사용 3 셋업을 **PlayerMovement로 교체**: PlayerCastleInteriorPlaytest L139/L179, LordInteractionTestSetup L369, IndoorWarTestSetup L452. CharacterController 구성(ConfigureController 계수)은 이식 유지.
- **B2** 승계 확인: E(침대 잠자기/세이브)·F(영주>병사>NPC 패널) = PM.HandleInteraction 동일 계약 / 넉백 = PlayerHealth.KnockbackRequested 구독(양쪽 동일 계약 이미 확인).
- **B3** 실내 접지·점프: 실내 바닥 MeshCollider 접지로 Space 점프 발화 확인, 구르기는 159차 실내 접지 폴백 승계. 벽 콜라이더 부재 씬은 실측 후 기존 room-clamp 이식 여부 판정.
- **B4** PlayerCastleInteriorTestMovement **클래스 삭제** + 테스트 재핀: InteriorInteractionTests(TestMovementPath 소스계약→PM 경로), PlayerKnockbackTests L73(→PM).

## Phase C — 전리품 좌클릭 → 중앙 설명창
- **C1** LootWindowUTK L556 슬롯에 **좌클릭 콜백 추가**(UTKDragDrop.MakeDraggable 시그니처 확인 — 좌클릭 파라미터가 있으면 사용, 없으면 slot 클릭 콜백 병기) → `ItemDescriptionWindowUTK.ShowItem(entry.Item, entry.Count)` (인벤 L1353 선례 동일 계약).
- **C2** 선택 피드백(슬롯 하이라이트) + 전리품 창 닫힘 시 `ItemDescriptionWindowUTK.Clear()` 동기(인벤 선례 L1348).
- **C3** UTKWireUp L62~65 구계약 갱신 — "전리품 라우트 상세창 개방 금지" → "좌클릭 시 개방 허용(겹침 수용, 상세창 전면 표시)". 관련 소스계약 테스트 갱신.

## Phase D — Test_10 패링 고품질 발화 진단 + 수리
- **D1** 정적 체인 추적 보고서(구현 전 산출): TestTerritoryCombatSetup 몬스터 구성(공격 활성 여부·관성화 영향) → AnimalAI/AfterlifeEnemy 텔레그래프 부착 → PlayerHealth.TakeDamage → TryParryReflection → PlayerCombat.TryParry → FX. 끊긴 고리 명시.
- **D2** 수리(진단 결과 한정): 공격 활성화/텔레그래프 부착/Aggro 등록 등 **연결 계열만** — 판정·FX 본체(95차 F1~F4+159차 P3) 원칙 무수정.
- **D3** 검증: 패링 성공 로그 마커([PlayerCombat] PERFECT/GOOD) + 기존 패링 테스트 3종(PlayerCombatParryInputTests/PlayerParryDamageTests/ParrySfxAudioTests) GREEN 유지.

## Phase E — 검증
- 컴파일 error CS 0 / EditMode 전수(baseline 10건 1:1, 신규 실패 0).
- 신규 계약 테스트: ①실내 셋업 7종 소스계약(IndoorCameraFollow 부착 부재 + PlayerMovement 부착) ②`_camPitch 57f` 핀 ③LootWindowUTK 좌클릭→ShowItem 소스계약 ④D 결과에 따른 Test_10 패링 경로 계약.
- **Play(사용자 승인 게이트)**: ①Test_10 포함 전 씬 각도 57° 체감 ②성 실내·동굴·아레나에서 점프/구르기(Shift+방향)/더블탭 달리기/커서 조준/휠 줌 ③전리품 좌클릭 → 가운데 설명창 표출 ④Test_10 패링 PERFECT(금색+히트스톱+지면 링)/GOOD 체감.

## 리스크
- **직교→원근 전환 체감 변화**: 실내 디오라마는 단면 컬링 관례(벽·천장 외측에서 컬링)로 위에서 비추면 가림 없음이 원칙 — 씬별 Play 프로브로 검증, 문제 씬은 절단 확장 대응.
- 원근 카메라의 실내 오프셋(거리 11m → 수평 ~6m): 작은 방에서 벽 밖 카메라 가능 — 컬링 관례로 투과되나, 이상 씬은 거리 상한 조정으로 대응.
- 관전/대화 등 특수 카메라 세션은 기존 억제 게이트 계약 유지(회귀 금지).

## 기본안(무응답 시 적용)
1. 각도 57°를 **Test_10 포함 전 씬 통일**("모든 씬 동일" + 각도 수정 실질 적용).
2. 실내 직교 → Test_10식 원근 오빗으로 전환 수용.
3. Test_23 관람 모드는 기존 유지(조작 없음).
4. PlayerCastleInteriorTestMovement는 삭제(단일 경로 계약).
