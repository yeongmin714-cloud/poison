# 147차 계획 — 에셋 기반 실내/던전/아레나 리빌딩 (DUNGEON·Farm·Restaurant 팩)

작성일: 2026-11-05 · 상태: **계획(미승인) — Phase별 진행 승인 필요**

## P0 실측 (2026-11-05 완료 — 계획 근거)

- **DUNGEON EXPORT.zip**: 모델 320종 × fbx/glb/obj/mtl 4포맷 + Dungeon.blend + `Dungeon Megapack.unitypackage` + 아틀라스.
  GLB 320개 전수 파싱 실측 — **삼각형 중앙값 314(최대 3,648) 저폴리**, 모델당 텍스처 1장 임베드(320/320), **실스케일**(Wall 4×3×1.03m, Floor 타일 2×2×0.13m, Chair 0.95m, 최대 높이 5m) — 145차 마을 GLB(1unit 정규화)와 달리 **스케일 계수 불필요, 접지만 검증**.
  벽 조각 높이 3m — 현행 cave BASE_WALL_HEIGHT 3.2와 근접.
- **Farm Pack.zip**: 304종(SM_ 접두어 — 헛간/작물/도구, fbx+glb). **Restourant.zip**: 1,020종(fbx+glb, RG POLY 라이선스 — 가구/음식 소품).
- **현재 체계 실측**: `IndoorFurnitureCatalog`에 **GLB-우선/프리미티브-폴백 패턴 이미 존재**(CreateBed 계열 — "GLB 실패 시 프리미티브 폴백" 계약) → 가구 에셋화는 카탈로그 항목 확장으로 진입 가능. 벽/바닥은 빌더별 절차 생성(백플레인 박스 + IndoorMaterialFactory/Resources/Indoor 타일링 — **불변 계약**).

---

## Phase A — 던전 팩 수용 파이프라인 (공통 기반, 선행 필수)

- **A1. GLB 전수 파서 검증**: zip→프로젝트 복사 후 145차 village5_glb_audit 선례로 320종 전수(glTF 정상성/메시/재질/이미지/치수) — `TestOutput/147_dungeon_glb_audit.txt`.
- **A2. 반영 경로 확정**: RuntimeModelLoader 별칭 맵 vs Resources 직로드 — 기존 GLB 수용 선례(RuntimeModelLoader 지연 로드, 145차 IdyllicPrefabs) 실측 후 단일 경로 채택. 폴더: `Assets/Resources/`하위 기존 GLB 관례 준용.
- **A3. 텍스처 계약 적용**: 원본해상도·BC7·q100 전 에셋(141차 계약), normal=NormalMap 타입. GLB 임베드=포프로 계약 유지 — 별도 추출/분리 금지.
- **A4. 별칭/매핑 테이블**: 모델명(id)↔카탈로그/빌더 사용처 매핑 상수 1파일(하드박제 — Random 0건).
- **검증**: 파서 감사 0 에러 + 컴파일 CS 0 + 소스계약테스트.

## Phase B — 던전 테스트씬(Test_21 동굴/던전) 리빌딩

- **B0. 방 크기 고정 계약 (사용자 지시 2026-11-05 확정)**: 던전 방은 **난이도(tier)와 무관하게 동일 크기**. `tierScale=1+0.25×(tier−1)` 폐기(기하 적용 지점 전부 스케일 1 고정 — 다각형/벽 높이/캡/암석링/포탈/스폰 포함). tier는 **기하 파라미터에서 제외**하고 난이도 파라미터(적/보물 밀도 등, 미래 확장용)로만 존속 — CaveLevelDirector API 시그니처 보존(SyncContext 유지). 부수 효과: ①147차 리스크 ②(에셋 벽 3m vs 3.2m×티어) 자동 해소 — 에셋 벽 3m 고정 + 벽두께/캡으로 0.2m 흡수 ②기존 zone 콘텐츠 무스케일 불일치도 자연 소멸 ③146차 스폰 스케일 동행 코드는 스케일 1 고정으로 사실상 무동작(제거하지 않고 고정값 위임 — 계약테스트 갱신).
- **B1. 벽 교체**: CaveInteriorBuilder 다각형 벽(백플레인 박스) → 던전 Wall 조각(4×3m) 변별 배치 공용화. 변 길이 > 4m는 조각 분할, 미달은 끝단/코너 조각(Wall_EndA·Corner·Wall_T/U/X) 흡수. Wall_Door/Wall_Crack 등 변형은 입구/이벤트 변에 매핑. 벽톱면캡(H1~H5 계약)은 에셋 벽 상단과 대체 정합 재정의.
- **B2. 바닥**: 2안 비교 — ①에셋 Floor 타일(2×2m) 다각형 내부 그리드 배치(여백은 기존 메시) ②기존 다각형 바닥 메시+에셋 텍스처 리스킨. **1안 우선 시안 후 Play 판정**(사용자 취향 확인 지점).
- **B3. 소품 승격**: 횟불→Torch/Candle/SkullCandle, 보석상자→Chest/Treasure, 채굴노드→Rocks/Pebble/Crystal(기존 ResourceNode 상호작용 유지 — 비주얼만 교체, 146차 잔석 패턴 준용), 감옥 Cage/Chain/ Skeleton은 3~5층(P9 2단) 확장 소재로 예약.
- **B4. 불변 계약 유지**: 룸 1개/리빌드 방식/층 파라미터(146차 수리 반영)/천장 금지/탑다운 원칙/캐릭터 1.8m — 전부 무손상.
- **검증**: 계약테스트 + 컴파일 + **사용자 Play 판정(별도 승인)**.

## Phase C — 실내 가구 에셋화 (IndoorFurnitureCatalog 확장)

- **C1. 매핑 정의**: 카탈로그 항목(Table/Chair/Shelf/Counter/Bed 등) ↔ 에셋 id(레스토랑 Armchair/BagFood 1,020종, 던전 Chair/Table/Shelf/Wardrobe/Book, Farm 바구니/도구) — 카탈로그에 GLB 경로 필드 추가(기존 폴백 패턴 확장 — GLB 부재 시 프리미티브 유지 = 기존 계약).
- **C2. 치수 정규화**: 에셋 실측 치수 → 목표 치수 스케일 계수(단일소스 헬퍼), 접지(밑면=바닥) 선례 준용. BoxCollider/Bed 등 상호작용 컴포넌트는 루트에 기존 로직 유지(비주얼만 교체).
- **C3. 순서**: ①레스토랑(카탈로그 가장 큼) ②성 내부 ③크래프트하우스/상점 — Phase별 Play 판정.
- **리스크**: Restourant 789MB — 전量 임포트 금지, 사용 종만 선별(용량/임포트 시간).

## Phase D — 실내 벽/바닥 에셋화 (공용 모듈러)

- **D1. 공용 벽 러너**: 실내 빌더 공용 "에셋 벽 세그먼트 배치" 헬퍼(엣지→4m 조각 분할/회전/접지) — 성(Castle/PlayerCastle)/하우스/상점/투기장 실내가 같은 헬퍼 소비. 기존 백플레인 박스는 **충돌 전용으로 유지**(비주얼 off) — 충돌 계약 무손상.
- **D2. 바닥 모듈러**: 타일 배치 헬퍼(방 폴리곤→타일 그리드) + 기존 머티리얼 폴백(Resources/Indoor 불변 계약 — 에셋 부재 시 기존 경로 그대로).
- **D3. 조명 무드**: IndoorLighting Flat 강제/앰비언트 표 유지 — 에셋 머티리얼이 조명 변화에 정상 반응하는지 Play 실측.
- **검증**: 성 8변형 × 소유분기 전수 시안 → 사용자 Play 3컷(성/상점/투기장).

## Phase E — 아레나 실내/외부 에셋화 (조사 선행)

- **E1. 조사**: ArenaInteriorBuilder/ArenaOutdoorMaterialFactory 현 구성 실측 → 대체 후보 매핑(던전 관중석/울타리? 145차 마을 GLB 46종 재활용? 협곡/투기장 외부용 EA Exterior) — **조사 보고서 먼저 제시 후 계획 확정**(사용자 방향 확인 지점).
- **E2. 리빌딩**: 확정 후 Phase D 헬퍼 재사용. 관전 카메라/듀엔 계약(orthoSize/관전석/시간연동 조명) 무손상.

---

## 셀프감사 (중복·계약 충돌)
- IndoorFurnitureCatalog GLB 폴백 패턴과 충돌 없음 — 확장형.
- Resources/Indoor 불변 계약 — 폴백 유지로 충족.
- 탑다운 천장 금지/벽톱면/1.8m/Random 0건/BC7 텍스처 계약 — 전 Phase 명시 유지.
- 146차 수리(스폰/티어/접지 게이트) 위에서 재기하지 않음 — 베이스로 사용.

## 리스크 / 열린 질문
1. B2 바닥 방식(타일 그리드 vs 리스킨) — 사용자 취향 → 시안 후 Play 판정.
2. ~~에셋 벽 3m vs 현행 3.2m(×tier 1.25=4m)~~ — **해소(B0 방 크기 고정 확정)**: 에셋 벽 3m 고정, 벽두께/캡으로 0.2m 흡수.
3. 레스토랑/농장 팩 중 실제 사용 종 선별 기준(사용자 지명 or 카탈로그 갭 기준).
4. obj/mtl 포맷은 미수용(fbx+glb만) — obj는 중복이라 폐기.
5. unitypackage(메가팩) 임포트 vs GLB 직수용 — 에디터 임포트가 안전하나 프로젝트 GLB 런타임 로드 관례와 충돌 가능 → A2에서 실측 결정.

## 게이트
- Phase A 승인 → A 완료 → B 시안 → 사용자 Play 판정 → C/D/E 순차 승인. Play·커밋은 각 별도 승인.
