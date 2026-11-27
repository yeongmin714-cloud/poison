# 182차 계획 — Farm Pack 농경 밭 시각 승격 (상태별 모델 적용)

**전제**: Farm Pack(보유 zip 2건, GLB 버전 포함)을 8상태 농경 밭 시각부에 적용.
**원칙**: 153~154차 프로시저럴 계약(텍스처·구멍·스케일·수분·틴트) 전부 보존 — 팩 모델은 "존재 시 우선, 부재 시 폴백" 레이어(153차 P3 작물 GLB 승격과 동일 관례).

---

## 1. 실측 요약 (2026-11-23)

### 현재 농경 시각부
- 8상태: `Grass → Dirt → Tilled → Planted → Growing → Ready → Withered / Rotted` (FarmPlot.cs L27)
- 밭 타일: **1×1m Cube**(localScale 1, 0.1, 1, y −0.05) + 절차 밭흙 텍스처+경준선 normal (FarmPlotVisuals.BuildSoilVisual L414)
- Tilled: 평탄 타일 비활성화 → 프로시저럴 구멍 메시(L738~740 — 154차 수리 계약)
- 작물: GLB 로드 후 **스케일만** 변화(0.12→0.4→0.8), 시듦/부패는 인스턴스 틴트(L766~775)
- 수분: 흙 밝기 = MoistureFraction 보산(촉촉×0.75 ↔ 마름×1.1)

### Farm Pack 보유 물건 (Godot zip = GLB 제공 — FBX 변환 불필요)
- `Farm Pack/Models/Fields and Crops/` 47종 GLB, 단일 아틀라스 `farm_atlas.png`(554KB, M_Atlas_Farm)
- **Field_1_x (1~7, +A/B)**: 빈 갈린 밭 타일 5~6m — 경준선 있는 흙 (렌더 판독: 작물 없음)
- **Field_2_x (1~7, +A/B)**: 작물이 자란 밭 — 높이 1.75 (2_2=중간 성장, 2_1/2_7=성숙 — 파이프클라우드 판독)
- **Field_3_x (1~3)**: 저상 데코 밭 (0.28 높이, 4~8m)
- 작물 단계 모델: **PotatoPlant_1/2/3**(3단계), **TomatoPlant_1_A/B→2_A/B**(2단계×2변형), CornPlant, EggPlant_Plant, CropPlant_1_1_A/1_2_A, Cabbage, BeetRoot_2, Carrot_1/2, Sunflower_1~4, Plant_Wheat/2, Tomato, Potato_1~3, EggPlant
- **WateringCan_1_A~E**(5변형), WaterTank_2_A~E, WaterTower 3종
- 그 외(미사용 후보): 헛간 3종(A/B), 울타리 다수, 트랙터/콤바인/트레일러, 동물(소/돼지/닭/오리/병아리), 새, 팻말(FARM 게이트)
- ⚠ zip 내부에 라이선스/README 문서 부재 — 출처 확인 필요

### 카탈로그 대응
- CropCatalog.Keys 98종, 밭 씨앗 = `crop_<Key>_seed` 소비, 시각 = `crops-fish/crop-<Key>.glb` 단일 모델
- 팩 커버 작물(성장단계 보유): **감자(potato)·토마토·옥수수(corn)·가지·양배추·비트·당근(carrot)** + 호박(pumpkin)/밀 대체는 목록 확인 후
- 나머지 90종: 기존 crop GLB 스케일 계약 유지

---

## 2. 매핑 설계 (땅 변화 ↔ 팩 모델)

| 상태 | 시각 | 적용 |
|---|---|---|
| Grass | 지형 잔디 톤 Cube | **현상 유지** — "밭 주변과 동일 톤" 계약 |
| Dirt | 흙땅 | **Field_1_x 승격** (bounds 정규화 → 플롯 1m, 수분 tint는 머티리얼 배수 이식) |
| Tilled | 구멍+둑 | **현상 유지** — 구멍은 게임 메커니즘 시각(154차 계약), 팩 고랑은 대체 불가. Tilled 시 팩 타일 비활성화 로직 재용(평탄 타일 스왑 구조 동일) |
| Planted | 평평한 흙길+씨앗 | Field_1_x + 작물 sprout(Planted 스케일 0.12 계약 유지) |
| Growing/Ready | 성장 | **커버 작물: 팩 단계 모델 스왑** — potato 1→2→3, tomato 1→2(변형 A), 나머지 커버 작물은 성숙 모델 1종 + 기존 보간 스케일. **비커버 90종: 기존 crop GLB 스케일 계약 그대로** |
| Withered/Rotted | 시듦/부패 | **현상 유지** — 틴트 계약(153차) 그대로, 모델 파생(hue 리맵) 불필요 |

---

## 3. Phase 구성

### P0 — 판별·실측 (구현 전, 0편집)
1. Field_2_x 단계 순서 확정 — 7종 렌더 판독(matplotlib/에디터 썸네일) → "몇 번이 sprout/중간/성숙인지" 매핑표
2. `WateringCanSystem.cs` — 물뿌리개 아이템 id·20회 소진 흐름 실측
3. FarmPlot ↔ HerbPickup/HerbType ↔ CropCatalog 관계 판독(밭 작물 결정 경로)
4. FarmPack.zip에 라이선스 표기 확인 — 부재 시 **구매 출처 질의**(사용자)
5. 기존 테스트 충돌 스캔: `Does.Not.Contain` 계약 중 밭흙/구멍/작물 경로 핀 조사

### P1 — 수용 (에셋 파이프라인)
- 대상 GLB 선별: Field_1_x 15종(전부 — 배치 변형용), Field_2_x 7종(판독 후 필요분), 팩 작물 ~20종, WateringCan 1종
- 위치: `Assets/새로운 glb/farm-pack/` + 원본 zip은 Downloads 유지(프로젝트 내 `쓸만한/Farm Pack (1).zip` 중복본은 별도 정리 승인 대기)
- farm_atlas.png: **141차 텍스처계약**(원본 해상도·BC7·q100) 준수 임포트
- glTF 전수감사(147차 파서: tri/COLOR_0/텍스처/라이선스) + CREDITS.md 등재(현재 204 → +N, 카운터 형식 유지)
- 아이템 id 관례: 팩 작물 GLB는 **기존 crop_<Key> 해석 체인 유지** — id 변경 0건

### P2 — 밭 타일 승격 (FarmPlotVisuals.cs만)
- `BuildSoilVisual`: 팩 타일 GLB 존재 시 Field_1_x 모델 생성(bounds 최대차원 → 1m 정규화, NormalizeScale 수학 재용), 부재 시 기존 Cube — 메서드 서명 변경 0
- 수분 밝기 산식: 모델 전 renderer 머티리얼 인스턴스화 후 tint 배수 적용(기존 ×0.75/×1.1 재용)
- Tilled 스왑 로직(L738~740) 무변경 — 모델이어도 동일 SetActive 패턴
- Grass/구멍 로직 무변경

### P3 — 작물 단계 모델 (FarmPlotVisuals.cs + 매핑표)
- 매핑표(신규 정적 클래스 또는 FarmPlotVisuals 내 상수): `potato: [PotatoPlant_1→2→3]`, `tomato: [TomatoPlant_1_A→2_A]`, `corn/가지/양배추/비트/carrot: [성숙모델 1종]`
- Planted→Growing: 커버 작물은 단계 스왑 + 기존 보간 스케일 유지(스케일 계약 메서드 그대로 사용 — 모델만 스왑)
- 비커버 90종: 변경 0 (기존 경로 그대로 → 테스트 무충돌)
- 바람 스웨이 훅(Tick) 유지

### P4 — 물뿌리개 모델 (1파일 수준)
- `WateringCan_1_<선정>` → 물뿌리개 아이템 GLB (관례후보1 — id 그대로 경로 해결)
- P0 실측 결과에 따라 아이콘 자동 적용/손장착은 별도

### P5 — 검증
- 컴파일 error CS 0 + 전수 EditMode **baseline 1:1**
- 신규 계약테스트: ①밭 타일 승격(팩 모델 우선/폴백 프로시저럴 존재) ②작물 단계 매핑(커버 키만 스왑, 비커버 crop GLB 유지) ③CREDITS 카운트 1:1 ④기존 153/154차 프로시저럴 테스트 전부 통과(폴백 경로 판정)
- Play 확인 목록: 굉이질/파종/성장/수확/시듦/부패 각 단계 시각 + 수분 밝기 판독성 + 비커버 작물 회귀 없음

### 별도 후보 (이번 라운드 미착수)
울타리·팻말·헛간·물탑·동물 — 마을/영지 데코 라운드로 분리(과밀 배제·마을계획 145차 정합 확인 후)

---

## 4. 리스크/유의
- **153~154차 계약 충돌 금지**: 교체가 아니라 우선 레이어 — 기존 테스트는 폴백 경로에서 계속 통과
- Field_2_x 단계 순서는 렌더 판독 확정 전까지 매핑 금지(P0 게이트)
- M_Atlas_Farm 단일 아틀라스 — 머티리얼 인스턴스화 시 텍스처 공유 참조(메모리 이중로드 방지)
- 라이선스 문서 부재 — 출처 확인 전 CREDITS 기입을 "저작자 미상(임시)"으로 하지 말고 사용자 확인 후 기입
- Farm Pack Godot.zip의 `.import` 파일(Godot용)은 수용 대상에서 제외
