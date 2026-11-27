# 153차 — 농경 고품질 다듬기 + 획득 VFX 계획

> 상태: **사용자 확정(2026-11-04 "진행해줘") — 구현 위임 진행중**. 원본 계획 기안에서 §4의 확정 결정 반영.
> 사용자 요구 3축: ① 굉이질(땅 파기) 시각 고품질 ② 농경/채집 애니메이션 고품질 ③ 획득 연출(부유 GLB) 주변 이펙트 — 양쪽 빛기둥 등, 시각적 재미.

## 1. 현재 실측 (2026-11-04 코드 열람 — 진단의 근거)

### 1-A 굉이질/밭 시각 — `Assets/Scripts/Systems/FarmPlot.cs`
- **Soil**: `BuildSoilVisual`(L544) — 1×0.1×1 `Cube` 프리미티브 + `URP/Lit` 단색. 텍스처/normal 전무.
- **Tilled 구멍**: `BuildHoleVisual`(L572) — Rim 4개 Cube(0.72×0.06×0.12) + Pit 1개 Cube(0.55×0.04×0.55, y −0.045). **실제 오목이 아니라 납작한 판** — 탑다운에서 파인 음영이 안 살고 직선 rim이 기계적으로 보임.
- **흙길(Planted~Ready)**: 상태색만 변경(`GetSoilColor` L733) — 경준선(furrow) 텍스처 없음.
- **작물**: `BuildCropVisual`(L625) — **Sphere 단색**. `CropCatalog.GlbPath`(작물 100종 GLB, `Assets/새로운 glb/crops-fish/crop-{key}.glb`) 존재하는데 밭작물엔 미사용(NaturalResourceSpawner는 야생작물로 이미 사용 — 재사용 인프라 실존).

### 1-B 농경 애니 — 채널과 애니가 분리되어 있음
- `GatherChannelSystem.Begin`(77) — 진행바만 표시, **애니 트리거 없음**. `OnImpact` 이벤트(133차 — 0.4초 타격 리듬) 실존.
- **FarmPlot 채널 4종(굉이질 2s/파종 1s/물 1.5s/정리 1.5s, L444~482) — OnImpact 미구독, 애니 미발화**. 플레이어가 서서 진행바만 채움 → "땅 파는데 아무 일도 안 일어나는" 주된 원인.
- 대조: `HerbPickup`(수확)은 이미 완성 — 채널 시작 시 `TriggerGather()`(L323) + OnImpact 구독(L259) + 완료 시 Harvest 3종 variant+`TriggerCheer`(L326~345). `ResourceNode`도 OnImpact(Wood/Mine)+WoodSway. **패턴은 이미 수확 경로에 확립 — 미보유 영역이 굉이질/파종/물주기/정리.**
- 수확 애니 자체의 품질: 채널 시작 직후 1회만 발화 — 2초 채널 동안 클립이 먼저 끊기고 플레이어가 멈춤(리듬과 무관).
- 보유 클립(MeshyUser 140종): 굉이질 전용 클립 없음. 최근접 후보 = `Male_Bend_Over_Pick_Up`.
- `HumanoidClipDriver.CanFireAction`(L241) — 현재 상태가 Gather/Craft/Cheer면 재트리거 차단 → 리듬 재발화는 `Animator.Play(state,0,0)` 경로로 설계.

### 1-C 획득 연출 — 이펙트 전무
- `AcquisitionCinema`(UI/Toolkit, 343줄): 패널(`OpenForAcquisition`) + 부유 GLB(`AttachFloatItem` — 앵커 1.85m, 상승 0.10m, 사인 진동, 빌보드, 0.8s 알파 페이드) + 상반신 정면 카메라(`BeginUpperBody frontView`). **빛/파티클/희귀도 색 연출 전무.**
- 재료는 다 실존: `HarvestResultBridge.LatestRarity`(ItemRarity 6단계 전달중) / `EquipmentRarityData.GetRarityColor`(Core — HarvestResultUTK 뱃지가 소비하는 단일 진실원) / 절차 VFX 프리펩 패턴(GatherVFX_* — FX_Ring.png 선례).
- Bloom 없음(`Settings/Default_VolumeProfile_Enhanced` 미사용) → "빛"은 **additive 빌보드 쿼드**로 구현해야 한다(포스트 이펙트 의존 금지).

### 1-D 피드백 인프라
- `GatherFeedbackKind`: Wood/Mine/Herb/FishBite/FishCast/WaterPour/Acquire — **Soil 없음**.
- `Resources/VFX/Gather/` 실존 프리펩은 LeafPuff/FishRipple 2종뿐 — **WoodChips/StoneFragments/WaterDroplets는 코드 경로만 존재하고 에셋 부재**(기존 "조용한 스킵" 계약으로 무해). 물주기 파티클(WaterDroplets)은 밭 물주기에 실제로 쓰이는 경로 → 이번에 함께 보급.
- SFX는 `SFX_Gather_Wood/Mine/Herb`, `SFX_Water_Pour` 등 존재 — 흙 굉이질 전용 SFX 없음.

## 2. 설계 계약 (기존 원칙 재확인)
- 탑다운 음영 판독 우선 — 구멍의 파인 음영은 내벽 경사면+AO로 살린다. 천장/매트 금지 계약과 무충돌.
- 바닥 원본 훼손 금지 — 플롯 시각 깊이는 기존 오프셋(Soil y −0.05)과 동일 수준(−0.1m 이내) 유지.
- **병사 경로 미연출 계약 유지(130차)** — 이번에도 publish/환호 배선 추가 없음. FarmPlot의 OnImpact 구독은 FarmPlot 소유 채널(굉이질/파종/물주기/정리) 전용 — `HerbPickup` 수확 채널과 이중 배선 금지.
- 에셋 최소 추가 — 시각은 프로시저럴(코드 생성) 우선. 신규 클립/사운드는 사용자 확정 후(§4).
- 소스계약 문자열 보존 — `DialogueCameraDirector` 무접촉, `ResourceNode`의 ApplyWoodSway는 재사용(수정 없음).

## 3. Phase 계획 (파일경로+검증 사전 제시)

### P1 — 굉이질/흙길 고품질 시각: 신규 `FarmPlotVisuals.cs` 분리
- **신규** `Assets/Scripts/Systems/FarmPlotVisuals.cs` — FarmPlot의 시각부(BuildSoilVisual/BuildHoleVisual/BuildCropVisual/GetSoilColor/GetCropColor)를 위임 분리. FarmPlot은 상태머신만 남긴다(26차 계획 P2의 "분리 권장" 이행).
- **흙 텍스처**: 절차 생성 `Texture2D` — 밭흙 베이스색+입자 노이즈+경준선(furrow) 줄무늬(정현파 밴드)+대응 normal — 탑다운 격차 판독 강화. static 캐시, 시드 고정 **결정론**.
- **구멍**: Rim 4Cube+Pit 단일 Cube 폐기 → **프로시저럴 격자 메시**(정점 단위 실제 오목: 중앙 내림+내벽 경사면+비대칭 흙둑 rim+정점색 어두운 AO). 탑다운에서 음영 판독이 확실해지는 게 핵심 품질 요소.
- **수분 가시화**: Planted~Ready 흙길이 `MoistureFraction()`에 따라 촉촉(어두움)↔마름(밝음) 보간 — 수분 바 없이도 밭 상태 판독 가능.
- **검증**: 신규 `Tests/EditMode/FarmPlotVisualsTests.cs`(텍스처 캐시 결정론/수분 보간 판정/메시 정점 범위) + `FarmPlotLifecycleTests` 기존 1:1.

### P2 — 농경 채널 애니 + 리듬 타격 피드백
- **굉이질 애니(Till 상태 신설)**: 전용 클립 부재 실측 → 기존 `Male_Bend_Over_Pick_Up` 클립을 임시 클립으로 배선. `HumanoidClipDriver`에 `TriggerTill()` 신설(SetTrigger("Till")) + **에디터 스크립트**(`Assets/Scripts/Editor/Farm153EditorTools.cs` 등)가 `Player_AC.controller`에 Till 상태 추가(UnityEditor.Animations — 배치 실행은 QA 단계). 사용자가 신규 괭이질 클립 투입 시 Till 상태의 클립 참조만 교체하면 된다(스왑 포인트).
- **리듬 싱크**: 채널 지속 중 `OnImpact`마다 동작 이어짐 — `Animator.Play(state,0,0)` 재시작 방식(`HumanoidClipDriver.ReplayAction(string stateName)` 신설 — CanFireAction 가드 우회 목적 아님, 채널 지속 동작 유지용). 대상: 굉이질(Till) + `HerbPickup` 수확(Gather). 완료 시 기존 TriggerCheer 배선 무변경.
- **타격 피드백**: FarmPlot OnImpact 구독(`HerbPickup` L259 패턴 1:1) — 타격마다 흙 파티클+사운드. 물주기 채널은 WaterPour(VFX/SFX 기존 매핑).
- **신규**: `GatherFeedbackKind.Soil` + `VfxPrefabName`→`GatherVFX_SoilBurst` + `ClipPrefix`→`SFX_Gather_Soil`. 부재 시 기존 "조용한 스킵(에러 로그 1회)" 계약 유지.
- **프리펩 보급**: 에디터 스크립트(기존 선례)로 `Resources/VFX/Gather/GatherVFX_SoilBurst`+`GatherVFX_WaterDroplets` 생성(후자는 기존 코드 경로가 참조하나 에셋 부재 — 보급으로 물주기 피드백 활성).
- **검증**: 소스계약 테스트(FarmPlot OnImpact 구독 존재/HerbPickup 이중 배선 부재) + `GatherFeedbackContractTests` 갱신분 + baseline 1:1.

### P3 — 밭작물 GLB 승격 (Sphere → 진짜 작물)
- `FarmPlotVisuals.BuildCropVisual` — Sphere 폐기 → `CropCatalog.GlbPath(_defaultCrop 인덱스)` 로드. 로드 경로 = resolver 선례(`#if UNITY_EDITOR` AssetDatabase → Resources 폴백 → **실패 시 기존 Sphere 폴백, 연출 중단 없음**).
- 성장 스케일 유지(0.12→0.4→0.8), 시듦(갈색 틴트+처짐)/부패(검붉은 틴트)는 기존 `_cropColorOverride` 경로 유지 — GLB에도 동일 적용(머티리얼 인스턴스화 선례 준용).
- Growing 대기 중 소진폭 바람 스웨이(`ResourceNode.ApplyWoodSway` 수학 재사용 — 원본 미수정).
- **검증**: 작물 GLB 로드 결정론 테스트 + `FarmPlotLifecycleTests` 1:1 + baseline.

### P4 — 획득 연출 VFX (부유 GLB 주변 — "양쪽 빛기둥")
- `AcquisitionCinema.AttachFloatItem` 확장 — 피벗 자식 `AcqFloatFX` 그룹 신설(런타임 절차 생성 — 에셋 불요):
  1. **양쪽 빛기둥(lateral light shafts)**: 좌우 대칭 additive 빌보드 쿼드 2장 — 절차 생성 세로 그라디언트 텍스처(상단 퍼짐, 아래로 좁아짐), 부유 앵커 기준 좌우 ±0.55m·세로 ~1.2m, 사인 바위가. **등급별 강약**: Common 길이 0.8m/알파 0.25 → 등급↑ 길이·알파 증가 → Unique 1.8m/0.75(코드 내 매핑 표 — 기본 6단계).
  2. **헤일로**: 아이템 뒤 soft radial additive 쿼드 1장(리링 글로우 — 빌보드 위상은 GLB와 동일).
  3. **스파클 파티클**: 아이템 주위 상승 스파클 — 런타임 절차 ParticleSystem(에셋 불요).
  4. **등장 팝**: 스케일 0→1.15→1.0 back-out 이징 0.35s(기존 바로 등장 → 튕겨 나오는 탄력).
  5. **P4b 환호 반짝(포함 확정)**: 연출 시작 시 플레이어 주위 소 스파클 1계통 — TriggerCheer 발화 지점 계약 무변경, cinema는 FX 스폰만.
  6. **희귀도 색**: `EquipmentRarityData.GetRarityColor(HarvestResultBridge.LatestRarity)` 단일 소비(UI→Core 소비는 HarvestResultUTK 선례 — 신규 매핑 표 금지). 빛기둥/헤일로/스파클 전부 이 색.
- **페이드 계약**: FX 렌더러를 `_floatRenderers`에 추가 등록 — 기존 0.8s 알파 페이드가 FX에도 동일 적용(종료 잔상 방지). 세션 End는 `_floatItem` 파괴 경로 그대로(수명 코드 무변경).
- **병사 경로 무관** — 부유 소비는 플레이어 publish 전용(기존 소스계약 유지).
- **검증**: `AcquisitionCinemaTests` 확장(FX 구성 결정론/희귀도 색 단일 소비/페이드 등록) — 기존 20건 1:1, `DialogueCameraDirector` 계약 무접촉.

### P5 — QA
- EditMode 전체 + baseline 대조. Play 영상(프레임 추출+vision) — **별도 승인 게이트**.
- 위임: 구현/QA 서브에이전트 분리(docs/AGENT_DELEGATION_POLICY.md 준용 — P1~P4를 위임 시 각 Phase당 파일·검증 범위 명시).

## 4. 확정 결정 (2026-11-04 사용자 답변 반영)
1. **굉이질 애니**: 프로젝트 내 전용 클립 부재 실측 → **Till 상태 신설 + 기존 `Male_Bend_Over_Pick_Up` 임시 배선**(에디터 스크립트로 Player_AC에 상태 추가). 사용자가 신규 Mixamo 괭이질 클립을 추가하면 Till 상태의 클립 참조만 교체(1줄 스왑 — 본 문서 §3 P2).
2. **빛기둥**: **등급별 강약 확정** — Common도 약하게 표시, 등급↑ → 길이·알파 증가(코드 내 매핑 표).
3. **`SFX_Gather_Soil`**: **신규 확보 확정** — 코드는 `SFX_Gather_Soil` 프리픽스 매핑, 에셋 도착 전엔 기존 "조용한 스킵" 계약으로 무해. 사용자 파일 투입 위치: `Assets/Resources/Sounds/SFX/Gather/SFX_Gather_Soil_000.ogg`(001/002 … variant).
4. **P4b(환호 중 플레이어 주위 반짝)**: **포함 확정** — TriggerCheer 발화 지점 직접 호출 계약 무변경, FX는 연출 시작과 동기 스폰만.

## 5. 셀프감사 (중복/키충돌 — 153차 기준)
- **E키 컨텍스트**: 무변경 — 상호작용 로직/채널 게이트/시간/수분 로직 터치 없음(시각·애니·피드백만).
- **UI 중복**: FarmPlateOverlayUTK/HUD 무변경 — 수분 가시화는 3D 텍스처 보간이라 게이지와 이중 아님(가독 보조).
- **이중 배선**: FarmPlot OnImpact 신설 — `HerbPickup`이 소유한 수확 채널은 건드리지 않음(수확 채널은 HerbPickup이 여전히 단일 소유). 리듬 재발화 확장만.
- **시그니처**: `HarvestResultBridge`/`GatherChannelSystem.Begin` 무변경 — `OnImpact` 구독만 추가.
- **소스계약 테스트**: DialogueCameraDirector 계약 문자열 미접촉, 병사 미연출 소스계약 통과 조건 유지.
- **성능**: additive 쿼드 3장+파티클 1계통+정점 메시 소폭 — 모바일 급 이슈 없음. 절차 텍스처는 캐시 1회 생성.
