# 실내 씬 AAA 업그레이드 — Meshy AI 에셋 규격서

- 날짜: 2026-09-10
- 대상: MedievalShellBuilder(96×72×32m 셸) + IndoorBuilder + CastleInteriorBuilder/PlayerCastle 등 빌더 8종 + Tools/Indoor 미리보기 13종
- 원칙: **모든 에셋은 "없으면 기존 절차 생성으로 폴백"** — GLB/텍스처가 있으면 고품질로 교체, 없으면 지금처럼 절차 생성(무손상)

## 0. 현재 구조 (규격의 기준)

- 셸: 96m(X) × 72m(Z) × 32m(천장높이) — 바닥 y=0, 남측 문 개구부 폭 2.2m
- 현재 바닥 타일링: 48×40 → **텍스처 1장 = 2m×1.8m** 영역. 벽: 세로 16타일 → **1장 = 2m 높이**
- 현재 가구: 프리미티브 조합(CreateTable 3×1.2×1.5, CreateChair, CreateShelf, CreateCounter, CreateBed), 기둥=Cylinder
- 머터리얼: URP/Lit — Albedo+Normal+Smoothness+AO 표준 PBR 경로

## 1. 표면 텍스처 (타일링 PNG) — ①류

**공통 규격**
| 항목 | 규격 |
|---|---|
| 형식 | PNG(권장)/JPG |
| 해상도 | **2048×2048** (필수, 2의 거듭제곱). 소형 보조만 1024 |
| 시맨틱 | Albedo=sRGB / Normal·Roughness·AO=Linear(비sRGB) — 유니티 임포트는 내가 처리하니 신경 X |
| 필수 맵 | Albedo + Normal (2장) |
| 선택 맵 | Roughness, AO (있으면 품질↑, 없으면 생략 가능 — Roughness는 코드에서 smoothness로 자동 반전) |
| 조건 | **반드시 시임리스(상하좌우 연결)**, 라이팅/그림자 구워진 Albedo 금지(플랫 컬러만) |
| 실물 크기 | **텍스처 1장이 몇 m×몇 m를 덮는지 알려줄 것** (기본: 2m×2m) — 타일링 계산에 필수 |

**Meshy 세팅**: Text-to-Texture → "Tileable/Seamless" ON, PBR Maps ON, 2K(또는 4K→내가 다운스케일), Quad UV.

**표면별 리스트 (우선순위 순)**

| ID | 표면 | 내용 | 1장 실물 크기 | 우선순위 |
|---|---|---|---|---|
| TX-01 | 성 바닥 | 플래그스톤(불규칙 석판, 마모·틈 흙) | 2m×2m | P0 |
| TX-02 | 성 벽 | 대형 석벽 블록(어두운 화강암, 모르타르 깊게) | 2m×2m | P0 |
| TX-03 | 나무 바닥 | 오크 판자(넓은 판, 마모) | 2m×2m | P0 |
| TX-04 | 나무 벽 패널 | 세로 판넬+장식 몰딩 톤 | 2m×2m | P1 |
| TX-05 | 석고 벽 | 아비또르 석고(오염·균열 은은) | 2m×2m | P1 |
| TX-06 | 천장 | 어두운 대들보+널판 | 2m×2m | P1 |
| TX-07 | 융단 | 화려한 중세 카펫(타일형 반복) | 3m×3m | P2 |
| TX-08 | 동굴 바닥/벽 | 거친 암석 | 2m×2m | P2 |
| TX-09 | 헛간 바닥 | 흙+짚 | 2m×2m | P2 |
| TX-10 | 교회 바닥 | 모자이크/대리석 | 2m×2m | P2 |

- 5국가 변형(동서남북)은 색만 다르면 됨 → **같은 패턴 재활용 가능**. 여유 되면 2종 추가(동양권 붉은 기와/석재, 서양 회석재). 없으면 코드 틴트로 대응.

## 2. 소품 GLB — ②류

**공통 규격**
| 항목 | 규격 |
|---|---|
| 형식 | **GLB**(텍스처 임베드 권장 — Meshy 기본값), 대체: glb+별도 PNG zip |
| 스케일 | 실제 미터(Unity 1u=1m). 아래 표의 목표 치수로 생성 (안 맞아도 코드에서 bounds 정규화하니 치수만 대략 맞으면 OK) |
| 피벗 | **바닥 중심(아래)** — y=0에 딱 붙는 기준. 벽걸이는 벽면 중심 |
| 방향 | 정면 **+Z** |
| 폴리곤 | 히어로(왕좌·화로·샹들리에) ≤8k / 중형(테이블·침대·선반) ≤5k / 소형(의자·항아리·촛대) ≤2k / 기둥 ≤4k |
| 머티리얼 | 1개 권장(최대 2) — 임베디드 텍스처 1024~2048 |
| 리토폴로지 | ON(게임레디). 고폴리 스캔풍(10만+) 금지 — 저사양(i5-6500/8GB) |
| 애니 | 불필요. 메시 1개 권장(부품 분리 시 이름 명확히) |

**Meshy 세팅**: Image-to-3D(스타일 통일에 유리) 또는 Text-to-3D → Retopology ON, PBR Texture ON, GLB 다운로드. 프롬프트 스타일 문구를 전 소품에 동일 사용:
> `medieval fantasy castle interior prop, realistic, weathered oak wood and aged iron, warm candlelight tones, game-ready prop, clean topology`

**소품 목록 (코드 배치 크기 기준)**

| ID | 소품 | 목표 치수(가로×높이×깊이 m) | 폴리 | 우선순위 |
|---|---|---|---|---|
| PR-01 | 기둥(Column) | 지름 1.2m × **높이 30m** — 또는 **모듈형**: 받침(1.2×1.2m)+몸통(1.2×10m, 세로 스택용)+수두(capital) | 4k | P0 |
| PR-02 | 성문(Door) | 폭 2.2m × 높이 3~4m (문틀+문짝+경첩) | 4k | P0 |
| PR-03 | 왕좌(Throne) | 1.2×2.2×1.2 — 높은 등받이+팔걸이 | 8k | P1 |
| PR-04 | 롱 테이블 | 3.0×1.5×1.2(현 코드 치수) — 상판+다리 | 5k | P1 |
| PR-05 | 회의 테이블 | 4.0×1.0×2.0 | 5k | P1 |
| PR-06 | 의자 | 0.5×1.0×0.5 (좌석 0.45m) | 2k | P1 |
| PR-07 | 화로(Fireplace) | 2.5×3.0×1.0 — 내부 태울 자리 | 8k | P1 |
| PR-08 | 샹들리에/횃불걸이 | 샹들리에 지름 1.5m / 횃불 브래킷 0.4m | 4k/1k | P1 |
| PR-09 | 침대 | 1.4×0.8×2.0 | 4k | P2 |
| PR-10 | 선반 | 1.2×2.0×0.4 | 3k | P2 |
| PR-11 | 상자/항아리/통 | 각 0.5~1.0m | 1~2k | P2 |
| PR-12 | 무기고 랙 | 1.5×2.0×0.3 | 3k | P2 |
| PR-13 | 작업대(크래프트) | 1.4×1.1×1.4 — 현재 테스트 박스 치수 | 4k | P2 |
| PR-14 | 요리 솥+화덕 | 1.2×1.5×1.2 | 5k | P2 |
| PR-15 | 연금 테이블(전통+유리구) | 1.2×1.3×1.2 | 5k | P2 |
| PR-16 | 제단/성구(교회) | 2.0×1.2×1.0 | 6k | P3 |
| PR-17 | 바 카운터(여관) | 3.0×1.1×0.8 | 4k | P3 |

- 저장고(Storage), 상점 카운터는 기존 CreateCounter/CreateShelf 치수 준용.
- 횃불 26개는 기존 절차 유지(많아서 GLB 대신 프레임 교체 효율) — 원하면 PR-08 브래켓만.

## 3. 전달 규약 (이대로 넣으면 바로 연동)

- GLB: `Assets/Models/UserProvided/Interior/<ID>_<이름>.glb` — 예: `PR01_column_a.glb`, `pr02_door.png` X. **파일명 ASCII+언더스코어만**(한글/공백 금지 — glTF 임포트+로더 키 규약)
- 텍스처: `Assets/Textures/Interior/<TX-ID>_<맵>.png` — 예: `TX01_CastleFloor_Albedo.png`, `TX01_CastleFloor_Normal.png`, `TX01_CastleFloor_Roughness.png`, `TX01_CastleFloor_AO.png`
- 실물 크기 필수 기입: 텍스처는 "1장 = N×Nm"만 알려주면 타일링 내가 계산. GLB는 대략 실측 치수(또는 건드리지 않고 그대로 배치).
- 유니티 임포트 세팅(sRGB/압축/Normal 타입/애니소)은 전부 내가 처리 — 신경 X.

## 4. 연동 방식 (네가 생성하는 동안 준비해 둘 것)

1. 셸 머터리얼 교체기: TX-ID→머터리얼 매핑 로드, 없으면 기존 절차 텍스처 폴백
2. 소품 교체기: 앵커별 GLB 로드(RuntimeModelLoader 키 "interior_<id>"), 없으면 기존 프리미티브 유지 — **현행 배치/치수/앵커 전부 보존**
3. Roughness→Smoothness 자동 반전, AO는 알베도 곱셈 옵션
4. 텍스처 압축: 바닥/벽 BC7, 노멀 BC5 — 임포트 프리셋 코드화

## 5. Meshy 프롬프트 템플릿 (영문 권장)

- 텍스처: `Seamless tileable PBR texture, medieval castle flagstone floor, irregular worn stone slabs with dirt in cracks, top-down flat albedo without shadows or baked lighting, neutral even lighting, 2m x 2m coverage`
- 소품: `Game-ready medieval throne chair, weathered oak wood with aged iron fittings, realistic PBR, clean topology, quadrilateral retopology, less than 8000 triangles, centered origin at base, facing +Z`
- 스타일 통일 접미사(전 소품 공통): `, consistent style: dark fantasy medieval, realistic weathered materials, warm muted palette`

## 6. 우선순위 요약

- **P0(먼저 뽑아줄 것)**: TX-01, TX-02, TX-03 + PR-01(기둥), PR-02(문) — 이것만으로 실내 인상 절반 이상 변화
- **P1**: TX-04~06 + 왕좌/테이블/의자/화로/샹들리에
- **P2 이후**: 침대/선반/상자/작업대 등 + 방별 특화(교회/여관/헛간)
