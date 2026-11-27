# 계획서: 영지 실내 타일 씬 통일 (무번호 32 — 2026-11-13)

**사용자 지시**: "같은 씬 내부에서 방마다/구역마다 타일이 나뉘면 통일성이 없다" — ①영지씬 실내 타일 통일 ②던전씬 내부 타일 = 가장 많은 한 타일로 통일. 카펫/러그 오버레이 유지 확인.

## 조사 결과

- **던전**: 156차에서 이미 흙 단일화 완료(CaveInteriorBuilder.CreateCellFloor → CreateDirtFloor, 지하 5층 전부 동일, 톤 스트립/Concrate GLB 폐기 계약). 유일한 변주 = 158차 P3-C 스폰→포탈 돌판 오버레이(BFS 안내 기능 — 유지).
- **영지 실내 8빌더** 바닥 제각각: 성(국가별 GenerateCastleFloor*)/집(GenerateHouseFloor)/공방(GenerateCraftHouseFloor)/교회(GenerateChurchFloor)/상점(GenerateShopFloor)/외양간(평면 목재색)/주점·식당(평면 갈색).
- **제외**: 아레나(국가색 모래 정체성)·투기장 피트·이계 공동묘지(잿빛 흙)·던전(위).

## 구현

| 항목 | 내용 | 파일 |
|---|---|---|
| 공통 석타일 단일 소스 | GetCommonInteriorFloorMat()(무번호 15 팔레트 승계, 국가 키 폐기) + CreateCommonInteriorFloorMat(w,d,owner)(공유 캐시 복제 + mainTextureScale=(w,d) — UV 0..1 쿼드 1m 타일 정합) | InteriorQualityBaseline.cs |
| 8빌더 교체 | 성/집/공방/교회/상점/외양간/주점/식당 바닥 전부 공통 석타일 — 구 전용 바닥 텍스처 생성·머티리얼·트래커 등록 제거 | PlayerCastle·House·CraftHouse(UI)·Church(UI)·Shop(UI)·Barn·Tavern·Restaurant |
| 성 국가 계약 | 바닥만 폐기 — 벽 텍스처(국가별 GenerateCastleWall*) 계약 유지(국가색=벽·깃발·장식) | PlayerCastleInteriorBuilder.cs |
| 오버레이 정합 | InteriorFurnishingPass castleStone_{nationStyle} → 공통 위임(카펫/러그 등 기능 오버레이 무수정) | InteriorFurnishingPass.cs |

## 검증

1. 계약테스트 InteriorTileUnificationTests 4건(헬퍼 잠금/오버레이 위임/8빌더 교체+구 식별자 부재/던전·이계 제외 확정)
2. 컴파일 error CS 0 → EditMode 전수 + baseline 10건 1:1
3. Play(사용자): 영지 전 실내 동일 석타일 체감 + 타일 밀도 1m 정합
