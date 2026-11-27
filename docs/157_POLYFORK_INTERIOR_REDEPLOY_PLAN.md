# 157차: Polyfork 에셋 수용 + 실내씬 재배치 계획서

> **작성일**: 2026-11-09
> **출처**: polyfork.dev (실존 검증 완료 — 저폴리 GLB 라이브러리, 전 모델 무료, 메시 CC BY 4.0)
> **전수 감사(147차 선례)**: GLB 13종 헤더 감사 완료 — magic OK·ver2·메시 1~13개·**머티리얼 1개·텍스처 0장**(정점컬러 — 원목 원색 계약 정합)·Draco/외부 의존 없음
> **스케일 계약**: Y-up·미터·y=0 착지·소품 1.8m 휴머노이드 — 캐릭터 1.8m 고정 계약 호환
> **라이선스**: CC BY 4.0(credit 1줄 의무) — CREDITS.md 수집 필수

## 0. 원칙

- **가산+선별 교체** — 절차 원목 가구 팩토리(BuildWoodTable류) 자체는 유지, Polyfork GLB가 더 좋은 곳만 교체 + 액센트 가산
- 배치는 `InteriorPropCatalog.Place(parent, candidatePaths, name, localPos, yaw, targetHeightM)` 재사용 — 콜라이더 제거(렌더러 전용)·높이 정규화 내장
- 존 규칙 준수(Craft/Storage/Barracks/Alchemy/Entry — 무번호2 실측 결함 재발 방지) + 기능 앵커 회피(Workbench(−40,25)·StorageShelf_2(40,25)·WeaponStand_0(−40,−25)·AlchemyTable(40,−25)·LordBed(0,29))
- 조명 계약: 성 내 Light 강도 ≤2.9·반경 ≤7 — 신규 메시 조명 배치 시 기존 PlaceCeilingLamp의 Light 처리 실측 후 계약 유지
- 84차 벡터메시 금지·665 — 무관(메시 GLB)

## 1. 에셋 13종 (ID → 용도 → 배치 대상)

| Polyfork ID | tri | 배치 |
|---|---|---|
| timber-trestle-table-024d88 | 540 | 로비·tavern·restaurant 테이블 교체 |
| alchemy-workbench-117b73 | 658 | 연금 탁자2·제작소 작업대 교체 |
| scroll-rack-f465dd | 774 | 창고 선반 보강 1 |
| chained-book-lectern-381597 | 724 | church 강단 1 |
| weapon-rack-082d7f | 630 | 병영 벽 라인 2 |
| timber-crate-ba0739 | 588 | 창고·crafthouse·barn 상자 |
| iron-banded-strongbox-698383 | 690 | 창고 보물 상자 2 |
| oak-barrel-b65311 | 666 | tavern 2·barn 2 |
| iron-chandelier-1337dc | 754 | 로비 천장등 2 교체 (origin_y=1.725 — y=0 배치) |
| wall-torch-bracket-f5ad14 | 704 | 벽 횔도 — localPos.y=2.1 (targetHeight 0 — NormalizeHeight 스킵) |
| standing-brazier-c9e4ee | 680 | 제작소·입구 화로 |
| candle-spike-cluster-d7c566 | 661 | 테이블 위 소품 (tavern/restaurant/연금) |
| looted-gilded-throne-008fc3 | 840 | 로비 북측 왕좌 1 (dragons-lair 키트 — 팔레트 혼용 1개 한정) |

## 2. 수용

- `Assets/Resources/Models/Polyfork/{id}.glb` 13종 복사 — Resources.Load 경로 `Models/Polyfork/{id}` (Place 메커니즘 동일 — Restaurant GLB 선례)
- `Assets/Resources/Models/Polyfork/CREDITS.md` — 13 credit 라인(JSON credit 필드에서 생성, /home/korea/tmp/polyfork/asset-*.json)
- `.meta`는 Unity 임포트 자동 생성(수기 작성 금지 — 113차)

## 3. 재배치 맵

### ApplyCastle (성)
- **로비**: 원목 탁자4 → Trestle Table 4 (동일 tablePos·targetHeight=기존 탁자 높이 실측) + 스툴 유지 + 촛불 테이블 위 + 천장등2 → Iron Chandelier + **왕좌 1** (팔각 로비 중심 북측, 남향 180°, 소파/기능앵커 회피)
- **침실**: 침대2·장롱2 유지(침대 GLB는 Polyfork 부재 — 절차 침대 유지) + 촛불 1(장롱 위) + 벽 횔도 2
- **제작소**: 화로·작업대 유지 + Standing Brazier 1 + 상자스택 → Timber Crate 2 + 벽 횔도 1
- **창고**: 선반4 유지 + 상자 → Timber Crate 4 + Strongbox 2 + Scroll Rack 1
- **병영**: 침대 유지 + Weapon Rack 2(벽 라인) + cabinet 유지
- **연금**: StationTable×2 → Alchemy Workbench×2 + potion 유지 + 촛불 1
- **입구**: 행거 유지 + Standing Brazier 1 + 벽 횔도 2

### ApplyBuilding (건물 7종)
- tavern: 테이블 → Trestle Table + Oak Barrel 2 + 촛불
- restaurant: 테이블 → Trestle Table + 촛불
- church: 벤치 유지 + Chained Book Lectern 1
- shop: 카운터 유지 + Timber Crate 2
- crafthouse: Standing Brazier 1
- house: 촛불 1 + Timber Crate 1
- barn: Oak Barrel 2 + Timber Crate 2

## 4. 코드 변경점

1. `InteriorPropCatalog.cs` — `PlacePolyfork(Transform parent, string polyforkId, string name, Vector3 localPos, float yawDeg, float targetHeightM)` 추가 (내부 candidatePaths=["Models/Polyfork/{id}"] — 기존 Place 재사용)
2. `InteriorFurnishingPass.cs` — §3 맵 적용 (교체점은 기존 좌표/시드 유지, 가산점은 존 규칙+앵커 회피 좌표)
3. 벽 횔도: targetHeightM=0 + localPos.y=2.1 — NormalizeHeight 스킵 경로 확인

## 5. 테스트

- `InteriorQualityBaselineTests` — 소멸 예정 문자열 있으면 갱신(94차 — 구 소스계약이 Polyfork PlacePolyfork 호출을 부정 어서션하면 갱신)
- 신규 `PolyforkFurnishingContractTests`: ①13 GLB Resources 존재(T5 선례) ②CREDITS.md 존재+13 credit 라인 ③ApplyCastle/ApplyBuilding PlacePolyfork 소스계약(호출 수 하한)

## 6. QA

- 컴파일 error CS 0 + EditMode 전수(baseline 10건 1:1 — 신규실패 빈 집합)
- 투어 캡처 `run_indoor_tour.sh`(14엔트리) 재생성 + PIL 픽셀 통계 + 탑다운 vision 판독(65° 뷰는 프레임 협소 — 탑다운 신뢰)
- Play 체감은 별도 승인
