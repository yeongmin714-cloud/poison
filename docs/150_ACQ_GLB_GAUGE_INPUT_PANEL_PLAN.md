# 150차 계획 — 획득 GLB 미표출(근본 확정)+건설 게이지 가시성+판매가 입력+획득 패널 좌상단

## §0 사용자 지시 (2026-11-03, 영상 "농경 테스트.mp4" 35.6s + Editor.log 실측)
1. 채집 시 얼굴 위 부유 GLB가 안 뜸.
2. 건물 건설 시 게이지 여전히 미표출.
3. 음식 판매 시 금액을 직접 입력하고 싶은데 숫자가 입력되지 않음.
4. 채집 성공 시 상호작용 패널이 가운데 하단에 떠야 하는데 왼쪽 상단에 뜸.
- "수정 계획부터" — 승인 전 구현 없음.

## §1 진단 (영상 프레임+로그 실측 — 원인 확정/미확정 구분)

### 1-A 부유 GLB — **근본 원인 로그 확정** ✅
- Editor.log에 `"Transform resides in a Prefab asset and cannot be set to prevent data corruption"` **4회** — AttachFloatItem L194(SetParent) 스택과 1:1.
- 체인: AcquisitionItemVisualResolver.LoadVisual이 `AssetDatabase.LoadAssetAtPath` 결과를 **Instantiate 없이 에셋 자체** 반환 → 씬 밖 프리팹 에셋을 SetParent 시도 → Unity 차단 → 씬에 진입 불가 → 미표출.
- resolver 자체는 정상(herb_purple/green/red 경로 해석, 폴백 아님). **구 손끝 부착(130~144차)도 동일 결함** — "GLB 전체가 보이지 않음"의 오래된 뿌리.
- 수리: LoadVisual이 에셋을 반환할 때 `Object.Instantiate(go)`로 씬 인스턴스 반환(단일 지점 — 호출부 전파 없음). Resources 폴백 경로(프리팹 반환)도 동일 처리.

### 1-B 건설 게이지 — 원인 가설 2건(Play 계측 필요) ⚠
- 실측: 부착 로그 `pos=(-418.8,2.0,-109.9) barY=4.6 children=2` + P4 덤프 `track/fill enabled=True matNull=False texNull=False` — **생성·머티리얼 정상**. LateUpdate 빌보딩은 manual 모드 포함 공용.
- 가설 ①**크기/거리**: 바 0.9m×0.11m — 채널링 바(2.5m, 플레이어 옆)와 달리 건설 사이트는 원거리 관측 → 픽셀 미달. ②**가림**: barY=4.6가 건설 중 구조물 메시(음식점 높이)에 가려짐.
- 영상 프레임 2에서 캐릭터 바로 위 "얇은 검은 선" = 채널막 바 트랙으로 추정(채움은 가늘어 식별 낮음) — 바 어휘 자체가 너무 작다는 방증.
- 수리: 사이트 게이지 스케일 상향(예: 트랙 2.4m×0.30m — 구조물 크기 대비 가시) + 앵커 높이를 구조물 최고점 위로(+진행 중 높이 변화 대응) + 프레임별 가시성 프로브 로그(카메라 거리/occlusion 레이/isVisible) → Play 1회로 확정 후 미세 조정.

### 1-C 판매가 직접 입력 — 진단 필요(정황 증거) ⚠
- 창에 등록가 TextField 존재(RestaurantWindowUTK L950) + 포커스 중 핫키 무시 가드 존재(L401 IsAnyTextFieldFocused).
- 가설 ①행 클릭 핸들러가 TextField 클릭을 가로채 포커스 미부여 ②Play 환경 UITK 키보드 이벤트 미전달(EventSystem/Panel 입력 구성 — 검색 필드(_searchField)도 같이 검증하면 판별: 검색은 되고 가격만 안 되면 ①, 둘 다 안 되면 ②).
- 수리: 원인별 — ①이면 필드 pickingMode/행 핸들러 정리 + 포커스 강제 ②이면 UITK 키보드 이벤트 전달 경로 수리(EventSystem/InputSystemUI 모듈 — 프로젝트 입력 체계 실측 후). 숫자 입력 중 핫키(숫자키=퀵슬롯) 충돌 가드 유지.

### 1-D 획득 패널 좌상단 — **로그로 직접 원인 포착** ✅
- 영상 프레임 6: 좌상단에 "치유초 x3를 획득했다! 계속하려면 클릭 ▶" (획득 패널) — 좌상단 실측.
- Editor.log: `[TargetProject] 투영 무효 — target=NPCDialogueAnchorProxy (전 카메라 뒤 z≤0 — 폴백 배치/무배치)` — 연출 시작 직후(NPCDialoguePanel 앵커 프록시가 카메라 뒤) → 폴백 배치 = 좌상단.
- 연계 의심: 클로즈업 카메라(frontView)가 **시각적으로 개입하지 않음**(프레임 4/6/9 모두 통상 시점) — 카메라가 옮겨지지 않았거나 방향이 반대 → 프록시가 카메라 뒤가 되는 정황. BeginUpperBody 개입 여부+방향을 계측으로 확정.
- 수리: ①연출 시작 시 패널 첫 투영을 카메라 정착 후로 지연(스윙 타이밍 분리) ②프록시 갱신을 연출 카메라 기준으로 재판정 ③카메라 무개입이면 DialogueCameraDirector 개입 경로 수리. 폴백 배치도 좌상단(0,0 근사) 대신 화면 중앙 하단 계약 위치로 교정.

## §2 Phase 계획
- **P1 부유 GLB 수리**(확정): LoadVisual Instantiate화 + 계약테스트 갱신(Instantiate 문자열 계약) — 즉시 효과 기대.
- **P2 획득 패널 위치**(로그 확정): NPCDialoguePanelUTK/TargetScreenProjector 폴백 산정 + 카메라 개입 계측 로그 → 수리. 가운데 하단 배치 계약 명시.
- **P3 건설 게이지 가시성**: 스케일/높이 보정 + 가시성 프로브 → Play 1회 → 확정 후 미세조정.
- **P4 판매가 입력**: 포커스/키보드 전달 진단 로그 → 원인 확정 → 수리(검색 필드 교차 판별 포함).
- 공통: 컴파일 CS 0 + 전수 EditMode baseline 10건 1:1 + 신규/갱신 계약 + 범위 감사. Play·커밋 별도 승인.

## §3 검증
- Play 확인: 채집(약초/나무/돌/낚시) 시 얼굴 위 GLB 부유 5초, 획득 패널이 화면 중앙 하단(플레이어 근접), U키 건설 시 게이지 가시(원거리 포함), 음식점 등록가 숫자 입력 동작.
- 순서: P1 → P2 → P4(진단 장착) → P3(보정+프로브) → 공통 QA.
