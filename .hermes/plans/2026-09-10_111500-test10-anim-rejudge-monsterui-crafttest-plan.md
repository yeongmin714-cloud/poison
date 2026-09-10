# Test_10 플레이어 이슈 재판정 + 몬스터 HP/레벨 표시 + 크래프트 테스트씬 전체 UI 계획

- 날짜: 2026-09-10
- 근거: Screenshots/테스트 영상 2.mp4 프레임 몽타주 분석 + Editor.log(11:00) 교차 검증

## 진단 (로그·영상 교차 판정)

1. **애니메이션 — 로그상 부트는 성공, 영상 1차 세션은 avatar=NULL**
   - 로그: "Player_AC 부착+레거시 제거 완료(5개)", "HumanoidClipDriver anim=OK avatar=NULL controller=Player_AC" → 그 후 "avatar=Player_Rigged_HeatAvatar:isValid=True" 로그 = **GLB 어바탓이 늦게 도착해 1차 세션에서는 애니가 죽은 상태(T포즈로 촬영됨), 이후 세션에서는 정상 부착**
   - 영상 몽타주: 1~7초 T포즈 고정 + 8초 대형 이펙트 후 모델 소실(캐릭터가 화면 밖 이동했을 가능성) + HumanoidClipDriver State 전환 로그(speed=0.68, clip=Weapon_Combo_2_withSkin 재생 흔적) → 재생은 되지만 **첫 세션에서 어바탓 지연 부착 실패**
   - 남은 위험: "avatar=NULL" 상태로 첫 프레임부터 Player_AC 재생 → 몽타주로는 재생 흔적이 안 보임. **아바타 확정 대기 루프 필요**(HumanoidClipDriver가 avatar=null이면 Player_AC 재생 보류 후 대기)
2. **GLB 부착** — PlayerModel은 부착됨(침하감시 bounds.center.y=1.309 실증). 다만 1차 세션에서 T포즈로 보임 = 어바탓 미할당 상태에서 Animator만 켜진 것. → 부트에 "avatar 검증+할당" 단계 추가
3. **피격** — CombatLog 39건(데미지 처리됨), 영상 8초 큰 슬래시 이펙트 + 14~18초 반복 타격 파티클 확인. 그러나 **SlashVFXRunner 로그 0건 = 훅 미발화 의심**(0.08s 쿨다운/try-catch 사일런트 가능성) → 발화 증거 로그 1회 추가
4. **몬스터 HP/레벨** — 기존 시스템 존재: AnimalAI.CurrentHP/MaxHP(89-91행), MonsterLevelManager.GetLevelDisplay/GetLevelColorTag(167/184행) — **화면 표시(UI)만 부재**
5. **크래프트 테스트씬(InteriorSystemsTest)** — UIManager에 창 5종(창고/크래프트/요리/연금/인벤)만 구성. HotbarUI/StatusWindowUI는 RuntimeInitializeOnLoadMethod(AfterSceneLoad) 셀프부트라 이미 존재할 것, MinimapUI/CombatLogUI/EquipmentWindow/KeyBindings(Q/I/M/P/E) 미연동. 게다가 빈 씬이라 지형 높이 계약(1+GetHeightAt) 미준수 바닥 → Test_10과 동일 침하 위험 + 몬스터 없음

## Phase 1 — Test_10 플레이어 3건 재판정·보강

파일: TestPlayerAnimatorBoot.cs (Test_10 전용)
1. **아바타 확정 대기**: 부트가 Player_AC 부착 후에도 HumanoidClipDriver.Start 시점 avatar==null이면(로그 실제 발생) → 코루틴으로 5초간(0.25s 간격) avatar!=null 대기 → 들어오면 animator.Rebind()+Play 재시작. 5초 초과 시 경고 로그 1회(원인 잔존 증거 확보)
2. **애니 검증 로그**: 부트 3초 후 animator state 재생 중 여부+현재 클립명 로그 1회 — "재생되는데 영상에서 안 보였다"와 "재생 안 됨"을 로그로 분리
3. **피격 증거**: PlayerCombat.TryAttack 스윙 훅 성공 시 1회성 로그("[SlashVFX] 스윙 FX 스폰") — SlashVFXRunner static bool warned 실패 로그와 구분. CombatFXGate.PlayImpact 성공 로그도 1회성(예산 초과 스팸 방지)
- 공유 파일 무변경(SlashVFXRunner/CombatFXGate에 "디버그 로그 1회"만 추가 허용)

## Phase 2 — 몬스터 HP/레벨 표시 (공유 신규, Test_10+메인 모두 이득)

신규 Assets/Scripts/Systems/MonsterHeadUI.cs (Billboard IMGUI — 프로젝트가 IMGUI 통일이므로):
1. AnimalAI에 GetInstanceID 연동 부착은 금지(공유 파일 최소화) → AnimalAI의 CurrentHP/MaxHP/MonsterId public 프로퍼티만 소비. 부착은 Test_10 전용 부트에서 Monster에 AddComponent + 메인씬은 기존 몬스터 스폰 경로(TerritoryBuilder/MonsterSpawner)와 별도로, 부착 범위는 이번 단계에서 Test_10 먼저(메인 확산은 Play 판정 후 — 사용자 승인 필요 항목으로 명시)
2. 표시: 몬스터 머리 위(world→GUI 좌표 변환: Camera.main.WorldToScreenPoint, 뒤면( z<0) 스킵) — 이름+Lv(MonsterLevelManager.GetLevelDisplay, 색 태그 사용) + HP바(배경 다크/전경 희귀도식 색: HP비율 녹→노랑→빨강) + 티어 아이콘
3. MonsterLevelManager.Instance 자동생성 선례(19-30행) 그대로 사용 — Test_10에도 EnsureGameManager에 MonsterLevelManager 이미 포함(직접 확인)
4. 거리 컬링: 카메라에서 40m 초과 숨김, 화면 밖 렌더 스킵
5. 레이블 폰트: 13px 기본 함정 피해서 fontSize 명시 스타일 사용

## Phase 3 — 크래프트 테스트씬 전 UI 구성

파일: InteriorSystemsTestSetup.cs + 필요시 신규 UIBootHelper
1. **셀프부트 UI 전수 확인**(런타임 자동): HotbarUI/StatusWindowUI/CombatLogUI는 RuntimeInitializeOnLoadMethod(AfterSceneLoad) 셀프부트 — 씬에 자동 등장하는지 코드 확인 후 그대로
2. **명시 부트 추가**: UIManager에 EquipmentWindow 연동 확인(장비창 E), MinimapUI 인스턴스 생성(TerritoryDatabase/TerrainSplatBaker 의존 — 빈 씬 가드 확인: LastWorldSplat null이면 경고 1회 후 대기, NRE 금지), 인벤토리 I/스탯 P/지도 M/퀘스트 Q 키는 KeyBindings 공유 경로라 그대로 작동
3. **테스트 아이템 시딩**: 빈 인벤토리면 UI 확인이 불가 → 부트 시 PlayerInventory에 테스트 아이템 자동 지급(약초/고기/약/재료/무기/방어구 각 3~5개, 희귀도 분포 포함 — 전설 아이템 1개는 글로우 검증용) + 핫바 1~3슬롯 자동 할당
4. **플레이어 구성 정합**: SetupPlayer가 PlayerMovement 기반으로 지형 계약 충족하는지 확인(InteriorSystemsTest는 Plane 바닥 60x60 → Test_10 미니지형 패턴 재사용 필요 — GetHeightAt 계약 준수 버전으로 교체, Test_10의 SurfaceY 로직 복제)
5. **몬스터 1기 추가**(HP/레벨 표시 검증용) + 닫기 버튼/창 이동 등 UI 상호작용 확인 가능 배치
6. 각 UI 열기 가이드 OnGUI 텍스트 갱신(I/P/M/E/X/K 키 안내)

## Phase 4 — 검증/기록

- 배치컴파일 CS=0(에디터 락 시 강제종료 절차)
- QA: diff 리뷰+공유 파일 범위 검증(PlayerCombat/SlashVFXRunner/CombatFXGate는 로그 1회성만)
- Play 판정: Test_10 영상 3 녹화 → 프레임 분석(애니 자세 변화/피격 이펙트/HP바) + 크래프트 테스트씬 스크린샷(UI 전수)
- 3곳 저장(QAPROGRESS/메모리/커밋푸시) + 텔레그램 알림

## 리스크/비고

- avatar 지연 부착: HumanoidClipDriver가 Start에서 Animator 캐시 → Rebind 타이밍이 어긋나면 클립 재생 실패 가능 → Phase 1의 3초 후 클립명 로그로 판별
- 몬스터 HP바: IMGUI world→screen 변환은 프레임마다 카메라 조회 — Camera.main 캐시
- 크래프트 테스트씬 지형 교체로 기존 실내 테스트 플로우 영향 0(바닥 높이만 계약 정합)
- Test_10 플레이어가 "8초에 모델 소실"은 영상 프레임상 화면 밖 이동으로 추정 — Phase 1 침하 감시 로그(편차 0.435m=정상)가 이미 정상 입증
