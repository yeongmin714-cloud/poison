# 158차: Polyfork 실내 보강 2차 — 27종 추가 수용 + 방별 배치 계획서

> **작성일**: 2026-11-09 · 157차(13종 배치 완료) 후속
> **전수 감사**: GLB 27종 헤더 감사 완료 — magic/ver2 무결·텍스처 0(정점컬러)·exts 0
> **라이선스**: CC BY 4.0 — CREDITS.md에 27라인 추가(누적 40)

## 1. 배치 맵 (27종 전부 사용)

### 성 (ApplyCastle)
- **침실**: 장롱2(Bedroom_Wardrobe_W/E — PlaceFurnit) → **wardrobe-f3b2f2** 교체 + **chest-of-drawers-cbe3e7** 1 가산 + 기존 AddRug(Bedroom_Rug) → **wool-rug-ab2bd1** 교체(동일 좌표/크기 정합) + **table-candle-lantern-a7b5af** 1 가산(서랍장 위)
- **로비**: 왕좌 양옆 **brass-candelabra-89ab76** ×2 + 로비 남측 입구 양옆 **potted-olive-tree-7eee05** ×2 + 북벽 **curtain-panel-ee2ed9** ×2 + **farm-lantern-747dc8** 1(입구 근처)
- **제작소**: 화로 옆 **anvil-1286cb** 1 + **firewood-stack-8a2899** 1
- **창고**: **grain-sack-401d96** ×2 + **woven-storage-basket-983d80** ×2 (선반 사이 바닥)
- **연금**: **library-book-stack-double-ce8d7f** 1 + **an-hourglass-with-sand-ec9f11** 1(탁자 위 y=0.9)
- **병영**: **greatsword-a956f3** ×2 + **bow-9a225d** ×2 (Weapon Rack 벽 라인 사이 — 바닥 대각 기대 세워 배치, yaw 15° 변주)

### 건물 (ApplyBuilding)
- **tavern**: **back-bar-shelf-and-f5f279** 1(북벽 2.6m 바) + **wooden-bench-02e2c8** ×2(테이블 옆) + **bubbling-tripod-cauldron-124e15** 1(주방 코너)
- **restaurant**: **clay-bread-oven-860b06** 1(북벽) + **wooden-bench-02e2c8** ×1 + **grain-sack-401d96** 1
- **house**: **armchair-980c7c** 1 + **potted-olive-tree-7eee05** 1 + **area-rug-5c56e2** 1(중앙)
- **shop**: **woven-storage-basket-983d80** ×2(카운터 옆 진열)
- **church**: **pipe-organ-36e035** 1(북벽 중앙 — 4×8×2m hero, targetHeight 0 자연 스케일) + **brass-candelabra-89ab76** ×2(제단 양옆)
- **crafthouse**: **anvil-1286cb** 1 + **firewood-stack-8a2899** 1
- **barn**: **saddle-on-a-rack-f034cd** 1 + **treasure-chest-373a2e** 1(뚜껑 리그 — 놓인 상태)

## 2. 수용

- `/tmp/polyfork_glb2/` 27 GLB → `Assets/Resources/Models/Polyfork/` (meta 자동 생성 — 수기 금지)
- CREDITS.md에 27라인 append(GLB 내장 asset.copyright 필드에서 추출 — 157차 방식 동일)

## 3. 코드 변경점

1. `InteriorFurnishingPass.cs` — §1 맵 적용(교체: 침실 장롱2·침실러그 / 가산: 나머지 전부). 교체 시 기존 좌표/시드 유지.
2. 스케일: furniture류(wardrobe/bench/armchair/bookcase/organ/oven)는 방 스케일 정합 targetHeight 실측 적용, 소품류(sack/basket/lantern/candelabra/candle)는 자연 크기(targetHeight 0).
3. 조명 계약: 신규 Light 생성 금지(강도 ≤2.9·반경 ≤7) — night 표기는 GLB emissive 소재일 뿐 Light 추가 없음(Place는 렌더러 전용).

## 4. 테스트 갱신 (계약 수치)

- `PolyforkFurnishingContractTests`: GLB 종수 13→**40**, CREDITS credit 라인 13→**40**, id 전종 13→**40**(리스트 27종 추가), 배선 하한 40→**68**(157차 40+158차 28 소스라인 — 실측 후 확정)
- `InteriorQualityBaselineTests`: 소멸 예정 문자열 발생 시 같은 사이클 갱신(Bedroom_Wardrobe PlaceFurnit → wardrobe 교체 시)

## 5. QA

- 컴파일 error CS 0 + EditMode 전수(baseline 10건 1:1) + 투어 캡처 + vision 탑다운 판독 + Play 별도 승인
