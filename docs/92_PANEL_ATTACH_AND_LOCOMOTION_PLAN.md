# 92차 스펙 — 패널 대상 부착 확정 + U키 건설창(완료 루프 근본 수리) + NPC 걸음 정합 + 대화 전신 카메라

> 발행: 감독자 (2026-10-16). 계획만 수립(사용자 지시 — 구현 별도 승인). 근거: Editor.log 포렌식 + 코드 재판독.
> ⚠ 사용자 보고(91차 이후 재테스트): ①상호작용 패널 여전히 대상 부착 X ②U키 건설창 여전히 미표시 ③NPC 걸음 흐느적거림(질문) ④대화 카메라 시점 이상 — 전신 정면 요구.

## 포렌식 확정 (Editor.log 최신 세션 — 91차 코드 유효: attached=True 포맷)

### 결함 1 — U키 건설창 "안뜸"의 진짜 원인: 완료 루프 폭발 (신규 발견)
- **증거**: "건설 완료: 마을 음식점" **3,450회** / "음식점 완공 플래그 id=struct_cab2375e" **3,449회** / 실물 생성 1회 — **동일 구조물이 매 프레임 재완료**.
- **근본 원인(ConstructionManager.cs 439~446행)**:
  ```csharp
  var s = _structures[i];              // 구조체 '사본'
  s.progress = ...;
  if (s.progress >= 1f) {
      CompleteStructure(s);            // 값 전달 — 내부 s.isComplete=true는 '메서드 지역 사본'만 변경
      _structures[i] = s;              // ← isComplete=false인 사본을 다시 씀
  }
  ```
  → 목록의 isComplete가 영원히 false → **매 프레임 StructureCompleted 재발화**.
- **연쇄 피해**: ①ConstructionWindowUTK.OnStructureCompleted → `IsOpen` 시 Refresh() 매 프레임(UI 전면 재빌드 쓰래시) ②`UTKToastService.Show("✅ 건설 완료…")` **매 프레임 → 토스트 큐 3,449개 누적** → ToastHost에 수천 Label 적층 → **UIRoot 레이아웃 붕괴** → 건설창이 열려도 렌더가 깨져 "안뜸" 체감. (개방 5회 전부 attached=True·예외 0 — 개방/부착은 정상, 렌더 환경 파괴가 원인)
- **부가 결함**: TryPlace 음식점 분기의 마을당 1개 가드가 restaurantBuilt(완성 시점에만 true)만 검사 → **시공 중 60s 창에서 재배치 허용** → 구조물 중복 등록(로그 스팸의 트리거).
- **수리**:
  1. Update 루프 — `s.isComplete = true`를 루프에서 직접 세팅 후 `CompleteStructure(s)`(또는 ref 파라미터) → 목록 write-back 보장. [92차] 마커.
  2. TryPlace 음식점 분기 — restaurantBuilt **OR** 진행 중 bp_restaurant 구조물 매칭(ConstructionManager.Structures 순회 — 88차 스팟 점유 판정과 동일 기준) → "이미 음식점이 있는 마을(시공 중 포함)".
  3. 방어(토스트 큐) — UTKToastService: 동일 문구 0.5s 내 중복 억제 + 큐 상한(예: 8) — 스팸 재발 시에도 UI 생존.

### 결함 2 — 상호작용 패널 대상 부착 실패 (PlacementTick 폴백 튐)
- **증거**: [TargetProject] 로그 패턴 — 개방 시(OpenForTarget) **성공**(예: screenPx=(716,433)), **1초 후 PlacementTick 무효**("전 카메라 폴백 실패") — 성공/무효 교차 반복 → 패널이 대상 ↔ 폴백(하단 중앙) 사이를 오가거나 폴백에 정착 → "부착 안 됨" 체감.
- **원인(무효 판정이 지나치게 엄격)**: ①앵커 = 렌더러 bounds **max.y + 0.55m**(머리 위) — 근접/대화 카메라(FOV 30~45°, 2m 거리)에서 프러스텀 상단 밖 → y 초과 무효 ②화면 ±64px 마진 밖 = 무효 — 패널은 **클램프로 화면 안에 넣을 수 있는데 무효화**해버림 ③z>0(카메라 앞)인데도 화면 밖이면 무효.
- **수리(TargetScreenProjector/InteractionPanelUTK)**:
  1. TryProject 계약 변경: **z>0(전방)만 성공/실패 기준** — 화면 좌표는 항상 반환(화면 밖이어도). 화면 밖 판정 폐기(±64px 마진 검사 제거). [92차] 마커.
  2. 앵커 개선: bounds max.y+0.55 → **bounds.center.y + bounds.extents.y×0.15**(머리 살짝 위 — 프러스텀 안쪽) — 대화 카메라로도 프레임 안 유지.
  3. PlaceAtTarget/PlaceAtAnchor: 받은 screenPx를 **루트 좌표 클램프 후 배치**(패널 전체가 화면 안) — 대상이 화면 가장자리여도 패널이 대상 방향에 붙어 유지.
  4. 폴백(하단 중앙) = **전 카메라 z<0(뒤)**일 때만.
  5. EInteractionPromptUTK(OnGUI)도 동일 클램프 적용 — 화면 밖 대상은 기존대로 스킵(프롬프트는 화면 밖 표시 무의미 — 스킵 유지, 단 z>0 & 화면 근접이면 표시).

### 결함 3 — 대화 카메라: [영상2 재판정] 스왑 방식 자체가 게임뷰 동결 유발 → 스왑 폐기 + 전신 프레이밍
- **[92차 보강·마을씬 테스트 2.mp4(78.4s) 포렌식]** 대화 세션 2회(게임 t≈46.0–48.4 / 52.2–57.2, [DialogueCameraDirector] Begin×2·End×2와 InputProbe 상호 정합) 동안 **게임 뷰가 픽셀 단위 0.000 변화(전면 동결) — 46.0s부터 57.8s까지 11.8초**. End#1(D키 복원, 48.4) 후에도 동결 지속, End#2(5s 타이머, 57.2) 후 ~0.6s에 재개. 동일 구간 게임 로직은 정상 가동(로그 스팸/HUD 진단/입력 프로브 간격 정상) → "렌더 출력 부재 = 마지막 프레임 고정" 패턴. 유저가 본 "시점 이상" = **확대가 아예 화면에 도달하지 않은 동결**.
- 동결 시작이 Begin#1과 정확히 일치(직전까지 diff 0.3–2.0 실시간 렌더) → 신규 DialogueCamera 생성+MainCamera 태그 교체+구카메라 disabled 스왑 구조가 트리거. End#1 후 구카메라 re-enable로도 회복 불가(카메라 스왑 경합/렌더 순서 불안정). 게다가 같은 세션에서 토스트 홍수(결함 1, 이 세션 완료 로그 1,297회 — 배치 직후 즉시 완료)로 **대화 패널도 로그상 OpenForVillager 실행됐는데 영상에 미표시(UI 전멸)** — 결함 1 수리가 선행 조건.
- **수리(방식 전환)**:
  0. **카메라 스왑 폐기** — 신규 Camera 생성·태그 교체·enabled 스왑 전면 제거. **기존 Main Camera를 직접 제어**: Begin 시 TopDownCameraController.enabled=false + position/rotation/FOV 저장 → 대화 프레이밍 적용 → End 시 원복+컨트롤러 재개. 카메라 개수 불변 → 태그/depth/렌더 순서 경합 원천 제거.
  1. 프레이밍 수식: `frameH = bounds height(≈1.8m)`, FOV 고정 40° → `dist = (frameH×0.5) / tan(FOV/2) × 1.18` ≈ 2.95m(1.8m 기준 실측 산식 — 주석에 근거) / 카메라 높이 = bounds center / **LookAt = bounds center(몸통 중심 — 매 프레임 재조준 유지)**.
  2. 방향 = **플레이어→대상 방향 그대로(정면)** — SightYaw 25° 폐기(병사 경로 유지 위해 오버로드 파라미터화: Begin(Component, displayName, fullBody:false) — 주민=true, 병사/영주 기존 false).
  3. NPC 정지 — PauseForDialogue(기존 유지) + 플레이어 응시 회전(기존 유지) — "대화 시 NPC 정지" 요구 이미 충족(영상 확인).
  4. 클립 방지(91차 90° 회전) 유지 + near plane 0.15 보정.
  5. **렌더 진단 로그**: Begin/End 시 `[DialogueCameraDirector] 렌더 상태 — camCount=N enabled=[...] depth=[...] pos=(...)` 출력 — 재발 시 즉격.
- **계약**: 병사 대화(73차) 기존 프레이밍 무변경 — DialogueFeedbackTests 기존 어서션 보존(스왑 경로 의존 어서션은 갱신).

### 애니 소스 판정 — [92차 보강·영상2 프레임 실측] **믹사모 FBX 도입 확정 (절차 재구축 NO / 현행 유지 NO)**
- **프레임 실측(10fps 20프레임 시퀀스 비교)**:
  - 플레이어(자체 리그+Meshy 클립 원본): 다리 교차/체중 이동 자연스러움 — 미세 moonwalk(속도 불일치)만. **합격**.
  - 주민(Player_AC 리타겟): ⑥~⑩프레임 발 제자리 미끄러짐(슬라이딩), ⑦⑧ 무릎 역꺾임 팝핑, ⑫⑬ 발목 비정상 회전, ④⑨⑭ 골반 역전 바운스, ⑱~⑳ 상체 중심 붕괴, ⑩ 스타터(루프 끊김). **불합격 — "흐느적거림"은 리타겟 왜곡+보폭 불일치의 합성으로 프레임 단위로 재확인.**
- **3안 판정**:
  1. 절차 애니 재구축 — **기각**: 2족 보행을 코드로 자연스럽게(발 접지 IK/무릎 해/골반 스웨이) 만들려면 작업 대비 품질 리스크가 큼. 절차 호흡 idle(기존 자산)은 유지.
  2. 현행(Player_AC 리타겟) 유지+튜닝 — **기각**: 보폭/속도 정합은 미끄러짐만 줄이고, 비례 차이에 따른 무릎/발목 물출렁(머슴 스페이스 과보정)은 코드 튜닝으로 해소 불가 — 89차 도입 시부터 구조적 결함.
  3. **믹사모 FBX 채택**: 리타겟 친화 최강인 소스(뉴트럴 비례·in-place 표준 인간형 리그)로 교체 — Unity 머슴 스페이스가 믹사모→커스텀 리그 변환을 가장 잘 처리. 주민 GLB(외형) 유지 + 애니만 교체.
- **도입 계획(점진 롤아웃)**:
  1. 유저가 믹사모에서 3종 다운로드: **Walking / Idle(Idle 가능한 호흡 계열) / Talking** — in-place, T-pose 없는 FBX(30fps).
  2. 프로젝트 임포트(Assets/Animations/Villagers_Mixamo/) — Humanoid 리그 타입 확인, 루트모션 off.
  3. VillagerClipDriver(또는 기존 AC 구조 유지)에서 주민 Animator 클립을 믹사모 3종으로 스왑 — **Speed 계약 유지(>0.55 Walk / <0.35 Idle)** → VillagerWanderer·테스트 무변경. Talk 트리거는 Talking 클립 매핑(TalkA/TalkB 유지).
  4. **보폭-속도 정합**: 믹사모 Walking 클립 사이클 실측 → `walkSpeed = 보폭(모델 bounds 다리길이 기반) / 사이클` — VillagerWanderer Init 파라미터(모델별). 미끄러짐 제거 = 체감 최대 레버.
  5. **1명 먼저(Villager_1) 적용 → 영상 재실측(프레임 추출+vision 판정) → 승인 후 전 주민 확대.**
  6. BuildHumanAvatar(91차) 경로는 비-표준 리그 폴백으로 유지.
- 검증: Play 로그 "[VillagerWanderer] 보행 정합 — model=girl1 walkSpeed=..." + 프레임 실측(내 ffmpeg+vision 재판정).

## 범위 화이트리스트 (구현 시)
- Systems: ConstructionManager.cs(완료 루프 write-back + 진행 중 가드), DialogueCameraDirector.cs(**스왑 폐기→기존 카메라 직접 제어** + 전신 프레이밍 — 오버로드 파라미터화), VillagerWanderer.cs(모델별 walkSpeed/anim.speed), VillageNpcSpawner.cs(walkSpeed 전달+믹사모 클립 스왑), TargetScreenProjector.cs(계약 변경 — 신규 파일), UTKControls.cs(UTKToastService 억제/상한)
- UI: InteractionPanelUTK.cs(클램프 배치), NPCDialoguePanelUTK.cs(클램프), EInteractionPromptUTK.cs(클램프)
- Animations(에셋, 유저 제공): Assets/Animations/Villagers_Mixamo/ — Walking/Idle/Talking FBX
- Tests: 신규/갱신 — [92차] 마커 소스 어서션 ①완료 write-back(루프에서 isComplete=true 직접 세팅) ②진행 중 음식점 재배치 거부 ③토스트 중복 억제+상한 ④TryProject z>0 계약+클램프 ⑤전신 프레이밍 산식+정면 파라미터+**스왑 폐기(신규 Camera 생성/태그 교체 코드 부재 어서션)** ⑥보행 정합(walkSpeed 전달/믹사모 클립 참조)

## 검증 계획
- 컴파일 error CS 0 / EditMode baseline 11 외 0.
- Play(승인 게이트): ①음식점 완공 후 로그 1회만("건설 완료" 스팸 소멸) ②U키 → 건설창 정상 렌더 ③F키 패널이 대상에 계속 부착(1초 후에도 안 튐) ④대화 → **화면 동결 부재(렌더 진단 로그 정상)+NPC 전신 정면(정지·응시)+대화 패널 표시** ⑤주민 걸음 흐느적거림 완화(믹사모 전환 후 영상 재실측 — 내 프레임 판정).
- **동결 재발 시**: 렌더 진단 로그(camCount/enabled/depth)로 격리 — 결함 1 수리와 무관한 재발이면 카메라 경로 2차 수리.

## 확정 사항(감독자)
- 우선 순위: 결함 1(완료 루프)가 최상 — 로그 스팸/토스트 붕괴/창 렌더 파괴의 뿌리. 결함 2/3/애니 교체가 후속.
- **애니 = 믹사모 FBX 도입(92차 보강 확정)** — 절차 재구축·현행 유지 모두 기각. 1명 선적용→영상 검증→전체 확대.
- **대화 카메라 = 스왑 폐기(기존 Main Camera 직접 제어)** — 영상2 동결 실측(11.8초 0.000 diff) 근거. 병사/영주 대화 프레이밍은 기존 유지(요구는 "말을 건 대상"=주민 맥락 — 병사도 원하면 후속 지시).
- 유저 준비물: 믹사모 3종 FBX(Walking/Idle/Talking, in-place) — 다운로드 후 경로 알려주면 임포트 진행.
