# 163차 — 음식 GLB 실빈틈 배선 스펙 (Polyfork 웨이브)

> 사용자 지시: "있는 glb는 말고 없는 glb만 폴리포크에서 배선". 기존 커버분(조리완성품 6종 grilled_skin/roasted_chicken/soup/tempura/yakitori_skewer/puding, crop_*, fish_glb_ 체인)은 **건드리지 않는다**.

## 1. 배선 대상 (아이콘 체인 실측 기반 — 전부 절차 폴백 중)

| id | 소스 | 파일 작업 |
|---|---|---|
| `meat_grilled` | polyfork charred-meat-haunch-be1ea2 (768tri, 정점컬러 1머티, 텍스처 0) | 복제 + 가시성 보정(아래) |
| `meat_boar` | charred-meat-haunch 파생 | 정점컬러 hue 재매핑 — 진한 적갈(hue≈8°, val≈0.42) |
| `meat_rabbit` | charred-meat-haunch 파생 | 연한 분홍(hue≈350°, sat 낮춤 0.4, val≈0.58) |
| `meat_wolf` | charred-meat-haunch 파생 | 어두운 적색(hue≈0°, val≈0.32) |
| `cook_01` | polyfork silver-cloche-platter-25dd3e (772tri, 2머티 정점컬러, 텍스처 0) | 무수정 복제 |
| `drugged_cook_01` | silver-cloche-platter 파생 | 마젠타 틴트 — Drug UI 카테고리색 (0.9,0.2,0.8) 근거, lerp≈55% + 명도 보존 |

- 생고기 원재는 polyfork 카탈로그 부재(162차 demand 기록 대기) → 원본 1종에서 파생 양산(no91 선례).
- charred-meat-haunch 실측 COLOR_0 avg rgb [0.061,0.058,0.065] — **거의 흑**. meat_grilled는 가시성 보정 필요: 프리뷰 렌더→vision 판정 후, avg val<0.15면 val 승수(×3.5 목표)로 "구운 갈색" 가독화(hue는 유지, no92 유리 밝히기 선례).

## 2. Phase A — 파일 수용 (구현 서브에이전트)

1. 원본 수용: `/mnt/c/Unity/code/Assets/Resources/Models/Polyfork/{charred-meat-haunch-be1ea2,silver-cloche-platter-25dd3e}.glb` — 이미 `/home/korea/tmp/polyfork_food_glb/`에 다운로드됨(검증済: 정점컬러·텍스처 0장·copyright 내장). **+meta 금지(Unity 생성)**.
2. CREDITS.md(같은 Polyfork 폴더) — 2라인 append + 헤더 (N) 갱신. credit은 GLB asset.copyright에서 추출.
3. UserProvided 복제·파생: `Models/UserProvided/{meat_grilled,meat_boar,meat_rabbit,meat_wolf,cook_01,drugged_cook_01}.glb` — 관례후보1(id 그대로) 자동해결, **코드 변경 0**.
4. 파생 도구: `Tools/derivative_bake.py`(hue/sat/val 승수·rgb op·--selftest) 우선, 부족하면 §13b struct 파싱 파이프라인. 재조립 규약: JSON 0x20 패딩·BIN NUL 패딩·4바이트 정렬, POSITION accessor min/max 재계산.
5. 무프리뷰 검증 루프: matplotlib Agg 6종 그리드 PNG → vision 판정("다른 아이템으로 인지/파손 여부") → PNG 경로 증거 제출.

## 3. Phase B — 테스트 갱신 (같은 서브에이전트)

1. `PolyforkFurnishingContractTests.cs`:
   - PolyforkIds = ls 실측 전체 재생성(현재 +2 = 신규 2종 포함).
   - SkipWiringCheckIds +2 (아이템 시각용 수용 — 원본 경로 미배선, 게임 경로 UserProvided 복제본 사용 — 사유 주석).
   - T1/T2/T3 카운트 값+주석 일괄 갱신(125차 규칙 — ls·rg 실측값만 사용, 추정 금지).
2. `FoodGlbCoverageTests.cs` **신설**: 6종 ①Resources.Load 존재 ②GblItemIconRenderer.ResolveModelKey 체인 통과(id 그대로) ③drugged_cook_01≠cook_01 바이트 서명(틴트 실제 반영 검증). 스타일은 `PotionHerbVisualCoverageTests`/`MaterialGlbCoverageTests` 준거.
3. Split(None)만 사용(StringComparison 오버로드 CS1503 금지), 카운트 계약 갱신은 값+메서드명+주석 일괄.

## 4. 완료 기준·증거 (구현)

- 수정 파일 경로 목록 + diff(CREDITS/테스트) + 컴파일 증거(error CS=0) + 프리뷰 PNG 경로 + 범위 준수 선언.
- 범위 화이트리스트: 위 Polyfork 폴더 GLB 2, CREDITS.md, UserProvided GLB 6, PolyforkFurnishingContractTests.cs, FoodGlbCoverageTests.cs(신설). 범위 외 발견 시 즉시 보고.

## 5. QA (별도 서브에이전트)

- ① 컴파일(error CS=0) ② EditMode — FoodGlbCoverageTests + PolyforkFurnishingContractTests 집중 후 전수 ③ Play 미실행(승인 게이트) — 3단계 구분 표기 + 로그 경로 증거.
