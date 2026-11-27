# [계획] 월드맵 풀스크린·Fluent 패널 / 건설창 Figma 재정합 / 아레나 외부 Idyllic 지형

작성: 2026-10-26 12:01 (125차 후속37 후보 — 승인 대기, 구현 없음)
근거: 읽기전용 서브에이전트 3건 실측 (WorldMapWindowUTK / ConstructionWindowUTK / ArenaTestSetup+지형 파이프라인)

## 0. 요청 이해 (사용자 발화 4건)

1. **월드맵 풀스크린**: 월드맵을 켰을 때 지도가 화면 전체를 꽉 채우길 원함.
2. **월드맵 좌우 패널 Fluent**: 양쪽에 뜨는 퀘스트창·설명창이 플루언트(반투명 글래스)로 뒤의 지도가 비치길 원함.
3. **건설 UI Figma 재정합**: 건설창이 최근 이상하게 바뀜 — Figma 스타일로 다시 정합.
4. **아레나 외부 지형**: 아레나 실내(로비)가 아닌 외부(스테이징)가 현재 체크무늬 빈 바닥 — 메인씬과 같은 Idyllic 지형을 배치해 메인씬과 이어진 것처럼 표현.

## 1. 실측 확정 사실 (조사 결과)

### 월드맵 (WorldMapWindowUTK.cs, 2177행)
- **풀스크린 구현이 이미 존재**: 117차 "월드맵 = 풀스크린 오버레이(사용자 지시)". raw 1920×1080 설계공간, `ApplyCanvasBounds()` L338-357이 창=루트 전체 + `_content` 축별 스케일(rw/1920, rh/1080)로 화면 전체 stretch. 생성자+OnWindowOpen(16/120/400ms 재시도) 호출.
- 좌 패널 = `BuildQuestListPanel()` L618 — "지역 탐색 리스트" (48,109) 456×888, **불투명 #161B22**(L626)+스트로크, 퀘스트/지역 탭.
- 우 패널 = `BuildQuestDetailPanel()` L1010 — "TACTICAL INTEL" (1392,109) 480×888, **불투명 #161B22**(L1018).
- 배경(지도 아트)은 3레이어(WorldMapBackground PNG+오버레이+양피지) explicit backgroundSize (1920,1080) — 패널 제외 영역은 이미 화면을 채움.
- `DisableWindowShadow()` 미호출(UTKWindowBase L181 소속).
- 계약 테스트: WorldMapFigma124Tests.cs — 배경 explicit size 단언(L47-52)·우패널 좌표 단언(L194-204) + 색/텍스트/배선 단언 일부.

### 건설창 (ConstructionWindowUTK.cs, 2489행, 미커밋 변경 0건 = 커밋 b5812b4b 상태)
- 시계열: 후속12(10-25) 사용자 지시 플루언트 전환 — 루트 `Color.clear` 투명화 + 좌/우판 `UTKTheme.GlassPanel`(흑 0.85). 후속13(10-25) — `BuildHeaderBar` 호출 폐기(L312, 전초기지 경로 그리드 소실).
- **신규 발견 잠재 버그**: `Refresh()` L718 → `RefreshHeaderTime()` L720 무조건 호출 → L731-735 `_headerDayLabel.text` **널가드 없음** → 개방 즉시+500ms 폴링마다 NRE 구조.
- Figma 기준선: 노드 **239:683 "전초기지 건설 관리" 1600×1040**, 코드 재실측 좌표 기록 완비(L50-59, 100차 F-4): LeftPanel 480×882@(64,78) / RightPanel 968×882@(568,78) / HelpBar 1472×20@(64,984).
- 테스트: ConstructionWindowTests `BackgroundFallback_And_DomAncestorChain_94`(L249 투명 루트 단언/L251 GlassPanel 단언) + 헤더 폴백 제거 단언 3곳(L237/310/392) + MeasureGeometryAndDefend HeaderBar blockNames(현재 "HeaderBar=미빌드" 불일치 로그 매 개방 기록).

### 아레나 외부 (ArenaTestSetup.cs, Test_18_Arena)
- 씬 파일 = 125행 빈 씬(오브젝트 전무) — 전부 런타임 부트스트랩 생성.
- 체크무늬 유력 후보: `SetupGround` Plane 머티리얼 미지정/`Shader.Find("Universal Render Pipeline/Lit")` 실패 → error/default 렌더. (비교 선례: TestTerritorySetup.cs L251-270은 URP Lit+색상 명시 할당)
- 보조 후보: 원점(0,0,0) 오브젝트 공존(RenderAuditProbe에 이미 "Ground 공존/잔존" 디버깅 코드 존재 — 팀도 추적 중), 후속33 N1 Ground 토글 상호작용.
- **후속36 A5 "우측 체크무늬 원인 미특정"과 동일/연결 대상일 가능성 높음 — 본 사이클 C0 진단이 A5 답변을 겸함.**
- 메인씬 Idyllic: `Assets/Idyllic Fantasy Nature/` 팩 존재. 코드 유틸 = `TerrainTextureApplier.cs`(Terrain 레이어/텍스처 코드 적용), `IdyllicDecoPlacer.cs`, `TerrainPathGenerator.cs`, `VillageBuilder.cs`(+`BuildVillageGroundPatch(center,140)` 국소 지형 패치 선례 — TestTerritoryCombatSetup). `Test_12_TerrainOnly.unity` 지형 전용 테스트씬 존재.

## 2. Phase 계획

### Phase A — 월드맵 풀스크린 확인 + 좌우 패널 Fluent (파일 2개)
- **A0 재실측(수리 전)**: 사용자 Play 1회 — M키 월드맵 개방 → 스크린샷/영상. 이미 화면 전체를 채우는지 확인(117차 구현 존재 → 재발 여부 판정). 마진/축소가 보이면 `ApplyCanvasBounds` 회귀 분석이 선행 과제.
- **A1 패널 글래스화**: L626·L1018 배경을 `UTKTheme.ApplyFigmaGlass`(Figma 토큰 #161B22@0.85 + GlassStroke w1.2 + radius 14.4)로 교체 — **좌표/크기/내부 레이아웃 불변**(테스트 좌표 단언 보존). `DisableWindowShadow()` 추가. 신규 토큰 0건(기존 토큰 재사용 — Arena/Construction 선례와 동일 경로).
- 범위 확정: HUD헤더(0,0,1920×77)는 사용자 언급 없음 → 불변(열린 질문 Q3).
- 검증: 컴파일 error CS 0 / EditMode 1608/1597/11 baseline 1:1(WorldMapFigma124Tests 색·배경 단언 해당분 갱신) / 재Play — 패널 뒤로 지도 비침, 마커·툴팁·탭 동작 무결.

### Phase B — 건설창 Figma 재정합 (파일 2개 + Figma API)
- **B0 Figma 재실측**: 노드 239:683+자식 GET(키 02vPXOGzFJUUYTaEPSVz3R, 토큰 ~/.figma_token) — 세션 간 개정 대비 현재 진실 확정 후 수치 확정.
- **B1 헤더 복원+NRE 수리**: 생성자 L312 BuildHeaderBar 호출 복원(후속13 역전) + `RefreshHeaderTime` L731-735 널가드 추가(방어 — 헤더 복원 후에도 유지).
- **B2 불투명 복원(후속12 역전)**: 루트 `Color.clear` → `FigmaRootBg #0B0E14`(상수 이미 존재, 미사용 잔존), 좌/우판 GlassPanel → `PanelBg #13181F`+`Stroke #30363D@0.5` 복원. 좌표(Figma 100차 F-4 실측치) 불변.
- **B3 테스트 갱신**: ConstructionWindowTests L249/251을 불투명 계약으로 역갱신 + 헤더 폴백 단언 3곳(L237/310/392) 재검토 + MeasureGeometryAndDefend HeaderBar 불일치 로그 자동 해소 확인.
- 범위: **건설창만** Figma 복귀 — 월드맵 패널은 A1에서 Fluent(사용자 요청이 정반대 방향이므로 상호 무충돌). UTKTheme.GlassPanel 토큰 자체는 8개 창 공유 → 토큰 수정 금지.
- 검증: 컴파일 error CS 0 / EditMode baseline 1:1 / 재Play — Figma 239:683 대조(스크린샷+현재소스 열수 둘 다 확인 계약).

### Phase C — 아레나 외부 Idyllic 지형 (원인 확정 선행)
- **C0 체크무늬 원인 확정(수리 금지 계약)**: RenderAuditProbe 씬 루트 덤프+RT 캡처로 체크무늬 렌더 오브젝트 특정 — 후보 ①SetupGround Plane 머티리얼 결함 ②Shader.Find 실패 ③원점 공존 오브젝트. A5 답변 산출.
- **C1 메인씬 지형 경로 실측**: 메인씬 Terrain 생성 주체 클래스/씬 구성, TerrainTextureApplier의 레이어 세트, Test_12_TerrainOnly.unity 참조 구조, IdyllicDecoPlacer 밀도 파라미터 — "메인씬과 이어진 것처럼"의 기준 수치 확정(나무수준=밀도 동일화 원칙).
- **C2 배선**: ArenaTestSetup.SetupGround를 Idyllic Terrain 생성으로 교체(TerrainTextureApplier 재사용+결정론 seed) + 주변 Idyllic 데코 + 필요시 VillageBuilder 마을 연결 표현. 스테이징 60×60 대비 지형 범위는 C1 실측 후 기본안 제시(기본안: 아레나 외곽 충분 반경 — 투기장/로비 빌더 좌표 불변).
- **C3 검증**: 재Play — 체크무늬 소멸+Idyllic 지형 가시+투기장/로비/성내부 무변화+메인씬 무영향(테스트씬 전용 경로)+EditMode baseline 1:1.
- 리스크: 단독 실행 씬에서 지형 파이프라인 의존 시스템 초기화 실패 가능 / 원점 공존 / TerrainData 런타임 생성 vs 저장.

## 3. 셀프감사 (중복·키충돌·계약)
- 중복 폐기: A1 글래스=기존 ApplyFigmaGlass 토큰 재사용(신규 0) / B2 FigmaRootBg 상수 기존 존재 / C2 TerrainTextureApplier 기존 유틸 재사용 — 신규 인프라 0.
- 키충돌: 월드맵 M키 폴링/테스트씬 부트 계약 무변경. 건설창 훈련소 매핑 case 무변경.
- 계약 변경 명시: B는 후속12/13(사용자 지시) 역전 — 본 요청이 정정 지시이므로 계약 갱신 근거 명료. A는 Figma124 색 단언 갱신. C는 Ground 계약 교체(후속33 N1 토글 대상 오브젝트 변경 가능 — C2에서 동기 확인).
- 순서: A → B → C (UI 2건 경량, C는 진단 포함으로 시간 소요 큼). 각 Phase 독립 승인+재Play 게이트.

## 4. 열린 질문 (구현 전 답변 요청)
- Q1: 건설창 "이상하게 바뀐" 증상이 헤더 소실+반투명화가 맞는지 — 스크린샷 있으면 확정. (유력 원인 2건 실측됨)
- Q2: 아레나 외부 지형 범위 — 전면 Terrain(메인씬 동일 파이프라인) vs 국소 패치+데코(C1 실측 후 기본안 제시 예정, 선호 있으면 선행 고지).
- Q3: 월드맵 상단 HUD헤더도 글래스 적용할지 (요청은 좌우 패널만 언급 — 기본안 불변).

## 5. 진행 원칙
- 계획 승인 후에만 구현 착수 / Play·커밋 별도 승인 / 원인 확정 전 수리 금지(C0) / Figma 재실측 선행(B0) / 각 Phase 완료 시 QAPROGRESS 후속 기록+메모리 갱신.
