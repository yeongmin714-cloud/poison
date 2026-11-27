# 154차 계획 — 대화판넬 하단 고정 수리 + 굉이질 오목 검정 수리 + 획득 FX 고도화 + 음식점 배치 시 골드 입력

작성: 2026-11-05 (154차). 승인 전까지 구현 없음.
근거: 사용자 영상 `Screenshots/마을 테스트 17.mp4`(209s, 1240×828) 프레임 판독 + 현재 세션 Editor.log + 소스 열람.

## 0. 사용자 지시 (4건)
1. 대화 패널이 여전히 중앙 하단에 고정 안 됨
2. 땅이 파진 모습이 검정색 — 땅처럼 보이지 않음
3. 획득 이펙트가 흰색 빛뿐이라 단조로움 — 다듬기
4. 음식점 배치 시 골드(가격)를 직접 입력하고 싶은데 안 됨 — 구현 방법?

## 1. 진단 결과 (영상·로그·소스 교차 — 확정/추정 구분)

### ① 대화 패널 상단 발생 — **원인 확정: y축 이중 반전(double-flip) 버그**
- 영상 증거: 16s 획득 패널("독나물 x3을 획득했다!")·24/32s 농경 문구 패널("씨앗이 부족합니다") = **화면 중앙 상단**,
  81s NPC 대화창(Test15Customer) = **중앙 상단 좌측 치우침**. 전부 NPCDialoguePanelUTK 1개 창(획득/트랜지언트/NPC 대화 모두 동일 패널).
- 코드 원인(`UI/Toolkit/NPCDialoguePanelUTK.cs`):
  - `ApplyBottomCenterPlacement`(L372) → `ApplyAnchorLayout(new Vector2(Screen.width*0.5f, Screen.height*0.85f), …)`.
  - `ApplyAnchorLayout`(L352)는 세계 투영용 산식으로 y를 **다시 반전**한다: `canvasY = (Screen.height − screen.y) * CanvasH / Screen.height`.
  - Unity Screen 좌표는 **아래가 0**(bottom-up)이므로 0.85를 넣으면 반전 후 **0.15(=화면 상단 15%)**가 됨.
  - 이어서 `style.top = clamp(scaled.y − panelH, 0, …)` → 음수 → **0 클램프** → 패널이 화면 상단에 고정.
- 로그 교차: 현재 세션 Editor.log에 `[151차 P2] 대화 패널 — 항상 중앙 하단 고정 배치(스크린 50%, 85%)` 다수 발생 —
  **입력값은 0.85로 찍히지만 결과는 상단** = 계약테스트 `DialoguePanel_AlwaysBottomCenter_151`은 상수 존재만 검증하고 최종 픽셀 좌표를 검증하지 않은 문서-코드 불일치.
- 150차 폴백 산식(투영 좌표)에서는 반전이 옳았으나, 151차가 "고정 85%"를 같은 반전 경로에 통과시킨 것이 버그.

### ② 굉이질 오목 검정 — **원인 후보 4종, P0 격리 프로브로 확정 후 수리**
- 영상 증거(24s 4배 줌): Tilled 플롯 = **평면 흑색 사각형**(흙둑 융기 없음, 경준선 텍스처 안 보임, 테두리 번짐),
  배경 잔디와 급격 경계. 대조: 160s 이후 Planted 밭 = **갈색 정상**. 즉 검정은 **Tilled(구멍 메시 활성) 상태에서만** 발생.
- 153차 프로브 스틸(farm153_1_tilled_hole.png)도 회색조·저명도 — "프로브 한계(프로덕션 결함 아님)" 판정은 **기각됨**(실제 결함).
- 후보 가설(`Systems/FarmPlotVisuals.cs` 열람 근거):
  - **H1 지오메트리 침범**: 구멍 메시는 타일 전체 1×1을 덮고 가장자리 링이 y=0 — 흙타일 Cube(−0.1..0)의 **상면(y=0)과 동일 평면**,
    내부(−0.08..0)는 **흙타일 부피 안에 묻힘** → 오목이 화면에 안 보이고 동일 평면 대역에서 z-파이팅.
  - **H2 자가 그림자**: 구멍 렌더러가 흙타일/자기 둑 그림자를 받아 무광 → 검정.
  - **H3 정점색 무시**: URP/Lit은 메시 정점색을 반영하지 않음 → `VertexMul`(0.40~1.0) AO 계약이 **시각 반영 0**(죽은 코드).
  - **H4 재질**: 배수 1.12·텍스처·normal 배선은 정상 범위(검정 직접 원인 아님).
- 판정 절차: P0 격리 프로브(흙타일 on/off × 구멍 on/off × receiveShadows on/off × normal 맵 on/off — 실제 씬 조명)로
  검정 재현 조합 확정 → 조합에 맞는 수리 적용.

### ③ 획득 FX 단조로움 — 구조 개선 대상(색 계약 유지)
- 현황(`UI/Toolkit/AcquisitionCinema.cs`): 좌우 빛기둥 쿼드 1겹씩 + 헤일로 1장 + 스파클 PS(radial 텍스처 배선 확인됨).
- 영상 판독: 빛기둥 = 흰~연청 단색 세로 띠, 입자 = 사각형으로 판독(16s) — radial 텍스처 적용 실측 필요.
- "흰색 단조로움"의 구조적 원인: Common 등급색 = 회색(0.6,0.6,0.6) → additive 합성에서 **무채색 발광**으로 판독 + 쿼드 1겹/모션 단일.
- 계약: 등급색 단일 소비(`EquipmentRarityData.GetRarityColor`) 유지 — **신규 색 매핑 표 금지**. 개선은 구조/모션/층으로.

### ④ 음식점 배치 시 골드 입력 — **운영창 입력은 동작, 배치(건설) 시점 입력 부재**
- 영상 실측(139~152s): 음식점 배치 창(운영) 가격 필드에 **포커스·커서·백스페이스·타이핑 모두 동작**(30→31 편집, 150s 블러=커밋, 152s 값 유지).
  → 151차 P0 키보드 브리지는 정상 작동. **"입력 불가"의 실제 공백은 다른 시점**으로 판정.
- 부재 구간 확정: 건설 배치(ConstructionManager.TryPlace 음식점 분기 L208~)는 검증→**허가세 자동 산출**→재료 소비뿐,
  등록가는 씬 시딩값(30) 그대로 — **배치 확정 시점에 사용자가 가격(골드)을 입력하는 단계가 없음**.
- 재사용 인프라: `VillageDrugState.SetRegisteredPrice(nation, villageIndex, price)`(Core L279) + 151차 onTextInput 브리지 패턴
  (`utk-textfield-keyboard-bridge` 스킬 절차).

## 2. 설계 계약 (기존 원칙)
- 대화 패널 배치 = "항상 중앙 하단" 단일 계약(151차) 유지 — 수리는 산식 1곳만, 호출부 5곳 시그니처 무수정.
- 농경: FarmPlot 상태머신/E키/채널/수분 로직 무변경 — 시각부(FarmPlotVisuals)만. 바닥 원본 훼손 금지(깊이 −0.1m 계약).
- FX: 등급색 단일 소비 유지, Bloom 비의존(additive), 병사 경로 무연출.
- 음식점: 음식점 실내 금지·마을당 1개·허가세 자동 산출 규약 불변 — 신규는 "배치 확정 시 초기 등록가 입력"만.
- 키보드 입력: 151차 onTextInput 브리지 패턴 준용(스코프드 부착/해제·자가 은퇴 프로브).
- Figma 단일 소스 — 신규 모달은 기존 창 스타일 계열(헤더/본문/버튼 톤) 준용.

## 3. Phase 계획 (파일경로+검증 사전 제시)

### P0 — 굉이질 검정 원인 격리 프로브 (배치 런, 에디터 종료 후)
- 신규 `Assets/Scripts/Tests/EditorTools/Farm154HoleIsolationProbe.cs`(에디터 스크립트 — Farm153PlayProbe 선례):
  4조합 캡처(RT+cam.Render+ReadPixels — ScreenCapture 헤드리스 불안정 선례): (a) 흙타일+구멍 (b) 구멍만 (c) 흙타일만
  (d) 구멍+receiveShadows=false. 각 컷 PNG 저장 + 조명/그림자 실제 씬 세팅 사용.
- 판정: 검정 재현 조합 확정 → ② 수리 방향 확정. **산출물: /TestOutput/farm154_isolation_*.png + 판정 로그.**

### P1 — 대화 패널 중앙 하단 더블플립 수리
- `NPCDialoguePanelUTK.cs`:
  - `ApplyBottomCenterPlacement` 입력을 **bottom-up 환산값**으로 수정: `new Vector2(Screen.width*0.5f, Screen.height*0.15f)`
    (0.15 = Unity Screen 좌표 기준 "위에서 85% 지점"). 주석에 반전 관계 명시(재발 방지).
  - 배치 순수 산식을 `internal static (float left, float top) ComputeBottomCenterLayout(…)`로 추출 — 테스트 가능화.
- 테스트 `Tests/EditMode/NPCDialoguePanelPlacementTests.cs` 갱신: 기존 `DialoguePanel_AlwaysBottomCenter_151`을
  **최종 style.top이 하단 밴드(≥ rootH 60%)에 착지하는 산식 검증**으로 재편(상수 존재 검증 폐기).
- 검증: 집중 테스트 GREEN + Play 프레임(획득/농경 문구/NPC 대화 3컷) vision 판독 — **별도 승인 게이트**.

### P2 — 굉이질 오목 시각 수리 (P0 판정 후 적용 — 예상 수리 세트)
- `FarmPlotVisuals.cs`:
  1. **동일 평면 제거**: 구멍 상면 기저 y를 흙타일 상면보다 +0.002 위로(또는 흙타일 상면 −0.002 하강 — 시각 전용, 계약 −0.1m 이내 유지) → z-파이팅/부피 침범 해소.
  2. **그림자**: 구멍 렌더러 `receiveShadows=false`(실외 그림자 손실 미미 — 프로브로 확인) 또는 light ambient 보정.
  3. **AO 전환**: 정점색(URP 무시 확인) → **구멍 전용 AO 프리멀티 텍스처**(소일 텍스처 × 링별 명암 — static 캐시·고정 시드 결정론).
     정점색은 호환 유지(무해) 또는 제거 — P0 후 확정.
  4. 흙둑 가시화: 둑 높이/명암 상향(탑다운 판독 강화).
- 테스트: `FarmPlotVisualsTests` 갱신(정점 범위+AO 텍스처 결정론+기저 y 클램프) + 기존 12건 1:1.
- 검증: Play 프레임(굉이질 직후/파종 후) vision — "흙처럼 보이는 갈색 오목" 판정. **별도 승인 게이트.**

### P3 — 획득 FX 고도화 (등급색 단일 소비 유지)
- `AcquisitionCinema.cs` 확장:
  1. **다층 빛기둥**: 각 측 2겹(코어 얇음·알파 높음 / 아우터 넓음·알파 낮음), 서로 반대 위상 사인 바위가.
  2. **회전 링**: 부유 아이템 주위 수평 링 쿼드 1~2개(등급색, 반대 방향 회전 — 탑다운 판독 최적).
  3. **스파클 강화**: 등급색+백색 코어 2색, 수명 페이드 커브, 티어 배수(개수/크기), 발화 초기 버스트.
  4. **등장 링 웨이브**: 팝과 동기화한 확산 링 1발(등급색).
  5. Common 대응: 색 매핑 추가 없이 **구조(코어+링)+모션**으로 변별력 확보, 티어 최소 알파 상향.
  6. 입자 사각형 판독 재실측(radial 텍스처 적용 확인 — 미적용 경로 발견 시 배선 수리).
- 테스트: `AcquisitionCinemaTests` 확장(FX 구성 결정론/티어 배수/페이드 등록) — 기존 42+5건 1:1.
- 검증: Play 영상(획득 연출 3컷+) vision — "단조로움 해소" 판정. **별도 승인 게이트.**

### P4 — 음식점 배치(건설) 확정 시 초기 등록가 입력
- 신규 `UI/Toolkit/RestaurantInitialPriceModalUTK.cs` — 배치 확정 직후 1회 표출 모달:
  - 구성: 제목("음식점 초기 등록가"), 숫자 TextField(기본값=권장가), 안내(허가세·재료는 기존 자동 로직 표시), 확인/취소.
  - 확인 → `VillageDrugState.SetRegisteredPrice(nation, villageIndex, price)` → 기존 Refresh 경로 갱신.
  - 취소/무입력 → 기본값(권장가) 유지 — 게임 상태 변경 없음.
  - 키보드: `RestaurantWindowUTK.AttachPriceKeyboardBridge` 패턴 이식(스코프드 부착/FocusOut 커밋/자가 은퇴) —
    `utk-textfield-keyboard-bridge` 스킬 함정(패치 모드 V4A·각괄호 훼손 검사) 준용.
- 배선: `ConstructionManager.TryPlace` 음식점 성공 반환 지점 → 모달 호출(Systems→UI 단방향 — 이벤트/브리지로, 152차 Core 경유 선례).
  ConstructionHologram 배치 확정 클릭 이후 시공 시작 전에 모달 — 시공 중 취소와 무충돌(모달 확인 전 시공 시작하지 않음).
- 테스트: 모달 산식/커밋 경로/취소 무변경 소스계약 신규 + `ConstructionTests` 갱신(음식점 성공 경로에 가격 커밋 호출 핀).
- 검증: 집중 GREEN + Play(신규 음식점 배치→가격 입력→운영창 값 일치→NPC 구매가) — **별도 승인 게이트.**

### P5 — QA
- 컴파일 CS 0 + EditMode 전수 + baseline 1:1 대조(현 10건) + Melee 플래키 관찰.
- Play 영상 프레임 추출+vision: ①대화 패널 3컷 위치 ②굉이질 갈색 오목 ③획득 FX ④배치→가격 입력→구매가.
- 위임: 구현/QA 서브에이전트 분리(docs/AGENT_DELEGATION_POLICY.md) — Phase당 파일·검증 범위 명시.
- Play 실행·커밋·push는 별도 승인.

## 4. 확정 사항 (사용자 답변 2026-11-05 — 기록)
1. **④ 입력 대상 = 기본안 확정**: 배치 확정 시 "초기 등록가(판매 가격)" 입력 모달. 허가세/투자액 아님.
2. **③ = 모션/구조만 강화 확정**: 등급색 표(EquipmentRarityData) 무변경 — Common 색 교체 없음.
3. **② = 구멍 메시 상승(+0.002) 확정**: 흙타일 원본 무변경. 그림자/AO 세트는 P0 판정에 따라 병행.

## 5. 셀프감사 (중복/키충돌)
- **P1 vs 기존 계약**: PlaceAtAnchor 시그니처/호출부 5곳 무수정 — 산식 입력값만 수정. InteractionPanelUTK(하단 우측 버튼 패널) 무접촉.
- **P2 vs 농경 계약**: 상태머신/채널/수분 로직 무변경, 깊이 계약(−0.1m 이내) 유지, 153차 FarmPlotVisualsTests와 충돌 없음(갱신만).
- **P3 vs 계약**: 등급색 단일 소비 유지(신규 매핑 표 없음), 병사 경로 무연출, _fxRenderers 페이드 계약 준수.
- **P4 vs 기존**: 음식점 배치 게이트(실내 금지/마을당 1개/허가세) 무변경 — 입력 단계는 **증분 삽입**. 키 충돌 없음(모달은 배치 확정 후 1회).
  E키 컨텍스트 무변경. RestaurantWindowUTK 운영창 브리지와 별개 인스턴스(이중 부착 없음 — 모달 닫힘 시 해제).
- **154차 내 상호 충돌**: P1/P2/P3/P4 서로 다른 파일 — 병렬 위임 가능(P0 → P2만 순서 의존).
