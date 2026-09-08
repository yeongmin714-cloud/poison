# 미니맵에 지형 렌더링 — 계획 (MM-TERRAIN)

작성: 2026-09-08 | 상태: **계획 대기** (사용자 "계획을 세워줘" → 실행은 별도 지시 대기) | 선행: 현재 미니맵=UI 프레임만, 지형 없음

## 배경·현황 분석 (코드 확인 완료)
- **MinimapUI.cs**(Assets/Scripts/UI/Functions/MinimapUI.cs): IMGUI(OnGUI) 원형 미니맵. `_mapTexture`(Texture2D)가 있으면 `DrawMinimapBackground()`에서 `GUI.DrawTexture(_minimapRect, _mapTexture, ScaleToFit, true)`로 그리는 구조 — **현재 `_mapTexture=null`이라 검은 배경만**. `SetMapTexture(Texture2D)`/`SetMapScale(float)` 공개 API 이미 존재. 플레이어 마커는 `WorldToMinimapLocal = worldPos * _mapScale`(L436, 현재 0.001 → 사실상 고정 위치).
- **핵심(비용 0 재사용 지점)**: `TerrainSplatBaker.BakeWorldSplat(res, seed, nationTextures)`가 **이미 월드 전체(2000×2000, 중심 0)를 5국가 합성 마스터 텍스처**로 생성함 — 절벽(L3)/흙길(L4)/수변·이끼(L5)/꽃밭·융단 마스크 + **릴리프 대비(골짜기×0.85·능선×1.06)** + **호수 심수색 깊이 비례**까지 전부 반영.
- `TerrainTextureApplier.ApplyWorldSplatToGround()`(L783)이 이 텍스처를 지형 메시에 이미 적용 중이며, **`WorldSplatCachePath()` 디스크 캐시(시드+해상도 키)** 로 Play마다 재베이크 ~390초를 피함. `_splatResolution=2048`, `_splatSeed=20260902`.

→ **결론**: `ApplyWorldSplatToGround()`가 만드는 `worldSplat` Texture2D(2048×2048, 지형 전체 색)를 미니맵에 그대로 주입하면 **추가 베이크 0, 이미 게임에 보이는 지형과 100% 일치**하는 지형 미니맵 완성.

## 해결해야 할 2가지
1. **worldSplat 접근성**: 현재 `ApplyWorldSplatToGround()` 내부 로컬 변수 → MinimapUI가 못 읽음. 공개 저장 지점 필요.
2. **좌표 정합**: `_mapScale`(0.001)이 월드 크기(2000m)와 안 맞음 → 미니맵 지름(220px)에 맞춰 `scale = minimapDiameter / WORLD_SIZE` 로 조정해야 마커가 지형 위에 정확히 놓임.

## Phase 구조

### Phase M1 — worldSplat 공개 저장 (TerrainSplatBaker.cs / TerrainTextureApplier.cs)
- `TerrainSplatBaker`에 `public static Texture2D LastWorldSplat;` 추가 (또는 TerrainTextureApplier에 `public static Texture2D WorldSplatTexture;`).
- `ApplyWorldSplatToGround()`에서 worldSplat 확보 직후(캐시 로드 or 베이크 후) 해당 static에 저장. 캐시 로드면 그 reason 그대로 저장.
- 결과: 미니맵을 포함한 **어떤 시스템에서도 최신 지형 스플랫을 읽을 수 있게 됨** (Systems→UI 참조 순환 규약 준수: TerrainSplatBaker는 Systems, 읽는 쪽 MinimapUI도 Systems 테러가 아니라 UI지만 API는 static이라 참조 방향 문제 없음 — 확인 필요).

### Phase M2 — MinimapUI 지형 렌더 (MinimapUI.cs)
- `Start()`에서 `TerrainSplatBaker.LastWorldSplat`(또는 공개 Getter)를 `_mapTexture`로 세팅. `SetMapTexture` 사용.
- `_mapScale` 재계산: `_mapScale = _minimapDiameter / WORLD_SIZE` (WORLD_SIZE 상수 확인 — BakeWorldSplat에서 2000). DrawMinimapBackground의 GUI.DrawTexture(ScaleToFit)와 중복 정렬될 수 있으니 ScaleMode 동기화 검토.
- 좌표축: 미니맵 원형 중심=월드 원점. 텍스처 UV(0,0)=월드(-1000,-1000) 맵핑 확인 — BakeWorldSplat은 `u=(x/r-0.5)`, `v=(1-(z...) )` 형태이므로 WorldToMinimapLocal과 부호(좌우반전) 정합 필수.
- 플레이어 마커: scale만 맞추면 기존 `WorldToMinimapLocal`이 지형 위에 정확히 둠. (구현 시 부호 검증: +z=북, 화면 위.)

### Phase M3 — (선택) 영지·퀘스트·랜드마크 마커
- `QuestMarkerSystem`(이미 존재, L1 검색에서 확인)의 마커 좌표를 미니맵 로컬로 변환해 겹침. 지형 1차 완성 후 후속으로 옵션화.

## API/기존 시스템 충돌 최소화
- MinimapUI.OnGUI 구조·온도/소음/시간 게이지 **무수정** — 지형 텍스처만 주입.
- BakeWorldSplat·ApplyWorldSplatToGround 시그니처/캐시 로직 **무수정** — static 저장 1~2줄만 추가.
- 결정론 유지(기존 텍스처 그대로 사용).

## 검증 (구현 시)
1. 배치컴파일 error CS=0 선행 필수.
2. Play에서 미니맵에 ①방위별 색 경계 ②호수 심수색으로 보이는 점 ③흙길/절벽 흔적 ④플레이어 마커가 지형 위 정확 위치(이동 시 정합) 확인.
3. Editor.log: `[TerrainTextureApplier] Phase Y1` 성공 + 캐시 로드/베이크 로그 + Minimap 텍스처 주입 로그.

## 실행 배열
- M1(저장) → M2(렌더) 단일 파일 순차. 코드 에이전트 1기 위임 + QA 검토 + 배치컴파일. 이후 3곳 저장(메모리/QAPROGRESS/ROADMAP+push).