# [125차 후속35-R] 아레나 전역 고품질화 통합 계획 (개정판)
## — 실내: tavern 교체 텍스처 사용 / 실외: 기존 절차 텍스처 유지 / 파일 추가·교체 0개

작성: 2026-10-26 / 개정: 2026-10-26 (사용자 지시 "실내는 교체 텍스처, 실외는 현상 유지, 둘 다 최대 고품질" 반영)
성격: 계획만 — Phase별 승인 게이트, 구현 없음
선행 계획: 125_35 초판(로비 한정) → 본 문서가 대체(스코프 실내+실외 통합)

## 0. 확정 스코프 (사용자 결정)

1. 실내(로비, Rusty Dragon Inn): tavern 6종 텍스처 사용 확정 — 후속32 배선 유지.
   기존 성 내부 5종과 병행(교체 금지 계약 유지).
2. 실외(타원형 오픈에어 투기장): 절차 텍스처(모래/에이프런/벽) 현상 유지 — 파일 교체 안 함.
3. 양쪽 모두 "텍스처 파일 교체 없이" 가능한 고품질화 레버를 최대한 적용.
4. 노멀 맵 추가 제작은 하지 않음(사용자 결정) — 기존 노멀 소스 유지.

## 1. 실측 기반 + 기존 인프라 감사 (중복 방지 — 반드시 먼저 읽을 것)

### 1-1. 이미 구현돼 있어서 "신규 구현이 아니라 확장"인 것 ⭐
| 자산 | 실측 | 시사점 |
|---|---|---|
| **IndoorPostProcessController** | Assets/Scripts/Systems/IndoorPostProcessController.cs — 런타임 절차 프로필(Bloom 0.28/Vignette 0.35/WhiteBalance 15/Saturation 6/Contrast 8), 게이트=씬명 IndoorScene|"Interior", 톤 상한 계약(F4, 2026-10-10) | **로비 후처리는 이미 활성 중** — Editor.log L2784 `[IndoorPostProcess] 게이트 전환 — 씬='IndoorScene' 실내=True weight=1` 실증. 신규 볼륨 구현 금지 → 확장만. GlobalVolumeProfile 에셋(Resources/PostProcessing)은 불필요 |
| **FlickerRunner** | Systems/IndoorLighting.cs L219 — StartFlicker(Light) 코루틴 플리커, SetupIndoorLighting(flicker:) 파라미터로 중앙광 연결 | **플리커 신규 파일 계획 폐기** — 재사용. 아레나는 현재 flicker=false(L134)로만 미사용 |
| TorchFlicker / ShellTorchFlicker | CaveInteriorBuilder L207 / MedievalShellBuilder L169 | 동일 계열 선례 — 참조 패턴 |
| ArenaOutdoorMaterialFactory | 파일 우선·절차 폴백 계약 (후속24 A1) | 실외 머티리얼 확장의 삽입점 |
| 품질 레이어 | 벽캡/마모/블롭 — 로비+피트 양쪽 다수 존재(피트: ArenaPitWearStage/Floor/Duel/Gate/Apron 5세트) | 실외 데칼 기반 이미 충실 — 증강만 가능 |

### 1-2. 환경 실측
- URP 에셋(Assets/URP/URPAsset.asset): HDR ✓ / MSAA 4x / m_ShadowDistance 50 / **m_RequireDepthTexture 0**
- 렌더러: Assets/URP/UniversalRendererData.asset — 렌더러 피처 없음(SSAO 미설치)
- 로비 조명: 앰비언트(0.38,0.32,0.25)+중앙광 3.0/26m+보조광 3등+횃불 4곳+방향광 필(ArenaFillSun 0.75, 섀도우 None)
- 실외 조명: 중앙광(SetupIndoorLighting 경로)+ArenaFillSun+횃불 4곳 — 모두 섀도우 None
- 섀도우None 계약(12차): 구 저사양 PC 보호용 — 메인 실행 PC는 고사양 별도 데스크톱(10-15 고지)이라 가드 불필요

## Phase 1 — 라이팅 (체감 최대) [승인 게이트 1]

### P1-1 실내 섀도우 활성 ⚠섀도우None 계약 변경 (명시 승인 대상)
- 파일: Systems/IndoorLighting.cs (AddPointLight 시그니처 확장 — `bool shadows = false` 디폴트 추가,
  기존 호출부 전부 무영향) + Systems/ArenaInteriorBuilder.cs
- 적용: 로비 7등 — 중앙광(SetupIndoorLighting 내부 포인트)/보조광 3등(L138-140)/횃불 4곳(L154-157) →
  LightShadows.Soft. 섀도우 거리 50 이미 설정.
- 스코프: **아레나 로비+실외 투기장 한정** — 성 내부/기타 실내는 디폴트(None) 유지로 무변화 계약.
- 2단 옵션(성능 여유 확인 후): 횃불 4곳은 섀도우 제외(중앙광+보조광 4등만) — 시각 차이 미미, 비용 절반.

### P1-2 실외 섀도우 — 실외 최대 체감항
- 파일: Systems/ArenaPitInteriorBuilder.cs (조명 생성부)
- 적용: ①ArenaFillSun(Directional 0.75) shadows None→**Soft** — 외곽 벽(8m)·6게이트 기둥·왕가 박스가
  모래(톱 y=0.15)에 실그림자. ②중앙광+횃불 4곳 Soft.
- 근거: 투기장은 지금 "붙어 있는 듯한" 평면 인상 — 그림자가 공간감을 만든다. 섀도우거리 50 충분.

### P1-3 플리커 — 기존 FlickerRunner 재사용 (신규 파일 0)
- 파일: Systems/IndoorLighting.cs
- ①ArenaInteriorBuilder L134 `SetupIndoorLighting(..., flicker: false, ...)` → **true** (로비 중앙광 진동)
- ②AddPointLight에 `bool flicker = false` 확장 → 보조광 3등(촛불 인상)+횃불 4곳 연결 —
  FlickerRunner에 AddLight(Light) 복수 대응 메서드 추가(기존 StartFlicker 유지).
- 계약: FlickerRunner는 기존 인프라(코루틴 기반 선례) — 신규 코드는 이벤트 사슬에 두지 않음(무음사망 계약 회피).

### P1-4 반사 프로브 1개 (실내만)
- 파일: Systems/ArenaInteriorBuilder.cs — 로비 중앙 ReflectionProbe(Realtime 1회 베이크, 128px,
  boxProjection) — 오크 판자(Smoothness 0.25)에 천장·벽 반사.
- 실외 제외: 모래 Smoothness 0.20(마른 모래 계약) — 이득 미미.

### P1-5 실내 대비광
- 파일: Systems/ArenaInteriorBuilder.cs — 북측 투기장 문 뒤 쿨톤 디렉셔널 림 1등(0.75,0.85,1.0, 0.4) —
  웜광 대비 심화. 섀도우 없음.

### P1-6 실내 화로 불꽃 (텍스처 없는 발광)
- 파일: Systems/ArenaInteriorBuilder.cs BuildFireplace — ①굴뚝 기단에 이메시브 링 쿼드
  (URP Lit _EmissionColor 웜 주황, 글로우 1.2) ②화로 전용 웜 점광원(플리커 연결).
- 근거: 화로=방 중앙 심볼인데 불이 없음 — 불꽃 인상+진동광이 로비 분위기 직결.

### P1-7 실외 횃불 이메시브 (O-F1)
- 파일: Systems/ArenaPitInteriorBuilder.cs — 횃불 위치 화염 쿼드 이메시브(웜 주황)+플리커 연동 —
  P2 Bloom과 결합 시 발광 불꽃. 텍스처 0개.

## Phase 2 — 후처리 확장 [승인 게이트 2]

### P2-1 ACES 토너매핑 추가 (기존 컨트롤러 프로필 확장)
- 파일: Systems/IndoorPostProcessController.cs BuildIndoorProfile() — Tonemapping(ACES) 추가.
- 계약: 톤 상한 계약(F4) 준수 — 새 상수 추가 시 상한 문서 갱신. 기존 Bloom/Vignette 값 불변.
- 효과: 하이라이트 롤오프 — 촛불/횃불 글로가 자연스러워짐.

### P2-2 투기장 게이트 확장
- 파일: Systems/IndoorPostProcessController.cs Update() — 게이트 판정에
  `GameObject.Find("ArenaPitRoom") != null` 추가(0.5s 스로틀 폴링 — N1과 동일 패턴).
- 실외 진입 시 weight 1 — ACES+Bloom이 투기장에도 적용. 스테이징은 제외(중립 유지, 옵션).
- 웜 화이트밸런스 15는 횃불 조명 투기장과도 정합 — 단일 프로필 유지(분기 프로필은 과설계).

### P2-3 SSAO 렌더러 피처 ⭐사용자 에디터 조작 필요 (코드 불가 영역 — 유일)
- (사) 사용자 조작 1회: Assets/URP/UniversalRendererData.asset 선택 → Inspector Add Renderer
  Feature → **Screen Space Ambient Occlusion**(Intensity 0.6 / Radius 0.6 / Interval 2 / Half Res).
- (코드) URPAsset.asset의 `m_RequireDepthTexture: 0→1` — 이건 YAML 스칼라 수정으로 안전(구현 시 수행).
- 효과: 게이트 기둥·벽 기단·왕가 박스·가구 접지 음영 — 실내/실외 동시(전역).
- 수기 YAML로 렌더러 피처 추가는 guid/직렬화 리스크로 **기각**.

## Phase 3 — 머티리얼 심화 (텍스처 파일 교체 0) [승인 게이트 3]

### P3-1 런타임 AO 맵 합성 — 실내+실외 공용
- 파일: Core/IndoorTextureGenerator.cs — `SynthesizeOcclusionFromAlbedo(Texture2D)` 신설:
  휘도 하위 구간→크레비스 판정→블러→AO 맵(GetPixels32/SetPixels32, 1회 생성 후 정적 캐시).
- 주입: Systems/IndoorMaterialFactory.cs CreateTavernFloor(실내 — tavern_floor_plank 파생) +
  ArenaOutdoorMaterialFactory.CreateSand(실외 — GenerateSandTexture 파생)에 _OcclusionMap+
  _OcclusionStrength 0.6.
- 계약: 파일 쓰기 없음(메모리 내 합성) — 원본 텍스처 무손상.

### P3-2 런타임 거칠기 마스크 (실내 오크 왁스)
- 파일: 동일 — 휘도 분산 기반 roughness 맵 → _Smoothness 마스크. 왁스 얼룩 반사 변화.

### P3-3 디테일 노이즈 맵 — 타일 반복 완화
- 파일: IndoorMaterialFactory(CreateTavernFloor) + ArenaOutdoorMaterialFactory(CreateSand) —
  절차 미세 노이즈 → _DetailAlbedoMap/_DetailNormalMap(Scale 0.3, keyword _DETAIL_MULX2).
- 실내: 19×25 타일 / 실외: 모래 8×8 타일 반복감 완화.

### P3-4 노멀 강도 조정
- 파일: CreateTavernFloor — `_BumpScale` 1.0→1.3 (기존 tavern_floor_plank_n 그대로, 파일 교체 없음).

### P3-5 패럴랙스 (최후 — 기대치 낮음 명시)
- URP Lit Height Map에 절차 하이트맵(판자 라인 기반). 탑다운 수직 시선에서 효과 미미 —
  3인칭 저각도(베팅데스크 대화 등)에서만 유효. 전 Phase 완료 후 판단.

## Phase 4 — 실내 구조 디테일 (기존 tavern 파일 소비) [승인 게이트 4]

### P4-1 상부 들보 링 — tavern_wall_timber 소비 ⭐준비된 머티리얼의 첫 사용처
- 파일: Systems/ArenaInteriorBuilder.cs — 4벽 상단(ROOM_HEIGHT 부근)에 단면 0.25×0.25m 목재 들보 링,
  머티리얼=CreateTavernWoodFrame(후속32 T2에서 준비만 되고 소비처 0건이었음).
- 계약 검증: Top-Down 원칙 — 천장 금지와 무충돌. 들보는 벽 상단에 붙는 얇은 요소(벽톱면캡 계열)로
  탑다운 시야에서 방 중심을 가리지 않음(두께 0.25m, 벽면 접착). 천장판/존매트/걸레받이 아님.
- 효과: 주점 인테리어 완성도+timber 텍스처 활용.

### P4-2 마모 스탬프 좌표 정정 (후속34 O3 수리)
- 파일: Systems/ArenaInteriorBuilder.cs L118-123 — `halfWidth + 11f`(x=33.8, 방 밖) 2곳을
  방 내부 좌표로 정정(예: halfWidth-4f) — 지금 방 밖에 있어 보이지 않는 마모 2장 회수.

## Phase 5 — 실외 구조 디테일 [승인 게이트 5]

### P5-1 벽 링 톱면 캡
- 파일: Systems/ArenaPitInteriorBuilder.cs — 48세그먼트 외곽 벽 링 상단에 캡 링(두께 0.15m) —
  실내 AddQuadWallCaps 계약과 동일 컨셉의 실외판. 국가 절차 벽 텍스처 그대로(파일 없음).

### P5-2 게이트 액센트 (국가색)
- 파일: 동일 BuildGateArches — 6게이트 상단에 국가색 현판 쿼드(절차 색 머티리얼 — 파일 없음) +
  게이트 양쪽 횃불 이메시브(P1-7 연계).

### P5-3 듀얼존 마모 증강
- 기존 ArenaPitWearDuel/Gate/Apron 스탬프의 크기·톤 실측 후 미세 조정(신규 생성 최소화).

## 셀프감사 (중복·키·계약 충돌 검증 — 완료)

1. **후처리 중복 폐기** — IndoorPostProcessController 이미 로비 활성(로그 실증) → C1 신규 구현 계획 폐기,
   확장(ACES+게이트)만. GlobalVolumeProfile 에셋 사용 계획 폐기(런타임 절차 프로필이 진실원).
2. **플리커 중복 폐기** — FlickerRunner 존재 → CandleFlicker 신규 파일 계획 폐기, 재사용+복수 광원 확장.
3. **섀도우None 계약** — P1-1/P1-2가 계약 변경(아레나 한정). 명시 승인 필요. 성 내부 디폴트 유지.
4. **코루틴 무음사망 계약** — 신규 코드는 코루틴 금지(FlickerRunner는 기존 인프라 선례).
   게이트 폴링은 Update 기반(N1 선례).
5. **Top-Down 원칙** — P4-1 들보는 벽 상단 접착 얇은 요소(벽톱면캡 계열) — 천장/존매트/걸레받이 아님.
   P5-1 캡도 벽 톱면 — 시야 중심 무가림(기하 검증 계획 포함).
6. **기존 실내 5종 교체 금지** — P4-1은 tavern_wall_timber(신규 tavern 파일) 소비 — 기존 5종 무관.
7. **성 내부 무변화** — 공유 경로(IndoorLighting/팩토리) 확장은 전부 디폴트 보존 파라미터 추가 방식.
   ApplyHighQualityInterior arena 분기 계약 유지.
8. **A6 후처리 대기 항목** — 본 계획 Phase 2가 구체안 대체(중복 해소).
9. **키 충돌** — 신규 파일 0개(전부 기존 파일 확장+메서드 추가). 클래스/키 신설 없음.
10. **Play·커밋** — 별도 승인 게이트 유지. 각 Phase 독립 검증(컴파일+baseline+재Play).

## 검증 공통
- 컴파일 error CS 0(qa_125h3X_compile.log) + EditMode baseline 1608/1597/11 1:1.
- 성 내부/투기장/로비 회귀: 변경 스코프 밖 씬 무변화 확인.
- 재Play 판정(사용자 게이트) 항목 — Phase별 아래 참조.

### Phase별 재Play 판정 항목
- **Phase 1**: 로비 — 가구·벽 그림자/촛불 진동/바닥 반사/북측 쿨톤 림/화로 불꽃.
  실외 — 벽·게이트·왕가 박스 그림자가 모래에 떨어짐/횃불 진동+발광.
- **Phase 2**: 톤 변화(ACES)/횃불·촛불 블룸/구석 SSAO 음영/투기장 진입 시 후처리 적용/실외·스테이징 회귀.
- **Phase 3**: 바닥 크레비스 음영/왁스 얼룩 반사/타일 반복감 완화.
- **Phase 4**: 상부 들보 가시(timber 결)/마모 스탬프 방 안 가시.
- **Phase 5**: 벽 톱 캡/게이트 국가색 현판/횃불 위치 정합.

## 성능 요약 (메인 고사양 PC 전제 — 저사양 가드 불필요)
- 포인트 섀도우 최대 11개(로비 7+실외 4)+방향광 섀도우 2개(StagingDirectionalLight 제외 — 실내 게이트에서
  비활성 중) — 섀도우 거리 50 내. 고사양 PC 여유.
- SSAO Half-Res+Interval 2 — 프레임 비용 소폭.
- AO 합성: 1024² 1회(정적 캐시) — 로비/투기장 진입 1회씩.
- 반사 프로브: 1회 베이크(로비 진입 시) — 상시 비용 없음.

## 추진 절차
Phase 0(없음 — P4-2 마모 정정은 Phase 4에서) → **Phase 1(라이팅)** → 재Play 판정 →
**Phase 2(후처리)** → 재Play → **Phase 3(머티리얼)** → 재Play → **Phase 4(실내 디테일)** →
**Phase 5(실외 디테일)**. 각 Phase 시작 전 사용자 승인, P2-3만 사용자 에디터 조작 1회 동반.
