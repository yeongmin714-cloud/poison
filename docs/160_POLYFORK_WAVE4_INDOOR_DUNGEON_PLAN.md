# 160차: Polyfork 실내 보강 4차(24종) + 던전 소품 패스(5종 승인 배치) 계획서

> **작성일**: 2026-11-09 · 159차 후속 · 사용자 지시: "진행하고 다른 후보들도 뽑아줘 / 던전도 던전에 어울리는 가구로"
> **전수 감사**: 신규 24 GLB 무결(magic/ver2) — 누적 수용 **94종**(CREDITS 94라인)
> **라이선스**: CC BY 4.0 — CREDITS.md 유지

## 1. 실내 배치 24종 (ApplyCastle/ApplyBuilding 가산 — 교체 0)

| id | 배치 |
|---|---|
| dragonfire-brazier-6bc2ce | 로비 왕좌 뒤 중앙(z0*0.46+1.8, night) |
| velvet-mat-tile-0ccce3 | 로비 왕좌 앞 발판(z0*0.46−0.8, 142tri 평면 th 0) |
| campaign-map-scroll-d4a382 | 로비 테이블 위(y 0.76 — candle과 0.5m 오프셋) |
| harpsichord-ac1869 | 로비 서측 연주 코너(harp 동측과 대칭 — −(x0−3.4), z0*0.15, yaw 90) |
| dressing-mirror-7c0871 | 침실 — 서랍장 동측 옆 |
| framed-wall-art-d2c8a2 | 침실 북벽(y 1.8 마운트 — localPos y=1.8, th 0) |
| battle-map-tile-69e0b1 | 병영 바닥(전술 지판 — rack 라인 앞) |
| commander-s-camp-stool-d7fa4c | 병영(barX+1.5, barZ+2.4) |
| smelting-furnace-3209f1 | crafthouse hero(화로 북측 — 1870tri 자연 크기) |
| tool-rack-f77b81 | crafthouse 서벽 |
| laced-hide-stretching-rack-8152a1 | crafthouse 서벽(tool rack 옆 1.6m) |
| controllable-food-smoker-aac206 | tavern 주방 코너(cauldron 옆 1.6m) |
| saloon-upright-piano-4b9c64 | tavern 북벽(back-bar 동측 끝 옆) |
| whiskey-bottle-and-glass-fa24b4 | tavern 테이블 위(y 0.76) |
| clay-wine-jug-b4ebd3 | tavern 테이블 위(다른 테이블) |
| legendary-chest 유지 | (159차 배치 완료 — 무수정) |
| concert-pedal-harp-eb1a21 | restaurant 동측 코너 |
| ventilated-hide-food-larder-f53174 | 창고 벽(선반 라인 사이) |
| saddle-quern-food-mill-8e3657 | 창고 바닥(sack 근처) |
| flame-brazier-bbf74a | 창고 중앙(조명 보조 메시 — night) |
| milk-churn-stand-130fe2 | barn(churn 라인 뒤) |
| basil-pot-125949 | house 식탁 위(herb pot 옆 0.5m) |
| travelling-trunk-2616ab | house 입구(침대 대각) |
| taheta-stone-basin-336021 | shop 진열(바구니 옆) |

## 2. 던전 소품 패스 (CaveInteriorBuilder — 사용자 승인)

**원칙**: DungeonAssetCatalog/Models/Dungeon 카탈로그 계약 **무접촉**(147 카운트 테스트 보호) — Polyfork GLB는 Models/Polyfork 경로 그대로 PlacePolyfork 사용. 던전 5층/룸 구조는 CaveInteriorBuilder 존 구조 준수(에셋벽 3m·tierScale 폐기 계약 유지 — 기하 무관 소품만).

**배치 (룸당 결정론 — rng 시드 파생, 콜라이더 자동 제거)**:
- stone-sarcophagus-cc96a4 — 묘실 코너(룸당 최다 1)
- barred-cell-door-571e9c — 벽 라인 부착(감옥 느낌 — 룸당 1)
- ossuary-skull-shelf-a8fdf0 — 벽 라인(룸당 1)
- iron-bound-door-e0e953 — 통로 출구 옆(룸당 1)
- mimic-chest-57e440 — GemChest 옆(룸당 1 — 전리품 함정 연출)

**구현**: CaveInteriorBuilder에 `CreateDungeonProps(zone, rng)` 신설 — CreateCrates(zone) 호출부 뒤 1줄 훅(룸 빌드 경로). 존당 최대 3~4프롭(과밀 배제 계약). 배치 좌표는 존 bounds에서 유도(벽 마진 0.5m). [160차 P2] 마커.

## 3. 테스트 갱신

- PolyforkFurnishingContractTests: GLB 70→**94**, credit 70→**94**, PolyforkIds +24, 배선 하한 126→**실측(FurnishingPass 126+신규)**, DungeonReserveIds 5종 → **던전 배선으로 전환** — T10(미배선 역어서션) 폐기/갱신 → **T11 신설: CaveInteriorBuilder에 5종 배선 계약 + [160차 P2] 마커**
- 던전 배치는 FurnishingPass 외부 파일 — T4(실내 배선) 계속 제외 유지(이제 CaveInteriorBuilder에서 배선 — T11이 담당)

## 4. QA

컴파일 CS 0 + EditMode baseline 1:1 + 투어 DONE + 던전 캡처(있으면 run_cave 스크립트/기존 던전 캡처 선례 확인) + Play 별도 승인
