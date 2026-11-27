# 161차: Polyfork 야외 건축물 패스 + 실내 hero 4종 계획서

> **작성일**: 2026-11-09 · 160차 후속 · 사용자 지시: "진행하고 야외 건축물로 쓸 수 있는 것도 몇 개 더 뽑아줘"
> **전수 감사**: 신규 18 GLB 무결(magic/ver2) — 누적 수용 **112종**

## 0. 핵심 신설 — PlacePolyforkSolid

야외 건축물은 플레이어 통과 방지를 위해 **콜라이더 필요**(실내 계약=렌더러 전용과 다름):
- `InteriorPropCatalog.PlacePolyforkSolid(parent, polyforkId, name, localPos, yawDeg, targetHeightM)` — 기존 Place 복제 + 렌더러 bounds 기반 **BoxCollider 1개 부여**(센터/사이즈 = 전체 renderer bounds; 콜라이더 스트립 생략)

## 1. 실내 hero 4종 (InteriorFurnishingPass — [161차 P1])

- concert-grand-piano-85c5b1(1427tri) — 로비 동측 연주 코너(harp/harpsichord 라인 확장 — x0−3.4+1.6f? harp(x0−3.4) 옆 z −2.2f, yaw −90, th 0)
- baroque-panelled-wall-8658ed(1656tri) — 로비 북벽(알현실 뒷벽 — z0+0.15f, northHalfWidth*0.5f 중심, yaw 180, th 0)
- overflowing-treasure-chest-3d6ee0(1397tri) — 왕좌 뒤 보물 중심(loot sacks 라인 뒤 z0*0.46+2.6f, th 0)
- hand-lantern-cdbe1a(1224tri) — 성 창고(basket 라인 근처 바닥, th 0)

## 2. 야외 구조물 패스 (신규 PolyforkOutdoorStructures.cs — [161차 P2])

**훅**: IdyllicDecoPlacer.PlaceAll 호출부 뒤(또는 내부 말미) 1줄 — `PolyforkOutdoorStructures.PlaceAll(center, parent)` 패턴 복제. y는 기존 GetHeightAt 경로 재사용(IdyllicDecoPlacer 선례 — GROUND_BASE+GetHeightAt). 결정론(고정 오프셋 — 엔진 난수 0).

**배치 10종** (스폰 좌표 기준 고정 링/오프셋 — 마을 중심 회피, 서로 8m+ 이격, SpawnArea 제외 반경 존중):

| id | 용도 | 대략 위치(스폰 기준) |
|---|---|---|
| four-wheel-wagon-adb946 | 짐수레 | 스폰 북동 24m |
| wheelbarrow-13f422 | 수레 | 스폰 북동 30m |
| blacksmith-forge-09fa85 | 대장간 | 스폰 북서 28m |
| water-mill-b31972 | 물레방아 | 호수/하천 방향 남서 40m |
| windmill-3aad49 | 풍차 | 남동 60m (스카이라인) |
| livery-stable-c01ee0 | 마구간 | 북 36m |
| open-stable-dbf2d5 | 개방 축사 | 북 44m |
| mine-headframe-735cf4 | 광산 양새 | 서 70m (던전 입구 연상) |
| ancestral-shrine-de7ed2 | 사당 | 동 32m |
| temple-bell-tower-745387 | 종탑 | 남 55m |

- 전부 PlacePolyforkSolid(targetHeight 0 — 실물 스케일). 콜라이더 1개/구조물.
- IdyllicDecoPlacer SPAWN_EXCLUDE 6m 존중 — 최소 24m 이격.

## 3. 던전 가산 — rope-bridge-span-159f8c

CaveInteriorBuilder CreateDungeonProps에 가산 — 룸당 1(다리 — 통로 위/옆, th 0). [161차 P3] 마커.

## 4. 수용만 (미배치 reserve)

- opera-box-tier-d7fdd1 — 대연회장 전용 보류
- pante-n-chapel-62dccd — 이계 공동묘지 사이클 보류
(기존 cemetery-lantern-post와 동일 그룹)

## 5. 테스트

- PolyforkIds 94→**112**, CREDITS 94→**112**, T3 하한 149→**153**(실측)
- 실내 배선(T4) 제외 집합 확장: 던전 5 + 야외 10 + 로프다리 1 + reserve 2 + 기존 1
- **T12 신설**: PolyforkOutdoorStructures 소스계약(10종 id 각 ≥1 + PlacePolyforkSolid 정의 + [161차 P2] 마커)
- **T13 신설**: PlacePolyforkSolid가 콜라이더 스트립 생략+BoxCollider 부여 소스계약(InteriorPropCatalog)
- T11 갱신: CaveBuilder 던전 6종(+로프다리)
