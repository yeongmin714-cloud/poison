# 159차: Polyfork 실내 보강 3차 — 30종 수용 + 방별 배치 계획서

> **작성일**: 2026-11-09 · 158차(40종 누적) 후속 · 전수 감사: GLB 30/30 무결(magic/ver2)
> **라이선스**: CC BY 4.0 — CREDITS.md 누적 70라인(40+30)

## 1. 배치 맵 (실내 25종 + 던전 보류 5종)

### 성 (ApplyCastle)
- **로비(보물/왕권)**: 왕좌 주변 보물 액센트 — spilled-coin-pile ×2(왕좌 앞 계단식)·gold-coin-stack ×2(옆)·jewel-casket ×1(앞)·loot-sack ×2(뒤) + **gilded-harp ×1**(동측 연주 코너) + **standing-knight-statue ×1**(남측 입구 옆) + **hanging-plant-basket ×1**(천장 — oy 2.142 원점, LampY 근처 회피)
- **연금**: **spellbook ×1**(작업대 위)·**divination-orb ×1**(야간 글로우)·alchemy-flask ×2(작업대 위)
- **제작소**: **leather-forge-bellows ×1**(푸세 — 화로 옆)·**quench-barrel ×1**(모루 옆 1.2m) + 주방 보강: basket-of-produce ×1·milk-churn ×1·table-herb-pot ×1·bread-loaf ×2(작업대 위)
- **병영**: **archery-target ×1**(벽 기대)·**armour-stand ×0(성 입구로)**·spear ×1(벽 전시)·shield-24b9f0 ×1(벽)·great-helm ×1(cabinet 위 y≈1.0)·quiver ×1(벽)
- **성 입구**: **armour-stand ×1**(행거 옆 대기)
- **침실**: **fern-plant ×1**(코너)

### 건물 (ApplyBuilding)
- **tavern**: **legendary-chest ×1**(night, 벽 뒤) + gold-coin-stack ×1(상자 옆)
- **restaurant**: basket-of-produce ×2·bread-loaf ×2(테이블 위)
- **house**: table-herb-pot ×1 + fern? (fern은 침실) → house: herb-pot만
- **shop**: basket-of-produce ×2 진열 + milk-churn ×1
- **barn**: potting-bench ×1 + milk-churn ×2
- **church/crafthouse**: 158차 완성 — 신규 0

### 던전 전용 보류 5종 (배치 미지정 — 별도 승인 필요)
stone-sarcophagus(석관)·barred-cell-door(감옥문)·ossuary-skull-shelf(해골선반)·iron-bound-door(철문)·mimic-chest(미믹)
→ 지하 5층 보강은 던전 계약(tierScale 2f 고정·에셋벽 3m·레이아웃 5종) 건드림 — 별도 사이클에서 사용자 승인 후 배치. 이번엔 수용(CREDITS)+테스트에 포함만.

## 2. 수용

- /tmp/polyfork_glb3/ 30 GLB → Assets/Resources/Models/Polyfork/ (누적 70종)
- CREDITS.md append 30라인 (GLB asset.copyright — 누적 70)

## 3. 테스트 갱신

- PolyforkFurnishingContractTests: GLB 40→**70**, credit 40→**70**, id 전종 40→**70**, 배선 하한 85→**실측값 기준(85+신규≈120)**
- 소멸 예정: 이번 교체 0건(전부 가산)

## 4. QA

컴파일 CS 0 + EditMode baseline 1:1 + 투어 DONE + PIL + (vision 성 로비/병영/연금 3컷)
