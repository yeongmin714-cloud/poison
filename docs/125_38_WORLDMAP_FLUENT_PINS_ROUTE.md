# [125차-38] 월드맵+미니맵 — 플루언트 글래스 패널 · 플레이어 현재지 핀 · 지점 전송→미니맵 경로

작성: 2026-10-26 (계획+구현 기록 — 완료)
Figma 진실: 2026-10-26T03:19Z 사용자 수정본 (사전 실측값 인계 — 본 문서는 소스 반영 기록)
대상: WorldMapWindowUTK.cs / MinimapUTK.cs / MinimapRouteOverlay.cs(신설) / WorldMapFigma124Tests.cs

## 0. Figma 진실치 요약 (반영 기준)

- 좌패널 '지역 탐색 리스트'(95:188) 456×888: fill **#161B22@0.80** + stroke #30363D@0.50 w1.2 + r14.4
  + DROP_SHADOW + BACKGROUND_BLUR(플루언트 글래스) / Decal 코너(#58A6FF L자) / CloseButton 32×32
  #161B22@0.80 r=16 / QuestSlot 408×120.8 r9.6(활성 #FFFFFF@0.08+#58A6FF w1.8, 비활성 #FFFFFF@0.02+#30363D w1.2)
- 우패널 '선택 지역정보'(95:255) 480×888: 동일 글래스 사양
- 플레이어 핀 '지역 마커 · 현재지'(95:238): PinBase 38.4×38.4 r19.2 fill #39D353@0.10 + stroke #39D353 w1.8,
  내부 원 14.4 #39D353 솔리드, 외부 펄스 링 52.8 stroke #39D353 w1.2(핀 밖 넘침 — overflow visible),
  PinLabel h=24.6 fill #161B22@0.80 + stroke #30363D@0.5 w1.2 r4.8 + DROP_SHADOW,
  텍스트 '{영지명} (현재지)' #F0F6FC 15px
- 경로선 레퍼런스(95:167 내 Line 2개): stroke #FF9E2C w3.0 / #58A6FF w2.4 — 경로=목적지 핀 색
- 지도 프레임 95:167은 풀블리드 지도+HUDHeader(#0D1117@0.50)만 — 패널은 지도 프레임 밖 독립
  (구현상 기존 위치 유지, 글래스만 적용)

## 1. 구현 기록 (작업 4건)

### ① 패널 플루언트 글래스 — WorldMapWindowUTK.cs
- BuildQuestListPanel L638: 배경 #161B22(불투명 GitHubDark.Panel) → **#161B22@0.80**
  (`new Color(0x16/255f, 0x1B/255f, 0x22/255f, 0.80f)`). 치수(456×888 @48,109)/Decal 코너/스트로크 불변.
- BuildQuestDetailPanel L1031: 동일 — _intelPanel 480×888 @1392,109 나머지 불변.
- ⚠UTK 백드롭블러(background-blur) 미지원 → **BACKGROUND_BLUR는 알파 0.80 근사로 대체, 주석 명시**
  (GlassPanel 선례와 동일 취급 — Theme.uss GlassPanel 계열과 같은 반투명 패널 패밀리).
- CloseButton L395-411: 32×32 + fill #161B22@0.80 + r16(구 PanelSub r4) — Figma 진실치.
  단, 본 창은 Frameless 크롬(타이틀바 숨김)이라 런타임 노출은 없으며 Standard 복귀 시 정합(USS 전역 28×28 대신 창 로컬 오버라이드).

### ② 플레이어 현재지 핀 — WorldMapWindowUTK.cs
- **선존 실측**: 핀 자체는 [124차-ImplB]에서 이미 신설돼 있었음(PinBase/도트/링/라벨 + WorldU/WorldV 위치식 +
  ResolveTerritoryAt 라벨 + 250ms PollTick 갱신). 본 라운드는 **2026-10-26 재실측 진실치 정렬**을 수행:
  - PinBase 38→**38.4×38.4**(L279-280), 내부 원 14→**14.4** r7→**7.2**(L296-298),
    외부 펄스 링 53→**52.8**(오프셋 -7.5→-7.2, r26.5→26.4)(L303-306),
    PinLabel fontSize 13.2→**15** + 고정 h=**24.6** MiddleCenter(패딩 수직 0)(L316-319),
    앵커 translate -19px→**-19.2px**(L269).
  - 라벨 fill #161B22@0.80 / stroke #30363D@0.5 w1.2 / r4.8 / #F0F6FC — 기존 구현이 이미 진실치(무변경).
- **마커 좌표 공식 단일 소스 준수**: 위치식 = 기존 공용 `WorldU/WorldV`(u=0.5+x/3200, left%=u*100,
  top%=(1-v)*100 — BuildTerritoryMarker/TerritoryBuilder 아레나 핀과 동일 산식, UpdatePlayerMarker L1606-1613).
- 라벨 텍스트 = `TerritoryDatabase.ResolveTerritoryAt(플레이어 좌표, 80f)` → 영지명 + " (현재지)"
  (UpdateHeaderAndCompass L1628-1646 — HUDHeader 영지명과 동일 데이터 경로 단일 소스).
- 250ms PollTick 편입(PollTick → UpdatePlayerMarker → UpdateHeaderAndCompass) — 실시간 갱신 계약 유지.
- 창 닫힘 시 숨김: 핀은 _mapCanvas(창 트리 내부) 소속 — 창 Hide(display:none)와 함께 숨김.
  플레이어 부재 시에도 UpdatePlayerMarker가 display None 처리(기존 계약).

### ③ 지점 전송 → 미니맵 경로 — WorldMapWindowUTK.cs ExecuteFastTravel(L1285-1323)
- **기존 동작 실측(보존/통합 판단의 근거)**: 구 핸들러는 `FastTravelSystem.ExecuteFastTravel(_selectedDef.id)` 위임.
  FastTravelSystem.cs 실측(Assets/Scripts/Systems/FastTravelSystem.cs):
  ① 소유(PlayerOwned)+타 영지+골드 충분 가드 ② Ring 비용 5~25G PlayerStats.SpendGold 차감
  ③ 3초 로딩 화면(LoadingManager) ④ TeleportPlayer — TerritoryManager.GetTerritoryCenter로 즉시 텔레포트.
  즉 **기존 버튼 = 유료 즉시 텔레포트**(미소유/무골드 시 무음 거부 — UI 피드백 없음).
- **통합 판단: 보존(위임 유지) + 경로 표시 병행** — 신계약 ①~④를 앞단에 배선하고 기존 위임을 그대로 유지:
  - 미소유 영지에서는 기존 시스템이 무음 거부 → **경로 표시가 실효 피드백**(신계약의 본래 의도).
  - 소유 영지에서는 텔레포트 진행 → 도착 직후 오버레이 도착 판정(24m)으로 경로 자동 소멸(잔상 없음).
  - 전용 창(FastTravelUTK/FastTravelUI)의 텔레포트 진입점은 무변경 — 기능 손실 0.
  - 대안(버튼에서 텔레포트 제거→경로 전용화)은 기존 유료 기능의 계약 변경이라 채택하지 않음(보고 사항).
- 신배선: 선택 없음 → 토스트 안내 `UTKToastService.Show("영지를 먼저 선택하세요 …")`
  (상호작용불가=피드백 필수 계약, 구: 로그만). 선택 있음 →
  ① `Vector3 dest = _selectedDef.worldPosition`(영지 핀 월드좌표 단일 진실원)
  ② `Hide()`(지도가 미니맵을 가리지 않게 닫기 선행)
  ③ `MinimapRouteOverlay.ShowRouteTo(dest, GitHubDark.Accent)` — 색=Figma Line #58A6FF w2.4
     ('경로=목적지 핀 색': 목적지 핀은 선택 시 #58A6FF 렌더 — SetSelected 강조색과 동일)
  ④ 토스트 `경로 표시: {영지명}`(UTKToastService 기존 계약 재사용).

### ④ MinimapRouteOverlay 신설 — Assets/Scripts/UI/Toolkit/MinimapRouteOverlay.cs(204행)
- static API: `ShowRouteTo(Vector3 targetWorldPos, Color color)` / `ShowRouteTo(Vector3)`(기본색 #58A6FF) / `ClearRoute()`.
- **벡터메시 금지 계약(84차) 준수**: generateVisualContent/Mesh 일절 미사용 — 얇은 회전 사각형 세그먼트
  (VisualElement + border-radius 원통형 캡 + `rotate`)의 직선 대시 폴리라인(기본안: dash 14px/gap 10px/두께 2.4px,
  세그먼트 풀링 — 프레임마다 신규 할당 금지, 상한 64).
- **플레이어 중심 크롭 동기**: 월드→미니맵 변환은 MinimapUTK.UpdateMapCrop/FillBlips와 동일 산식
  (px=half+dx/(2·localRadius)·side, py=half−dz/(2·localRadius)·side — +x=우, +z=북=상).
  MinimapUTK.Refresh() 말단 훅(**1곳**, L442-447)에서 리프레시마다 재계산 → 플레이어 이동 추종.
- 자동 숨김: 경로 비활성/플레이어 부재/프레임 레이아웃 0 → 세그먼트 숨김(상태 유지 — 복귀 시 재표시).
  도착 판정(플레이어↔목적지 XZ 24m 이내) → 자동 ClearRoute.
- 호스트는 MapFrame 직속(마커 위), pickingMode Ignore — 미니맵 우클릭 이동명령 투과 보장,
  원형 클립은 MapFrame overflow Hidden이 담당.
- **MinimapUTK 자체 변경은 훅 1곳뿐**(최소 변경 계약).

## 2. 테스트 갱신 — WorldMapFigma124Tests.cs

- `Figma124_ImplB_FastTravel_Wiring` 갱신(L220-235): 기존 위임 보존 단언 유지 + 신계약 배선 4점
  (토스트 안내/dest 산식/ShowRouteTo/경로 토스트) 추가.
- 신설 4개:
  - `Figma125_38_Panels_FluentGlass_161B22A080`(L238): 좌/우 패널 #161B22@0.80 배경 + BACKGROUND_BLUR 근사 주석
    + CloseButton 32×32 글래스.
  - `Figma125_38_PlayerPin_RemeasuredTruth`(L252): 38.4/14.4/52.8/24.6/15px/앵커-19.2px + WorldU·WorldV
    단일 소스(빌드 마커·아레나 핀 공용 산식) + ResolveTerritoryAt 라벨 + PollTick 배선.
  - `Figma125_38_MinimapRouteOverlay_SourceContract`(L274): API 시그니처/크롭 동기 산식/기본색 #58A6FF/
    회전 사각형 렌더 + **벡터메시 금지 부재 단언**(generateVisualContent/new Mesh/MeshGenerationContext).
  - `Figma125_38_MinimapUTK_RouteHook_MinimalChange`(L297): 훅 1곳 배선 단언.
- 기존 Figma124 계약 테스트 전수 무손상 확인(핀 구조/선택/우패널/툴팁/폐기 단언 — 구문 그대로 통과 조건 유지).
- WorldMapSceneWiringTests 불영향 확인(Ensure 배선/M키 폴링 단언은 변경 무관 — grep 실측).
- QAPROGRESS.md 수정 없음.

## 3. 셀프감사

1. **마커 좌표 공식 단일 소스**: 플레이어 핀 = `WorldU/WorldV` 공용 산식(BuildTerritoryMarker·AddArenaMarker와
   동일 함수 호출 — 신산식 신설 0). 미니맵 경로 세그먼트 = MinimapUTK UpdateMapCrop/FillBlips와 동일 산식
   (오버레이 파일 내 복제 주석 명시 — MinimapUTK private 필드 접근 회피용 복제이며 수식 일치를 테스트가 단언).
2. **벡터메시 미사용**: WorldMapWindowUTK/MinimapUTK/MinimapRouteOverlay 전체에서 generateVisualContent/
   Mesh API 0회(테스트 부재 단언 포함). 핀·경로 전부 border-radius/rotate 요소.
3. **기존 동작 실측 결과(Fast Travel)**: 유료 즉시 텔레포트(위 1③) — 보존 통합으로 계약 무손상,
   미소유 영지 무음 거부 문제를 경로 표시+토스트로 보완.
4. **변경 파일:행 목록**:
   - Assets/Scripts/UI/Toolkit/WorldMapWindowUTK.cs — L261-271(핀 주석/앵커), L279-280, L296-298,
     L303-306, L316-319(핀 재실측), L395-411(CloseButton), L638(좌패널 글래스), L1031(우패널 글래스),
     L1285-1323(ExecuteFastTravel 신계약)
   - Assets/Scripts/UI/Toolkit/MinimapUTK.cs — L442-447(훅 1곳)
   - Assets/Scripts/UI/Toolkit/MinimapRouteOverlay.cs — 신설 204행
   - Assets/Scripts/Tests/EditMode/WorldMapFigma124Tests.cs — L220-235 갱신, L238-302 신설 4테스트
   - docs/125_38_WORLDMAP_FLUENT_PINS_ROUTE.md — 본 문서(신설)
5. **컴파일/테스트 실행**: 부모 사후 단일 패스 몫(Unity 실행 금지 계약 준수 — 본 세션 Unity 미실행).
   정적 자감: 신규 API 시그니처/네임스페이스(ProjectName.UI.Toolkit 동일 어셈블리)/using 점검 완료.

## 4. 남은 위험

1. **QuestSlot/영지 핀(TerritoryMarker·ArenaMarker) 미재실측**: 본 라운드 진실치는 좌우 패널+플레이어 핀+경로선.
   영지 핀 PinLabel(13.2px)·QuestSlot(408×120.8)은 신실측 대상 외 — 다음 재실측 라운드에서 95:188 자식 노드
   전수 정렬 권고(QuestSlot r9.6 활성/비활성 스펙은 기존 구현과 진실치가 동일해 무변경 판단).
2. **BACKGROUND_BLUR 근사 한계**: 알파 0.80만으로는 실제 백드롭 블러 없음(글래스 질감 절반).
   URT UI Toolkit 백드롭블러 지원 시 패널 2곳에 블러 추가 필요(주석에 계약 기록).
3. **CloseButton 런타임 노출 없음**: Frameless 크롬에서 타이틀바 숨김 — 스타일은 Standard 복귀 시 유효.
   Figma상 CloseButton은 패널 노드 소속이므로, 패널 내 CloseButton 신설 여부는 별도 승인 항목.
4. **HUDHeader fill**: 진실치 #0D1117@0.50 대비 현 구현 무배경(투명) — 본 라운드 작업 범위 외(미변경),
   다음 라운드 정렬 후보.
5. **경로 직선 한계**: 기본안=직선 대시 — 장애물 회피/웨이포인트 경로는 미지원(작업 지시 기본안 준수).
   미니맵 원 밖 목적지는 원형 클립으로 절단 표기(설계대로 — 방위 힌트).
6. **미니맵 방위 계약 인계**: 경로 세그먼트는 유닛 블립(FillBlips)과 동일 north=up 산식 — MinimapUTK 내
   "terrain-dependent 마커 보류" 캐비앳은 지형 정렬 미확보 건이며, 블립·경로가 공유하는 크롭 수식
   (UpdateMapCrop 보완 top 오프셋) 기준으로는 상쇄 완료. 크롭 자체가 반전되면 블립과 함께 영향(기존 승인 상태와 동일 리스크).
7. **텔레포트 병행 UX**: 소유 영지에서 지점 전송 시 '경로 표시' 토스트 후 3초 뒤 텔레포트 완료 —
   경로가 도착 판정으로 즉시 소멸(의도된 통합 동작이나, 경로 지향 사용자에게는 텔레포트가 먼저 체감될 수 있음).