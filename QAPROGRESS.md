## 11-27 14:5x — 207차 — 말 누움(클립 루트 보정)·화살 조준 카메라 v2·활 그립·폭탄 착지/호 플루언트 (QA GREEN — Play 대기)

**사용자 지시(11-27 오전 Play 후 4건)**: 말 누움·화살 조준 불일치·활 바닥 쳐짐·폭탄 착지 미표출+플루언트 → docs/207_HORSE_ARROW_BOW_BOMB_PLAN.md 승인 실행. 세션 종료로 중단 후 CLI+텔레그램 병행 분업으로 완결(P1=텔레그램 세션 14:29 '승인' 소유, P2·P4-2·P0·QA=CLI 세션).

**P0 프로브(5차 런 성공)**: 1차 런이 C정리로 삭제된 Library 재구축 중 12:43 강제종료 → 재개 2~4차 런이 InitialRefresh에서 크래시(UNITY_EXIT=5 — 덤프 판독: Unity.exe null 역참조 READ 0x18, OutputImporterLogsForArtifact). 수술: ArtifactDB·SourceAssetDB·lock 2종·TempArtifacts·Bee·ShaderCache·StateCache 삭제 + Artifacts 34G 보존 + 킬윈도우 아티팩트 296건 삭제 → 5차 런 정상 종료(UNITY_EXIT=0·error CS 0·리포트+PNG 2장).
- P0-a 카메라: 정상 세션=카메라 1개 전바인딩 정상. 원점 카메라 생성 주체=PlayerMovement.ApplyFollowCamera "씬에 카메라 부재 시 원점 생성" 분기(로그 스택 :1609 실측) — 세션 잔존 카메라가 Camera.main을 탈취해 조준 원점 고정 구조.
- P0-b 말: 애니 클립이 루트 본("Root")을 270도 눕힘(애니 유 localEuler=(270,0,0)·bounds(0.79,2.16,1.62) vs 애니 무 (0,0,0)·(0.34,0.87,1.09)) → 클립 분기 확정.

**P1 카메라 단일화(병행 텔레그램 세션 소유 — v2 재구현)**: IsPlausibleAimCamera(60m 플라우시빌리티 가드)+BindFollowCameraFromScene(Awake 취득 체인 추출·자가치유 공유)+ApplyFollowCamera 자가치유 3단(파괴/비활성 재바인딩+SyncFollowBindingsToRendered 수렴+FollowCamera 불변식 동기화)+양불합격 null 폴백+경고 세션 1회+ArrowManager [BowAim] camPos. CLI 세션 v1(SyncFollowBindingsToRendered 단독+90m)은 병행 세션이 재설계 교체 — 구 API 잔존 0건 검증.

**P2 말 누움 수리(CLI 세션)**: HorseMountSystem.HorseRootPoseLock — 스폰 시 루트 본 바인드 localRotation 캡처(Animator 첫 평가 전)+LateUpdate 회전만 복원(position 무손상), 본 미발견 시 폴백 스킵. SpawnHorse 단일 경로로 3품종 커버 — 에셋 무손상(클립 수술 불요).

**P3·P4(이전 세션 착지 확인+잔여)**: 활 그립(WeaponEquipManager 4마커)·폭탄 착지 y쿼리(Bomb 2)·호 그라디언트+대시 흐름(BombThrower 11)·착지링 펄스(TelegraphArcIndicator 5) 착지 확인 + BombThrower OnDestroy 누출 정리 신설(선례 패턴).

**계약**: HorseArrowCamera207Tests 9건(A1 Sync수렴/멱등·A2 배선·A3 가드매트릭스·A4 분기핀·A5 camPos·H1 배선·H2/H3 리플렉션 캡처→270도 변형→복원·B1 OnDestroy) + BowAimAlignmentTests 신규 1건(가드 배선) — **10/10 GREEN**(병행 세션이 A2~A4를 v2 API로 재결합, H/B 핀 무손상).

**검증(구분 표기)**: 컴파일 error CS **0** · 전수 EditMode **2877 중 2857 통과 / 20 실패 = 206차 baseline 1:1(diff 0)** · 런 5분40초 · Play 미실행(사용자 게이트).

**Play 확인(통합 재Play — 195~207차)**: ①말 서서 스폰(누움 소멸)+길들이기→탑승 ②화살 리티클 조준점 착탄([BowAim] camPos=팔로우 실좌표) ③활 손 정위치(바닥 긁힘 소멸) ④폭탄 호(그라디언트+대시)+착지 원형 동시 표출(MainScene) ⑤강도 트림 후보: 그라디언트 알파·펄스 주기·대시 1.6u/s·폭 0.03→0.08.

**누적 대기**: 통합 재Play(195~207차) · 커밋 승인.

## 11-27 09:5x — 206차 — 활 드로 교체·화살 카메라 단일소스 1차·말 균일 스케일 복귀·폭탄 트레일 강화·창 콤보 속도 (백필 — 텔레그램 병행 세션, QA GREEN — Play 대기)

**사용자 지시(오전 재보고 5건)**: 활쏘기 애니=플레이어 활쏘기.fbx 교체 미완 / 창 2휘두르기 콤보(1·2단 데미지+슬래시)+속도 상향 / 말 찌그러짐 재보고+탑승 체감 부재 / 폭탄 트레일 강화+충격링 제거 / 화살 조준선 불일치. 읽기전용 리서치 3서브 → P206 구현·QA.

**P1 활애니**: DrawShoot(홀드 드로) 모션 → 플레이어 활쏘기.fbx 교체(ArcheryShot 무변경 — 195차 반영 유지) + MixamoControllerBuilder 매핑 갱신, 구 guid 잔존 0 검증.
**P2 창 콤보**: 데미지 체인 입증(1단 0.16·2단 0.66 → ResolveComboSwingHit→TakeDamage 도달 — 수리 불요), 속도 m_Speed 3→4(풀콤보 1.51→1.13s), 핀 2건 갱신+1·2단 소스핀 보강.
**P3 말**: 205차 y보정 제거 → 균일 스케일 복귀(찌그러짐 근원 상수 삭제), saddle.glb 안장 시각 부착(길들이기 성공 시 등 부착·멱등), PlayerMovement 탑승 게이트 3건 HorseMountSystem 통일, Test_10 안장 지급 지연 재시도(3회). Play 튜닝 대기: 안장 등높이 0.65/폭 0.22.
**P4 폭탄**: 트레일 강화(연기 0.6→1.2s·밀도 24→48, 스파크 0.25→0.5s·밀도 12→24, maxParticles 96→256) + TrailRenderer 리본 신설(폭 0.22→0.02 테이퍼) + 폭발 충격링 제거(ShockwaveRingFX D 스테이지 호출부만, 공유 유틸 무손상) — 착지 빨간 링 유지 계약 확인.
**P5 화살**: 조준 카메라 PlayerMovement.FollowCamera 단일소스 고정(Camera.main 폴백) + [BowAim] camId 추가 + TestTerritoryCombatSetup 카메라 중복 생성 수리(기존 카메라 재사용·태그 중복 해소).

**검증(구분 표기)**: 컴파일 error CS **0** · 전수 EditMode **2867 중 2847 통과 / 20 실패**(기존 baseline+공격·가드·홀드 구간 — P206 무연관 판정) · Play 미실행(사용자 게이트).
**후속(207차 이월)**: 10:13 Play 실측 — 화살은 여전히 원점 매몰 카메라(camId -2578) 조준(레이 원점 세션 내내 (0.03,-0.08,0.06)), 말은 균일 스케일에도 **옆으로 누운 상태**(영상 고배율 판독) → 207차 계획 docs/207_HORSE_ARROW_BOW_BOMB_PLAN.md(P0 프로브 확정 후 수리).

---##

## 11-27 01:4x — 205차 — 전투 FX·말·인벤 8건 수리 (QA GREEN — Play 대기)

**사용자 지시(8건)**: 활애니 미교체 / 화살 조준선 / 창 2연타 / 패막 청백 / 폭탄 알림 제거 / 폭탄 VFX 고품질화 / 말 찌그러짐 / Test_10 인벤 안장. 계획 문서: docs/205_COMBAT_FX_HORSE_INVENTORY_PLAN.md — "진행" 승인 실행.

**P1 활애니 — 교체 정상 입증(프로브 결정 증거)**: Player_AC.ArcheryShot 모션=**이미 플레이어 활쏘기(772a9018) 귀속**(195차 교정 반영 — YAML 재실측) → BowProbe205 에디터 프로브(신설, QA 실행): 클립 "Take 001" **5.067s(152프레임)·humanMotion=True·소스 GUID 1:1 일치·플레이어 아바타 isHuman=22본·자동수리 미진입(결함 0)** — 병사 대조군 교차 배선 격리 유지. 코드상 교체 완료 → 잔여는 재Play 체감 검증.
**P3 창 2연타 — 근원 확정+수리**: 스피어 공속 **1.5s > 팔로업 클릭 윈도우 0.85s** = 세그먼트2 구조적 도달 불능(Editor.log 실측 — 거부 14건·팔로업 해결 0건). 수리: SpearFollowupClickWindow=1.8s + 스피어 경로 4개 소비 스위치 일치(이중발화 가드·176차 계약 유지). P3 보고: PlayerCombat 거부식의 스피어 플래그 미반영 — 즉시 더블탭은 쿨다운 게이트로 별도 판정 여지.
**P4 패막 순백 — 근원 확정+수리**: 색은 전부 이미 순백(ParryWhite 1,1,1) — **근원=플래시 피크 알파 0.85(오파크 미도달 → 씬 15% 잔존 = 청톤 씬에서 청백) + 삼각 램프가 피크 관통 불가(실측 최대 ~0.95)**. 수리: 피크 1.0(씬 기여 0 — 수학적 순백) + 피크 홀드 구간(30~144fps 오파크 프레임 보장). ParryWhite·링/림 무손상.
**P7 말 찌그러짐 — 비율 근원 수리**: GLB 3종 축별 실측 — 비균일 스케일 0건, **근원=모델 비율(체고/체장 0.74~0.76 vs 실제 말 표준 0.62~0.66)**. 수리: NormalizeHorseScale 비균일 확장(체장 2.3m 유지 + HorseTargetHeightRatio=0.64 y축 보정 — 백마 yScale×1.99 재계산 검증). 195차 핀 갱신.
**P5 폭탄 알림 제거**: PlayerNotify.Notify 1행 제거 — 조용한 거부(return+로그 유지). BombDamageConsume193Tests 핀 갱신(Not.Contain).
**P6 폭탄 VFX 고품질화**: 투척 트레일(Bomb_Trail — 연기 퍼프+주황 스파크 파티클·World 시뮬·머티리얼 캐시) + 폭발 다단(A 플래시 퀵 0.05s → B 화구 펄스 0.15s → C 기존 화염 유지 → D 충격 링 ShockwaveRingFX → E 연기 페이드 1.2s). 193/189/195차 핀 전부 무손상.
**P8 Test_10 인벤 안장**: 폭탄 지급 블록 직후 `bombInv.AddItem(PlayerInventory.Saddle, 1)` 1줄(204차 월드 바구니 병존 유지).
**P2 화살 조준**: 정적 소거 소진 상태(195차 산술 정상+176차 수식 단일화) — 재Play 후 영상 프레임 실측 예정(본 차 미수리 — 검증 게이트만).

**계약**: 신규 19건(BombNotifyAndSaddle 3·HorseRatio 1·BowProbe 핀 1·SpearComboHit 7·ParryWhite 3·BombExplosionVFX 4) + 핀 갱신 3건(HorseScaleNorm 비균일·ParryFlash T3 피크 1.0·BombDamageConsume Notify 제거) — **전부 GREEN**.

**검증(구분 표기)**: 컴파일 error CS **0** · 전수 EditMode **2866 중 2846 통과 / 20 실패 = 204차 baseline 1:1(diff 0)** · BowProbe205 종료 0(결정 증거 확보) · 런 성립 3점 통과 · Play 미실행(사용자 게이트).

**Play 확인(통합 재Play — 195~205차)**: ①활 발사 시 플레이어 당김→발사 애니(코드 교체 정상 입증 후 체감 검증 — 미재생 시 영상 프레임 실측) ②화살=조준선 3자 정합 체감(미정합 시 영상 실측) ③창 2클릭=2타(두번째 휘두르기 타격) ④패막 시 화면 순백 플래시 ⑤폭탄 0개 무장 시도=알림 없이 조용한 거부 ⑥투척 트레일+폭발 다단 체감(강도 트림 후보: 트레일 0.6s·플래시 0.05s·연기 1.2s) ⑦말 비율(체고 1.47m/체장 2.3m) 자연스러움 ⑧Test_10 인벤에 말 안장 표시.

**누적 대기**: 통합 재Play(195~205차) · 커밋 승인.

---##

## 11-27 00:1x — 204차 — Test_10 말 이동 테스트(야생 말 1 + 안장 바구니 1) (QA GREEN — Play 대기)

**사용자 지시**: "테스트씬 10에 안장도 하나 넣어줘 말 움직임 테스트해보게".

**실측 근거**: Test_10은 말 전무 — ①HorseMountSystem 야생 스폰 = MainScene 전용 씬 게이트(195차 P5 — HorseSpawnSceneGate195Tests G1~G4 핀) ②씬 파일에 말 배치 0건 ③길들이기 체인은 완결 존재(안장 보유+E → HorseTamingUTK 미니게임(자동부트 DDOL) → 성공 시 mat_saddle 소비+Tamed → E 탑승 → MountLocomotion(2.2/7.5) → E 하차·좌클릭 발길질) — 스폰 세트만 보강하면 전 흐름 테스트 가능 ④안장 픽업 = LootBasket.Create 선례(단, 기본 lifetime 30s 자동 소멸 → 3600f 필수).

**구현(HorseMountSystem.cs 단일 파일, +22행)**: Awake 말미 `TestSceneSpawn()` 훅(1회) — 씬명 게이트(`!= "Test_10_TerritoryCombat" return`) → basePos=PlayerMovement 위치(SpawnHorses 선례 동일) → **야생 백마 1**(SpawnHorse 경로 재사용 — 195차 P5 체장 2.3m 정규화+접지+애니 자동, 오프셋 +8,0,5) + **안장 바구니 1**(LootBasket.Create lifetime 3600f — 30s 소멸 방지, 오프셋 +2,0,4, AddItem(PlayerInventory.Saddle,1)). 195차 야생 스폰 게이트·ApplyImport 게이트 1행 무수정(별도 명시적 경로).

**계약**: HorseTestSceneSpawn204Tests 6건(씬 게이트 배선·말1+바구니1·lifetime 3600f·195차 G3 재핀·pending 복원 후 훅 순서·호출 1회) — **6/6 GREEN** + HorseSpawnSceneGate195Tests 5/5 무손상. 중간 CS0117 1종(부모 스펙 오류 — `PlayerInventory.ItemData.Saddle`는 존재, 실제 `PlayerInventory.Saddle` 정적 필드) 수리(시스템+핀 동시).

**검증(구분 표기)**: 컴파일 error CS **0** · 전수 EditMode **2847 중 2827 통과 / 20 실패 = 203차 baseline 1:1(diff 0)** · 신규 6건 GREEN · 런 성립 3점(XML mtime 00:09:43·EXIT 2 정상종료·프로세스 정리) 통과 · Play 미실행(사용자 게이트).

**Play 확인(Test_10)**: ①씬 시작 시 시야 근처 백마 1 + 안장 바구니 1 ②바구니 E → 안장 픽업 ③말 근접 E → "[E] 말 길들이기 (안장 장착)" → 미니게임 성공 → 안장 소비 확인 ④E 탑승 → WASD 걷기/Shift 갈수기 이동 ⑤좌클릭 발길질 ⑥E 하차. 위치/거리 불편 시 오프셋 상수(8,0,5 / 2,0,4) 트림.

**누적 대기**: 통합 재Play(195~204차) · 커밋 승인.

---##

## 11-26 23:5x — 203차 — 건설 홀로그램 VFX 에셋 통합(머티리얼 스왑 + 프로젝션 레이) (QA GREEN — Play 대기)

**사용자 지시**: "홀로그램 vfx에셋를 넣어뒀거든 지난번에 계획 세웠던 건물 홀로그램화 진행해줘" — 170차 계획 실행 + 임포트 에셋(Assets/Holograms/, 11-26 23:20) 통합.

**170차 상태 재확인(실측)**: 형태 패리티(Phase A/B)는 **이미 구현·테스트 잠금 완료**(ConstructionHologram.cs [170차 P1] 경로 — BuildHologramVisual 단일 소스 + P170_* 계약 3건 GREEN, 코드 11-23 반영) — QAPROGRESS 기록 부재로 보고 누락됐던 상태. 이번 라운드 잔여 = **머티리얼(VFX 에셋) 통합**.

**실측 근거**: 에셋=Shader Graph 기반(Template_Hologram.shadergraph, VFX Graph 17.4.0 설치) · 주 틴트 속성 `_Hologram_Color`(alpha=투명도)/`_Texture_Tint_Color` · 베이스 Grid_Hologram_Empty.mat(텍스처 참조 없음 클린) · HologramVFX.prefab=VisualEffect+Light 완전 리그(프로젝션 레이+먼지).

**구현(위임 — 2파일+리소스 복사)**:
- **P0 리소스 복사**: Grid_Hologram_Empty.mat → Resources/Holograms/ConstructionHologram_Mat.mat · HologramVFX.prefab → Resources/Holograms/HologramVFX.prefab(원본 데모 무손상, 로드 경로 확보)
- **P1 머티리얼 스왑**: CreateHologramAssetMaterial 신설 — **3단 폴백 체인**(Resources.Load → Shader.Find("Shader Graphs/Template_Hologram") → null=기존 URP Lit 반투명 경로 유지). 속성 세팅: _Hologram_Color=틴트(alpha 0.35)·_Texture_Tint_Color=틴트(alpha 0.2)·Fresnel 1.5·Emission 2(에셋 기본 20 과강 트림)·Pattern_Density 250·renderQueue 3000 계약 유지. UpdateTint는 자산 속성 우선(_Hologram_Color/_Texture_Tint_Color) → 기존 _BaseColor/_Color 폴백 분기 뒤 유지
- **P2 프로젝션 레이 VFX**: BuildHologramPrimitives shape 성공 경로 끝(폴백 경로 미적용)에 Resources.Load 가드 스폰 — 이름 Hologram_VFX, 스케일 Mathf.Max(1f, footprintRadius/2.5f), 로드 실패 시 조용한 no-op. ghostRoot 하위 → 접지 이동·완료 소멸 자동 추종
- 유효(청록)/무효(적) 색 계약: ValidColor/InvalidColor 리터럴 무변경 — 자산 셰이더의 색 파라미터에 그대로 매핑(170차 확인1 기본값 준수)

**계약**: ConstructionHologramTests [203차] 6건(3단 체인 순서 핀·UpdateTint 자산 우선·queue/알파 리터럴·VFX 가드 스폰 위치·리소스 사본 실존·원본 무손상) — **6/6 GREEN**, 기존 25건 무손상.

**검증(구분 표기)**: 컴파일 error CS **0** · 전수 EditMode **2841 중 2821 통과 / 20 실패 = 202차 baseline 1:1(diff 0)** · 신규 6건 GREEN · 런 성립 3점(XML mtime 23:54:35 신선·러너 마커·EXIT 0) 통과 · Play 미실행(사용자 게이트).

**Play 확인(마을씬 건설창 — 170차 Play 투어 통합)**: ①6종 설계도 배치 시 홀로그램=실물 형태(형태 패리티 — 기존 게이트) ②홀로그램 표면 스캔라인/그리드/프레넬 발광(VFX 에셋 머티리얼 적용) ③무효 위치 시 적색 틴트 전환 ④건물 바닥 프로젝션 레이+먼지 파티클 ⑤시각 과강 시 트림 후보: _Emission_Scale(2)·HologramTintAlpha(0.35)·VFX 스케일(footprintRadius/2.5) 상수 보고

**누적 대기**: 통합 재Play(195~203차) · 커밋 승인.

---##

## 11-26 19:0x — 202차 — F 상호작용 소속 병사 장비 착용 + Test_10 적용 증명 (QA GREEN — Play 대기)

**사용자 지시**: "테스트 10씬에도 장비 부착 확인 + F로 병사와 상호작용 후 자기 소속 병사에게 장비 착용".

**실측 근거**: ①Test_10_TerritoryCombat 병사 = **GuardPlaceholder 경로**(SpawnSoldier — 201차 배선 적용 대상, SoldierTargetHeight 1.8m 정규화) ②**EquipAllyItem이 이미 존재하나 호출점 0개**(P30-C 사장 코드 — 6슬롯 분류·인벤 소비·이전 장비 반환·GVS 즉시 갱신 전부 내장) ③F키 체계 선례 완비(SoldierInteractBridge.RaiseInteract → SoldierInteractUTK 지급 플로우 — 음식/약 2모드).

**구현(2파일 additive)**:
- **GuardPlaceholder**: SelectionMode에 SelectingEquipment 추가 + BeginEquipmentSelection(음식/약 진입 선례 동일) + GetInventoryItemsByMode 장비 필터(Weapon||Armor — 무기 16종+투구/갑옷/신발/장갑/방패, shield=Armor 카테고리)
- **SoldierInteractUTK**: ItemMode.Equipment + 아군 메뉴 "장비주기" 버튼(물약/음식 옆 — 2열 그리드 3행) + ShowItemSection 3분기("🛡️ 장비 선택") + **GiveItem 장비 분기 → EquipAllyItem 활성화** — 적병사는 내부 가드로 거부("적병사에게는 장비를 직접 등록할 수 없습니다")·기존 음식/약 경로 무손상
- 착용 동작(기존 EquipAllyItem 그대로): 슬롯 키워드 분류(무기=id category/투구·갑옷·신발·장갑·방패=id 접두+표시명 폴백 — wood_boot/steel_glove/wood_shield 단수형 id도 표시명 폴백으로 정확 분류) + 인벤 소비 + 교체 장비 반환 + RequestRefreshFor 즉시 GLB 재부착
- 201차 랜덤 장비와 조합: 스폰 장비 위에 플레이어가 F로 상급 장비 교체 가능 — 사망 시 착용 장비 그대로 드롭(DropEquippedItems)

**Test_10 증명**: 소스 체인 계약(SpawnSoldier→GuardPlaceholder→Start→SpawnEquipment→[201차] BootIfMissing+GVAS 자가 생성) — 씬 진입 없이 배선 증명. 실렌더는 Play 게이트.

**계약**: GuardEquipFlow202Tests 4건(UI 배선·GP 모드/필터·EquipAllyItem 핀·Test_10 체인) — **4/4 GREEN** + 스테일 핀 1건 완화(InteractionActionActivationTests 72차 ItemMode 시그니처 — Equipment 추가 [202차] 갱신). 중간 컴파일 실패 1회(핀 갱신 시 주석이 닫는 쉼표를 삼킴 — CS1003 즉시 수리).

**검증(구분 표기)**: 컴파일 error CS **0** · 전수 EditMode **2835 중 2815 통과 / 20 실패 = baseline 20 1:1(diff 0)** · 신규 회귀 0 · Play 미실행(사용자 게이트).

**Play 확인(Test_10)**: ①F로 병사 접근 → 아군 메뉴 "장비주기" → 인벤 장비(무기/방어구) 목록 → 주기 → 착용+GLB 즉시 부착 ②적병사에 장비주기 시도 → 거부 메시지 ③교체 시 이전 장비 인벤 반환 확인 ④스폰 장비(201차 랜덤)와의 조합.

**누적 대기**: 통합 재Play(195~202차) · 커밋 승인.

---##

## 11-26 18:5x — 201차 — 폭탄 투척 GLB 부착 + 병사 랜덤 장비(부트 수리·무기 롤·신장 정규화) (QA GREEN — Play 대기)

**사용자 지시**: "폭탄 던질시 구형에 폭탄 glb 부착 + 병사 랜덤 장비/무기 착용, 병사 크기 맞춤 부착, 레벨 높을수록 고급 장비, 죽이면 전리품 드롭" → 계획 v2 승인 실행.

**P0 격차 확정(실측)**: ①투척 폭탄 = 런타임 Sphere 프리미티브(BombArmController.SpawnThrownBomb — 프리팹은 메시 없음) ②**병사 무장비 근원 = GuardEquipmentSystem 런타임 부트 전무**(테스트만 AddComponent — 씬 Instance null → SpawnEquipment 조용히 스킵 → 장비 시각·장착장비 드롭 전부 공백. GVAS Ensure도 무호출) ③**SpawnPlayerLoadout에 무기 롤 자체가 부재**(helmet~shield만) ④드롭 파이프라인은 이미 완결(GuardPlaceholder:634 — LootBasket+SoldierDropTable+**DropEquippedItems 장착 장비 100% 드롭**+최소 보장) — 착용이 생기면 자동 충족.

**P1 폭탄 GLB(BombArmController)**: SpawnThrownBomb — 구형 **물리 전용 유지**(SphereCollider/Rigidbody/Bomb/폭발·히트스톱·셰이크 무손상) + MeshRenderer OFF + 자식 GLB(bomb_explosive → 폴백 bomb → **구형 무음 폴백** — 177차 발사체 계약 준용) + 월드 최대축을 구형 지름(bombScale) 정규화 + bounds 무효 Destroy 가드 + 정적 캐시 1회 로드.

**P2 병사 장비**:
- **GES.BootIfMissing 신설**(Play 전용·DDOL·멱등) + SpawnEquipment 상단 부트 — 무장비 스킵 소멸(착용 → 시각+드롭 동시 충족)
- **무기 100% 착용 신설**: gp.WeaponItem = RollWeaponLoadout — 등급=RarityProbabilityTable.Roll(level)+럭키+난이도 승격 → **티어 밴드(레벨 높을수록 wood→steel→stone→crystal)** + **kind 4종 랜덤**(검/창/활/단검 — GLB 16종 전종 실존) · id 규약 weapon_{kind}_{tier} · DB 부재 시 검 폴백
- **GVAS 자가 생성**(SpawnPlayerLoadout 후 — RequestRefreshFor 자가힐 패턴 준용) — 착용 데이터의 GLB 부착 보장
- **크기 맞춤 부착**: GuardVisualAttachSystem.NormalizeHandItemScale — 손에 쥐는 슬롯(무기/방패)만 **병사 신장(body bounds 높이)×비율**로 실측 bounds 균일 보정(검 0.70·창 0.95·활 0.75·단검 0.40·방패 0.40) — GLB 자연 크기 편차·덩치 1.8x 무관. 신체 착용 슬롯(Helmet/Armor/Shoes/Gloves)은 본 공간 자동 전파 유지(부착 핏 무손상). 스윙 트레일(ST3) 무손상.

**계약**: SoldierBombVisual201Tests 9건(폭탄 3+배선 4+스케일 2) — **9/9 GREEN**. 기존 189/193차 폭탄 핀·ST3 트레일 핀 무손상.

**검증(구분 표기)**: 컴파일 error CS **0** · 전수 EditMode **2831 중 2811 통과 / 20 실패 = baseline 20 1:1(diff 0)** · 신규 회귀 0 · Play 미실행(사용자 게이트).

**Play 확인**: ①투척 폭탄이 bomb GLB로 보이는지(구형 아님) — 크기 틀리면 bombScale 대비 비율 트림 ②병사 무기/방어구 착용(레벨 고급 밴드+kind 랜덤) — 크기 어긋나면 비율 상수 트림(0.70/0.95/0.75/0.40) ③병사 처치 시 장착 장비+드롭테이블 전리품 바구니 생성 ④과다 부착 창백화 여부.

---##

## 11-26 17:5x — 200차 — UI 클릭 글로우(P4 — 눌린 요소 경계 플래시) 구현 (QA GREEN — Play 대기)

**사용자 지시**: "클릭 글로우도 진행" — 194차 계획안 P4(눌림 글로우) 승인 실행. 근거 회수: 세션DB(20261126_004235/110934) — "물결은 커서 위치 전용이고 요소 자체의 눌림/글로우가 없다" 확정 분.

**구현(UTKPressGlow.cs 신설 + 부트 훅 1줄)**: UTKClickRipple(커서 위치 원형 물결 — 기존 운영)과 상호 보완 — 물결=클릭 지점, 글로우=**눌린 요소 자체 경계**.
- **앵커 산정**: evt.target(최심 피킹=라벨/아이콘)에서 최소 크기 26px 요소까지 최대 4층 상승(버튼/슬롯 판) — 루트 상승 금지(전화면 글로우 방지), 찾지 못하면 target 자체
- **연출**: worldBound 2점 → WorldToLocal → 요소 경계+3px 패드 라운드사각(반경 8px) 순백 경계 2px + 내부 채움 0.10 — **스케일 없는 원위 플래시**(0.55 피크 즉시 → EaseOutQuad 페이드 0.22s — 눌림 즉시 발광 감각)
- **계약 연속성**: pickingMode Ignore·절대좌표·Every(16L) 드라이버·세대 토큰·MaxGlows=8 예산 퇴거·BringToFront 재선두·빈 화면 가드 — 리플 선례 1:1. 벡터메시 0(84차). 커서는 자체 재선두로 글로우 위 유지
- **부트**: UIToolkitBootstrap — UTKClickRipple.Ensure() 직후 1줄(리플 계약 테스트 무손상)

**계약**: UTKPressGlowTests 6건(상수 핀·오버레이/앵커·상승 경계·세대/예산·부트 훅 순서·벡터메시 0) — **6/6 GREEN**. 리플 테스트(기존) 무손상 통과.

**검증(구분 표기)**: 컴파일 error CS **0** · 전수 EditMode **2822 중 2802 통과 / 20 실패 = baseline 20 1:1(diff 0)** · 신규 회귀 0 · Play 미실행(사용자 게이트).

**Play 확인**: 모든 UTK UI(인벤/핫바/창/버튼/슬롯) 클릭 시 — ①눌린 요소 경계에 순백 플래시 ②클릭 지점 원형 물결(기존) 병존 ③과다 연타 시 예산 퇴거로 창백화 없음. 강도 트림 대기: 피크 0.55·수명 0.22s — Play 후 미감 보고 시 상수 트림(185차 96→64 선례).

**누적 대기**: 통합 재Play 판정(195~200차) · 커밋 승인.

---##

## 11-26 17:4x — 199차 — bow JPEG 감량(26MB→0.5MB 텍스처) + dish COLOR_0 정점수술 12종 (QA GREEN — Play 대기)

**사용자 지시**: 198차 옵션 "1.2 모두 진행" — ①bow 감량 ②dish 정점색 수술.

**옵션1 — bow 감량(Tools/bow_jpeg_slim199.py)**: wood_bow/stone_bow 임베드 JPEG 4096²(13MB×2) → **1024 LANCZOS q88 재임베드**(12.6MB→257KB·13.1MB→271KB — GLB 15.7→3.4MB·16.1→3.2MB, 총 **24.4MB 감량**). 192차 relayout 레시피(버퍼뷰 전체 재배치·byteOffset/byteLength 재계산·이미지뷰만 대체) + 재파싱 디코딩 검증. 백업 Tools/bow_prebake_backup199/(원본 4096 보존). **vision 패리티 확정** — before/after 아이콘 시각 동일.

**옵션2 — dish 정점수술(Tools/dish_quality_bake199.py)**: UV붕괴 팔레트형(dish_ 12종, 720² Food_ColorGrid2)의 유일 디테일 개선로 — **COLOR_0 VEC3 f32 주입 = 정점AO(절대반경 5cm·밀도정규화)×에지다크닝(위치그룹 면법선 상호편차>55°→×0.84)**, 스펙클 없음(음식 부자연)·**팔레트 PNG 바이트 무수정**(도구 내 백업 대조 검증). **신규 실측: dish GLB 2메시 구성 = 시각 메시(NORMAL·TANGENT) + 충돌 프록시(POSITION 전용·OMI_physics_body)** → 수술·AO 대상을 NORMAL 보유 프리미티브로 한정(충돌 프록시 스킵 12종). 백업 Tools/dish_prebake_backup199/ + 매니페스트 dish_bake_manifest_199.json + 재수술 가드. 크리스 밀도 실측: braise 7127/7880·steak 5815/6108(하드엣지 스타일 — 에지 강조가 골조화 역할).

**검증(vision 3점)**: dish 12종 — 접시/식재료 접촉면·틈새 음영으로 **떠 보이는 이질감 소멸·무게감/밀도감 개선, 파손·과음영·형상 변형 0**("즉시 적용 가능" 판정) / bow — 시각 패리티. avgRGB 변위 반영: stirfry 0.74→0.60·jeon 1.00→0.83(AO 곱연산 — Play 후 과음영 시 AO_FLOOR 상향 폴백).

**계약**: ItemAssetQuality199Tests 5건(bow 1024·<600KB·<4.5MB + 백업 4096 보존 2건 / dish COLOR_0 주입·매니페스트 12종·팔레트 PNG 바이트 동일 3건) — **5/5 GREEN**. before/after 고립 재베이크 = IconBakeSurgery199(신설 에디터 프로브 — SyncBakeItem 리플렉션, 14종, TestOutput/icon_surgery_199/{before,after}).

**검증(구분 표기)**: 컴파일 error CS **0** · 전수 EditMode **2816 중 2796 통과 / 20 실패 = baseline 20 1:1(diff 0)** · 신규 회귀 0 · Play 미실행(사용자 게이트).

**Play 확인**: 인벤/월드 요리 아이콘 입체감(틈새·접촉 음영) — 과어둡으면 AO_FLOOR 0.60 상향 트림. 활 무변화(패리티) 확인. 누적 대기: 195~199 통합 재Play · 커밋 승인.

---##

## 11-26 16:1x — 198차 — 아이콘 per-item 텍스처 오버라이드(개별 교체 인프라) + P0 전수 감사(교체 이득 종 판정) (QA GREEN — Play 대기)

**사용자 지시**: "개별 오버라이드 텍스처 개선도 가능해?" → 가능 확인 → "진행" 승인(P0 감사→훅 구현→판정 보고).

**P0 전수 감사(377종 — GLB JSON/BIN 직파싱, /home/korea/tmp/icon_tex_audit.json)**:
- **텍스처형 228 / 정점컬러형 149**. 아이콘 대상(관례후보1+명시맵) 임베드 알베도: 무기·방어구·부속·약초(a/h/m/p 1-10)·음식·퀘스트 = **1024~2048**, 국기·활 3종 = 4096 — **교체 이득 저해상 = 0종**
- **dish_ 12종 = 720×720 + UV붕괴**(uv 유니크율 0.001~0.05 — Food_ColorGrid2 팔레트, 면단위 스왑 픽) — 텍스처 교체로 공간 디테일 불가(192차 확정 레버: 정점색 수술). 팔레트 스타일 자체라 현상태 유지 판정
- crop 식물 9종 = 외부 URI(../../Textures/farm_atlas.png) — **실존 1024² 확인**(월드 정상). 감사 함정: 외부 URI는 임베드 파싱 실패(max=0)로 나오므로 mime/uri 2차 확인 필수
- **wood_bow/stone_bow = 임베드 JPEG 4096² 13MB×2(26MB)** — 시각 여유 과다(256px 아이콘·월드 노출 모두 불가시 차이), 파일량/베이크 디코드 부담만. 1024 재임베드 감량(~0.4MB) 옵션 보류(승인 별도)

**P1 훅 구현(2파일+1리소스)**:
- **ItemIconTextureOverrides.cs 신설** — item id→Resources 텍스처 경로 테이블(운영 등록 0건) + TryApply: MPB _BaseMap/_MainTex 교체(머티리얼 인스턴스 금지 — DishGlbCatalog.ApplyTint 동일 규약), GetPropertyBlock 선행으로 _BaseColor 틴트 보존(공존), 미등록=false 무변경
- **GblItemIconRenderer 훅 2곳**(TryMount / SyncBakeFromPrefab — ApplyTint 직후 배치: 오버라이드가 텍스처 최종 결정)
- 월드 비주얼 미개입(GLB 원본 무수정 — 141차 원본해상도 계약 유지), 아이콘 베이크 경로 전용
- probe 리소스: Resources/Textures/icon_override_probe.png(16² 레드 — 테스트 전용, meta Unity 민팅)

**계약**: ItemIconTextureOverrideTests 7건(미등록 무변경·등록 MPB 교체·리소스부재 안전 false·null 가드·dish 틴트 공존(_BaseColor 보존)·테이블 등록분 전부 실로드(향후 수용 자동 감시)·HasOverride 미러) — **7/7 GREEN**.

**검증**: 컴파일 error CS **0** · 전수 EditMode **2811 중 2791 통과 / 20 실패 = baseline 20 1:1(diff 0)** + 197차 스테일 핀(SyncBakeBody) 1건 소멸 — 리핀분 전수 통과 확인 · 신규 회귀 0 · Play 미실행(사용자 게이트).

**결론+선택지**: ①197차 파이프라인으로 아이콘 품질 한계 도달 — 텍스처 교체 이득 대상 없음(정점컬러 다수+dish UV붕괴) ②훅 상시 가동 — 고해상 아트 수용 시 "테이블 1줄+리소스 1장" 즉시 적용 ③옵션(별도 승인): bow JPEG 1024 감량 / dish 정점색 수술.

---##

## 11-26 15:1x — 197차 — 아이콘 고품질화+그리드 충전(직교 전환·4× 슈퍼샘플·3점 조명 릭) (QA GREEN — Play 대기)

**사용자 지시**: "아이콘이 모두 저품질로 보이고 그리드에 꽉차지도 않는다 — 꽉차고 고품질로".

**근원(실측)**: 베이크 128px·MSAA 없음·퍼스펙티브 30°+1.25마진+extZ 후퇴 → 실체 픽셀 ~35px(충전률 저하+흐림). 194차 Play 체크리스트의 "살짝 작아짐" 부작용 체감 확정.

**수리(GblItemIconRenderer 단일 파일 + 계약)**:
- **직교(orthographic) 카메라 전환** — orthographicSize = max(extents.x, extents.y)×IconFillMargin. 194차 extZ 가산(원근 확대 절단 방어)을 구조적으로 폐기 — 깊은 물체도 프레임 정확 핏
- **IconSize 128→256** + **4× 슈퍼샘플**(RT 1024→256, 16탭 박스다운샘플 — MSAA 대체)
- **MSAA 크래시 실측+대체**: MSAA RT ReadPixels가 배치 d3d12에서 GPU 디바이스 크래시(IconBakeFix194 재베이크 실측) → 슈퍼샘플 경로로 대체, antiAliasing 잔존 0 계약
- **3점 조명 릭**(키 웜 0.45/필 0.15/림 0.30 — 카메라 자식, 정리 동반) — 플랫 조명 해소. 1차 릭(1.15/0.4/0.7)은 흰 재질 백색클리핑 실측 → 2차 하향 확정
- **IconFillMargin 1.25→1.12** — 충전률 ~89%(기울임 모서리 여유 포함, Play 후 트림 대상)
- tilt(_orientationOverrides)·고정 정면(center+(0,0,-dist)) 계약 유지

**렌더 검증(vision 이터레이션 3점)**: 접촉시트 — 조명 릭으로 플랫 블롭 해소(음영 깊이·하이라이트·림 확보), 백색클리핑 해소(톤레인지 복원), 경계 선명. 잔여 한계 = 에셋 자체(저폴리 GLB·내장 저해상 텍스처) — 파이프라인 한계 아님.

**계약**: IconQuality197Tests(IconSize 256·BakeRTSize 4×·슈퍼샘플 헬퍼 배선 2경로·MSAA 잔존 0·직교·마진 1.12·조명 릭 3배선)+194차 리핀(직교 수식)+GblItemIconOrientationTests 스테일 핀 1건 정정.

**검증**: 컴파일 error CS **0** · 전수 EditMode **2804 중 2783 통과 / 21 실패** = baseline 20 1:1 + 스테일 핀 1건(SyncBakeBody 시퀀스 핀 — 헬퍼 위임 갱신) → 리핀 후 집중 재런 **18/18 GREEN** · 신규 회귀 0 · Play 미실행(사용자 게이트).

**Play 확인**: 인벤/핫바 아이콘 — ①그리드 충전률(~89%, 모서리 붙으면 1.12→1.15 트림) ②선명도(256px+4×AA) ③입체감(조명 릭). 과노출/어두움 재보고 시 릭 강도 상수 트림.

---##
## 11-26 14:0x — 196차 — 던전 바닥 타일 반짝임(z-fighting/깊이 모호성) 근원 진단+수술 (QA GREEN — Play 대기)

**사용자 지시**: "던전에서 이동하다보면 바닥이 반짝반짝한 지형이 하나 — 눈이 부신. 원인만 파악" → 진단 후 "수리해줘".

**진단(정적 전수+RT프로브 렌더+vision 3점)**:
- 바닥 타일 6변형 재질 전수 = metallic 0·roughness 1.0·emissive 없음 — 재질이 반짝일 수 없음(192차 스펙클=알베도 ±3.5% 각도 무관)
- GLB 기하 전수(BIN 헤더 파싱 함정 수정 — BIN 청크 8바이트 헤더 스킵): **6종 중 5종(3A 제외)이 주 상면에서 0.4~1.3mm 위에 또 다른 상향 면 그룹(삼각형 ~500cm²급)** — 스타일리즈드 불균일 스톤의 제2 층위. 이 sub-mm 높이편차가 중거리 깊이버퍼 정밀도 내에서 시점·섀도우 플리커 = "이동 중 반짝임". 3A는 단일 층위(반짝임 없음) — 사용자 "하나" 지각과 정합(해시 배치)
- v1 수술(무차별 퇴화) 오류 실측: 가시면 삭제 → 구멍(vision "돌의 삼각형 누락·검은 void") → 즉시 정정

**수리(Tools/floor_grout_zfix196.py v2)**: 근접 삼각형을 주면 발자국 XZ 판정으로 분류 — 겹침 lip만 인덱스 퇴화, **분리 스톤 탑은 삭제 없이 주면 높이로 정점 스냅**(0 퇴화/스냅 3·6·0·12·6·6정점 — 근접 잔존 0). 바이트 레이아웃 무변경·COLOR_0(174/192차 AO) 보존·POSITION min/max 재계산. 백업 DungeonFloorGrout196/ + 매니페스트 키(원본=진실원, 멱등).

**렌더 검증(vision 3점)**: v1 — z-fight 소멸하나 void 보고 → v2 — "z-fighting 없음·누락면/구멍 없음·렌더 클린" 확정.

**계약**: DungeonFloorGrout196Tests 4건(G1 수술 GLB md5≠백업 5종·G2 3A 무영향·G3 매니페스트·G4 도구 상수/스냅/퇴화 경로).

**검증**: 컴파일 error CS **0** · 전수 EditMode **2803 중 2783 통과 / 20 실패 = baseline과 FAIL 목록 1:1(diff 0)** · 신규 4건+195차 계약 전부 GREEN · 신규 회귀 0 · Play 미실행(사용자 게이트).

**Play 확인**: 던전 이동 중 바닥 줄눈/테두리 반짝임(플리커) 소멸 — 특히 이전에 반짝이던 지점. 가까이서 보면 변화 없음(원래 정상), 중거리에서 소멸이 정상(깊이 정밀도 개선).

---##
## 11-26 11:4x — 194차 — 전 아이템 아이콘 정면화(오버라이드 20키+프레이밍 Z보정) + 초록 아이콘 근원 종결 (QA GREEN — Play 대기)

**세션 복구**: 닫힌 세션(10:18 텔레그램)의 193차 잔여 QA는 이미 완결 확인 — 컴파일 CS 0·EditMode 2784 중 2764/20 baseline 1:1·QAPROGRESS 193차 기록 착지(10:30:40). 런 결과 통보만 세션 종료로 유실. 본 차는 승인 범위 잔여(P2 판정·P3·P4 계획안) 수행.

**P2 초록 아이콘 — 근원 종결(신규 수리 0)**: 189차 근원(InventoryWindow 프리뷰 본체가 베이크 자리 (10000,1000,10000) 점유 → 11000 이동)이 정답이었음을 3중 실증 — ①IconBakeProbe193 3종 avgRGB 중성(초록 아님) ②전수 베이크 114종 GREENISH 플래그 0건(유일 dish_pickle = 재료 hue 틴트 정상 동작) ③고립 재베이크 24종 오염 센서스 LEAK_TOTAL=0. 사용자 "초록으로 깨짐" 보고는 189차 수리 이전 Play(stale) — 재Play에서 최종 판정.

**P3 전 아이템 정면 아이콘**:
- **IconBakeAll194 신설 프로브** — PlayerInventory 정적 ItemData 전수(공개/비공개+AllBombs/AllTieredGear)+어종 지명종 38종(fish_glb_ 규약·AssetDatabase GLB 직결)+요리 버킷(RecipeCatalog dish_ id → DishGlbCatalog.ModelKey 버킷별 대표 1종·실틴트) = 128 jobs → **114 GLB 베이크·14 절차 폴백**(crop 8·flower_wild·fruit_apple·herb_silver/yellow·dark_cloak·stealth_boots — 기존 폴백 경로 정상)·miss 0.
- **판독 이원화** — 접촉시트 5장 vision(2배) → 후보 41셀 → 4배 정밀 재판정 + 픽셀 절단 검사(배경 TOL 2단)로 오판 제거: 저배율 "잘림" 4건(dish_006/028·mat_stone·wood_armor)은 픽셀 검증 부정, "빈 아이콘" 의심 2건(voidplate·steel_gas_mask)은 어두운 모델 오판으로 해소.
- **확정 결함**: 접시/음식 13버킷(XZ 눕힘·원본 (90,0,0) 굽음 → 측면만 보임)·물약 2·삽(Y장축 자동 롤 Euler(0,0,−90) 눕힘)·곡괭이(평면 위시뷰)·crystal_shard(눕힘)·boar/wolf tooth(Z장축 눕힘)·wood_armor(바닥면 노출) + **실절단 2건**(tool_watering_can 하단·dish_pie 우측 — 픽셀 실측 확정).
- **수리 ①**: _orientationOverrides 20키 등록 — 접시류 (-70,0,0) 통일·물약 2/삽 (0,0,0) 원본 복원·곡괭이 (-65,0,0)·shard (0,0,90)·tusk/tooth (-90,0,0)·wood_armor (0,0,0).
- **수리 ②**: FrameIconCamera dist에 extZ 가산 — 비좁형(자동 회전 제외) Z-깊은 물체의 근접면이 원근 확대로 반시야각 초과(실측 atan(0.63/1.99)=17.5°>15°)하던 근원 수리. 기존 계약 핀 문자열("center + new Vector3(0f, 0f, -dist)"·"Mathf.Max(halfV, halfH)") 무손상 가산형.
- **검증**: 고립 재베이크 24종(IconBakeFix194 — 실코드 경로 SyncBakeItem) → 픽셀 절단 **0건** + bbox 종횡비 전환 실증(원판 접시 0.24→0.99 정면화·장형 0.19→0.53 경사) + vision 8배 판독(경사뷰·직립·내용 판독 GREEN). dish_pie "거대 평면"은 신규 프레이밍+틸트에서 미재현(구 프레이밍에서 파이 자체 측면 프로필이 프레임을 채운 것 — 별개 오염 아님).

**계약테스트**: IconOrientationOverride194Tests 4건 신설(핵심키 등록값·접시 13키 통일틸트·대조군 미등록 계약(dish_pickle/dish_tea/mat_stone)·FrameIconCamera Z보정 순서 어서션) — 전부 GREEN.

**검증(구분 표기)**: 컴파일 error CS **0** · 전수 EditMode **2788 중 2768 통과 / 실패 20 = 193차 baseline과 FAIL 목록 1:1(diff 0)** · 신규 4건 전부 통과 · 신규 회귀 0 · Play 미실행(사용자 게이트). ⚠ 런 무효 1회 경험: 신규 테스트 파일 컴파일 에러(CS0234)로 런이 조기 종료됐는데도 래퍼 EXIT=0 + 파서가 **구 XML을 재출력해 허위 동일 수치** — XML mtime·러너 마커 3점 검증으로 적발 후 재런(qa-verification 스킬 함정 실측 추가).

**Play 확인 목록**: ①아이콘 — 접시/음식류가 내용물 보이는 3/4 뷰·물약/삽 직립·곡괭이 T자·엄니/수정 직립·목갑옷 정면 ②아이콘 잘림 소멸(물뿌리개·파이) ③전 아이콘이 이전보다 살짝 작아짐(Z보정 부작용 — 과하면 상수 트림 요청) ④초록 아이콘 재발 유무(189차 수리 이후 첫 Play).

**P4 계획안(승인 대기 — 구현 미착수)**: UI 클릭 "눌림+글로우" 3요소 — ①클릭된 요소 스케일 1.0→0.96 press(90ms·PointerUp 복귀) ②요소 경계 웜화이트 글로우 스윕(약 300ms·overflow hidden 클립 — CraftBench 골드 물결 선례 재사용) ③기존 64px 커서 물결 유지+불투명도 0.85→0.6 역할 분리 트림. UTKClickRipple 훅 재사용(2중 등록 금지)·신규 계약 4건 예정·레이아웃 무영향.

---

## 11-26 13:2x — 195차 — 몬스터 적색링·청록 회전체 제거 + 활쏘기 리그·배선 수리 + 말 던전 게이트·크기 정규화 + 폭탄 플루언트 고급화 (QA GREEN — Play 대기)

**사용자 지시(6건)**: 적색링+돌아가는 푸른 파티클 잔존 / 화살 커서 불일치 / 활쏘기 애니 미교체 / 폭탄 고급화 / 말 찌그러짐+던전 출현 / 전체 계획 후 진행.

**근원(런타임+정적 실측 확정)**:
- 적색링+청록 회전체 = **SpawnAlert 패키지**(어그로 시 빨간 링 + teal(0.188,0.835,0.784) 체브런 3본이 3~6회전/초 회전 — Play 중 9회 발화 스택 실측) + 몬스터 타격 붉은 지면 링(159차 SpawnImpactRing). 190차가 "유지"한 분이 사용자가 보던 것. 화살 퍼프 웜톤·MagicHit 정리 완료·슬라임 스킬 0종 — 정적 소거로 확정.
- 활쏘기 미교체 = **교차 배선 + 제네릭 리그 이중 결함**: Player_AC.ArcheryShot 모션이 병사 활쏘기.fbx(467cd2e6) 참조(플레이어 활쏘기=772a9018 — 168차 뱅크 guid 귀속 반전), 양쪽 FBX animationType=2(Generic) → 휴머노이드 리타깃 불가. AnyState 전이는 원래 정상(초기 YAML 오판 정정). 트리거 11회 발화 로그와 정합.
- 말 = 씬 게이트 부재(전 씬 무조건 스폰) + **크기**(GLB 체장 ~0.45m; 바인딩은 38/38 정상 — horse v1 오판 정정).
- 화살 방향 = 발사 메커니즘 정상(22발 전부 총구→커서월드점 일치 — [BowAim] 산술 검증).

**수리**: ①SpawnAlert 시각+SFX 제거, SpawnImpactRing 2건(AnimalAI·AfterlifeEnemy 정예 오라) 제거 — 판정/전이 로직 무수정 ②BowRigFix195 원샷: FBX 4종 Generic→Humanoid(isHuman=True 4아바타) + 모션 원본 교정 2건 + 계약 4건(BowClipRig195Tests) ③말 씬 게이트(MainScene 전용·세이브 데이터 보존) + 체장 2.3m bounds 정규화 + 계약 4건 ④폭탄: 호 그레이딩(두께 감쇠+끝단 글로우) + 착지 링 sin 펄스(2Hz) + 폭발 히트스톱 0.05s + CameraShake 연동 — 193차 핀 14건 유지.

**검증**: 컴파일 error CS **0** · 전수 EditMode **2799 중 2777 통과 / 22 실패** = baseline 20 1:1 + 스테일 핀 2건(ParryChain — 적색링 스폰이 우연 만족 / BowClipRig195.R4 — Unity6 API 패치 문자열) → 의도 리핀 후 집중 재런 **20/20 GREEN**. Play 미실행(사용자 게이트).

**Play 확인 목록**: ①어그로·피격에서 적색링/청록 회전체 소멸 ②활 발사 시 플레이어 활쏘기 애니 재생 ③병사 아처 사격 애니 ④말 체장 2.3m 정상 + 던전 미출현 ⑤폭탄 호/링 펄스/폭발 임팩트 ⑥화살 커서 정합 체감(발사 로그는 정상 — 재보고 시 프레임 실측).

---##
## 11-26 10:4x — 193차 — FX 잔여 정리(가드 적색링·피격 청백 Shockwave) + 폭탄 대미지 단일소스·소비 게이트 + 투척 플루언트화 (QA GREEN — Play 대기)

**사용자 승인 범위(어제 밤)**: P1 가드 적색링+Shockwave 제거 · P2 폭탄 로직 · P3 투척 플루언트화 · P4 초록 아이콘 진단+전 아이템 정면 아이콘(P4는 본 차 잔여 — 아래).

**P1 — FX 잔여 정리(11-26 00:0x 적용)**: ①GuardPlaceholder 적색링 제거 — 190차(몬스터 AnimalAI·AfterlifeEnemy)에 이은 병사 3번째 소스, 몬스터 3종 TelegraphArcIndicator.Spawn 잔여 0건(FxCleanup193Tests 계약) ②MagicHit Shockwave 서브이미터 제거("피격시 돌아가는 파란색 파티클" 사용자 지시 — SlashVFXRunner 193차 제거 블록, Simbol 189차와 공존) ③느낌표 경보(SpawnAlert)·폭탄 착지 텔레그래프·공격 판정 무수정(계약 유지).

**P2 — 폭탄 대미지 단일소스(11-26 00:3x)**: 몬스터 대미지 적용 + 투척 시 개수 감소 + 0개 게이트 + UI 갱신(PlayerInventory 변경 이벤트 구독 — Systems→UI 직접 호출 불가 어셈블리 구조 준수, HotbarUIUTK 포함) — BombDamageConsume193Tests 5건.

**P3 — 투척 플루언트화**: 폭발 범위 **빨간색** 텔레그래프(착지 링=explosionRadius) — BombFluidPreview193Tests 5건.

**P4① 초록 아이콘 — 정적 소거 완료(부모 직접, 11-26)**: Theme.uss 초록 토큰 = --c-guild-green 정의뿐 사용처 0 · ProceduralIconGenerator 색 테이블 소거(무기=빨강·폭탄=검정 — 목검/창/폭탄 초록 매핑 부재) · UI 초록 = 내구도/퀘스트 텍스트 전부(아이콘 무관) · IconBakeProbe193(목검/창/폭탄 실베이크 스냅샷 — SyncBakeFromPrefab 그대로 호출) 실행 후 근원 보고 예정. 189차 원인(프리뷰 본체 베이크 자리 점유) 수리는 189차에 착지.

**검증(구분 표기)**: 컴파일 error CS **0** · 전수 EditMode **2784 중 2764 통과 / 실패 20 = 191·192차 baseline과 FAIL 목록 완전 동일(이름 단위 diff 0)** · 신규 계약 14건(FxCleanup 4 + BombDamageConsume 5 + BombFluidPreview 5) 전부 GREEN · 신규 회귀 0 · Play 미실행(사용자 게이트).

**Play 확인 목록**: ①가드·몬스터 어그로/공격 시 붉은 링·호 소멸(판정은 유지) ②몬스터 피격 시 청백 회전 파티클 소멸(히트 원+플래시만) ③폭탄 몬스터 대미지+개수 감소+0개 시 투척 차단+핫바 갱신 ④투척 홀드 중 빨간 폭발 범위 링.

**참고 — 사용자 Play 보고 4건 판정(11-26 오전)**: 적색링·파란 회전 파티클·화살 조준선·활쏘기 애니 모두 현재 코드 반영 완료 상태. 에디터 로그 실측 — 어제 23:51 세션(씬 열기만)·오늘 10:03 세션 모두 Play 흔적 0 → 보고는 수리 이전 시점의 Play 결과로 판정. 재Play 검증 대기.

---##

## 11-25 17:5x — 192차 — 던전 바닥 고품질화·실내씬 수준 마감(정점AO/에지다크닝/스펙클/회전 다양화) (QA GREEN — Play 대기)

**사용자 지시**: "영지 실내씬 처럼 바닥이 조금 더 고품질화 처럼 보이게".

**전제 실측(부모 — GLB 텍스처 추출+vision 판독)**: 팩 Floor_1_*A 6종 = **albedo(팔레트 그라디언트 "Concrate")만 존재** — 노멀맵·오클루전·COLOR_0 전무. 벽(CreateWallSide)/돌판경로(CreateStonePathMat)는 절차 줄눈+노멀 릴리프 보유 → 바닥만 평면 도장처럼 보임. **팔레트 UV=면단위 붕괴 방식이라 노멀맵 파생 무효** → 선례(172차 에지다크닝·174차 정점AO)인 정점색 수술로 대체.

**구현**:
- **Tools/floor_quality_bake.py 신설** — COLOR_0(VEC3 f32) 주입 + 정점AO(174차 compute_ao 재사용·R절대5cm·s²/d²·AO_FLOOR 0.60) × 에지다크닝(위치그룹 면법선 최대상호편차 >55° → ×0.84) + 팔레트 스펙클(저주파64px ±3.5% + 고주파256px ±1.2% — 실내 절차타일 0.06 정합, vision 판독 "subtle·밴딩 완화")
- **6종 전부 적용**(정점 642~1294·굽은면 481~936·이미지 교체) — 슬랩 이음매·테두리가 그루트처럼 읽힘
- 백업 Tools/ao_prebake_backup/DungeonFloor/ 6종 + 매니페스트 DungeonFloor 키 6건(AOBakeContractTests 무충돌 — 전수카운트 min160 여유)
- CreateCellFloorTiles **회전 다양화** — (cell.x*13 + cell.y*29) & 3 → 90° 스텝 4방위(변형 해시와 독립 위상·팔레트 UV 붕괴 스타일 무해·정사각형 슬랩 bounds 정렬 불변·스태틱 결합 무영향)
- 함정 수정 2건: write_floor 변이 j 유실(재파싱 버그)·relayout 대체 PNG byteLength 미갱신(잘린 PNG) — 백업 복원 후 재적용, 검증 강화(전체 PNG 디코딩 어서션)

**검증**: 컴파일 0 에러 · 전수 EditMode **2770 중 2750 통과 / 실패 20 = baseline 완전 동일** · 신규 **DungeonFloorQuality192Tests 4건 GREEN**(COLOR_0 주입/백업 원본 대조/매니페스트 6키/회전 해시/도구 상수 — GLB JSON 청크 실측 어서션) · 191차 계약 무손상 · glTFast 재임포트 성공(log 실측)

**Play 확인**: ①던전 바닥 슬랩 테두리·이음매 음영(그루트 느낌) ②타일 표면 미세 그레인 ③타일 방향 4방위 섞임(반복 타파) ④부하·런타임 무변화

---##

1|## 11-25 16:5x — 191차 — 던전 고품질화 ①바닥 통일(팩 석재 타일) ②포탈 계단굴 (QA GREEN — Play 대기)

**홀로그램 임포트**: 보류 계획 확정 — 웹 다운로드=My Assets 추가만 → 에디터 Package Manager(My Assets)에서 Download(+Import) 후 말씀 → 에셋 실측→SetHologramMaterial 교체→valid/invalid 색 매핑→QA 일괄 진행(사용자 PC 조작 불가로 대기).

**사용자 지시**: "던전을 조금 더 고품질화 — 바닥 지형을 통일하고 지하로 가거나 올라올때 계단 지형".

**전제 실측(부모 — GLB accessor 파싱)**: 던전 팩 Floor GLB 전종 **2.0×0.127×2.0m** = CELL_SIZE(2m) 1:1 정합(팩 원 설계 확인)·Stairs_1A 2.656×2.971×4.274(1층높이)·기존 던전 바닥=156차 흙 단일화(CaveMaterialFactory.CreateDirtFloor 병합 쿼드+MeshCollider 충돌 단일소스).

**① 바닥 통일 — 팩 석재 타일(서브에이전트 구현+부모 수리)**:
- Floor_1 시리즈 6변형 **전 5층 통일 적용**(CellTileVariantKeys — Floor_2/4/5 시리즈 잠금=통일성 계약)
- 결정론 변형 로테이션 `(cell.x*31 + cell.y*17) % 6`(Random 0건) · 타일 bounds 정렬 top≈+0.01
- **돌판 경로(BFS 낟길) 셀은 타일 배제** — 흙 낟길이 석재 바닥 위에 남는 대비(158차 P3-C 안내 기능 무손상, CreateStonePath 무수정)
- StripColliders(비주얼 전용) + **StaticBatchingUtility.Combine**(드로우콜 통합) — 병합 쿼드 MeshCollider 충돌 단일소스 유지

**② 포탈 계단굴(계단 지형)**:
- **하강포탈 = 실제 걸어 내려가는 계단우물** — 굴 셀 2칸을 주바닥 쿼드에서 제외(진짜 구멍) → 굴 바닥 쿼드 y=−1.5+MeshCollider, 박스 벽 3면, **Stairs_1A ×0.5 하위 메시 MeshCollider(CC 접지 이동 가능)**, 아치 굴 바닥(y=−1.5) 이전 — CreatePortalFrame y 지정 오버로드+AlignVisualBottomToWorldY 신설
- 진입 방향 = BFS 경로 결정론 정렬 / 상승포탈은 기하 무변경(리빌드 계약 무손상) / 스폰-포탈 ≥2.2m 이격 계약 유지

**검증**: 컴파일 0 에러 · 전수 EditMode **2766 중 2746 통과 / 실패 20 = baseline 완전 동일** · 신규 계약 **DungeonQuality191Tests 7건 GREEN**(타일 통일/결정론/경로배제/스태틱/비콜라이더/계단굴/상승무변경).

**Play 확인**: ①던전 바닥이 팩 석재 타일로 통일(흙 낟길만 흙) ②하강포탈에서 계단 따라 실제로 내려가 아치에서 E ③상승포탈 기존과 동일 ④부하(스태틱 결합 후 프레임).

---##

## 11-25 15:4x — 190차 — 몬스터 적색링(공격 텔레그래프 시각) 제거 (QA GREEN — Play 대기)

**사용자 지시**: "몬스터 적색링은 아직 안 없어진거 같던데 확인해줘" — 189차 파란 파티클(Simbol) 제거에 이은 잔여 요소.

**근원(특정 완료)**: 적색링 = **몬스터 공격 텔레그래프 호**(TelegraphArcIndicator.Spawn — AnimalAI:1029 / AfterlifeEnemy:204, 발밑 붉은 반투명 링·원 주위 회전 스윕 밴드 동반 — 95차 F5 프리미엄화 계열). 피격 MagicHit의 Shockwave는 청백(실측 — prefab gradient)으로 무관, 텔레그래프가 사용자 어휘 "적색링"과 정확히 일치.

**수리(부모 직접)**: 두 파일의 Spawn 호출 삭제 → `_telegraphIndicator = null` 영구 — 발동 플래시(ConcludeMeleeTelegraph)·인디케이터 정리 null 가드 무영향 확인(사용처 전수 실측). **공격 판정(부채꼴 IsInFrontArc·거리 재확인·빈 스윙)은 무수정** — 시각 없이 판정만 존속. SpawnAlert(!링)·폭탄 착지 텔레그래프·병사 텔레그래프는 별도 유지.

**계약 갱신(2건 — 잠금 리핀)**: CombatFxTelegraphPhaseBTests.T6 — 스폰 경로 Does.Not.Contain 제거계약 + `_telegraphIndicator = null` 핀 / CombatFxSizeQualityPhaseHIJTests.H1 — 동일 리핀(StringAssert.Does 미지원 CS0117 → Assert.That Does.Not 교체).

**검증**: 컴파일 error CS **0** · 전수 EditMode **2759 중 2739 통과 / 실패 20 = baseline과 FAIL 목록 diff 기준 완전 동일** · 신규 회귀 0.

**Play 확인**: 몬스터 어그로 시 발밑 붉은 링/호 소멸 — 공격 회피 판정(거리·부채꼴)은 그대로(시각 경고만 제거).

---##

## 11-25 12:4x — 189차 — 폭탄 UX 고도화(조준/궤적/효과음) + 피격 시그널 제거 (QA GREEN — Play 대기)

**사용자 지시(5건)**: ①아이콘 여전히 이상 — 재질문 "초록색으로 깨져서 떠" ②클릭 뗄 때 던져지도록 ③호모양 궤적으로 각도·방향 미리보기 ④몬스터 피격 원 안 파란 회전 파티클 제거 ⑤폭탄 효과음 폭발음 교체(현재 불 소리).

**근원(특정 완료)**:
- ②근원: press에 즉발 ThrowTowardCursor(애니 후 0.35s 릴리스) — 유저 체감 "바로 던져짐".
- ④근원: 피격 FX(MagicHit 프리팹) 4레이어 중 "Simbol"(마법 심볼) 층 — startColor 그라디언트가 흰색이어서 틴트 통일 경로를 우회, **파란색은 파티클 텍스처에 구움** → 재색 불가, 층 비활성만 유효. 원(Shockwave)·플래시는 유지.
- ⑤근원: 폭발 시 생성되는 불 VFX 프리팹(VFX_Fire_01_Big)에 **AudioSource가 프리팹에 내장**(불 소리) + 프로젝트 폭발음 에셋 0개(70개 오디오 전수 실측).

**아이콘 근원(특정 완료)**: 아이콘 카메라 실측 프로브(IconMaterialProbe — GLB 자체 정상 확인: 텍스처 바인딩·좌표 무결) 후 원격 베이크 자리 오염 특정 — **InventoryWindow 3D 프리뷰 본체(Player_Rigged_Heat 청록)가 아이템 베이크 자리 (10000,1000,10000)와 동일 좌표 상주 점유** → 인벤 열어둔 상태의 핫바/인벤 아이콘이 청록 본체를 겹쳐 찍음(farClip 20 내) = "초록색으로 깨짐". 187차 아이템-아이템 겹침(DestroyImmediate)과 별개 잔여 원인.

**수리(부모 직접 — InventoryWindow)**: 프리뷰 본체 전용 자리 (11000,1000,11000) 이동 — 10000아이템/10500병사/11000본체 상호 farClip 20 밖 격리(최소 707m). 계약 IconBakeSpot189Tests 4건(전용자리/잔존 0건/관례 동결/병사자리 동결).

**수리(서브에이전트 3병렬 — 위임정책 준수)**:
- A 투척 UX: press=BeginAim(조준) / release=ThrowTowardCursor(던짐 — 기존 애니+0.35s 릴리스 유지). AttackSystem 무장 분기는 차단만(이중 투척 원천 제거). 활 전 release 선례 패턴 준용.
  궤적 미리보기: LineRenderer 호(32점, dt=1/24, **실제 투척 물리 동일 시뮬** — aimDir×throwForce+up×upBoost+중력) + 착지 링(반지름=explosionRadius — 폭발 범위 그대로 표시). 색=웜골드(168·172 골드 계열).
- B Simbol 제거: PlayImpactMulti만 DisableImpactSymbolLayer(instance) — 재귀 탐색 "Simbol" SetActive(false). PlayCross는 미적용(사용자 범위 몬스터 피격).
- C 폭발음: **절차 합성** SFX_Bomb_Explosion.wav(1.5s·44.1k·서브톰 65→26Hz 스윕+로우패스 노이즈 바디+럼블 테일+크랙·tanh 클립·−3dBFS·시드 결정론) → BombArmController가 Resources.Load 주입, 불 VFX 인스턴스의 AudioSource만 Destroy(**Molotov 경로 무영향** — 계약 핀).

**계약**: 신규 3파일 15건(BombReleaseAim189 6 / HitFxSymbolRemoval189 4 / BombExplosionSfx189 5 — 파일 존재·소스계약·기존 핀 무손상).

**검증**: 컴파일 error CS **0** · 전수 EditMode **2755 중 2735 통과 / 20 실패 = baseline 1:1(188차와 동일 20건)** · 신규 회귀 0건.

**Play 확인 목록**: ①폭탄 들고 **좌클릭 홀드** → 웜골드 포물선 호 + 착지 폭발범위 링 표시 ②**클릭 떼면** 던지기 애니 → 투척(방향·거리=미리보기 그대로) ③몬스터 피격 시 원+플래시만(Simbol 파란 심볼 소멸) ④폭탄 폭발 시 붐 소리(불 소리 소멸) ⑤아이콘 — 첨부 근사 렌더와 비교 후 증상 확정.

---##

## 11-25 12:2x — 188차 — 폭탄 무장 배선 수리(핫바 들기=무장) (QA GREEN — Play 대기)

**사용자 지시**: "여전히 폭탄던지기가 안돼" — 진단 근거 = Editor.log 실측: 좌클릭 전부 `[CombatInput] melee accepted`(근접 소비) + **[BombArm] 로그 0건** → 폭탄이 무장 상태 자체가 아님.

**근원(특정 완료)**: 폭탄 무장 경로가 **은퇴한 구 IMGUI QuickSlotUI**에만 존재(`if (UtkActive) return;` 가드로 죽은 경로) — 현재 게임의 UTK 핫바 "들기"(68차 P3)는 비무기 아이템을 `ItemHoldManager.Hold`(시각 홀드)만 하고 **BombArmController.Arm을 호출하지 않음** → "폭탄을 들어도" 좌클릭 폭탄 분기(IsArmed) 미발화 → 근접공격 소비 = 던지기 불가.

**수리(부모 직접 — HotbarUIUTK)**:
- 들기=무장: TryActivateHoldSlot ItemHold case **폭탄(isBomb) 선행 분기** → `BombArmController.Ensure().Arm(item)`("폭탄 들기 = 무장", 좌클릭 투척 연결 — 185차 던지기 애니 연계)
- 재입력=내려놓기: ToggleOff case 폭탄 분기 → `Ensure().Disarm()`
- 단일 손 홀드 정합: 무기 장착 시 `BombArmController.Instance?.Disarm()`, 비폭탄 홀드 전환 시도 해제
- 활성 판정: IsSlotItemActive 폭탄 분기 신설(`ArmedBomb.id == itemId` → 동일 재입력 토글 트리거)
- 폭탄은 **모델키 해석 전 분기** — 폭탄 GLB 부재 상황에서도 무장 가능(IsBombItem null 안전 헬퍼 신설)
- 인벤 UseItem 경로(폭탄 무장)는 기존 유지 — 두 경로 동일 동작

**계약**: HotbarBombArmTests 신설 3건(무장/토글/단일손정합/모델키 선행분기 소스계약).

**검증**: 컴파일 error CS **0** · 집중 EditMode **3/3 GREEN** · 전수 EditMode **2740 중 2720 통과 / 20 실패 = baseline 1:1, 신규 회귀 0**.

**Play 확인 목록**: ①핫바에 폭탄 등록(인벤 드래그) → 숫자키로 들기 → 로그 "[BombArm] 💣 무장 완료" ②좌클릭 → 던지기 애니 + 폭탄 포물선 비행(185차) ③같은 숫자키 재입력 → 내려놓기 ④무기 장착/다른 아이템 홀드 시 폭탄 자동 내려놓기 ⑤투척 후 잔여 개수 소모·인벤 갱신.

---##

## 11-25 12:0x — 187차 — 아이콘 겹침 수리(동기베이크 지연 파괴) (QA GREEN — Play 대기)

**사용자 지시**: "아이콘화가 잘못 되어있어 몇개의 아이템이 겹쳐진거 처럼 보이거든" — 185차 ④(아이콘 즉시표출) 회귀.

**근원(특정 완료)**: 동기베이크는 **같은 프레임에 여러 아이템을 연속** 베이크하는데(185차 확장), `Object.Destroy`는 **프레임 끝까지 지연** 파괴라 직전 아이템 노드가 **같은 원격 위치(10000,1000,10000)에 프레임 끝까지 생존** → 뒤 아이템의 오프스크린 카메라(cullingMask ~0)가 겹쳐 촬영. 비동기 큐 경로는 프레임 간 1개 처리라 무결(185차 이전 원본 동작).

**수리(부모 직접)**: SyncBakeFromPrefab 3소(성공·예외·Renderer 0 조기반환) `Object.Destroy` → **`Object.DestroyImmediate`**(node/camGo/rt — [126차] DestroyImmediate 스폰 코드 규약). 비동기 큐 경로는 원본 유지(프레임 간이라 무영향).

**계약 갱신(같은 사이클)**: GblItemIconSyncBakeContractTests — Destroy(node/camGo) 핀 → **DestroyImmediate** ×2 경로 핀 + **동기 섹션 내 지연 Destroy(node/camGo) 재발 금지 0건 핀** 신설.

**검증**: 컴파일 error CS **0** · 집중 EditMode(아이콘 2파일) **13/13 GREEN** · 전수 EditMode **2737 중 2717 통과 / 20 실패 = baseline 1:1, 신규 회귀 0**.

**Play 확인 목록**: ①인벤 전 슬롯 아이콘 — 겹쳐 보이는 아이템 소멸(재료/장비 다수 보유 상태 특히) ②첫 오픈 시에도 동일 ③상세창/창고/상점 아이콘도 동일 경로 — 같이 확인 ④전수 동기베이크 프레임 히트 감지(185차 계속).

---##

## 11-25 11:3x — 186차 — 인벤 가독성 조정(폰트 상향+세로 여백) — 사용자 승인 디자인 편차 (QA GREEN — Play 대기)

**사용자 지시**: "폰트나 여백이 위아래가 조금 달라서 인벤토리를 읽기가 힘들어, 폰트도 조금 더 커지면 좋겠고" — 185차 ③ 후속. Figma 15:4 재실측이 기하 1:1 정합(185차)이었으므로, 본 라운드는 **Figma 베이스 대비 사용자 승인 가독성 조정**(인벤 스코프 — 공용 토큰·타 창 무영향).

**폰트 상향(InventoryWindowUTK — 인벤 전용, 리터럴로 스코프 격리)**: 탭 라벨 14.4→**16.8** · 부제 14.4→**16.8** · 섹션 라벨 "장착 중인 장비" 14.4→**16.8** · 슬롯 Count 13.2→**15.6** · 적재량 라벨/값 15.6→**18** · EQ 배지 9.6→**12** · 웨폰 넘버 10.8→**13.2**.

**세로 여백(하우징 확대 — 폰트 클리핑 방지)**: WeightLabelRow 18→**21.6** · WeightValueRow 20→**24**(y 재정렬, WeightRow 26.6 내 수납) · Count 하우징 17→**20**(bottom 4.6→3.2) — 이 값들은 무핀(적용부)이라 계약 영향 0.

**계약 갱신(94차 '갱신 동봉' 규약 — 같은 사이클)**: EquipFigmaSpecTests 3핀(EQ 12·Count 15.6·넘버 13.2 + FooterLabel 18) + InventoryFigmaTabsFooterTests 1핀(WeightRow 18f) — 전부 "[185차 후속] 사용자 가독성 조정" 메시지로 Figma 원값 기록 보존. InventoryClusterPanelRegions 주석 갱신. 공용 FontTab/FontBody/FontRowLabel 토큰 무변경(타 창 무영향).

**검증**: 컴파일 error CS **0** · 집중 EditMode(인벤 4파일) **33/33 GREEN**(1차 런 32/33 — FooterLabel 핀 미갱신 1건 즉시 수리) · 전수 EditMode **2737 중 2717 통과 / 20 실패 = baseline 1:1, 신규 회귀 0**.

**Play 확인 목록**: ①인벤 텍스트 전반이 커져 읽기 쉬운지 ②큰 폰트가 기존 프레임에서 클리핑 없는지(탭/적재량/Count) ③세로 빡빡함이 완화됐는지 — 추가 여백이 필요한 구역 지정 시 187차에서 지점별 확대.

---##

## 11-25 10:5x — 185차 — 클릭물결 축소 + 폭탄 투척 애니 + 아이콘 즉시표출 + 인벤 피그마 재실측 (QA GREEN — Play 대기)

**사용자 지시 4건**: ①클릭 물결 원 축소 ②폭탄 들고 좌클릭 → 던지기 애니+투척 ③인벤토리창 피그마 정합 ④아이콘 전 아이템 즉시·균일 표출.

**①물결 축소(부모 직접)**: UTKClickRipple.RippleDiameter 96→**64px**([185차] "조금 더 작게") + UTKClickRippleTests 핀 96f→64f.

**②폭탄 투척 애니(위임 P3 600s 0산출 → 부모 직접)**: 근원 = BombArmController.ThrowTowardCursor가 좌클릭 즉시 스폰(애니 미발화) — 투척 파이프라인(무장→좌클릭→포물선)과 Player_AC Throw 트리거/ThrowObj 상태/HumanoidClipDriver.TriggerThrow(L540)는 이미 존재. 수리: ThrowTowardCursor = `_isThrowPending` 연타 가드 + `driver.TriggerThrow()`(GetComponent/InChildren 조회 — ConstructionManager 선례) + `ThrowReleaseRoutine` 코루틴(`ThrowReleaseDelaySeconds=0.35f` — ThrowObj 윈드업 후) → ReleaseBombNow(조준 재계산·스폰·소모·Disarm — 기존 본문 이동). 드라이버 부재 시 즉시 투척 폴백(테스트 무해). BombThrowIssuedThisFrame는 클릭 프레임 유지.

**③인벤 피그마 재실측(정합 확인 — 추가 수치 수정 0)**: Figma 15:4 재실측(11-25) — InventoryPanel 504×912 · 메인 그리드 **5열×5행 셀 81.6 갭 9.6** · 탭 5개 87.36×36.2 stride 92.16(갭 4.8) · EquipGrid 2×5 셀 81.6 갭 4.8 · 아이콘 **38.4×38.4 오프셋 21.6 중앙** · TierStrip 81.6×3.6 — 전부 소스(InventoryClusterPanelRegions: CellSize 81.6/CellGap 9.6/EquipCellGap 4.8/InventoryTabStride 92.2·InventoryTabWidth 87.4, InventoryWindowUTK iconHost 21.6/38.4)와 **1:1 정합**. 따라서 남은 불일치는 수치 스코프 밖(런타임 표출 차이 후보: 가방 슬롯 표출 수 — Figma 25 vs 소스 전체 40 슬롯 뷰포트 스크롤) — **사용자 지정 요소 특정 대기**.

**④아이콘 즉시표출(위임 P2 단발 착지 — 감사 후 수용)**: 근원 = 베이크 큐 1개/프레임+마운트→다음프레임 ReadPixels 2프레임 갭 → N번째 아이템 ≈ 2N프레임 후 표출. 수리: 물고기 전용 동기베이크([34차])를 **전 아이템으로 확장** — GetOrCreateIcon이 모델 해석 후 `SyncBakeItem(id, modelKey, glbPath)`(AssetDatabase GLB 직결→Resources.Load "Models/UserProvided/" 폴백) → SyncBakeFromPrefab 동일프레임 완결(마운트→Render→ReadPixels→캐시), 실패 시 기존 큐 폴백. MaxCacheSize 검사 동기전 이동(물고기 포함 균일 적용). 임시 노드/카메라/RT 성공·catch 양쪽 파괴.

**계약테스트**: BombThrowAnimationTests 신설 2건(발화핀·순서·가드) / GblItemIconSyncBakeContractTests 신설 5건(진입순서·에디터게이트·동일프레임·TryMount 패리티·큐폴백) / UTKClickRippleTests 핀 갱신.

**검증**: 컴파일 error CS **0**(return code 0) · 집중 EditMode **12/12 GREEN** · 전수 EditMode **2737 중 2716 통과 / 21 실패 = baseline 20 + FbxModelShaderProbe2GlbTests 180s 타임아웃 플래키 1(집중 재런 GREEN — 신규 회귀 0)**.

**Play 확인 목록**: ①클릭 물결이 작아졌는지(64px) ②폭탄 무장 후 좌클릭 → 던지기 애니 → 애니 중 폭탄 손에서 이탈·포물선 낙하·착지 텔레그래프 ③연타 시 애니 1회씩만(중복 투척 금지) ④인벤 열면 **전 슬롯 아이콘이 동시에** 뜨는지(별 표출 시점 없음) ⑤첫 오픈 시 프레임 히트 감지(전수 동기베이크 비용 — 캐시 이후 무히트).

---##

## 11-25 10:2x — 184차 — UTK 클릭 빛물결 FX(순백) 전 UI 클릭 피드백 (QA GREEN — Play 대기)

**사용자 지시**: "UI가 눌릴때마다 눌리는 느낌이 잘 안들어서 클릭시 물결이 지나가는 것 처럼 빛나게... 모든 ui에서 클릭이 필요할때" — 클릭 색감 선택 = **순백(중립적)**.

**구현(위임 착지 + 부모 수리 4건)**:
- 신규 `UTKClickRipple.cs`(UI.Toolkit): 단일 UIRoot 오버레이(pickingMode Ignore · 100%×100% 절대) + 루트 `PointerDownEvent(TrickleDown)` 훅 1개로 전 UI 클릭 포착. CraftBench 완료연출 선례 계승 — `schedule.Execute().Every(16L)` 드라이버 + 세대 토큰(연타/재부착 레이스) + 동시 물결 예산(MaxRipples=6, 초과 시 최고령 퇴거).
- 연출: 클릭 지점 중심 96px 원형 링(border-radius 스타일링 — 벡터메시 0)이 0.25→1.0 EaseOutQuad 확산 + 피크 알파 0.85 선형 페이드, 수명 0.45s. 색상 = **순백 #FFFFFF 링 + 백색 10% 내부 빛**(서브 골드 착지분 → 사용자 순백 선택으로 부모 교체).
- 부모 수리: ①색상 순백 교체 ②**UI 전용 가드**(`evt.target == null || evt.target == _hookRoot` return — 빈 화면 월드 클릭 무물결, "UI 클릭 필요 시만" 계약) ③스폰 시 `BringToFront()` 재선두(열린 창 위 렌더 보장 — 커서 오버레이는 자체 재선두 유지로 커서 항상 위) ④테스트 핀 정정(`_generation++`, 순백 핀, 가드/재선두 핀 추가).
- 훅: `UIToolkitBootstrap.Ensure()` 내 `UTKClickRipple.Ensure();` 1줄(커서 오버레이 이후).

**계약테스트**: UTKClickRippleTests.cs 신설 5건 — 상수·드라이버·순백 핀 / 오버레이(Ignore·절대·원형·가드·재선두) / 세대토큰·예산 / 부트 1줄 훅(순서) / 벡터메시 0.

**검증**: 컴파일 error CS **0**(return code 0) · 집중 런 **5/5 GREEN** · 전수 EditMode **2730 중 2710 통과 / 20 실패 = 기준선 1:1, 신규 회귀 0**(서브 착지분의 전수 런도 동일 — 신규 실패 1건은 핀 오타이핑 `++_generation`으로 수리 후 소멸).

**Play 확인 목록**: ①인벤/상점/제작 등 창 내 버튼 클릭 → 순백 원형 물결(창 위 렌더) ②창 배경·핫바·HUD 슬롯 클릭 → 물결 ③빈 화면(월드) 클릭 → 무물결 ④연타 → 물결 중첩(최대 6개)·오래된 것 즉시 소멸 ⑤물결 중 커서 가려짐 없음 ⑥드래그 시작 지점 물결(슬롯 잡기 피드백).

---##

## 11-23 16:5x — 183차 — Farm Pack 작물 단계 모델 매핑 (약초 5종 → 팩 식물 스왑) (QA GREEN — Play 대기)

**사용자 지시**: "밭에서 약초가 자라는게 잘못된거야 작물이 커야해 매핑시켜줘" — 182차 P3 보류분을 사용자 매핑 승인으로 확정 실행.

**매핑 계약 (사용자 승인안)**: Red→TomatoPlant_1_A→2_A(2단계) · Green→PotatoPlant_1→2→3(3단계) · Purple→EggPlant_Plant · Yellow→CornPlant · Silver→Cabbage. **성장 스왑 계약**: Growing은 마지막(성숙) 단계에 도달하지 않는다(0..stageCount-2 Clamp) — 성숙 룩은 Ready에서만 등장(수확 신호). 비표시 상태(Grass/Dirt/Tilled)·Planted=0.

**구현(위임 단발 177s — 편집+자체 grep, Unity 배치 부모)**:
- GLB 8종 수용: /tmp 원본(md5 검증) → `Resources/Models/UserProvided/`(SM_ 접두 제거) — 토마토 2·감자 3·옥수수·가지·양배추
- FarmPlotVisuals.cs 편집 3소(982→1062행): ①단계 매핑 섹션 — CropStages_* 5배열·ResolveCropStageGlbKeys(미상 null)·**ComputeCropStageIndex**(순수 함수 — 26차 EditMode 표준)·LoadCropStagePrefab(부재 시 null→폴백)·CurrentCropStageIndex(어댑터) ②BuildCropVisual 재빌드 가드 확장(타입+단계 불일치)·모델 선택부(**팩 단계 우선 → herb GLB 폴백 → Sphere 3단계 체인 유지**)·`_cropGlbBuiltForStage` 기록 ③RefreshVisuals 성장 스왑 블록(TickPhase Growing 매 프레임 호출 — 실측 — 로 단계 경계 통과 시 즉시 스왑)
- FarmPlot.cs: `internal int _cropGlbBuiltForStage` 필드 1건(777행)
- 스테이징 임포트 경로 복원: Assets/Textures/farm_atlas.png 재복사(farm-pack staging GLB의 ../../Textures 참조 — Resources와 이중 아틀라스, 원본 무수정)

**계약테스트**: FarmPackCropStageTests.cs 신설 4건 — ①매핑 키 계약(길이·순서·미상 null) ②ComputeCropStageIndex 경계(단계 1/2/3·p=0/0.49/0.5/0.99/1.0·Ready/Withered/Rotted 성숙) ③GLB 8키 Resources 존재+herb_red 폴백 실존 ④폴백 체인 src 계약.

**검증**: 컴파일 error CS **0**(return code 0) · 전수 EditMode **2725 중 2705 통과 / 20 실패 = 기준선 1:1, 신규 회귀 0** · FarmPack 계열 7건(182 3건+183 4건) 전부 GREEN. 기존 ResolveCropGlbKey herb 5핀 무손상(폴백 경로 유지).

**Play 확인 목록**: ①각 약초 밭 파종 → 어린 식물(TomatoPlant_1/PotatoPlant_1) ②성장 중간 단계 스왑(감자 0.5 경계) ③Ready에서만 성숙 모델(토마토 열매·감자 완숙) ④시듦/부패 갈색·검붉은 틴트가 성숙 모델에 적용 ⑤물주기→수분 밝기가 밭 타일(182)에 반영되는지.

---##

## 11-23 16:4x — 182차 — Farm Pack 농경 밭 시각 승격 (밭 타일 팩 모델화 — QA GREEN — Play 대기)

**사용자 지시**: "농경 시스템에 땅이 변화할 때마다 쓰면 좋을 것 같은데, 쓸만한 거 찾아서 구현 계획 세워줄래" → 계획(docs/plans/2026-11-23_182-farm-pack-field-states-plan.md) 승인 후 "라이센스는 괜찮아 진행해줘" (범위 B: P2+P3).

**P0 실측(계획 정정 2건)**: ①Field_2_x는 성장단계가 아니라 작물 종류별 밭 변형(2_2=싹·2_4=중간·나머지 성숙) → 스킵 확정 ②밭은 작물 98종이 아니라 **약초 5종**(HerbType Red/Purple/Yellow/Silver/Green — Seed_Red 등 소비)을 기르는 시스템 → P3(작물 단계 모델 매핑)는 계획 대상 수정 필요 → **무응답 10분 → 기존검증 최소설계 규칙으로 P3 스킵(약초 시각 현상 유지)**.

**구현(서브 위임 타임아웃 — 구현 전량 착지·검증 미수행 → 64차 선승인 부모 폴백 검증)**:
- **P1 수용**: Farm Pack Godot zip GLB — Field_1_1 → `Resources/Models/UserProvided/Field_1_1.glb`(SM_ 접두 제거, md5 동일) + farm_atlas.png 단일 아틀라스 + 스테이징 8종(감자 3단계·토마토 2·옥수수·가지·양배추·물뿌리개 → `새로운 glb/farm-pack/`, 미배선 inert) + glTF 전수감사 통과 + CREDITS 1행(Farm Pack — 보유 팩, 라이선스 사용자 확인) + **중복 zip 삭제**(쓸만한/Farm Pack (1).zip — Downloads 원본과 md5 동일 검증 후).
- **P2 밭 타일 승격**(FarmPlotVisuals.cs만): `SoilPackResourcesPath = "Models/UserProvided/Field_1_1"` — Resources.Load 1회 프로브+캐시 → 플롯 자식 "SoilPack" 빌드(NormalizeGlbScale 재용 1m·bounds 상면 y=0·콜라이더 파괴·머티리얼 인스턴스화) → **RefreshVisuals 게이트**: Grass=기존 잔디 큐브 현상, Tilled=구멍만(큐브·팩 타일 둘 다 비표시), 그 외(Dirt/Planted/Growing/Ready/Withered/Rotted)=팩 타일 표시+큐브 비표시. 로드 실패 시 전 상태 기존 큐브(폴백 계약 — 연출 중단 없음). 수분 밝기 산식(촉촉×0.75↔마름×1.1) 팩 타일 _BaseColor 틴트로 이식(ApplyPackTileTint — 기존 산식 동일). FarmPlot.cs 상태머신·구멍(154차)·스케일·틴트·스웨이 계약 전부 무변경.

**수리(부모 직접 — 1건)**: **farm_atlas 경로 산술 오류** — glTFast가 glb 위치 기준 `../../Textures/farm_atlas.png`를 `Assets/Resources/Textures/`로 해석(2단계 위)하는데 아틀라스를 `Assets/Textures/`에 배치 → **임포트 실패**(meta reportItems "Couldn't load texture"). 수리: 아틀라스 이동+스테일 meta 삭제+touch 재임포트 → 성공. (Assets/Textures는 기존 폴더라 보존 — Ground_Grass.asset 존재 확인.)

**계약테스트**: FarmPackSoilTests.cs 신설 3건 — ①Resources 키 존재 ②Grass 유지·Tilled 양 비표시 게이트(src) ③폴백 경로(src).

**검증**: 컴파일 error CS **0**(return code 0) · 전수 EditMode **2721 중 2701 통과 / 20 실패 = 기준선 1:1, 신규 회귀 0**(editmode182b.log, XML 16:23) · 신규 +3 전부 GREEN. 179→182차 4연속 동일 20 실패 세트.

**보류(결정 대기)**: **P3 약초 5종 ↔ 팩 식물 매핑**(제안: Red→토마토·Green→감자 3단계·Purple→가지·Yellow→옥수수·Silver→양배추 — 승인 시 스테이징 GLB로 즉시 적용) · P4 물뿌리개(파일은 스테이징 완료, tool_watering_can 배선만).

**Play 확인 목록**: ①밭 Dirt/파종 후 팩 흙 타일 룩(텍스처 경준선) ②수분에 따른 타일 밝기 변화 ③Grass·구멍(Tilled) 상태 기존 룩 유지 ④잔여 기존 경로(폴백) 회귀 없음.

---##

## 무번호 181 — 폭탄 폭발 VFX 에셋 배선 + Test_10 지급 + 착지 텔레그래프 (2026-11-23, QA GREEN — Play 대기)

- **사용자 지시**: "테스트10씬 인벤에 폭탄 추가 + 터질 때 VFX 에셋으로 + 날아가는 것 + 떨어질 때 피격 범위 몬스터처럼 표시" — 계획 docs/181_BOMB_VFX_TELEGRAPH_PLAN.md 후 "진행".
- **현황 실측**: 폭탄 시스템·투척·퓨즈·피해 **기존 완전 존재**(Bomb 4종/BombArmController 커서 포물선 투척/bomb_explosive 아이템) — 부재는 ①VFX 배선(null+절차 스파크만) ②Test_10 지급 ③텔레그래프.
- **구현(위임 완수 4파일 + 부모 수리 1건)**: ①Bomb.Explode 타입별 에셋 VFX — Explosive=`MeteoriteVFXRunner.PlayImpactSequence`·Molotov=`FX/Fire/VFX_Fire_01_Big`·Smoke=`FX/Death/DeathSmoke`·PoisonGas=기존 유지 + `BombVfx` 헬퍼(지연로드+캐시+수명 소멸·LDR) ②`TelegraphArcIndicator.SpawnCircle(pos, radius, duration, followRoot, isElite)` 신설 — **360° 원형 메시(BuildCircleMesh 48분할 폐곡)+적색 패밀리·Pulse·스윕 밴드 승계**·기존 호(BuildFanMesh) 무수정·followRoot 추적 모드(폭탄 낙하 x/z 추적·지면 y0.02 고정 — 퓨즈가 투척 시점부터라 고정점이 아니라 추적 필수) ③Test_10 폭탄 3개 지급 ④퓨즈·투척력·피해 소스 동결(로직 무수정).
- **부모 수리**: 지급 블록에 씬 가드 부재(Test_14/15/17도 지급할 뻔) → `Test_10` 씬명 가드 추가(isShopScene 선례).
- **계약**: `BombVfxTests` 6건(VFX 배선 소스계약·에셋 디스크 실존·동결 핀(퓨즈/throwForce/소스 "Explosion")·SpawnCircle 신설+Fan 잔존·지급 핀·절차 파티클 금지).
- **검증**: 컴파일 error CS **0**(가드 수리 후 재검증) · 전수 EditMode **2718 중 2698 통과 / 20 실패 = 기준선 1:1, 신규 회귀 0**(editmode183.log) · 테스트 +6.
- **후속 발견(사용자 지적 "운석 말고 폭탄 전용 에셋")**: **Hovl Studio Magic effects pack에 폭발 전용 완성품 존재** — `Hits and explosions/Explosion.prefab`(다층 파티클 폭발 본체)+`AoE effects/Ground AOE explosion.prefab`(지면 AOE 확산)+Smoke effects 패밀리 + `Resources/Bombs/Bomb_Explosive.prefab`(투척 실물 프리팹)+`bomb.glb` — 통합 선례 풍부(DeathVFX·LootSpawnFX·패막 Holy). **대기**: 폭발 본체를 Hovl 전용으로 교체하는 미니 라운드 승인 대기.
- **대기 게이트**: ①Play 육안(투척→낙하 중 지면 원형 예고 링(폭탄 추적)→폭발 시퀀스) ②커밋 승인(별도).

---##

## 무번호 180 — 몬스터 스킬 Intermediate+ 확장 + 보유 VFX 배선 (2026-11-23, QA GREEN — Play 대기)

- **사용자 확정**: "몇몇 상위 몬스터는 스킬을 사용하게" + "에셋중에 스킬 관련 vfx" + "Intermediate도 적용" — 계획 docs/180_MONSTER_SKILL_VFX_PLAN.md.
- **근거 발견**: `MonsterSkillSystem` 이미 존재·배선(810줄 — TryAttack 50% 선택) — 실제 문제는 ①Intermediate+ 14종 중 스킬 없는 10종 ②**VFX 전무**(46차 "절차 파티클 제외" — Fireball=무광 구체).
- **구현(위임 완수 — 3파일)**: ①_skillMap +8종(electric_porcupine AOE 4m/1.2·swamp_croc Leap 6m/1.8·forest_spirit Debuff·wild_troll Charge 8m/1.5·ogre AOE 5m/2·banshee Debuff·griffin Leap 8m/2·salamander Fireball 10m/1.8) — **미노타우로스·그림자암살자는 기존에 스킬 보유 확인돼 유지**(중복키 크래시 방지) ②VFX 배선 9곳 — Fireball=발밑 MagicCircle2(0.5s 페이드, 시각만·로직 지연 0)+히트 MagicHit / AoEExplosion=`MeteoriteVFXRunner.PlayImpactSequence` 신설(03+LightFlash→0.08s→04 — 01_Anticipation 미포함으로 판정-시각 정합 유지, Burst 무수정) / Charge·Leap=BasicHit / Teleport=DeathSmoke ×2 / PoisonSting=BasicHit2 / Debuff·Heal=마법진·버프 링 ③`MonsterSkillVfx` 헬퍼(6경로 지연로드+캐시+수명 소멸·색/머티리얼 조작 0) ④**밸런스 동결**: 데미지·쿨다운·50% 선택·기존 8종 완전 무수정 — 커버리지+시각만.
- **계약**: `MonsterSkillVfxTests` 8건(커버리지 10종 id·VFX 경로 실존(File.Exists)·절차 파티클/벡터메시 금지·기존 밸런스 라인 핀·Random50 잔존·PlayImpactSequence+AnticipationDelay 무수정·호출부 존재).
- **검증**: 컴파일 error CS **0**(위임 자체 검증+부모 무결성 재검증: 3파일만 수정·균형 0·AnimalAI 무수정) · 전수 EditMode **2712 중 2692 통과 / 20 실패 = 기준선 1:1, 신규 회귀 0**(editmode182.log) · 테스트 +8.
- **대기 게이트**: ①Play 육안(스킬 발동 VFX — 화염구 마법진/히트·폭발 임팩트 시퀀스·미노타우로스 등 기존 스킬 무손상) ②커밋 승인(별도).

---##

## 무번호 179 — 몬스터 체력바 = 병사 체력바 동일화 + 몬스터 스킬 가능성 조사 (2026-11-23, QA GREEN — Play 대기)

- **사용자 지시**: "몬스터 체력바도 그냥 병사 체력바와 동일하게" — 조사 결과 몬스터 체력바만 [F5] **플랫 적색 #E53E3E 단일**(경고 전환 없음)이고 병사는 청록·초록 그라디언트+HP<30% 주황·적 경고 전환.
- **수리(부모 직접 — 수술 2라인)**: `NameplateOverlayUTK.RefreshMonster` fill 배경 → **병사와 동일 그라디언트 계약**(`_gradGuardWarn`/`_gradGuard` 텍스처 공용 — 별도 텍스처 생성 0). 이름표 고스트 트레일·비율 라벨은 기존 공통 계약 그대로. **유지**: 몬스터 티어 배지·스탯 패널(HP/공격/의약)·bounds 앵커·40m 컬링은 체력바 외 요소라 무수정(전체 병사형 승격 원하면 듀엘 선례(125차 후속26) 적용 별도 라운드). **정리**: 소비처 0이던 옛 `_gradMonster` 적색 그라디언트 사자산 제거(선언+생성 2곳).
- **계약**: `NameplatePlateAlignTests.Monster_HpFill_Uses_Soldier_Gradient_Contract_179` 신설 — RefreshMonster 본문 경계판정(경고 임계 ratio<0.3f 동일·Warn/Guard 공용·구 플랫 0xE5 잔존 금지·`_gradMonster` 소멸).
- **몬스터 스킬 가능성 조사(구현 전 확인 — 미구현)**: **가능 3근거** ①VFX 완성품 — `Resources/FX/Meteorite/VFX/` 캐스트 시퀀스(01_Anticipation→03_GroundImpact+LightFlash→04_GroundImpactAdds)+MagicCircle2·MagicHit·Buff·EarthTrail ②기술 선례 — MeteoriteVFXRunner(78차)가 .vfx 발화 패턴 정립(지연로드+캐시+예산 가드 동시6+결정론·unscaled·LDR 캡)·AnimalAI 텔레그래프 상태머신에 스킬 훅 자리(0.5s 예고 창 피해 공정성 계약 유지) ③`MonsterTier` Beginner/Intermediate/Advanced 존재 — Advanced(또는 지정 id)만 조건 분기 가능. 제안 스킬: 운석 낙하(Advanced)·마법진 캐스팅(Intermediate+) — 사용 중 에셋이라 빌드 코스트 0. 대기: 스킬 종류·티어 범위 확정 후 계획서.
- **검증**: 컴파일 error CS **0** · 전수 EditMode **2704 중 2684 통과 / 20 실패 = 기준선 1:1, 신규 회귀 0**(editmode181.log) · 테스트 +1.
- **대기 게이트**: ①Play 육안(몬스터 체력바 병사 톤 동일+HP<30% 경고 전환 확인) ②커밋 승인(별도).

---##

## 무번호 178 — 크래프트 제작완료 연출 3페이즈(빛 차오름→번쩍→조합품 등장) (2026-11-23, QA GREEN — Play 대기)

- **사용자 지시**: "크래프트시에 너무 단조로우니깐 그리드가 빛으로 차오르다가 번쩍 하면서 조합품이 나오는 방식" — 이해 재확인+설계 기본값(골드·전 벤치·로직분리) 제시 후 "진행" 승인.
- **구현**: 위임(600s 타임아웃 — 구현 전량 착지·검증 미수행) → 64차 선승인 부모 폴백 감사·완결. `CraftBenchBaseUTK` **[제작완료 연출] 3페이즈** — A 재료 슬롯 왼→오 **골드(#D4AF37)** 글로우 물결(스태거 0.12s·인/아웃 0.3s·피크 α0.8) B 조합 영역 **화이트 플래시**(0.72s 스파이크 α0.85→0.35s 페이드) C 결과 슬롯 **scale 0.6→1.0 0.25s EaseOutQuad**+골드 글로우. 드라이버 `schedule.Execute().Every(16ms)` 진행도 순수함수·panel null 조기종료·오버레이 pickingMode Ignore·**연출 요소는 시작 시 생성/종료 시 제거(기존 창 렌더 무변화)**·OnWindowClosed 잔여 정리. `ExecuteCraft` = 로직(TryCraft→OnCraftSuccess→RebuildBook→RefreshStorage) 전부 실행 후 **성공 시에만** 연출 — 순수 장식(연출 중단돼도 수령 보장)·실패 분기 연출 없음. 벡터메시 금지(84차). **3종 벤치(Crafting/Alchemy/Cooking) 공통 베이스 동시 적용**.
- **품질 수리(부모)**: 연속 재조합 레이스 — 이전 연출 세션의 종료 정리가 신규 세션 오버레이를 조기 제거 → `_craftFxGeneration` 세대 토큰(종료 정리·스케일 원복은 최신 세대만 수행).
- **계약**: 신규 `CraftCompletionFxTests` 5건 — ①시간 상수 소스계약(0.72/0.12/0.25/0.84/0.35·#D4AF37·Every(16L)) ②실패 분기 연출 호출 부재 ③로직선행(OnCraftSuccess/RefreshStorage < 연출 호출) ④pickingMode Ignore+scale 원복(1,1) ⑤CreateMesh/Mesh( 부재(84차).
- **검증**: 컴파일 error CS **0**(중단된 1차 런의 고아 Unity 배치 프로세스 2종+UnityLockfile 정리 후 수리본 재검증 — 멀티 인스턴스 락 교훈) · 전수 EditMode **2703 중 2683 통과 / 20 실패 = 기준선 1:1, 신규 회귀 0**(editmode180b.log) · 테스트 +5.
- **대기 게이트**: ①Play 육안(조합 실행 — 골드 물결→번쩍→조합품 등장 순서·연속 재조합 안정) ②커밋 승인(별도).

---##

## 무번호 177 — RG Poly 메디벌 팩(CC0) 수용 — 화살 3종 신규·고기 6종 교체 + 월드모델 복원정정 (2026-11-23, QA GREEN — Play 대기)

- **⭐정정(11-23 사용자 지시 — "화살은 실제로 glb 붙이지 말고 이전 상태가 나아")**: 하단 [화살 월드 비주얼 — 코드] 섹션 **전면 복원 폐기** — ArrowProjectile.cs = HEAD와 바이트 동일(비행 시각=기존 그대로 **순백 네온 트레일만**, 10-04 사용자 원요구 복귀). 계약 2건 원본명·원본 단정 복구(OffCenterAimReachesProjectileVelocityWithNoMeshVisuals·SpawnCreatesOnlyWhiteNeonTrailAndPhysicsBody — MeshRenderer Empty), [176-V] 신설 매핑계약+`AssertChildVisualsFollowArrowModelContract` 헬퍼 제거. **GLB 3종은 아이콘용 보존**(관례후보1 — ArrowGlb_3Variants_UserProvidedCoverage 존속). [172-A] 조준 기하 수리·[176차] 리티클 변환 수리는 별개 수정으로 유지. 복원 후 QA: 컴파일 error CS **0** + 전수 EditMode **2698 중 2678 통과 / 20 실패 = 기준선 1:1, 신규 회귀 0**(editmode179.log).
- **팩 실측**: `Assets/Medieval Megapack - Low Poly.zip`(1.1GB, RG Poly — **CC0 라이선스 실측**, 귀속 불요·재판매 금지). GLB 1,265종+FBX 2,530종. 선별 추출(Weapons/Food Props/Tools 263종, 198MB — /home/korea/tmp/rgpoly) 후 전수 감사: **Draco 0·텍스처 1장 임베드·정점컬러 없음** — glTFast 임포트 무해(텍스처형 선례: Restaurant 팩·말 GLB). FBX는 이번 라운드 미사용(GLB로 커버).
- **[아이템 시각 수용 — 관례후보1 자동해소, 코드 변경 0]**: 신규 4종 — **arrow_regular/arrow_reinforced/arrow_magic**(기존 화살 시각 부재 — 트레일만)/meat_raw(174차 잔여 "raw고기 원재" 해소). 교체 5종 — meat_boar/rabbit/wolf/horse/grilled(구 charred-meat-haunch hue 파생 → RG Poly 고기 모델, 같은 게임 id 유지). 전부 glTF 매직 헤더 검증(md5 원본 대조). bread/cheese/egg/potato/mug는 게임 id 미존재 확인 후 스킵. dark_cloak·stealth_boots는 팩에 형상 부재 — pending 유지.
- **CREDITS**: 말미에 `## RG Poly - Medieval Megapack (CC0)` 섹션 append — **"by Polyfork" 문자열 미포함으로 T2 카운트 207 무충돌**, Models/Polyfork/ 파일 추가 0.
- **[화살 월드 비주얼 — 코드]**: ArrowProjectile에 **[176-V]** — `GetModelKeyForType(ArrowType)` 매핑(Regular=arrow_regular/Reinforced=arrow_reinforced/Magic=arrow_magic) → `RuntimeModelLoader.TryGetModel`(UserProvided 우선 폴백 체인+캐시) → 모델 홀더 자식 부착: 렌더러 bounds 합산 정규화(**0.7m 실길이**), 최장축 판정(+Z/+Y/+X 폴백)으로 비행축 정렬(90° 배수 회전 — 비균등 스케일 전단 없음), **자식 Collider/Rigidbody/Animation/Animator 일식**(루트 캡슐+리지드바디 계약 무손상). 로드 실패/예외 시 기존 트레일만 무음 폴백(EditMode 안전 — Resources.Load는 에디터 안전). 트레일·damage/power·GetSpeedForPower 계약 무수정. SetArrowData 주입 시 티어 교체.
- **계약 갱신**: BowAimAlignmentTests 충돌 핀 2건 갱신(MeshRenderer Empty/"트레일만 비주얼" 구계약 → 모델 홀더 계약 헬퍼 `AssertChildVisualsFollowArrowModelContract`) + 신규 소스계약 `ArrowModelVisualMapsTierToUserProvidedGlbKeys`(3티어 매핑+로드경로+0.7m 고정) + FoodGlbCoverageTests `ArrowGlb_3Variants_UserProvidedCoverage`(존재+관례후보1+HasModel). meat 교체분 BIN 대조 어서션은 존재하지 않음 확인 — 완화 불필요.
- **QA**: 컴파일 error CS 0(중간 1차 런에서 CS0118 `Animation` 네임스페이스 충돌 1건 — `UnityEngine.Animation` 완전 한정 수리) · **전수 EditMode 2699 중 2679 통과 / 20 실패** — 실패 집합 = 이전 20건과 1:1(신규 회귀 0; baseline 10 + 이전 기인 10). 테스트 +2(ArrowGlb 커버리지·매핑 계약).
- **Play 확인**: ①인벤/드롭 화살 3티어 아이콘=실물 GLB ②활 발사 시 화살 모델 비행(0.7m·트레일 병행) ③고기 6종 교체 룩(에러 파생 대비 품질) ④meat_raw 아이콘 ⑤기존 요리/디시 GLB 무손상.
- **대기 게이트**: ①이전 기인 10건 귀속·수리(별도) ②Play 육안 ③커밋 승인(별도). 팩 잔여 1,256종은 세션 캐시(/home/korea/tmp/rgpoly 263종 추출분 포함) — 세계 배선(팰리세이드·도크·네이처)은 별도 라운드.

---##

## 무번호 176 — 창 콤보 2클릭 분리·활 조준 3자 정합·패막 FX 정리·호 고급화·검 그립 교정 (2026-11-23, QA GREEN — Play 대기)

- **경과**: 사용자 Play 리포트 5건(창 콤보 미분리·찌르기 슬래시 부재·검 반대·패막 방사형 이펙트·완벽 패막 청백 화면 + 활 조준 고장·호 고급화 추가). "찌르기→창" 사용자 정정 수용. 위임 5건 중 2건(패막·호) 서브 단발 완수, 3건(활·창·검) 600s 타임아웃 → 64차 선승인으로 부모 직접 폴백 완수(활·창·검).
- **[창 콤보 2클릭 분리]**: 오프너 스윙1 2곳 FireComboSlash 복원 + **오프너 내장 스윙2 자동 해결 게이트(시간·상태 이중 가드) 철수** → 1클릭=1타, 2클릭=2타(기존 _spearFollowupAuthorized 승인 경로 단일 소스 — L1089 클릭 수용 존속). 양 타 흰색 = ComboStageTint가 항상 CoreTint(흰색) 반환하는 기존 계약 확인으로 신규 작업 불요. FireComboThrust·PlayThrustStage 정의는 미사용 존속(추후 재사용). CombatFxThrustSeparationTests 6건 재작성(철회·2클릭 게이트·흰색·[166-C] 존속·쿨다운 부등식) + AttackSwingJudgmentContractTests 핀 교체(시간게이트→ShouldResolveSpearFollowupHit). HumanoidClipDriverComboTests 리플렉션 핀 존속을 위해 ShouldResolveSpearOpenerSwing2 판정 함수 정의 유지(호출부만 철수).
- **[활 조준 3자 정합]**: 근원 = BowAimReticleUTK.PositionAtScreenPoint가 RuntimePanelUtils.ScreenToPanel에 마우스(바텀-레프트 원점)를 그대로 투입 → 패널(탑-레프트) Y 플립 누락 = **수직 미러**. 드로 중 AlignCenterToScreenPoint 강제 정렬로 오버레이 커서까지 같은 좌표로 끌려가 "조준선=커서 ≠ 실제 커서 레이"(사용자 체감: 화살 한방향). 수리 = UTKCursorOverlay·UTKDragDrop.GetPanelPointerPos의 Play 검증 수식(root.worldBound.width/Screen.width 스케일 + worldBound.height − y×scale 플립)으로 단일화. 릴리즈 픽셀→ScreenPointToRay 솔버 경로는 원점 일치로 무수정. BowAimAlignmentTests 핀 2건 교체.
- **[패막 FX 정리]**: 방사형 산란 전부 제거 — wave1 별섬광·십자글린트, wave2 곡선 위스프·리플렉 궤적(SetNextReflectPerfect 포함), wave3 SpawnCritBurst(12/6방향 파편 = "막대기 사방" 주범), ParryAssetFX 내 Sparks 2건+SparksPath. 호출부 wave0 단일화. **청백 화면 원인 확정 = ScreenChromaticFX.Spike(0.35, RGB 프린지) → 철수**(화이트 프레임·비네트·라이트는 이미 ParryWhite=(1,1,1) 순백). 유지: 지면 링·아우터·트리플·수렴-역전 링·GOOD 미니링·히트스톱·무기 플래시·SFX·Guard_01/Holy. 테스트 9파일 핀 갱신(파일 소멸 0).
- **[호 텔레그래프 고급화]**: 광택 스윕 밴드(±52° sin 1.7 rad/s — 결정론, 정적 메시 캐시) + 이중 레이어(내부 글로우+외곽 림 — 5링 알파 프로파일) + 펄스 smoothstep 완화(0.88~1.0) + 가장자리 감쇠 심화(SmoothStep 0.45,1 ×0.92). 반경=판정 1:1·적색 2색·Random 0건·페이드 타이밍 계약 무변경. 기존 테스트 갱신 불요 전수 확인.
- **[검 그립 교정]**: GLB 16종 정점 직접 파싱(20슬라이스 단면 재현 — struct 파싱, 최대 12k 샘플) → **stone_sword만 테이퍼 판정 뒤집힘 확인**(끝 단면 −0.100 vs +0.090 — 10% 차). 수리 = ComputeWeaponDataGrip 검/단검 분기: 가드(최대 단면)가 스팬 중앙에서 ±0.1 초과 이탈 시 스팬 판정(긴 쪽=칼끝), 중앙 근처면 기존 테이퍼 유지 → **stone_sword만 교정, 나머지 15종 판정 무변경 실측**(wood_sword 가드 T0.47 중앙, steel_sword T0.68, daggers T0.38 전부 기존값과 일치).
- **QA**: 컴파일 error CS 0 · **전수 EditMode 2697 중 2677 통과 / 20 실패** — 실패 집합 = 이전 20건과 이름 1:1(신규 회귀 0; baseline 10 + 이전 기인 10 — 175차 기록 참조). 중간 1차 런에서 신규 1건(BowShotAndUtkReticleUseTheSameMouseCursorAimPoint — 핀 스코프 오판 positionMethod vs reticleSource) 자가 수리 후 소멸 확인(2차 런).
- **Play 확인**: ①창 1클릭=1타·2클릭=2타(양 타 흰색 슬래시) ②활 조준선=커서=화살 착탄 3자 일치 ③검(전 tier — stone_sword 포함) 칼날 방향 ④패막: 원형 링만+순백 화면 ⑤호 텔레그래프 고급 렌더.
- **대기 게이트**: ①이전 기인 10건 귀속·수리(별도 라운드 — AnimalAI 00:04 병행 수정분 계약 needle 등) ②Play 육안 ③커밋 승인(별도).

---##

## 무번호 175 — 잔여 재스캔(fish 3종 수용) + 찌르기 콤보 분리 + 인벤 첫오픈 그리드·여백 Figma 정합 (2026-11-23, QA GREEN — Play 대기)

- **경과**: 전일 야간 세션의 위임 2건이 402/600s로 부분 수행 → 본 세션에서 "재시도" 지시로 3건 병렬 재위임(정책 준수). task B 재위임도 600s 사망했으나 [61차 변형] "구현 전량 착지+검증만 미수행" 확인 → 부모 직접 배치 검증으로 종결(64차 선승인).
- **[찌르기 분리 — 구현 서브에이전트 착지, 집중 32/32 GREEN]**: 오프너(AttackThrust) 경로 4라인 FireComboSlash→**FireComboThrust** 전환(HumanoidClipDriver L1277/1338/1351/1361, [174차] 주석+ [166-C] 문자열 보존 — AttackSwingJudgmentContractTests grep 계약 무손상). 팔로업(WeaponCombo)/레거시 콤보/세그먼트 루프는 슬래시 유지, FireComboTrail 전부 유지. **부착 지연 수리**: ComboFxAnchorPos(L1781) — WeaponSwingTrail.GetTrailTipAnchor() 무기 팁 앵커 우선, 폴백 기존 +0.8/+1.25 — 슬래시·찌르기 공용. **쿨다운 트림 불요 실측**: 오프너 최악 간격 0.54s·근접 세그먼트 0.76s(WeaponCombo 상속 속도 3, Player_AC.controller L4070) > MIN_STYLIZED_SLASH_INTERVAL 0.25s — 스테이지 소실 없음, MinComboStageGapSeconds=0.54 부등식 테스트 고정. 신규 **CombatFxThrustSeparationTests 6건**.
- **[인벤 첫오픈 그리드 + 여백 — 구현 착지]**: **근원 = RefreshUIScale()가 winRect 산출(L881)·DrawCloseButton(L883) 이후 호출** — 세션 최초 OnGUI 프레임만 창박스/섀도우가 _uiScale 초기값 1.0, 그리드는 갱신 스케일 → 1920×1080 이외 해상도에서 첫 프레임 어긋남. 수리: RefreshUIScale+스케일 변경 검사+InitStyles()를 winRect 이전으로 이동 + OnShow() 보조 호출. **여백(Figma 단일소스 — 프레임 15:4 fluent-game-inventory-ui 실측)**: 슬롯 갭 6→**9.6**, 패널 인셋 실효10→**24(PANEL_CONTENT_PAD 신설)**, 장비 섹션 인셋 **14.4**, 장비 셀 갭 **4.8**, 탭 갭 **4.8** — 전부 ×_uiScale. 슬롯 폭 산식을 **플러시 합산**(갭×(열수−1), 선행 마진 제거)·contentHeight=행수×rowHeight−갭으로 전환. 열수: Figma 메인 그리드는 **5열** vs 소스 6열 — GRID_COLUMNS=6 "무수정" 계약으로 유지(불일치 근거 보고만). 신규 **InventoryLayoutSpecTests**(여백 토큰·무수정 계약·RefreshUIScale 순서 단정 4계약). 1080p 검산: 슬롯 88.67·장비 cellW 106.4/cellH 58.5.
- **[잔여 재스캔 — 자산 서브에이전트 착지]**: demand 기록분 재판정 — **가죽 원재·boar tusk/fang·raw고기 원재는 2회 재스캔에도 원재 0종**(완제품·작업대만; skin의 molted-dragon-skin=파충류 탈피 기각) → **정제 문구로 demand 재기록 3건 recorded:true**(완제품 배제 명시). **fish legacy 3종 = fish_common/rare/legendary** — 세션 덤프 기록이 "테스트 씬 전용"이었으나 **LordFoodCatalog.SeaIds 영주 급여 선물 경로로 실노출** 정정 → koi-base(3,508tri)·reef-grouper-964612(3,948tri)·coelacanth-base(3,556tri) 감사 3/3 통과(정점컬러·텍스처 0·Draco 무) 수용 → Polyfork 원본 3종 + **UserProvided 복제 3종**(fish_common←koi 등급별 실루엣 구분) — 관례후보1 자동 해소, 코드 변경 0. CREDITS **204→207**(헤더 카운트 4 뒤처짐 정정). 카탈로그 총량 3,505(신규 증가 없음).
- **[계약 카운트 갱신 — 구현 서브에이전트, 집중 37/37 GREEN]**: PolyforkIds 204→**207**·SkipWiringCheckIds 158→**161**([175차] 아이템 시각용 수용 주석, §18 닫기 인덴트 함정 대응)·T2 CREDITS 207 — **FoodGlbCoverageTests에 FishGlb_3Ranks_UserProvidedCoverage 신설**(3종 존재+관례후보1 체인).
- **QA**: 컴파일 error CS **0** · **전수 EditMode 2697 중 2677 통과 / 20 실패** — **10건 = 기존 baseline 1:1**(11-21 목록과 이름 일치: Aero/Instrument×3/InvadedLord×2/Cooking/H2/Recipe/ThemeUSS). **잔여 10건 = 본 세션 이전 기인**(AnimalAI.cs mtime 11-23 00:04 — [169-P6] 부채꼴 판정 삽입 등 병행 라운드 수정분; 소스계약 needle 앞뒤 불일치 + 근접 히트 0건 유형) — 본 세션 수정 파일(HumanoidClipDriver·InventoryWindow·테스트 3)과 무관, 내 스코프 테스트(Thrust 6·InventorySpec·Fish3Ranks·Combo·TabsFooter·Polyfork) 전부 Passed. **미완료**: 잔여 10건 귀속·수리는 별도 라운드 권장(169-P6/병행분 재실측 필요).
- **Play 확인**: ①창 공격 오프너=직선 찌르기 FX·팔로업=슬래시 아크(명백한 구분) ②슬래시 부착 타이밍(팁 앵커) ③인벤 첫 오픈 프레임부터 그리드 정합(1920×1080 이외 해상도에서) ④여백(Figma 9.6/24/14.4/4.8) ⑤물고기 등급 3종 아이콘·영주 급여 선물 노출.
- **대기 게이트**: ①잔여 10건 실패 귀속 판정 ②Play 육안 ③커밋 승인(별도).

---##

## [C정리] 2026-11-21 A~C 전부 실행 (windows-disk-cleanup 실행계약)

- **A**: AppData Temp(+1일 22건)·휴지통 2.5G·Unity Caches 110M·code_test 캐시(Assets 보존 확인) — 완료.
- **B**: all-6400.zip 4.1G·angular·rounded·음식 573M·적토마/황마 GLB(해시 대조 동일 중복 확인)·Setup Guide 2.2G·Unity 6000.3.17f1 구버전 1.1G·TestOutput 30일+ 668건·Screenshots 에이전트 진단물(사용자 mp4 보존) — 완료. **D: 드라이브 부재** → Desktop 개인파일/~/poison 구클론/Downloads 수용 소스(EXPORT.zip·necropoly·Pack.zip 등) 보존(rm 금지 준수).
- **C-1 state.db 수술**: 스키마 실측(started_at/timestamp — created_at 부재) → 청크 삭제 시도(per-row fts 트리거 병목 — 18분 미커밋) → 트리거 drop→벌크 단일 DELETE→fts 재구축→트리거 복원 경로로 전환 완료. 468 세션 보존/17,870 삭제, 트리거 6/6 복원(3건 타임아웃 누락 → 재검증 복원), 세션검색 정상 확인. 백업 사본 삭제(계약 절차 준수). VACUUM은 WSL_COMPACT.bat 내부(shutdown 후 단일 라이터)로 이관 — 예상 9.5G→4.6G.
- **C-2 Library 35G rm**: 완료(잔여 219M — ArtifactDB/BurstCache 락·I/O 오류 파일, WSL 재시작 후 제거 가능하거나 Unity 재생성이 관리).
- **C-3**: Desktop/WSL_COMPACT.bat 갱신 — shutdown→vacuum_db.py→sparse→compact 4단계. 사용자 더블클릭 대기.
- **부수 수리**: /home/korea 하위 자기참조 깨진 심볼릭 링크 8건(Desktop/Music/Videos/Public/Downloads/Templates/Pictures/Documents — 자기 자신을 타깃으로 하는 루프) 제거 — terminal 툴 cwd 스캔 막힘(Symlink loop) 원인이었음.
- **회수**: C: 12G→54G(+42G) — vhdx 압축(사용자 bat) 후 추가 회수 예상(vhdx 41G, 내부 VACUUM 후 ~5G 감소).


---##

## [169-P6] 공격 범위 호(arc) 텔레그래프 + 부채꼴 판정 + 병사 2페이즈 (2026-11-21, 텔레그램)

- **계획**: docs/plans/2026-11-21_test16-combo-vfx-bow-anim.md [169-P6] — 사용자 확정("레드 유지하고 진행"). 실측 근거: 적색 링=AttackTelegraphIndicator(AnimalAI:1019·AfterlifeEnemy:195, windup 0.5s), 느낌표=SpawnAlert(19-E1, 유지), 타격 판정=원형(거리 재확인), 구르기 무적=이미 존재(C21-02 PlayerHealth.ApplyDamage), 병사=즉발 데미지 무예고(GuardPlaceholder.PerformAttack L1444).
- **[TelegraphArcIndicator 신설]**: 절차 부채꼴 메시(±60°, fan 24분할, Sprites/Default 반투명 펄스 — 결정론 Random 0건), 적색 2색 패밀리 승계(Elite/Normal — 사용자 "레드 유지"), **반경=실제 판정 반경**(25-A3 링 0.5 축소 미적용 — 표시=판정 정합이 목적), FlashConclude 페이드 계약 유지, IsInFrontArc 정적 헬퍼(부채꼴 단일 소스).
- **[몬스터 배선]** AnimalAI·AfterlifeEnemy: 원형 링 발화선 → 호 스왑 + `_telegraphArcDir`(예고 시작 시점 타겟 방향 **고정** — 이탈 회피 가능 설계) + 타격 2경로(어그로/플레이어·AttackPlayer)에 부채꼴 재판정 추가 — 호 밖=빈 스윙. FlashConclude GetComponent 타입 스왑. 느낌표(SpawnAlert) 무수정.
- **[병사 2페이즈]** GuardPlaceholder.PerformAttack: 즉발 데미지 → 호 스폰 + 0.5s 예고 코루틴(PerformAttackStrike) + 타격 시점 거리/부채꼴/생존 재판정(빈 스윙) + 예고 중 재발동 차단(쿨다운 예약). 회피 2종: 이탈/구르기.
- **[계약 갱신]** CombatFxTelegraphPhaseBTests(T6 링 스폰 문자열 → 호)·CombatFxSizeQualityPhaseHIJTests(동일) — 소스핀 갱신. AttackTelegraphIndicator 본체·SpawnAlert 계약 무수정.
- **부모 직접 구현 근거**: 2병렬 위임 600s 타임아웃 ×2(context 미전달 조기 조사사망, 착지 0) → 64차 상설승인 폴백. 수리 이력: 가드 치환 시 `{` 라인 소실 2건+중복 닫음 1건(브레이스 균형 재판정 — 리터럴 제거 후 0 확인).
- **검증 상태**: ⚠ **Unity 배치 러너 라이선스 블로커 지속** — 컴파일/EditMode/프로브 미실행. 정적 검증만(리터럴 제거 브레이스 균형 4파일 0 + 심볼 grep + 계약 핀 갱신). 사용자 재인증 후 순행: 컴파일→EditMode→horse168 진단→bowaim168 진단→hitf166 백색 캡처→호 렌더 캡처.
- **대기**: ①사용자 Unity 재인증 ②호 각도(±60°)/색 판정 ③Play 일괄 판정 ④커밋 별도 승인.


---##

## [168차] 공격씬 테스트16 피드백 — 콤보 2클릭 재절단·스윙 3.0·피격 백색화·1.6 축소·사거리·활쏘기 애니 교체·조준 정합 + 말 자세 진단 (2026-11-21, 텔레그램)

- **계획**: docs/plans/2026-11-21_test16-combo-vfx-bow-anim.md — 사용자 승인("진행해줘") + 2차 요구 추가(피격 축소·말 자세).
- **[168-P1 콤보]** 실측 근거: Weapon_Combo_2 4.53s를 3등분(1/3·2/3·1) → 1클릭=1.51s = 스윙 2회 포함 체감. 수정: MaxMeleeComboClicks 3→2(MeleeComboStateMachine 상수 3곳 포함), playbackLimit=클릭/2(0.5/1.0 보유-재개), 클릭 윈도우 0.85s 유지. 슬래시 VFX 조기발화: FireComboSlash를 데미지 스트라이크((seg-1+0.32)/2)에서 분리, ComboSlashVfxLeadNormT=0.12 조기 + _stageVfxResolved 1회 가드(이중발화 제거). 계약 갱신: HumanoidClipDriverComboTests(0.5 계약 23/23 GREEN)·AcceptedAttackSequenceTests·PlayerAttackSequenceTests(NeverExceedsTwo)·RegisteredMeleeComboTests(경계/스트라이크 2세그먼트).
- **[168-P1 속도]** Player_AC WeaponCombo·AttackCombo3 상태 m_Speed — 기존 2.5(RegisteredMeleeComboTests 정규식 핀이 증거)를 A팀이 1.2로 오탈 설정(느려짐) → 부모 재판정 **3.0**(기존보다 +20% 빠름, 4.53s×3=1.51s 풀콤보). 테스트 속도 핀 3.0 갱신.
- **[168-P2 피격]** SlashVFXRunner — TintParticles(흰)+NormalizePurpleParticles 임팩트 경로 복귀(45차 BOTW 선례, 스윙 아크와 통일) + ImpactBaseScale 2.5→1.6(166차 무가시 1.0↔유가시 2.5 중간, Play 트림 1.4~2.0).
- **[168-P2 활]** ArrowProjectile.BaseSpeed 84→100(사거리 ×1.19, 수명 10s 유지). 조준 정합: UTKCursorOverlay.AlignCenterToScreenPoint 신설 + BowAimReticleUTK 드로 중 단일 화면좌표 정렬 훅(커서 오버레이 중심=리티클 중심) + BowAimDiagProbe(산출 경로 차이 5종 열거 진단 도구, A/B/C/D 시나리오).
- **[168-P3 활쏘기 애니]** FBX 감사(휴머노이드 23본·5.0s·Take 001) → 임포트 guid 발급: 플레이어 활쏘기=467cd2e6/병사 활쏘기=772a9018. Player_AC ArcheryShot 상태(MeshyUser/Archery_Shot b3e692d4→467cd2e6)·SoldierArcher_AC Attack 상태(Mixamo/Standing Draw Arrow→772a9018) 클립 스왑. DrawShoot(당기기) 유지.
- **[168-P5 말 자세 진단]** 원인 후보 좁힘: 적토마 GLB에 **Animator 컴포넌트 없음**(런타임 AddComponent 경로 — SpawnHorse L110 실측) + Idle 클립("갈색말 대기.fbx" guid bbc998aa) 본경로=Blender식(Root/spine.001...) = GLB 본이름과 매칭 가능. v1 프로브 경로 비교 오판(루트명 포함) 정정한 v2 프로브(AnimationMode 샘플링으로 바인드 포즈 vs Idle 포즈 직접 비교) 작성 완료 — **실행은 라이선스 블로커로 보류**.
- **검증**: 컴파일 error CS 0 + EditMode 전수 **2641/2631/10 = baseline 유지, 신규 실패 0**(계약 갱신 3회차 완료 — 1차 13건→2차 4건→3차 0건, 소멸 예정 문자열 목록 미동봉 비용 실측). 위임: A팀(600s 타임아웃 후 착지 확인=편집 전량·프로브 미착지→부모 보강)/B팀(max_iterations 보고와 달리 편집 전량 착지 — 129차 자기보고 과소 통지 변형 재확인)/부모(계약 갱신·프로브·검증).
- **⚠ 블로커**: Unity 배치 러너가 23:58경부터 전부 즉사(boot 단계, error CS 0, 로그 미갱신 — [Licensing] Access token unavailable → 토큰/클라이언트 재시작·네트워크 200 확인 후에도 지속). **사용자 PC에서 Unity Hub/에디터 1회 열어 재인증 필요**. 재개 후: horse168_run.bat(말 진단)→run_bowaim168_probe.sh(조준 진단)→hitf166 재실행(백색 임팩트 캡처) 순행.
- **대기**: ①사용자 Unity 재인증 ②Play 일괄 판정(콤보 2클릭 체감·속도 3.0·피격 백색/1.6 크기·조준 정합·활쏘기 애니·말 자세) ③커밋 별도 승인.


---##

## [167차] 히트 임팩트 Magic Hit 2 원형 복원 + 히트음 펀치업 + Free Slash 재부착 (2026-11-21, 텔레그램)

- **계획**: docs/plans/2026-11-21_hit-impact-magic-hit2-sfx-freeslash.md — 사용자 승인("진행", 판정 3건 기본안). 실측 근거: [38-F1R] 11-15 보라 시그니처 판정 → 11-21 사용자 재판정 충돌, ImpactSoundFX impact_* 클립 전무(Resources/Sounds/SFX=Gather만), Free Slash 호출부 주석(HumanoidClipDriver 9/25 트레일 일원화).
- **[167-A] Magic Hit 2 원형 복원**: SlashVFXRunner.PlayImpactMulti에서 41-b 딥퍼플 startColor 강제/40 스무스 서클 교체/40-b 틴트 강등/NormalizePurpleParticles 개입 제거 — 에셋 고유 색·텍스처 렌더. NormalizeMagicHitMaterials(56-P1)+DetectShaderErrorOnce 안전망 유지. 계약 갱신: AttackFxTextureRepair_55Tests(딥퍼플/서클클론 소멸 계약), FxMarkerRefineP40Tests(서클 배선 소멸+[167-A1] 마커).
- **[167-B] 히트음 펀치업**: MonsterHitSfx — LowPunchPitch=0.8 저역 쿵 레이어(동일 클립 동시 재생, 지터 승계+클램프) + ImpactVolumeBoost=1.25(훅 일괄, 크리 1.2배 위계 보존). 신규 에셋 0(impact_* 클립 부재로 B안 불가 → C안).
- **[167-C] Free Slash 재부착**: HumanoidClipDriver.cs:1756 주석 해제 — 트레일(WeaponSwingTrail) 유지+아크 추가 병행, [60차] Free Slash 우선 경로 발화.
- **검증**: 컴파일 error CS 0(Exiting batchmode 1) + EditMode 전수 2642/2632/10 = 166차 baseline 정확 동일(실패 10=Aero/CraftBench/IndoorQuality/Instrument 3/InvadedLord 2/RecipeCatalog/ThemeUss — 기존 부분집합, 신규 0) + hitf166 프로브 재실행(22:44 신선 캡처) — C_prod 생산경로 보라 임팩트 화면 12% 점유·전폭 확장 픽셀 실측(원형 렌더).
- **위임**: 구현 서브에이전트(2차 재시도 — 1차 402 크레딧 소진 0산출 → tasks 형식+앵커 박제 지시서로 570s 착지) / Unity 배치 검증은 부모 직접(148차 실행+편집 분리 규칙, 증거=XML/로그/PNG 파일).
- **대기**: 사용자 Play 일괄 판정(히트 룩/사운드/스윙 아크) — 보라 방향 불만족 시 골드화이트 통일 재판정, 크기 불만족 시 ImpactBaseScale 트림. 커밋 별도 승인.


---##

## 11-21 01:3x — 무번호 92 — 물약·약초 종류별 전용 GLB (파악→파생 생성→아이콘 체인 적용) (QA GREEN — Play 대기)

**사용자 정정**: "물약이나 허브 종류 중에 없는 glb를 파악하고 색이나 고품질화해서 적용하라는 의미" — 이전 라운드의 범용 부재 파악이 아니라 아이템 종류별 시각 커버리지가 본 의도.

- **파악(아이템 DB 실측)**: 허브 = HerbGlbIds 8종(채집/농경 핵심 herb_yakcho 포함) vs GLB 5종 — **herb_yakcho.glb 부재**(herb_green 폴백 공유). 물약 = ItemData 2종(은신 potion_stealth·진정제 potion_sedative) + AutoRouteSystem 참조 2종(potion_hp/mp) — **전종 전용 GLB 0**(절차 아이콘 폴백 상태).
- **[P1] 물약 4종 파생 베이크**: Polyfork potion-bottle-099ad7(628tri·유리/액체/코르크 3분리) 수용 → **액체 메시(144정점)만 hue 절대 재매핑**(hp=빨강 2°/mp=파랑 220°/stealth=청록 170°/sedative=연보라 272°) + **유리 명도 ×3.4·채도 ↓·알파 0.5→0.28**(액체 가시성). 3차 반복 개선(v1 어두움→v2 유리 리터치→v3 판정 통과: "4종 명확히 구분").
- **[P2] herb_yakcho**: herb_green bcf 틴트 파생(밝은 연두 0.80/0.94/0.62 — 텍스처 무수정) — 피어리와 구별되는 "들판 기본 약초" 룩. 판정: "형태 자연스러움".
- **적용(코드 변경 0)**: GblItemIconResolver 해석 체인의 관례 후보 1(id 그대로)이 파일만으로 자동 해결 — potion_hp/mp/stealth/sedative/herb_yakcho 5종 Resources/UserProvided 배치 완료. 계약 테스트 신설(PotionHerbVisualCoverageTests 3건 — 리소스 존재/해석 체인 통과/유리-액체 분리 유지).
- **계약**: PolyforkIds 179종(파일 180=pigeon)·CREDITS 180·Skip +1(potion-bottle — Polyfork 경로 미배선, 파생본 사용).
- **QA**: 컴파일 error CS **0** + 전수 EditMode **2609 — 2599 통과 / 10 실패 = baseline 1:1**(T2 미갱신 1건 자체 수리 후 GREEN).
- **Play 확인**: 인벤/핫바에서 은신 물약·진정제·약초 아이콘이 실물 GLB로 렌더되는지 + 물약 4종 색 구분.
- **대기 게이트**: ①Play 육안 ②커밋 승인(별도).

---##

## 11-21 01:0x — 무번호 91 — 부재 GLB 전수 파악 → Polyfork 변형 생성·배치 (갈색말 완결 포함) (QA GREEN — Play 대기)

**사용자 지시**: "지금 데이터로만 있고 실존하지않는 glb들 모두 polyfolk를 변경해서 최대한 비슷하게 만들어서 배치해줘 / 일단 없는 glb 파악 먼저".

- **파악(전수 스캔)**: 리터럴 52경로 전수 + 조립형 카탈로그 전개(접미사 테이블/Furnit 8×18/MP_* 전 코드 참조/UserProvided 키 레지스트리/던전 320/NPC2026 25폴더) → **오검출 정리**(Npc2026=FBX 존재·무기 16종=전부 존재·Afterlife류=표시명 인자) → **확정 부재 25종**: 갈색말.glb 1 + MP_Jar_1_A 1 + Furnit_D_1..18(D시리즈 통째 부재 — 폴백이 전부 Furnit_A_1 수렴) + 꼬리 변형 4(Sofa_1_F/G·Chair_2_G·Owen_D).
- **[P1] 갈색말 완결(no85 블로크 해소)**: ①GLB=**백마 baseColorFactor 틴트 파생**(15MB 텍스처 무수정 — 원본해상도 계약, 체스트넛 0.58/0.35/0.20) ②**갈색말_AC=백마_AC 구조 클론**(에디터 배치 No91BrownHorseController — 상태 4·전이 임계 승계·모션만 갈색말 FBX 4종 교체: Idle 2.30s/Walk 3.47s/Gallop 1.43s/Rear 4.53s, 저장·재로드 게이트 PASS) ③BreedSpec 3종째 추가 → 스폰 루프 자동 배치(안장=공용 오프셋·세이브=breed 문자열 — 무충돌 확인).
- **[P2] MP_Jar_1_A**: clay-storage-pot-626452(Polyfork) 수용 → Interior/Props 복제본 — 알케미 potions 6번째 null 해소. CREDITS 179·PolyforkIds 178.
- **[P3] 가구 변형 22종 복원**: Furnit_D_1..18←E시리즈 꿀색 틴트 / Sofa_1_F←D·G←E / Chair_2_G←F / Owen_D←E — baseColorFactor 틴트 방식(텍스처 무수정). 카탈로그 폴백 체인/라운드로빈이 자동 소비 — 코드 변경 0.
- **QA**: 컴파일 error CS **0** + 전수 EditMode **2606 — 2596 통과 / 10 실패 = baseline 1:1**(신규 회귀 0) + MountAssetContract 갈색말 확장(리소스 4종·4상태·전이 쌍) GREEN.
- **Play 확인 목록**: ①갈색말 스폰/길들이기/탑승/발길질(백마 동일 파이프라인) ②연금 실험대 위 항아리(MP_Jar) 표시 ③실내 캐비닛 배색 다양성(Furnit_D) ④소파/의자/화로 변형 인덱스 소비 확인.
- **대기 게이트**: ①Play 육안 ②커밋 승인(별도).

---##

## 11-21 00:4x — 무번호 90 — 약초/약재 GLB 6종 Polyfork 수용 (배선 대기) + Meshy 갤러리 라이선스 조사

**사용자 지시**: "메시 ai라는 사이트가 있는데 거기 갤러리에 있는 3d 모델들은 바로 게임에 써도 법적 문제가 없는건가 / 없으면 지금 부족한 약초 glb 들을 네가 받아줄 수 있어? 확인해봐".

- **Meshy 조사(공식 헬프센터 실측)**: ①내가 생성한 모델 — 유료 플랜=소유권 유지(커뮤니티 비공개 출판 시)·무료 플랜=**CC BY 4.0(상업 가능+크레딧 "Model created with Meshy – CC BY 4.0 License" 1줄)** ②**갤러리 타인 모델 — 권리 부여 조항이 "assets you generate"에 한정** → 타인 생성물은 해당 유저의 생성 시점 플랜에 따라 라이선스가 제각각(무료 유저분=CC BY 4.0일 수 있으나 문서상 명시 그랜트 부재) → **게임 직접 사용 권장하지 않음**. 결론: Meshy 쓰려면 "내 계정으로 직접 생성" 경로만 안전.
- **대안 수용(Polyfork — CC BY 4.0 전면 보장, 기존 파이프라인)**: 약초/약재 소품 **6종 다운로드+GLB 감사 통과**(glTF 2.0·정점컬러·텍스처 0장·KHR 리스크 없음·copyright 내장):
  healing-herb-bundle-4d9724(약초 다발 770tri) / mushroom-cluster-18dc1d(587) / mushroom-cluster-f2e3ba(531) / mushroom-679e55(112) / giant-mushroom-cluster-868de6(805) / pastel-flowering-plant-d16a03(695).
  스킵: 열대식물(바나나/타로/티)·초대형 지형용(16m 클로버/화분)·캐릭터(마녀/드루이드)·현대물(현금/케이블).
- **계약 갱신**: PolyforkIds 171→**177**(중복 0·파일 178=pigeon 포함) / CREDITS 172→**178**(T2) / SkipWiringCheckIds +6(수용만 — 연금술실 배치는 별도 승인 라운드).
- **QA**: 컴파일 error CS **0** + 전수 EditMode **2606 — 2596 통과 / 10 실패 = baseline 1:1**(신규 회귀 0) + T1/T2 갱신 GREEN.
- **후속**: 배치 승인 시 연금술소(시약 선반·실험대 위)에 약초 6종 배선. 부족 종은 `POST /api/demand`(무료 요청) 경로 존재.

---##

## 11-21 00:2x — 무번호 89 — 전 영주 성 실내 완전 동일화 (시설 배치 소유 무관) (QA GREEN — Play 대기)

**사용자 지시**: "전체 영지도 따로 두지말고 그냥 자기소속 영지와 동일하게 다른 영주 소속 영지도 실내가 완전히 동일하게 만들어줘". 실측: 구조·가구는 이미 통일(J1 동일 빌더 + SelectForTerritory→KeepOctagon 단일 + variant=0 + no88 가구 패스 전 영지 공용) — 잔여 차이는 기능 4종(잠긴 문 4/상점·크래프트하우스/병사 구성/국가색). 무응답 10m → **기존검증 최소설계**: 가산형 통일만 수행(파괴 0).

- **[P1] 상점·크래프트하우스 전 영지 배치**: IndoorSceneTransition castle 케이스 — SpawnInteriorFixtures를 소유 분기 밖(공통 경로)으로 이동. 타 영주 성에도 실내 상점/크래프트하우스(전이 트리거) 동일 배치 → 시설 구성 완전 동일.
- **유지(콘텐츠 계약)**: 잠긴 문 4개(J2 락픽 콘텐츠 — VeryHard/Legendary/Hard/Easy)·수비 병사+침략 영주 이벤트(게임 상태)·국가색 벽/깃발(사용자 설계 선호 "국가색 유지"). → 문 제거까지 원하면 1줄 지시로 별도 라운드.
- **계약**: LordCastleUnificationTests J3 신설(시설 배치가 소유 분기 선행 — 소스 순서 어서션). J1/J2 기존 핀 무충돌 사전 확인.
- **QA**: 컴파일 error CS **0** + 전수 EditMode **2606 — 2596 통과 / 10 실패 = baseline 1:1**(신규 회귀 0) + J3 GREEN.
- **Play 확인**: 타 영주 성 진입 시 로비에 상점/크래프트하우스 배치 확인 + 잠긴 문 4 유지 확인.
- **대기 게이트**: ①Play 육안 ②커밋 승인(별도).

---##

## 11-21 00:0x — 무번호 88 — 영주 성 실내 설계도 정밀 정합 (가구 크기·개수·종류) (QA GREEN — Play 대기)

**사용자 지시**: "크기와 개수 가구 종류까지 정밀하게 일치하도록 수정해줘. 부족한 glb는 polyfork에서 공수하고". 13패스 격자 판독(docs/plans/2026-11-20_no88-lord-interior-furnishing-analysis.md)을 코드 진실치(KeepOctagon 117.6×88.2·포탈 배선)로 환산해 InteriorFurnishingPass.ApplyCastle 전면 재배치.

- **[P0] Polyfork 공수 2종**: straw-training-dummy-64eeb4(훈련 더미 701tri·1.78m)·bubbling-retort-4f9721(증류기 753tri) — GLB 전수 감사 통과(정점컬러·텍스처 0), CREDITS.md 170→**172**(171종+pigeon). 계약 갱신: PolyforkIds 169→171, T4 skip 77→77(재배선 4 제외+신규 미배선 4 추가), T3 하한 94 유지(실측 116).
- **[P1] 로비**: 벽난로 중앙→**북벽 중앙 밀착 12.7m 절차 석재**(몸체+개구부+마틸) / 벤치 4: N·S·E·W 직교→**사대각 벽 4.0m·포탈 오프셋 4.1m**(대각벽 13.4m 중앙에 3.2m 포탈 — 10.6m 가설은 기하 양립 불가로 정정) / 횃불 스탠드 4→**벽부착 E/W 2**(남입구 2는 입구홀) / 카펫 9×6→**24×11m** / 석판 데칼 3 신설.
- **[P2] 침실**: 기능침대 authored 1.2×2.0→**3.67×4.49(×2.45=최종 9×11m)·북벽 밀착 (0,35.5)·머리 북향** + **캐노피**(기둥 4+프레임 4재, 절차) / 옷장 2.05m GLB→**서벽 10m 절차 장롱형** / 책상 세트: 북동 자리는 기능 지휘 책상(CommandDesk 5.4×2.2×0.95 이동 승계)+동벽 1세트 / **소파 7.2m+벽난로 9m 남측 서측**(중앙 문 회피) / 상자 1·횃불 4. 협탁 2·촛불 2 제거(설계도 부재).
- **[P3] 제작소**: Owen→**blacksmith-forge 5.4m(북벽 중앙)** + **smelting-furnace 원형 가마 8.2m(북동)** + 모루 3.3m(북서) + 작업대 4종(도구 6.5/가죽가공 laced-hide 동벽/갑옷 8.7 남동+거치대/가죽 5.4 남서) + 서벽 소형 작업대 2 + **장작 2** + 원자재 상자 3(가마 회피 이동).
- **[P4] 창고**: 동벽 선반 4→**북벽 절차 선반 3(8.2×4.5×1.2)** + **동벽 긴 선반 15m** + **서벽 긴 탁자 15m(절차)+자루 6**(바닥 3+상판 3 — TopY 실측 얹기) / 데스크 2→**물류 책상 8.6m+의자(남동)** + 바구니 1(재배선) + 보물상자 5.7m(북서·재배선) + 상자 3 + **통 3** + 횃불 2. 항아리 3 제거(설계도 부재).
- **[P5] 병영**: 작전 탁자 1→**작전 테이블 2(북벽 라인 11/12m — 원정 지도+전술 지판 battle-map-tile 재배선)** + 집무 책상 6.8m(90° 회전 — 기능침대 열 회피) + **중앙 보관함 5.7m(재배선)** + **훈련 더미 1(신규 GLB)**. 기능침대 20 유지(TerritoryBuilder 병사 수용 1명/침대 계약 — 설계도 6 표기와의 편차는 기능 우선).
- **[P6] 연금술소**: 시약 선반→**남벽 밀착 8.2m(남서)** + **가마솥 6m(남중앙)** + **증류 실험대 11.3m(절차)+증류기 4m+플라스크 2×1.2m(상판 TopY)** + **절구 2(taheta 4m — 맷돌 saddle-quern 대체, skip 전환)** + **동벽 실험판 7.5m(절차)** + 물약 6 재배치. 촛불 제거.
- **설계 의사결정**: 대형 가구(벽난로/옷장/소파/선반/긴 탁자)=**절차 빌더 신설 5종**(BuildBench/Fireplace/Wardrobe/Sofa/Shelf — GLB 종횡비 왜곡 회피: 옷장 10m→16m 높이 폭증 문제), 조각 소품(모루/가마솥/증류기/더미/보물상자 등)=GLB+NormalizeMaxAxis/높이 정규화. 구 AdminDesk·PlanningTable(로비 중앙)·책상 문서 제거(설계도 외·UI 없음 실측).
- **수리 이력**: ①storageX/alcX 참조 잔존 CS0103 예방 2건 ②패리티 테스트 소스 리터럴 카운트 오산 2건(보간 루프 `$"…{i}"`=선언 1건 — CanopyPost/NShelf) ③스테일 UnityLockfile(중단된 1차 컴파일 잔류) 제거.
- **QA**: 컴파일 error CS **0** + 전수 EditMode **2605 — 2595 통과 / 10 실패 = baseline 1:1**(신규 회귀 0) + no88 패리티 계약 **6/6 GREEN** + Polyfork 계약(T1~T13) 전체 GREEN.
- **Play 확인 목록**: ①로비 — 북벽 벽난로·사대각 벤치(문 옆)·대형 카펫·석판 3 ②침실 — 캐노피 대형 침대(세이브 E키 유지)·10m 옷장·소파+벽난로 ③제작소 — 화로 2종+모루+작업대 4종 ④창고 — 북벽 선반 3·긴 선반/탁자 15m·통/자루/상자 ⑤병영 — 작전 테이블 2+훈련 더미 ⑥연금 — 가마솥·증류기·절구·시약 선반 ⑦전 방 포탈 통행(가구가 문 막지 않음).
- **대기 게이트**: ①Play 육안 ②커밋 승인(별도).

---##

## 11-20 16:0x — 무번호 87 — 아레나 맨손 격투 (병사 신규 애니 5종 수용) (QA GREEN — Play 대기)

**사용자 지시**: "애니메이션 폴더에 병사 애니메이션 추가 — 그걸 기준으로 아레나씬 병사끼리 전투에서 맨손 격투 가능하게". 신규 FBX 5종(11-20 12:00): 병사 펀치/돌려차기/박치기/펀치 후 돌려차기/막기. 계획서: docs/plans/2026-11-20_no87-arena-fist-fighting-plan.md.

- **[P1] SoldierFist_AC 신설 (에디터 배치 — 감사 게이트)**: v1 배치에서 **클립-리그 경로 감사가 전건 불일치 차단** — 신규 FBX=Generic(animationType 2, `Root/Hips/Spine1...` 스켈레톤 21경로) vs 병사 본체=Humanoid(3, 믹사모 리그) → Generic 경로 바인딩으로는 재생 불가(T포즈 예방). **v2: Humanoid 재임포트 전환**(머슬 공간 리타깃 — 플레이어 믹사모 클립과 동일 파이프라인) → humanMotion+avatar valid 게이트 **5/5 통과**. 컨트롤러: 상태 9종(Idle/Move/Hit/Death=PunchShield_AC 모션·전이 임계 승계 — Speed>0.50/0.10, Punch/Kick/Headbutt/PunchKick/Block=신규 클립) · 파라미터 Speed/MoveRate/Hit/Death/Block · 액션→Idle exit-time 0.92 복귀 · Death 출구 없음(쓰러짐 유지). 클립 실측: Punch 3.73s/Kick 3.23s/Headbutt 4.57s/PunchKick 3.07s/Block 5.07s(라운드 간격 1.8s보다 긺 — Play 튜닝 여지).
- **[P2] 듀엘 결합 (ArenaBettingSystem)**: 결투선수 컨트롤러 FistAcKey 우선+SoldierShield_AC 폴백. StrikeRound 공격=**ReplayAction 결정론 선정**(드라이버 무수정 경로) — 데미지 ≥25→PunchKick 콤보, 그 외 [Punch,Kick,Punch,Headbutt] 사이클(DuelRuntime.AttackCount). 수비 반응 개편 — 구 SoldierShield_AC 미정의 no-op(Knockdown/Stun 트리거 부재 실측)을 가시화: KO/≥40→TriggerDeath(패자 쓰러짐)·≥25→TriggerHitLight(Hit 경직)·<25→TriggerBlock(드라이버 신설 1줄). **데미지 산식/회피/베팅/정산 수치 불변(애니 레이어만)**.
- **수리 이력**: 에디터 스크립트 CS1061(`ModelImporter.avatar` Unity 6 부재→FBX 서브에셋 Avatar 획득)+CS0117(`ModelImporterAnimationType.Humanoid` 멤버명→int 캐스팅 3) 2건 — 1배치 수리.
- **QA**: 컴파일 error CS **0** + 전수 EditMode **2599 — 2589 통과 / 10 실패 = baseline 1:1**(신규 회귀 0) + no87 계약 테스트 **5/5 GREEN**(ArenaFistContractTests — 9상태 배선·Humanoid 리타깃 클립·보행 임계 승계 0.50/0.10·Hit/Death 모션 승계·소스 계약).
- **Play 확인 목록**: ①베팅 듀엔에서 병사가 맨손(펀치/돌려차기/박치기)으로 서로 타격 ②강타 시 펀치+돌려차기 콤보 ③약타 피격자가 막기 ④KO 패자 쓰러짐 유지 ⑤접근 보행/정면 고정/넉백 기존 그대로 ⑥클립 길이 체감(3~5s — 리듬이 느긋하면 state.speed 가속 튜닝).
- **대기 게이트**: ①Play 육안 ②커밋 승인(별도).

---##

## 11-20 14:3x — 무번호 86 — 말 보류 기능 구현 (세이브 연동·승마 자세·발길질) (QA GREEN — Play 대기)

**사용자 "보류도 승인" 지시** → 보류 6종을 실측으로 분류: **착수 3종**(세이브 연동/앉기 자세/말 전투) vs **에셋 블로크 3종**(황마=GLB만 존재·애니 FBX 0종 / 갈색말=애니 FBX 4종·GLB 부재 / 사운드=Resources/Audio·Sounds 전수 스캔 마필 SFX 0종 — 오디오 제공 시 합류). 계획서: docs/plans/2026-11-20_no86-horse-deferred-features-plan.md.

- **[P1] 세이브 연동**: SaveData v5→**v6 승격** — HorseSaveEntry DTO(breed/tamed/posXYZ) + SaveData.horses 필드 + SaveManager 버전 검증(>6)·Save/Load 훅. HorseMountSystem ExportSaveData/ImportSaveData — **pending 버퍼 패턴**(Instance 미성립 시 보류 후 스폰 완료 시점 복원, 길들임+위치+GroundTo 재접지). 구세이브=빈 리스트 → 전부 야생 유지(멱등).
- **[P2] 승마 자세**: **컨트롤러 편집 0건** — 실측에서 Player_AC에 SitDown/SitUp 트리거 파라미터 + SitHold 상태가 기존 완비(베드 착석 파이프라인) 확인 → HumanoidClipDriver.SetMounted(bool) 신설(기존 트리거 재용, _anim 부재 EditMode 폴백) + Mount/Dismount에서 호출.
- **[P3] 말 전투(발길질)**: 승마 중 좌클릭 → TriggerKick — Rear(앞발 들고 서기) 발화 + 전방 OverlapSphere(반경 3m) IDamageable TakeDamage(플레이어 현재 무기 데미지 재사용, 부재 시 10 고정)·말/탑승자 제외·HashSet 중복 제거·쿨다운 1.0s 결정론. **클릭 소비자 2경로 게이트**: PlayerCombat(신규 Input System — Test_10 실제 경로, MeleeClickOwnershipTests 계약상 AttackSystem 비부착) + AttackSystem(레거시). **Rear 상태 출구 전이 없음(m_Transitions: []) 실측 → 쿨다운 경과 후 CrossFade("Idle") 명시 복귀**.
- **위임 이력**: 구현 위임 1회 600s 타임아웃(0산출 — 조사 사망) → 64차 선승인에 따라 **부모 직접 폴백**으로 전량 완수(실측 좌표 패치 6파일 + 테스트 3파일).
- **수리 이력**: MountKickTests 1차 실패 1건 — `PropertyInfo.CanWrite`는 private set 존재만으로 True(접근성 검사 아님) → `GetSetMethod(nonPublic: false)` 어서션으로 수리(테스트 결함, 구현 무결).
- **QA**: 컴파일 error CS **0** + 전수 EditMode **2594 — 2584 통과 / 10 실패 = baseline 1:1**(fixture 표기 변형 매핑 포함 전수 대조: Aero=IsolatedSession·Instrument=Performance×3·InvadedLord×2·Cooking/H2_Catalog/RecipeCatalog/ThemeUSS), **신규 회귀 0**, no86 신규 테스트 **13/13 GREEN**(HorseMountSaveTests 5·MountedPoseTests 4·MountKickTests 4).
- **Play 확인 목록**: ①길들이기→세이브/로드 후 길든 말 유지(위치 복원) ②탑승 시 플레이어 앉기 자세(SitHold)·하차 복귀 ③승마 중 좌클릭 발길질(전방 피해+Rear 연출+1초 후 로코모션 복귀) ④승마 중 검격/활 발사 억제 ⑤하차 후 공격 경로 정상 복귀.
- **보류 유지(에셋 블로크)**: 황마·갈색말·사운드 — 각 GLB/애니/오디오 파일 제공 시 합류.
- **대기 게이트**: ①Play 육안(85+86 통합) ②커밋 승인(별도).

---##

## 11-20 13:0x — 무번호 85-P2~P6 — 말 시스템 전체 구현 (야생 스폰·안장 크래프트·길들이기 미니게임·탑승) (QA GREEN — Play 대기)

**사용자 계획 v2 승분의 P2~P6 순차 실행**:
- **[P2] 안장 아이템 + 크래프트**: PlayerInventory.Saddle 신설(id=mat_saddle·Material·maxStack 5) + WeaponCraftDatabase 정적 폴백 레시피 추가(**멧돼지 가죽 ×5**, RequiredLevel 3 — armor_leather 선례 패턴).
- **[P3] 야생 말 스폰**: HorseMountSystem 신설(자가부트 RuntimeInitializeOnLoadMethod) — 몬스터 배치 패턴 승계: **결정론 황금각 산포**(Random 금지 계약) + 지형 레이캐스트 접지 + 백마/적토마 GLB 인스턴스(스케일 ×2.4) + Animator(백마/적토마_AC·applyRootMotion=false).
- **[P4] 길들이기 미니게임**: **HorseTamingUTK 신설(FishingUTK 동일 패턴 — UTKWindowBase 파생·자가부트 싱글턴·게이지 바 미니게임)** — 근접+안장 보유 시 E키 → 3라운드 중 2회 적중 = 성공(길들임·안장 소모) / 2회 실패 = 재도전. 마커 결정론 스윕(Random 금지). 어셈블리 순환 방지: 정적 이벤트 역방향 알림(EInteractionPromptBridge 선례) — 시스템이 이벤트 방사, UI가 구독해 창 개방, 결과 콜백 반환.
- **[P5] 탑승/이동/하차**: E키(길든 말) → 플레이어 말 자식화(안장부 로컬 1.05m)+PlayerMovement 비활성+MountLocomotion 활성(WASD 카메라 수평 기준·Shift 질주·**TerrainGenerator 수식 계약 접지**+레이캐스트 폴백·Speed 애니 게이트 0.1/3.0 정합) / 동일 E키 하차. 카메라 팔로우 무수정 계약.
- **[P6] 실패 연출**: 길들이기 실패 시 Rear(앞발 들고 서기) 직접 재생.
- **수리 이력**: ①HorseMountSystem CS0103(PlayerInventory=ProjectName.Core 네임스페이스 — using 추가) ②HorseTamingUTK CS1061(Unity 6 IStyle에 borderRadius 단일 속성 없음 — 제거).
- **QA**: 전수 EditMode **2581 — 2571 통과 / 10 실패 = baseline 1:1**(동일 명단), 신규 회귀 0, 컴파일 error CS 0.
- **Play 확인 목록**: ①야생 말 스폰 위치/스케일/대기 애니 ②안장 크래프트 제작 ③근접 E키 프롬프트+UTK 미니게임 ④길들이기 성공/실패 분기 ⑤탑승 후 이동/질주/하차 ⑥말 스프라이트가 지면에 접지되는지 ⑦안장 로컬 오프셋(1.05m) 체감.
- **보류(계획서 명시)**: 황마(애니 미비)·갈색말(GLB 부재)·사운드·세이브 연동·말 전투·앉기 자세 애니.
- **다음 게이트**: ①사용자 Play 육안 ②커밋 승인(더티 트리).

---##

## 11-20 12:3x — 무번호 85-P1 — 말 에셋 수용 계약 완료 (GLB 리소스화+컨트롤러 배선+계약 테스트) (QA GREEN)

**사용자 승인 계획 v2의 P1 실행** — docs/plans/2026-11-20_no85-horse-riding-plan.md:
- **에셋 감사**: 말 GLB 3종(백마/적토마/황마) 동일 40노드 리그·스킨 39조인트·**임베디드 애니 0**·머티리얼 1(baseColorTex ✓)·바운드 0.74~0.76m → 감사 리포트 `TestOutput/no85_horse_glb_audit.txt`. 걷기 애니 추가 확인(갈색/백/적토마 4동작) — 황마 애니 미비로 제외, 갈색말 GLB 부재로 제외.
- **리소스화**: 말 GLB 3종 → `Assets/Resources/Models/UserProvided/말/` 복사(Resources.Load 계약).
- **컨트롤러 배선**: 신설 에디터 배치 스크립트 `Assets/Editor/HorseControllerWiring.cs` — **백마_AC/적토마_AC 신설**, 상태 4종(Idle/Walk/Gallop/Rear) 각 모션 배선(마필별 FBX "Take 001" — __preview__ 제외), **Speed float 전이 쌍**(Idle↔Walk 0.1, Walk↔Gallop 3.0 — §10-4 복귀 전이 계약), 기본 상태 Idle, 멱등. FBX Generic 보정(애니 FBX 본명 일치 실측 — 리타깃 불필요).
- **계약 테스트 신설**: `MountAssetContractTests` 3건(리소스 로드/4상태 모션 배선/전이 쌍) — **전부 Passed**.
- **QA**: 전수 EditMode **2581 — 2571 통과 / 10 실패 = baseline 1:1**(동일 명단 프로그램 대조), 신규 회귀 0.
- **다음**: P2 안장 아이템+크래프트(가죽×5) → P3 야생 말 스폰(몬스터 파이프라인 승계) → P4 길들이기 UTK(낚시 패턴) → P5 탑승/이동 → P6 마필 전환. "진행"으로 순차 실행.

---##

## 11-20 12:0x — 무번호 84 — 창 찌르기 애니 교체(사용자 제공 휘두르기.fbx 배선) (QA GREEN — Play 대기)

**사용자 요구**: "창찌르기 애니가 이상해서 플레이어 애니메이션 폴더에 플레이어 창 휘두르기 애니메이션을 넣어뒀거든 그걸로 찌르기 애니메이션을 대신해줘."
- **대상 실측**: `Assets/플레이어 애니메이션/플레이어 창 휘두르기.fbx`(17.8MB, 11-20 11:51 추가, 단일 테이크 "Take 001" — FBX 바이너리 파싱 실측, 믹사모 브랜딩 없음).
- **[84-P1] 배선**: 신설 에디터 배치 스크립트 `Assets/Editor/SpearThrustClipWiring.cs`(§11-5 계약 — AssetDatabase/AnimatorController API만, fileID 추측 금지, 멱등) → **Player_AC AttackThrust 모션 교체: Armature|Thrust_Slash_withSkin → Take 001 (3.77s, legacy=False)**.
- **임포트 계약**: animationType **Human(휴머노이드)** — 같은 폴더 성공 선례(곡갱이질+망치질=3)와 동일. ⚠ Unity 6 실측 2건: ①휴머노이드 멤버명=Human(Humanoid 아님 — CS0117) ②**clipAnimations 자동 생성 없음** → 클립 리네임 경로 무효 → 테이크명 "Take 001" 원직 계약(프리뷰 클립 __preview__ 제외).
- **[84-P2] 드라이버 프래그먼트 갱신**: HumanoidClipDriver.SpearOpenerClipNameFragment "Thrust_Slash"→**"Take 001"** — 찌르기 타격 시간 게이트(45-SPEAR-TIMEGATE)가 새 클립 길이(3.77s×마커 0.50)를 해석. FxSpearTimeGateP45Tests 어서션 진화. HumanoidClipDriverComboTests(상태명 AttackThrust 잠금) 무손상 확인.
- **QA**: 전수 EditMode **2578 — 2568 통과 / 10 실패 = baseline 1:1**(동일 명단 프로그램 대조), 신규 회귀 0, 컴파일 error CS 0.
- **유의**: AttackThrust 상태명·전이 구조 무변경(모션만 교체) — WeaponCombo 팔로업/ExitTime 전이 그대로. 새 클립 리듬에 타격 마커 0.50 체감 조정 필요하면 Play 후 지시.
- **다음 게이트**: ①사용자 Play — 찌르기 애니 체감(새 클립+83차 촉 방향 수리 조합) ②커밋 승인.

---##

## 11-20 11:4x — 무번호 83 — 히트 가독성 라이트 복원 + 타겟 링 빨강 + 트레일 정제 + 창 방향 수리 (QA GREEN — Play 대기)

**사용자 4건 요구 직접 수리**(no82 후속, 세부 지시):
- **[83-P1] 히트 가독성 라이트 복원**: 사용자 보고 "hit impact 안 붙여졌고" — 실측(Editor.log+히트 동기 캡처) 임팩트 자체는 발화 중(MagicHit 스폰+[56-P1] 정규화+보라 시그니처 캡처 확인)이나 **Flash 수명 0.11s라 체감 부족** → FxImpactLight만 복원(빛은 시각 클러터 아님). Source_82 니들에서 라이트 제거.
- **[83-P2] 타겟 링 빨강**: ExplicitTargetColor 주황(1,0.588,0.196)→**빨강(0.85,0.16,0.12)** — CombatFxCommandRingPhaseCTests.T3 색계약 진화. FXPalette.AttackCommand는 위임 프로퍼티라 자동 동기(팔레트 잠금 무손상).
- **[83-P3] 트레일 정제**: FxWeaponGlow 폭 0.055→**0.045** + 알파 캡×0.72≈**0.45**(TrailAlphaScale 신설) — 수명 0.18은 P70 잠금 창 내 유지.
- **[83-P4] 창 방향 수리**: ComputeWeaponDataGrip의 desiredWorld — **창=팔뚝 반대 방향(hand−elbow)**. 근원: 검의 "칼날 하늘"(촉=elbow 방향) 계약이 창에 그대로 적용돼 찌르기 시 촉이 뒤를 향함. wood_spear.glb 정점 실측(단면 프로파일: 촉=Y−0.9 끝 단면 0.089 vs 버트 0.052) — **tipEnd 감지 자체는 정상, 회전이 버그**. 검/단검은 기존 계약 유지.
- **QA**: 전수 EditMode **2578 — 2568 통과 / 10 실패 = baseline 1:1**, 신규 회귀 0, 컴파일 error CS 0.
- **다음 게이트**: ①사용자 Play 육안 — 히트 임팩트 체감(라이트), 링 빨강, 트레일 톤, 창 촉 방향 ②커밋 승인.

---##

## 11-20 11:1x — 무번호 82 — 피격 이펙트 Free Hit Impact 단일화 + 타겟 링 단일색 + 이동 체브런 해제 (QA GREEN — Play 대기)

**사용자 4건 요구 직접 수리**(no81 후속, "진행" 승인):
- **[82-P1] 피격 이펙트 단일화**: CombatFXGate.PlayHitFXInternal의 보완 레이어 **8종 발화 해제** — 디스토션 웨이브[71-L2]·2차 리플(크리)[77]·임팩트 라이트[33-A1]·크리 블룸[33-B1]·파편 데브리("폭죽")[33-D1]·지면 크랙("바닥에 남는 금(균열)" — 사용자 b 확정)[37-H3]·먼지 컬럼[76-S3]·유성 4단계[78-P1]. **호출선만 주석 보류(시스템 본체·파일 유지 — 재배선 대비)**. 유지: PlayImpactMulti(Free Hit Impact 에셋) + 참격 아크 + 데미지 숫자 + 카메라/히트스톱(P4·P5) + 사운드 + 극미 플래시. **사망 VFX(FxEmberRise 소멸 불티) 무변경**.
- **[82-P2] 타겟 링 단일화**: AttackTargetRingManager → 신설 `CommandRingPresentation.PlainifyTargetRing` — 회전 세그먼트(두 색 반복의 실체)/이중 링/중앙 필/대시 OFF + 팀색 알파 0.6(더 투명). **이동/선택 링 무변경**(레퍼런스는 자식 쿼드 렌더러 — GetComponentInChildren).
- **[82-P3] 이동 경로 체브런 해제**: CommandMarker.Spawn의 TrySpawnMoveChevrons 호출 주석 보류 — **목적지 링만 표시**. 본체/공유 머티리얼 유지(AttackTelegraphIndicator SharedChevronMaterial 재사용 계약).
- **계약 테스트 진화**: FxGpuQualityP76Tests.Source_82_HitLayerSuspension(8종 라인 단위 활성 0건) + FxCombatLeapS1S2R1Tests.Source_82_MoveChevronSuspension 신설. 기존 잠금 어서션은 주석 잔코드로 문자열 존재 유지(Contains·IndexOf 순서 보존 — hookCount=1, wave>slash 실측 확인).
- **QA**: 전수 EditMode **2578 — 2568 통과 / 10 실패 = baseline 1:1**(Aero·요리·블롭섀도·바드×3·침공영주×2·레시피·테마 USS), **신규 회귀 0**. 신설 계약 2건 통과. 배치컴파일 통과(런 성공 — error CS 0).
- **다음 게이트**: ①사용자 Play 육안 — 링 단일색·투명도, 이동 경로 소멸, 피격 단일 임팩트 체감 ②커밋 승인(더티 트리 미커밋).

---##

## 11-20 10:4x — 무번호 81 — Fx79RectProbe 세션 유출(Test_10 몬스터·병사 소실) 근원 확정 + 무장 게이트 수리 (피해 0)

**사용자 보고**: Test_10 Play 시작 직후 몬스터(슬라임)·병사 동시 소실 + 세션 말미 게임/에디터 자체 종료.
**근원(Editor.log 직독 확정)**: 어제(11-19) 작성한 배치 프로브 Fx79RectProbe가 [InitializeOnLoad] playModeStateChanged 구독으로 사용자 세션에서도 활성 — **OnPlayModeChanged에 Armed 게이트 누락**(3형제 Bisect/Magenta/Pigeon은 보유) → Phase(0)<2 항상 참 → 사용자 Play에서 즉시 호스트 생성.
- L3339 [79R-PROBE] 호스트 생성 → **L5278 [79R-COMBAT] 씬 전투 차단 — AI 1, 병사 1** (slime GO + TestGuard_1 SetActive(false), Play ~3초 후 = "갑자기")
- prod/grad/alpha0 3페이즈 ~40초(아틀라스 스왑+아크 24회 스폰+캡처) → 요약 후 Exit 요청 → Play 강제 종료 → **EditorApplication.Exit(0)로 에디터 종료(10:23)**
**피해 0**: Play 모드 변경은 씬 미저장 + ATLAS_RESTORED=ON + 사망/예외 0건 → 다음 Play 원상복구(슬라임·병사 자동 복귀).
**부수 데이터**: 유출 세션이 no79 블렌드 수리의 라이브 에디터 세션 검증이 됨 — alpha0 판정 GREEN("알파정상샘플-사각소멸"), beige 기저 10~41px, 베이지 사각 재현 0.
**판독 보정**: 캡처 6장 vision 크롭 판독 — "중앙 거대 구조물" = 플레이어 본인(둥근 나무 방패+금속 돔 스파이크+투구, 공격 중). 플레이어/지형/카메라(11.5m 고정) 전부 정상, 실제 소실은 슬라임·병사 2건 뿐.
**수리 [81-P1]**: Assets/Editor/Fx79RectProbe.cs OnPlayModeChanged 선두 `if (!Armed) return;` 1줄(3형제 동일 계약 복원, 배치는 커맨드라인 태그 무장 유지).
**검증**: 4형제 Armed게이트=1 전수 대조 + 브레이스/괄호/대괄호 균형 0/0/0 + diff 1줄 확인. 배치컴파일 미실행(동일 클래스 기존 심볼 참조 1줄 — 에디터 포커스 리컴파일 대기). 커밋 미실시(별도 승인 게이트).

---##

## 11-19 23:x — [no80] 공격 VFX 고품질화 P1~P8 — 구현+전수 EditMode 배치 완료 (QA GREEN — Play·커밋 대기)

**계획**: docs/plans/2026-11-19_no80-attack-vfx-quality-plan.md. P1~P2(전세션): 플립북 아크 고해상화+크리 전용 아틀라스 선택+계약 lockstep(2562/2572). P3~P8(본세션, 사용자 "진행"):
- **P3 무기 트레일**: FxWeaponGlow TrailBurst 층(칼날 팁 즉발 잔상·수명 0.18s·알파캡 0.62 승계·RefTipSpeed 속도 게이트) — WeaponSwingTrail.CreateTrailMaterialShared 공개 팩토리 위임([26-ST2] 중복 구현 금지 승계), HumanoidClipDriver 기존 호출 지점 무변동.
- **P4 hit-stop**: 크리 전용 HitStopManager.RequestHitStop(0.06f, 0.05f) — CombatFXGate PlayCrit 분기, 일반 히트 미개입. **BulletTime dip lerp가 딥프리즈를 매 프레임 덮어써 사실상 사망하는 결함 발견 → 가드 추가(딥프리즈 ≤0.05 동안 쓰기 정지 — Apply+Update 양쪽)**.
- **P5 셰이크**: 계획서 "신규 FxScreenShake" 전제가 틀림 — **기존 CCE 셰이크가 죽어있는 게 진짜 갭**: 코루틴(Update 후)이 TopDownCameraController.LateUpdate 매 프레임 팔로우 재기록에 클로버링. 수리: 코루틴→LateUpdate(order 1000) **additive** 전환(포즈 덮어쓰기 없음) + stale-capture(_originalCamLocalPos) 필드 제거.
- **P6 스킵**: 계획서 대상 FxImpactSheetSpark는 같은 날(11-19) 사용자 "시트애니 스파크 촌스럽다" 판정으로 게이트 배선 해제된 시스템(P77 테스트가 해제 잠금) — clarify 무응답 → 사용자 결정 우선 스킵. 재배선 원하면 별도 지시 필요.
- **P7 피격 틴트**: 0.08→0.10s(림 실루엣 FxHitSilhouette 0.10s 창 합일) + 크리 hit-stop/셰이크 동시 발화(P4/P5). lockstep 3점: P70Tests·P71Tests·p70_verify.py.
- **내 실수**: 첫 검증 런 신규 실패 1건 — P71Tests Source_ExistingContracts_Unharmed가 여전히 FlashDuration=0.08f 잠금(내 P7 교체와 충돌, lockstep 갱신 누락) → 수정 후 재런에서 기존 부채로 복귀. 첫 배치 시도(exit 127)는 workdir 누락(/home/korea)으로 러너 미발견.
- **QA**: 전수 EditMode **2566 통과 / 10 실패 = 기존 부채 1:1**(Aero·요리·블롭섀도·바드×3·침공영주×2·레시피·테마 USS), 신규 회귀 0.
- **다음 게이트**: ①사용자 Play 육안 — 트레일/hit-stop/셰이크/틴트 체감(특히 P7 틴트는 계획서상 "배치 판정 불가, 세션 확정 필요") ②커밋 승인(작업 트리 미커밋).

---##

## 11-19 16:0x — 무번호 79-R15 — 시트 스파크 배선 해제(사용자 결정) + F10 마젠타 스왑 실험 관문 (QA GREEN)

**사용자 보고**: ①베이지 잔존(스트림 가드 미효) ②노란 스파크 소멸 안 됨.
**스파크 원인 특정**: 게이트 3본 콘은 이미 제거됐고 이번 세션 코드도 활성화됐던 것 확인([79-ARC] psrEnabled 필드 존재). 잔존 스파크 = **FxImpactSheetSpark**(강타격/치명타 게이트 — Edge 주황 × 시트 RGB (255,250,236) 근백 → 노랑-주황 버스트).

**조치 1 — 시트 스파크 게이트 배선 해제** (CombatFXGate.cs):
- if (isCrit || damage >= 15f) FxImpactSheetSpark.Burst(...) 활성 호출 제거(주석 잔코드 없음 — 카운트 어서션 0건 계약).
- 시스템 본체 유지. FxGpuQualityP76Tests/FxSecondaryRippleP77Tests 어서션 진화(활성 0건 + 배치 2단).
- 유의: 몬스터 피격 경로의 잔여 스파크성 FX = 패링 큐(PlayerCombat — 별섬광·골드, 설계 연출)만 남음.

**조치 2 — F10 마젠타 스왑 실험 관문** (MagentaSentinel.cs):
- F10 토글 → 아틀라스 RGB를 마젠타로 재작성(알파·아크 형상 보존, 판독 가능 텍스처 직접 SetPixels32).
- 아크 본체 + 아틀라스 얼리어싱 소비처가 전부 마젠타 렌더 → [79-FILE-SCAN]에 mag 카운트 추가,
  mag≥1500 시 [79-MAG-COVER](마젠타 커버 렌더러 역산 = 근원 이름 지목) + 덤프.
- 판정: F10 ON 후 베이지 박스가 마젠타로 변하면 아틀라스 소비 확정 → 커버 렌더러 이름이 수리 대상.
  변하지 않으면 아틀라스 아닌 근원 — beigeVP 커버 렌더러([79-COVER])가 대신 단서 제공.

**QA**: 전체 EditMode 2572 — 실패 10 = baseline 1:1, 신규 회귀 0.

**다음 세션**: Play → **F10 ON** → 전투 1~2분 → 종료. 베이지가 마젠타로 보였는지가 핵심 관찰점.

---##

## 11-19 15:5x — 무번호 79-R14 — 아크 스트림 재정합 가드 + 히트 스파크 제거(사용자 결정) (QA GREEN)

**진단 데이터 확정(이전 세션 [79-ATLASBIND])**: 웜화이트 아틀라스를 mainTexture로 바인딩한 렌더러 = SlashArcFlash 10/10건(다른 바인더 0건). [79-COVER]의 Shockwave(텍스처 RGB (64,62,62) 어두움 — 픽셀 실측)/비네트(Sprites/Default 틴트 레이어)는 중심을 "덮고 있을 뿐" 밝은 웜화이트 소스가 아님 → **베이지 = 아크 쿼드 불투명 렌더로 수렴 확정**.

**수리 — 아크 렌더 결정론 강화** (FxSlashFlipbookFlash.cs):
- Emit 직후 재정합 1회 + **ArcStreamGuard** 신설(발화 후 3프레임 LateUpdate 재정합, 멱등·자기 소멸) — Unity 모듈 자동 스트림 재계산이 정합 직후 스트림을 어긋나 재배치하는 세션 의존 사례(69-P2 UV 붕괴 동족)의 결정론 방어.

**사용자 연출 결정 — 히트 스파크 제거** (CombatFXGate.cs):
- PlayHitFXInternal의 39-T2 히트 방향 스파이크 3본 콘(Edge 주황→라이트 엔 왈료, 주석 보존) 제거 — "몬스터 피격 시 노란색 스파크 연출이 촌스럽다" 사용자 판정(11-19).
- SpawnDirectionalSpark 메서드 자체는 패링(PlayerCombat)/애프터라이프/AnimalAI(플레이어 피격) 소비처 유지 — 시그니처 불변.
- FxHitRewok39Tests Source_T2 어서션 진화: Contains 계약 → 라인 단위 활성 호출 0건 계약(주석 잔코드 우회 차단).

**QA**: 전체 EditMode 2572 — 실패 10 = baseline 1:1, 신규 회귀 0.

**다음 검증(사용자)**: Play → 전투 1~2분 → ①베이지 소멸 여부(스트림 가드 효과) ②노란 스파크 소멸 확인. [79-ATLASBIND]/[79-COVER]/[79-ARC] 관문 상시 가동 — 잔존 시 덤프가 다음 단서를 남긴다.

---##

## 11-19 15:3x — 무번호 79-R12/R13 — 캐시 수리 미효 확인 + 결함 순간 근원 특정 관문 완성 (QA GREEN)

**이번 세션 판독**:
- 히트 동기 캡처 16건 → 결함 포착(beige 6,038/2,085/38,873/3,894px — beigeVP 화면 중심대).
- **[79-ARC] 스윕 성공**: SlashArcFlash 인스턴스 5건 — 전부 pc=2·psTime 0.18~0.40(생존)·mat=SlashArcMat#-6424(캐시 무결 — matNull=N). **그러나 vp 대부분 화면 밖(y 1.01~4.45)** — 결함 박스(화면 중심)와 위치 불일치.
- [79-CULPRIT] 4건 전부 "1.2vp 내 PSR 없음" — 아크 GO(0.13,0.57)와 클러스터(0.47,0.49)의 err=0.42<1.2인데도 미매칭 → **결함 순간(캡처+3프레임)에는 결함 유발 아크가 이미 소멸(캡처가 수명 말단 50ms 창을 잡음) 또는 매칭 제외 요인** — 판정 유보.
- 캡처 정밀 판독: 박스 채움률 0.67~0.97·T자/계단형(십자 이중 쿼드 겹침)·지면 수준·캐릭터 머리가 박스 위로 노출 — **지면 인접 월드 쿼드 정합**.
- 색 수학: 웜화이트 아틀라스(255,244,214)×비네트 α0.22 = (250,197,172) ≈ 관측 — 팔레트 단일 후보 유지.
- 가설 서열 갱신: ①아크 플립북 불투명(UV 붕괴/알파 강제 — 69-P2 동족) ②**아틀라스 얼리어싱**(미바인딩 지면 머티리얼— TerrainGenerator L1733 지면청크 mat=신규 URP/Lit·색만 설정·텍스처 없음 — 아틀라스 GPU 슬롯 얼리어싱; F7 킬스위치가 아틀라스 생성 자체를 차단했으므로 F7 세션 무결함 설명 가능) ③Color 스트림 알파 미전달.

**신설 관문**:
- [79-COVER] — 결함 중심을 덮는 전 렌더러(메시 쿼드/데칼 프로젝터 포함) 역산(51-a DescribeCoveringRenderers 재사용, Camera.main 폴백).
- [79-ATLASBIND] — 결함 순간 웜화이트 아틀라스를 mainTexture로 바인딩 중인 렌더러 **전수 나열** — 아크 외 바인더(얼리어싱 근원)를 이름으로 지목.
- [79-ARC] 덤프 확장: psrEnabled·visible 추가(캡처+3프레임 시점 렌더러 활성 상태 고정).

**QA**: 전체 EditMode 2572 — 실패 10 = baseline 1:1, 신규 회귀 0.

**다음 세션**: 키 불필요, 전투 1~2분. [79-ATLASBIND]/[79-COVER]가 결함 순간 근원을 이름으로 고정 → 수리 설계(바인딩 정규화/스트림 강화/알파 경로별).

---##

## 11-19 15:0x — 무번호 79-R11 — 캐시 수리로 미해결 확인 + 히트 동기 결함 캡처 관문 신설 (QA GREEN)

**사용자 보고**: 무F7 세션에서 베이지 잔존 — SlashArcMat 세션 캐시(79-R10)로 미해결. 메커니즘은 머티리얼 생성 채널보다 깊다.
**현재 증거 체계**: F7 킬스위치로 근원(SlashArcFlash)은 확정. 결함 순간의 렌더 상태(스트림/블렌드)는 미고정.

**신설 — 히트 동기 결함 캡처 체인** (MagentaSentinel):
- RegisterFxSource(FX 스폰 시)에서 _beigeCapPending 예약(+0.12s 결함 창) → BeigeCaptureTick(2.5s 쿨다운) →
  Screenshots/79_beige_*.png 파일 캡처 → _fileScanPending → [79-FILE-SCAN] 판독 → beige≥2000 시 [79-CULPRIT] 근원 덤프.
- TryCaptureEvidence 10s 쿨다운은 결함 순간을 놓침(11:47 세션 실측: 캡처 2건 beige 42/79px뿐).
- **덤프 확장**: _SrcBlend/_DstBlend 부동소수 + **버텍스 스트림 순서**(69-P2 UV 붕괴=스트림 어긋남→UV 상수화→솔리드 박스와
  동일 증상 — 사용자 세션의 스트림 상태를 이름으로 고정). 정상=[Position, Normal, Color, UV, AnimFrame].

**QA**: 전체 EditMode 2572 — 실패 10 = baseline 1:1, 신규 회귀 0.

**다음 사용자 세션**: **키 입력 불필요** — Play 후 전투 1~2분만. 결함이 나는 순간 자동으로 캡처·판독·덤프된다.
판독 후 [79-CULPRIT]의 streams/src/dst 값으로 2차 메커니즘 확정 → 수리 설계.
유보: 2차 메커니즘 후보 = ①버텍스 스트림 재어글라이즈(사용자 세션 타이밍) ②블렌드 상태 파손 ③Color 스트림 알파 미전달.

---##

## 11-19 14:3x — 무번호 79-R10 근원 확정 + 수리 — SlashArcMat 세션 캐시(히트마다 생성/파괴 금지) (QA GREEN)

**근원 확정(사용자 세션 F7 킬스위치)**:
- F7 OFF 세션(11:12~11:13 캡처 8장): beige 4,424~22,918px 결함 상주.
- F7 ON 세션(11:36~11:37 캡처 5장): beige 최대 625px(기저 노이즈) — **결함 소멸**.
- 킬스위치는 FxSlashFlipbookFlash.Play만 차단 → **SlashArcFlash = 베이지 사각 근원 확정**.
- [79-FILE-SCAN] 관문 정상 동작 확인(5건 로그 — RT 블라인드 우회 유효). [79-CULPRIT]는 결함 미발생 세션이라 미발화(정상).

**수리 — SlashArcMat 세션 캐시** (Assets/Scripts/Systems/FxSlashFlipbookFlash.cs):
- 기존: 히트마다 CreateSoftParticle 신규 머티리얼 + 0.8s 후 파괴 → 발화마다 셰이더 변형 컴파일 채널 요청.
- 사용자 세션(인터랙티브, 컴파일러 부하)에서 이 경쟁이 알파 강제 1(불투명 웜화이트 셀·계단형 합성)로 렌더 —
  배치(컴파일러 여유)에서는 즉시 정합 컴파일로 반투명 정상. 색 수학·형상 수학·피격 타이밍 전부 정합.
- 변경: _slashArcMat 세션 1회 생성 캐시(48-T3 수리 캐시 선례) + 발화마다 파괴 금지. 버텍스 스트림 정합은 렌더러 인스턴스 경로(멱등 69-P2)라 캐시 무간섭.
- 계약 진화: FxQualityRoadmapP37Tests Source_S3_SlashFlipbook 어서션 갱신(Destroy 계약 → 캐시 계약, 근거 코멘트 동봉).

**QA**: 전체 EditMode 2572 — 실패 10 = baseline 1:1, 신규 회귀 0(중간 1회 회귀 발생→어서션 계약 진화로 소멸 확인).

**다음 검증(사용자)**: Play(무F7 — 아크 정상 발화) → 전투 1~2분 → **베이지 소멸 여부** 관찰.
- 소멸 = 수리 완료(근원+메커니즘 동시 차단). F7은 진단 토글로 상시 유지.
- 잔존 시 = 캐시로도 못 잡는 2차 메커니즘(Color 스트림 알파) → 다음 레버는 colorOverLifetime 의존 제거(텍스처 알파 전용).

---##

## 11-19 14:0x — 무번호 79-R10 킬스위치 키 재배치 F8→F7 — 실내 이동 바인딩 충돌 해소 (QA GREEN)

**사용자 보고**: F8이 실내 이동 토글(IndoorDebugEnterExit)과 충돌 — 킬스위치 확인 불가.
**조치**: KeyCode 전수 조사(F6·F8·F9·F12만 사용) → F7이 완전 비어 있어 킬스위치를 F8→F7로 이동. 실내 이동 바인딩 무변경.
**QA**: 전체 EditMode 2572 — 실패 10 = baseline 1:1, 신규 회귀 0.

---##

## 11-19 11:30 — 무번호 79-R9 사용자 세션 판독 — 베이지 확정 급진전: 웜화이트 아틀라스 불투명 렌더(계단형 합성) + 진단 관문 신설 (F8 킬스위치·[79-FILE-SCAN]·[79-CULPRIT])

**사용자 세션(11:00~11:13 Play+F9) 판독 결과**:
- [69-DIAG] F9 토글 8회 확인, [44-SENTINEL] 긴급 캡처 8장(11:12:21~11:13:25) 생성 — **결함 재현 포착**.
- 픽셀 실측: beige 4,424 / 10,603 / 22,918 / 20,876px (박스 86×83~194×139, 순흑 유지 검출 54~395px).
- **솔리드 베이지 박스 실측**: 채움률 0.85·평균 (248,194,167)·비전 판정 **계단형(다중 직사각 겹침)**·날카로운 가장자리·불투명·지면 수준(엔티티가 위에 렌더).
- 색 수학: 웜화이트 아틀라스(255,244,214) × 적색 비네트 α0.22 = (250,197,172) ≈ 관측 ✓ — 팔레트 단일 후보.
- 형상 수학: 여러 플립북 파티클(동일 포인트 오프셋 스폰)이 각각 불투명 웜화이트 셀로 렌더되면 계단형 합성 — SlashArcFlash 불투명 렌더 정합.
- **[69-BEIGE-UI] 0건인데 파일 캡처는 결함 보유** → RT 경로(CaptureScreenshotIntoRenderTexture)가 결함을 보지 못함 확정(R1 배치 실측과 동일 — RT 블라인드). 베이지 감지 관문을 RT에서 파일로 교체 필요.

**신설 관문(사용자 세션 확정용)**:
- **F8 SlashArcFlash 킬스위치** — MagentaSentinel.F8 토글(1/s 스로틀 로그) → FxSlashFlipbookFlash.Play 즉시 차단. Play 중 F8 ON 후 결함 소멸 시 근원 확정.
- **[79-FILE-SCAN]** — TryCaptureEvidence 파일을 3프레임 후 판독, 베이지/블랙 클러스터 파일 표준 산정(RT 블라인드 우회).
- **[79-CULPRIT]** — 결함 순간 클러스터 중심 화면 매칭 PSR의 머티리얼 투명 상태(surf/kw/blend/q/텍스처/파티클수) 1급 덤프.

**QA**: 전체 EditMode 2572 — 실패 10 = baseline 1:1, 신규 회귀 0 (MagentaSentinel/FxSlashFlipbookFlash 소스 어서션 테스트 통과 포함).

**다음 사용자 세션(확정 프로토콜)**: Play → F8 ON(로그 "[79-KILL] ... 전환" 확인) → 전투 1~2분 → 결함 관찰 → F9 ON(덤프 활성) → 종료 후 로그 회수. F8 ON 상태에서 베이지 소멸 = SlashArcFlash 근원 확정 → 결정론 알파 수리 설계. 소멸하지 않으면 [79-CULPRIT]/[79-FILE-SCAN] 덤프가 다른 근원을 이름으로 지목.

---##

## 11-19 13:30 — 무번호 79-R8 수리 효과 재검증 — 아틀라스·블랙 완화 모두 확인 (RealChain 재현기 클린)

**방법**: 프로브 버스트 RealChain(PlayHitFX 실체인 체인 — R4 블랙 재현기)×3 + Control×1, [79-ATLAS] 런타임 판독.

**결과**:
- **아틀라스 빈 셀 수리 확인**: cell6·7 중심 = RGBA(255,244,214,0) — 수리 전 (0,0,0,0)에서 웜화이트로 전환. 셀 0 중심 α0은 아크 중앙 구멍(원래 형상)으로 정상. 전순환(76-S1)에서 빈 셀 샘플링 소멸.
- **블랙 완화 확인**: RealChain×3 black=4/4/4(beige 17-20=UI 기저) — 수리 전 동일 체인 black=985. 수학 정합: 외곽선 최대 알파 0.65×초록 지형 블렌드=(31,69,35)·그림자 0.5=(45,98,50) — 순흑 판별식(r,g,b≤12) 미감지 → 데미지 숫자 기인 순흑 렌더 원천 차단.
- Control(ALL) black=4 — 클린.
- 유보: R4 재현율 ~33%(3버스트 중 1)였으므로 3연속 클린이 수리 효과 확정 증거는 아니고, 판별식 수학과 함께 "효과 정합" 판정. 잔여 솔리드 채움 요소 가능성은 사용자 Play+F9에서 종결.

**배치 프로브 시퀀스 종료 — 남은 것**: 사용자 Play+F9 캠페인(베이지 최종 확정) → Play 검증 → 커밋 승인.

---##

## 11-19 13:xx — 무번호 79-R7 수리 — 아틀라스 빈 셀 수리 + 데미지 숫자 블랙 완화 (EditMode QA GREEN·신규 회귀 0)

**수리 1 — 아틀라스 빈 셀 수리** (Assets/Scripts/Systems/FxFlipbookGenerator.cs):
- FillUnusedCells(px, w) 신설 — 미사용 셀(Frames=6 이상 슬롯 6·7)에 마지막 프레임(5) 복제.
- 근거: 79-R4 런타임 실측 slashArc 셀 6·7 중심 (0,0,0,0) — frameOverTime 0→1 전순환(76-S1 계약)이 수명 말 25%를 빈 셀 샘플.
- Generate(연기)·GenerateSlashArc(참격 아크) 양쪽에 호출. 프레임 0~5 내용 무변경 — 순수 보존 수리.

**수리 2 — 데미지 숫자 블랙 완화** (Assets/Scripts/Systems/CombatVFXController.cs, DamageNumberRunner):
- 근거: 79-R4 실측 — "12"×3 연타 스택 + 흑색 외곽선/그림자 합성이 82×58·3947px 순흑 덩어리로 굳음(IMGUI=게임뷰 전용).
- OnGUI 런타임 유효 알파: 외곽선 alpha→alpha×0.65, 그림자 alpha×0.8→alpha×0.5.
- 스택 간격 1.5×: dx 26→39·dy 15→22.5 (스타일 스케일 s 배수 유지).
- EnsureStyles 기본값(0.92/0.65)은 런타임에 덮어씌워지므로 무효값 — 실질 변경은 런타임 2행.

**QA**: run_editmode_check.sh 전체 — 2572 테스트, 실패 10건 = 기존 baseline 10건 1:1(AERO·Cooking·H2_Catalog·Instrument×3·InvadedLord×2·RecipeCatalog·ThemeUss). **신규 회귀 0**. 관련 테스트(FxShaderAssetsD/FxMonsterDetailM2/FxQualityRoadmapP37/FxCombatResidueP71/FxGpuQualityP76) 전부 통과.

**미완료(명시)**:
- 베이지 사각 근원 — 미확정(웜화이트×비네트 수학만 정합, 배치 6캠페인 재현 실패). 최종 확정 = 사용자 Play+F9 1회([69-BEIGE-UI]+[69-BISECT]★+[69-CULPRIT]) — 사용자 참여 필요.
- 블랙 박스의 솔리드 채움 메커니즘(3947px) — 데미지 숫자 합성 외 잔여 요소(R4 증거 캡처는 러너의 PNG 클린업으로 소실, 로그 기록 보존) 가능성 유보. 사용자 Play에서 재현 시 F9 판독으로 종결.
- Play 검증·커밋 — 별도 승인 대기(더티 트리에 수리 2파일 포함).

---##

## 11-19 12:xx — 무번호 79-R2~R6 바이섹션 프로브 5차 캠페인 — 블랙 박스 1개 실체 확정(데미지 숫자 합성)·베이지 배치 재현 실패·프로브 결함 3건 교정 (수리 없음·증거 확대)

**방법**: Fx79BisectProbe 개조 반복(R2 강화덤프→R3 씬전투 차단+단독발화→R4 불투명강제+실체인체인→R5/R6 OpaqueArcFlash 수정 재실행). 씬 전투 차단(AnimalAI+Guard 비활성), 스폰점 시야 투영 역산, 비네트 수동 발화, _BaseMap 픽셀 덤프, 머티리얼 투명상태 덤프.

**프로브 자체 결함 3건 발견·교정**:
- ①스폰점 Y 치환 버그 — 레이 5m점 XZ에 지면높이 치환 → 스폰이 화면 밖(vp.y≈2.0-3.0)으로 나감. **R1-R2의 "스폰점 결함" 판정 무효화** — 결함은 씬 전투 지점(화면 중앙) 것. 투영 역산 검색으로 교정(지형 오차로 vp 2.04 잔존 — 언덕).
- ②억제 오염 — 씬 전투(AI 병사/슬라임)가 억제 창 밖에 FX를 스폰 → 1차 SUP 판정 오염. 차단으로 해소.
- ③FindNewByPrefix 버그 — 스폰 후 스냅샷이라 신규 미식별(R4 OpaqueArcFlash 무효). R5에서 교정·확인(불투명 강제 1건 로그).

**핵심 실측**:
- **R3(차단+교정 스폰): 합성 스폰(MagicHit 4시스템 단독 격리+플립북+비네트)으로 결함 재현 0** — ALL/Simbol/Flash/Shockwave/Root/ArcFlash/VignetteOnly 전부 클린(beige 13-21=UI 기저, black 1-8).
- **R4(실체인 체인 PlayHitFX): 블랙 박스 재현** — RealChain2 캡처0 black=985(3947px 실측, 82×58, 채움률 0.83, 순흑). **비전 판독: "12" 데미지 숫자 3개 세로 스택 + 흑색 솔리드 합성물**. 데미지 숫자=IMGUI(OnGUI) → **게임뷰 전용 렌더 확인**(RT 캡처 0건의 메커니즘 해명).
- **사용자 세션 블랙 박스 재판독(091342)**: **"10" 데미지 숫자 포함** + L자 블랙(베이지 패널 뒤 블랙 패널이 가장자리로 노출된 구조) — 데미지 숫자 합성 + 스택 패널 이중 구조.
- **강제 불투명 실험(R5/R6)**: SlashArcFlash surf=0+키워드 OFF+큐 2000 강제 ×3버스트 + 비네트 hold 1.8s 연장 → **베이지 미재현**(beige 17-21). OpaqueMagicHit(4렌더러 강제)도 미재현. 순수 웜화이트(255,244,214)는 판별식(g≤222) 밖 — 판별식 한계 유보.
- **아틀라스 런타임 검증**: slashArc 512×256, 셀0 중심=(255,244,214,α0 — 웜화이트 RGB·알파만 형상)·**셀6·7=(0,0,0,0) 빈 셀 확정** — 수명 25%가 빈 셀 샘플링(별도 실버그).
- **MagicHit 시스템 실측**(56-P1 정규화 상태): Flash(2m·1-Point 512×512)/Shockwave(0.4m·1-Shockwave 2)/Simbol(1m 고정·1-Crit_)/emitter(0.13m·스무스서클 256×256 white@α45) — 전부 surf=1 kw=T 정상 투명 상태.

**현재 확정 상태**:
- **블랙 사각 1개 실체 확정 = 데미지 숫자 합성물**(IMGUI 흑색 8방향 외곽선 α0.92+그림자 α0.65×스택 3~8개 → 저해상도에서 블랙 덩어리). "피격 때마다"+"게임뷰 전용"+"숫자 사라지면 소멸(간헐)" 전부 정합.
- **베이지 사각 미확정** — 웜화이트×비네트 수학만 정합. 배치 6캠페인 재현 실패 → 사용자 세션 의존 가능성(66-P1 전례) 높음. 최종 확정 경로 = 사용자 Play+F9 1회([69-BEIGE-UI]+[69-BISECT]★+[69-CULPRIT]).
- 프로브 산출물: TestOutput/no79_*.png(수백 컷)+no79_bisect.txt. 프로브 파일은 코드 무수정 도구 — 세션 오염원 아님(태그 무장).

**다음(79-R7 예정)**: ①아틀라스 빈 셀 6·7 수리(마지막 프레임 복제) ②데미지 숫자 블랙 완화(외곽선 α0.92→0.6·그림자 0.65→0.4·스택 간격 1.5×) ③사용자 F9 캠페인으로 베이지 최종 확정 후 수리. 커밋 별도 승인 대기.

---##

## 11-19 11:xx — 무번호 79-R1 바이섹션 프로브 2회 실측 — SlashArcFlash 무죄·ImpactVFX 계열 유력·아틀라스 빈 셀+불투명 렌더 구조 확정 (코드 변경 없음)

**방법**: 배치 전용 바이섹션 프로브(Fx79BisectProbe — executeMethod 무장·사용자 세션 무개입·셀프 종료)로 Test_10 합성 플레이. 스폰점 임팩트 3회/버스트 + 게임뷰 파일 캡처 + 클러스터 스캔 + 근원 렌더러 덤프. 1차 전수(후보 11종) → 2차 집중 교차(BASE/SUP:SlashArcFlash 6+6).

**실측 결과**:
- 결함 재현 확정: 82-88px 솔리드 정사각 @ 스폰점(화면 중앙), 베이지(248,200,173)와 순흑(0,0,0) 이중 색, 캡처당 ~35% 간헐.
- **SlashArcFlash 무죄**: SUP:SlashArcFlash 버스트(프로브 인스턴스 억제 확인 — 덤프에 d≈0 인스턴스 부재)에서도 베이지 1006·2006, 블랙 1487 출현. +크기 불일치(2.6m 쿼드=200-300px ≠ 82-88px).
- **AttackArcRibbon 무죄**: 덤프 전체에 리본 렌더러 0건(스윙 앵커 부재로 메시 미생성).
- **씬 전투 오염 발견**: 병사 AI 자체 전투가 SlashArcFlash/ImpactVFX를 스폰(d=7.1 인스턴스 덤프 실측) — 프로브 억제는 스폰 시점에만 적용되어 씬 전투 인스턴스는 억제 탈출 → 1차 SUP 해석 오염.
- **덩어리 위치 전수**: 결함은 전부 스폰점(중심 348-390, 226-242) — 씬 전투 지점 아님.
- **ImpactVFX 시스템 구성**(56-P1 정규화+69-P1 재정합 적용 상태): Flash(2m 커브·tex=1-Point)/Shockwave(0.4m·1-Shockwave 2)/**Simbol(1m 고정·1-Crit_)**/emitter(스무스 서클·color 1.0,0.85,1.0).
- **크기 정합**: 82-88px ≈ FOV60° 5m에서 1.07-1.15m → **Simbol(1m 고정) 정합**, SlashArcFlash(2.6m)/Flash(2m)/Shockwave(0.4m) 불일치.

**구조적 결함 확정(코드 판독)**:
- FxFlipbookGenerator 아틀라스 4×2=8슬롯 중 **6프레임만 기록** — 셀 6·7=(0,0,0,0) 빈 셀. frameOverTime 0→1 전체 순환 → 수명 25%가 빈 셀 샘플링.
- 아틀라스 RGB는 셀 전체 균일 웜화이트(255,244,214 — 알파만 아크 형상) → **알파 무시(불투명) 렌더 시 셀 전체=솔리드 웜화이트**, 빈 셀=솔리드 블랙 — 관측 이중 색을 단일 메커니즘으로 설명.
- 색 수학: 웜화이트+비네트 틴트(α0.18-0.22)=(248-250,197-200,172-173) ≈ 관측 (248,200,173) 정확.
- 전례: 66-P1 "소프트 파티클 키워드 제거"가 동일 증상(웜화이트 베이지+암흑 사각, 세션 의존)을 수리한 이력 — 알파 경로의 재발.

**다음 라운드(79-R2 예정)**: ①Simbol/Flash/emitter 개별 억제 실험으로 1m 사각 근원 특정 ②결함 순간 _BaseMap 텍스처 크기+픽셀 샘플 덤프(GPU 슬롯 내용 직독) ③아틀라스 빈 셀 수리+frameOverTime 범위 수정은 근원 확정 후 일괄.

---##

## 11-19 10:xx — 무번호 79-R0 "베이지·검정 사각 재보고" 재개 분석 — 오늘자 세션 캡처 픽셀 포렌식+비전 판독 (수리 없음·증거 수집)

- **사용자 보고**: Test_10에서 베이지·검정 사각 여전. R6 스코치 킬스위치·R7 비네트 신선 텍스처 활성 상태(사용자 로그 [69-R6] 차단 발화+[69-R7] texID 고유 발화 실측)에도 재현 — 잔여 근원 존재.
- **오늘자 캡처 15장(44_magenta_0900~0913) 픽셀 실측**: 베이지 최대 8,712px(304×231 @ (124,180)-(427,410), 톤 2종 (249,198,172)/(251,221,191)), 블랙 2,593px = **116×116 균일 (6,6,6) 정사각**. 사가 서명색과 정확 동일 — 결함 재현 확정.
- **색 수학**: 웜화이트(255,244,214 — FxFlipbookGenerator 참격 플립북 톤) × 피격 비네트(0.9,0.12,0.1)@α0.22 = (250,197,172) ≈ 관측치 — 베이지 = 참격 아크 계열 쿼드가 **솔리드로 렌더 + 비네트 틴트**.
- **비전 판독(091322/091342 확대)**: 베이지 박스 안에 몬스터 이름표 콘텐츠(이름·등급·HP·공/속 스탯, 하늘색 숫자) 정상 렌더 — 박스는 콘텐츠 뒤 배경 쿼드. 크기 변동(128×142~304×231) = scale 파라미터 빌보드(FxSlashFlipbookFlash startSize 2.6m×scale 정합). 블랙 사각 = 캐릭터 발밑.
- **용의자(확정 전)**: ①FxSlashFlipbookFlash 빌보드(69-P2 재정합 이후에도 재현 — AnimFrame 외 경로) ②**정적 공유 런타임 텍스처 패밀리**(FxFlipbookGenerator._slashArc·_smoke / AttackArcVFX._slashTexture / NameplateOverlayUTK _gradGuard·_gradGuardWarn·_gradMonster / BlobShadow._staticTex / UIStyleManager 캐시 6종) — R7 비네트와 동일 리스크 프로파일(참조 파괴→GPU 슬롯 재사용→직전 업로드 텍스처 표시 = 웜화이트/블랙 동시 설명).
- **센티널 무보고 이유**: 중량 진단(F9) 기본 OFF(R5) — 게임뷰 캡처+바이섹션 미가동, 경량 RT 스윕은 게임뷰 전용 결함 미관측.
- **다음**: 근원 이름 확정 후 수리(확정 전 수리 금지 계약 유지) — A)사용자 Play+F9 1회 또는 B)배치 합성 프로브 자가 수집(에디터 닫힘 확인).
- QA/코드 변경 없음.

---##

## 11-19 01:xx — 무번호 78 P1 Meteorite VFX Graph 샘플 배선 + FBX 결손 머티리얼 리맵 수리 (v5 OnAssignMaterialModel)

- **구현(위임→부모 검증)**: MeteoriteVFXRunner.cs 신규(221줄 static — 4단계 시퀀서: Anticipation(0.6배/0.4s) → t=0.12s GroundImpact(크리 1.5배 위계)+LightFlash 동일프레임 → t=0.20s GroundImpactAdds(77 리플 뒤). ImpactBuffer 발화 금지) + MeteoriteVfxP78Tests.cs(164줄, T1~T7) + CombatFXGate.cs L150 강타격 밴드 1라인 배선. 부모 재검증: [무번호 76-S3] 카운트 1 보존·brace 균형 3파일·Random 0건·예산 가드 6·unscaled 단일 언어 ✅.
- **FBX 결손 수리(부모 직접 — 5사이클)**: P78 임포트 3개 FBX(GroundImpact·Meteorite·MeteoriteBlastMesh2) meta externalObjects가 소스 레포 guid 참조(프로젝트 .mat 부재) → 렌더러 null material → [43-PROBE] 신규 실패 1건. v2 SerializedObject 편집 applied=True에도 네이티브 무반영·meta 디스크 불변(네이티브→직렬화 일방향 실측) → v3 GetExternalObjectMap 사전 변이 무효(복사본 반환 — diskGuid 구값 유지 실측) → v4 SetExternalObjectMap CS1061(Unity 6 미존재) → **v5 AssetPostprocessor.OnAssignMaterialModel**(공식 임포트 콜백 — dangling remap은 유효 리맵이 아니므로 콜백 발화, 43-PROBE는 렌더러 sharedMaterials만 검사) → **PASS(verify_broken 0)**. 중간 수리: CS0019(SourceAssetIdentifier 구조체 null 비교)·CS0117(WriteImportSettings 미존재)·CS1061(Next(bool) bool 반환 → Copy() 반복자)·**GroundImpact FBX 내부 머티리얼=lambert2 유일(lambert1 부재) → 필터 lambert* 접두 완화**. No78MeteoriteMaterialAssigner = 프로젝트 상설 가드(Meteorite 메시 스코프·lambert* → Basic Hit_URP). VFX Graph 메시 출력은 그래프 자체 셰이더 렌더 — 할당 머티리얼 런타임 미사용(프로브 무결성 목적).
- **검증(독립 QA)**: 전수 EditMode **2572 total / 2562 passed / 10 failed = baseline 1:1 복원**(AERO·Cooking·H2_Catalog·Instrument×3·InvadedLord×2·RecipeCatalog·ThemeUSS) — 리맵 전 2561/11 → [43-PROBE] Passed 복원·신규 실패 0. **P78 7/7 GREEN**(T1 경로·T2 NoBurst·T3 예산·T4 게이트 주석 카운트·T5 수명 상수·T6 LightFlash 동일프레임·T7 ImpactBuffer 미발화). 리맵 근거: TestOutput/no78_material_remap.txt(VERDICT=PASS)·no78_remap.log.
- **대기**: ①Play(사용자 일괄 — 74~78 누적: 낚시라인·벌목·채광·플린치·시트스파크/더스트컬럼·크리 리플·유성 4단계) ②커밋 승인(별도).

---##

## 11-17 16:0x — 무번호 69-R7 진범 추정 — 비네트 정적 공유 텍스처 별칭(스코치 무죄 확정 후 잔여 공유 텍스처 보유자) (qa106)

**사용자 보고**: "여전히 베이지 검정 사각 남아있어" — R6 킬스위치 세션(13:36~13:40, BatchMode:0 대화형 실측)에서 `[69-R6] ScorchMark 스폰 차단` 3회 발화 = **스코치 데칼이 존재하지 않은 상태에서 베이지 관측 → 스코치 무죄 확정**. 이전 바이섹션 판정(2회 동일 근원)은 비네트 펄스 페이드 타이밍과 억제 측정 시차의 혼동으로 결론 수정. 참고: 해당 세션도 `[69-DIAG] 종료 감지` 정상 종료 = **팅김 2연속 소멸(R5 효과 재확인)**.
**진범 모델(숫자 합치)**: ScreenVignetteFX — 피격/패링 시 카메라 자식 풀스크린 쿼드(2.4× 프러스텀, Sprites/Default) + **정적 공유 텍스처(1회 생성 영구 재사용 `_vignetteTex`)**. R3가 사용자 세션에서 실증한 "공유 런타임 텍스처 → 직전 업로드 텍스처(슬래시 아크 웜화이트 아틀라스 255,244,214) 별칭" 패턴의 잔여 보유자. 별칭 시 출력 = 아틀라스 웜화이트 × 적색 틴트(0.9,0.12,0.1)×α≈0.22 = **(249,196,172) ≈ 사용자 세션 실측 베이지 (249,201,173) 정확 합치**. 실측 박스 120×88px ≈ 960×540 풀스크린 위 8×6 플립북 아틀라스 셀(120×90) 크기 합치. "게임뷰 전용" 관측 = RT 스캔 0.5s 주파 vs 펄스 수명 0.36s 타이밍 미스로 설명 가능.
**왜 지금까지 못 잡았나**: ①R1 바이섹션이 "카메라 자식 = 의도 오버레이"로 후보 제외 — 진범 계열을 영원히 못 지목하는 맹점(스코치 오지목의 직접 원인) ②정적 공유 텍스처라 세션 내내 오염 지속 ③부수 발견: 펄스마다 `new Material` 생성 후 미파괴(누출).
**R7 수리**:
- ①ScreenVignetteFX — 정적 공유 텍스처 폐기, 펄스별 신선 생성(`CreateVignetteTextureFresh`)+Apply+러너 등록, 펄스 종료 시 텍스처+머티리얼 동반 파괴(R3 계약 승계+누출 수리). Begin/BeginManual 공개 시그니처 불변(UVWX/ScreenChromaticV3 계약 호환).
- ②MagentaSentinel 바이섹션 — 카메라 자식 "제외"→"[CAM-CHILD] 태그 보고" 전환(맹점 제거, 판정 문자열 유지).
- ③펄스별 1급 로그 `[69-R7] 비네트 펄스 color=... texID=...` (6회 한정) — 다음 세션 베이지 목격 시각↔펄스 시각 상관 데이터.
**QA(qa106)**: 컴파일 error CS **0** · EditMode **2511/2501/10** — baseline 1:1, 신규 회귀 0, 69 계약 7/7 GREEN.
**계약 유지**: 종결 선언 금지 — 사용자 세션 확인 전까지. 잔존 시 F9 1회 → [CAM-CHILD] 태그 포함 [69-BISECT]★+[69-CULPRIT] 숫자 판정. 커밋 별도 승인 대기.

---##

## 11-17 15:1x — 무번호 69-R6 스코치 데칼 킬스위치 — 근원 오브젝트 미생성으로 단일화, "종결 선언" 금지 계약 재확인 (qa105)

**사용자 보고**: "무슨 자꾸 종결이야 계속 나타나는데, 피격시마다 베이지색 남겨지는 건 알고 있지? 다음 수정에서 무조건 고쳐지게 해" — ①종결 선언 반복에 대한 정당한 불신 ②피격 때마다 베이지가 **남는다(지속형)**는 신규 관측.
**타임라인 확정**: 사용자 마지막 에디터 세션 13:07:50 시작(BatchMode:0 대화형, Editor.log 헤더 실측) — R3+R4+R5 컴파일 후 플레이, 13:10 `[69-DIAG] 종료 감지` 정상 종료. **①이번 세션은 팅김 없이 종료(R5 효과 1차 긍정, 재검증 대기) ②R4 방어 틴트 활성 세션에서도 베이지 잔존** — 인스턴스 머티리얼 출력=tex×_BaseColor(0.30,0.27,0.27) 수학상 베이지 불가이므로, 실제 렌더가 인스턴스 경로 밖에서 개입하고 있다는 미확정 변수가 남음.
**판정 전환**: 이론 단계(어느 경로로 베이지가 나오는가)를 더 좇지 않고 **확정성 최대 원칙**으로 전환 — 바이섹션 2회 연속 동일 근원 [Decal_ScorchMark_0/Decal_Quad](억제 시 98~99% 붕괴)을 "아예 스폰하지 않음"으로 제거. **존재하지 않는 오브젝트는 렌더되지 않음** — 렌더 경로 불문 보장. "피격시마다 남는다"는 관측과 근접 명중 지점 그을음 데칼(수명 10s, 매 명중 스폰, AnimalAI 2곳 호출)의 성질 정확히 일치.
**R6 구현**: DecalSpawner에 `public const bool ScorchMarkSpawnEnabled = false` 킬스위치 + SpawnDecal 선두 게이트(GameObject 생성/레이캐스트 전 차단, 부작용 0) + 차단 로그 3회 한정. 혈흔/독/발자국은 바이섹션 미지목 — 유지. 재개 조건 = [69-CULPRIT] 덤프로 근원 렌더 경로가 숫자로 확정된 후.
**계약**: 사용자 세션 확인 전 "해결/종결" 표현 금지 — 잔존 시 F9 1회 → [69-BEIGE-UI]+[69-BISECT]★+[69-CULPRIT] 숫자 보고로 근원 재특정.
**텔레그램 연동**: 기존 게이트웨이 정상 확인 — 배달 테스트 성공(msg 28514, 영민 조 DM). 라운드 보고 동일 채널 전송.
**QA(qa105)**: 컴파일 error CS **0** · EditMode **2510/2500/10** — baseline 실패 10건 1:1 동일, 신규 회귀 0, 69 계약 6/6 GREEN(Decal_69_R6_ScorchKillSwitch 신설 통과).
**커밋**: 별도 승인 대기(69-P1~R6 누적).

---##

## 11-17 14:0x — 무번호 69-R5 "자꾸 팅김" 원인 규명+오염원 제거 — 프로브 세션 상주 삭제·종료-안전 게이트·중량 진단 F9 기본 OFF (qa104)

**사용자 보고**: "안 없어졌고 게임 실행시 자꾸 팅겨서 꺼져. 녹화 못 했다. 자꾸 추측하고 해결됐다고 말만 하는 거 거짓말 아니냐" — 정당한 지적. **오늘 5번째 수리 시도(69-P1·P2·R1·R3·R4) 동안 배치 검증으로 사용자 세션 검증을 대신했고, 미검증 "해결" 선언이 반복됐다.** 이 라운드부터 사용자 세션 증거 없이는 해결 선언 금지.
**팅김 판정(증거)**: Windows Application Error 이벤트 — 오늘자 Unity.exe 크래시 0건(최신 11-07). Editor.log 12:51 종료 = 정상 셔다운 시퀀스. → 하드 크래시가 아니라 **ExitPlayMode 무한 대기**(영상 15 t=2.3 "Application.ExitPlayMode — Waiting for Unity's code to finish executing" 다이얼로그 실측) + 사용자 강제 종료.
**원인 2건 확정·제거**:
- ①**Fx69BlobProbe 세션 상주 오염(치명)** — [InitializeOnLoad] 프로브가 사용자 에디터에서도 구독 유지 → 플레이 중 프레임 320에서 **제멋대로 임팩트+아크+텔레그래프를 카메라 정면에 스폰**(사용자 세션 [69-P1] 로그 오염 설명 — 임팩트 수가 조작량과 불일치했던 것) + 종료 전환 중 EditorApplication.update 경유 SubmitRenderRequest → ExitPlayMode Hang 직접 후보. **파일 삭제 완료**(진단 역할은 런타임 바이섹션이 승계).
- ②**센티널 중량 진단 상시 가동** — 게임뷰 캡처(0.5s)+Update 내 렌더 요청이 종료 중 붕괴 파이프라인에 진입하는 경로 → **종료-안전 게이트**(OnApplicationQuit/OnDisable 시 _shuttingDown → 전면 중단, [69-DIAG] 로그) + **중량 진단(게임뷰 캡처+바이섹션) F9 토글 기본 OFF** — 평시 Play는 경량 티어(월드 RT 스캔)만 가동.
**유지**: R4 스코치 방어 틴트(샘플 이상 시에도 어두운 출력 강제)·R3 신선 텍스처·베이지 티어·[69-CULPRIT] 덤프.
**QA(qa104)**: 컴파일 error CS **0** · EditMode **2509/2499/10** — baseline 1:1, 신규 회귀 0, 69 계약 5/5 GREEN.
**다음 절차(사용자 부담 최소)**: 에디터 재포커스(리컴파일) → Play → 베이지 노출 시 F9 1회(진단 무장) → 계속 플레이 → 종료. 로그는 제가 직독 — [69-CULPRIT]/[69-BISECT] 숫자로 원인 확정 후에만 종결 선언. 커밋 별도 승인 대기.

---##

## 11-17 13:4x — 무번호 69-R4 재발 억제(방어 틴트)+범인 라이브 상태 덤프 — RT 미관측·게임뷰만 노출 실측 (qa103)

**사용자 보고**: R3 이후에도 베이지 잔존. 세션 로그 직독 — `[69-BEIGE-UI] 9819px 중심(0.49,0.51)` → `[69-BISECT] #0 억제 [Decal_ScorchMark_0/Decal_Quad] → 40px ★` — **R3 신선 텍스처로도 동일 데칼이 범인**(재현 2회 연속 동일 근원 = 신뢰성 확보). 신선 생성+바인딩으로도 샘플이 깨진다 = 텍스처 수명/바인딩 문제가 아님.
**핵심 실측 — 렌더 경로 분리**: 카메라 RT 스캔(R1 베이지 티어, 1.5s 스윕×데칼 수명 10s)에서 베이지 0건 vs 게임뷰(UI 포함) 캡처 9,819px — **박스는 게임뷰에만 렌더되고 카메라 RT(SubmtRenderRequest)에는 존재하지 않음** = 세션 한정 렌더 경로 이탈(카메라 RT에 못 보이는 월드 쿼드 — 제7층 변형).
**수리 [69-R4] 이중 방어**:
- ①**스코치 방어 틴트** — ScorchMarkMat _BaseColor를 어두운 톤(0.30,0.27,0.27)으로 강제. URP Particles/Unlit 출력 = texRGB×_BaseColor — 샘플이 세션 이상으로 밝은 값(249,201,173)을 반환해도 출력 = garbage×0.3 = **어두운 그을음 톤(수학적으로 베이지 불가)**. 텍스처 자체가 암색이므로 정상 세션 배색 불변(농도만 미세 가중).
- ②**[69-CULPRIT] 라이브 덤프** — 바이섹션 ★ 판정 순간 범인의 머티리얼 전체 상태(셰이더명/큐/_BaseColor/_Surface/_Blend/키워드/_BaseMap 이름·해상도·가독성·픽셀 평균 RGBA)를 로그로 확정 + 스캔RT/UI 증거 페어 저장 — 다음 Play에서 원인 확정.
**QA(qa103)**: 컴파일 error CS **0** · EditMode **2509/2499/10** — baseline 1:1, 신규 회귀 0, 69 계약 5/5 GREEN.
**다음**: Play 판정 — ①베이지 소멸(방어 틴트 유효) ②잔존 시 [69-CULPRIT] 덤프로 원인 확정 후 최종 수리. 커밋 별도 승인 대기.

---##

## 11-17 13:2x — 무번호 69-R3 최종 종결 — ★범인 확정: Decal_ScorchMark 쿼드(런타임 바이섹션 6,502→34px) — 스코치 텍스처 샘플러 별칭 → 데칼별 신선 텍스처 생성 수리 (qa102)

**사용자 Play(12:2x) 후 로그 직독 — 런타임 바이섹션이 범인을 이름으로 뱉었다**:
- `[69-BEIGE-UI] 🟤 게임뷰(UI 포함) 베이지 클러스터 — 6502px 중심(0.48,0.50)` → `[69-BISECT] #0 억제 [Decal_ScorchMark_0/Decal_Quad] → beige-ui px=34 (baseline 6502) ← ★결함 근원` — **98% 붕괴, 단일 근원 확정**.
**메커니즘(색 포렌식)**: 베이지 박스 (249,201,173) 균일 솔리드 = **슬래시 아크 웜화이트(255,244,214)를 피격 적색 비네트(α≈0.22)로 틴트한 값**(249,197,172 ≈ 관측치) — 스코치 데칼의 _BaseMap 샘플러가 **직전 업로드 텍스처(아크 아틀라스)로 별칭**(공유 런타임 텍스처 업로드/바인딩 세션 취약성 — 임팩트마다 아크가 방금 업로드되고 데칼이 직후 스폰되는 순서 정합). 스코치 본래 텍스처는 (15,13,13) 암색 — 베이지는 이론상 불가능하므로 샘플러 별칭만이 관측을 설명.
**수리 [69-R3] 데칼별 신선 텍스처(구조적 제거)**: DecalTypeConfig에 `textureGenerator` 델리게이트 신설(4종 배선 — 결정론 유지) → SpawnDecal이 **스포너 시점 신선 텍스처 생성+Apply+인스턴스 슬롯 재바인딩**(`_BaseMap`/mainTexture 이중) → DecalInstance.instanceTexture 보관 → 수명 말 인스턴스 머티리얼과 **동반 파괴(누출 금지)**. 생성 실패 시 공유 텍스처 폴백(스폰 중단 금지). 어떤 세션에서도 "방금 업로드된 자기 텍스처"만 샘플링 — 별칭 창 원천 소멸.
**QA(qa102)**: 컴파일 error CS **0** · EditMode 전수 **2509/2499/10** — baseline 1:1, 신규 회귀 0. 추이 2508→**2509(+1)**, HitDefectClosure_69Tests 5/5 GREEN.
**잔여 관측점**: [69-BEIGE]/[69-BEIGE-UI]/[69-BISECT]는 상시 활성 — 재발 시 즉시 근원 이름 보고. 커밋 별도 승인 대기.

---##

## 11-17 13:0x — 무번호 69-R2 영상 15 포렌식+UI 레이어 커버 — 배치 전 경로 클린 재확인, 게임뷰(UI 포함) 스캔+바이섹션 배선 완료 (qa101)

**영상 15 판독(59프레임 전수 스캔)**: ①베이지 박스(248-252,197-203,166-172 솔리드 ~120×88 @ x312-432,y220-308) **t=0~2.3 플레이 중 존재 확인** ②t=2.3 이후의 흰 박스 3,771px = **에디터 "Application.ExitPlayMode" 진행창**(사용자 stop — 게임 결함 아님) ③t=14.7 화면 전환. 베이지 박스는 실제 플레이 중 렌더 확정.
**배치 재검증 [69-CTRL v2]**: 스폰을 **카메라 정면 5m(viewport 0.50,0.55 — 화면 밖 가능성 제거)**로 고정, MagicHit+SlashArc+Telegraph+Shockwave 링 전체 발화, 7캡처 — **전부 clusters=0**. 배치 환경에서는 재현 불가 확정 = 세션/UI 레이어 의존.
**수리 [69-R2] UI 포함 게임뷰 스캔(MagentaSentinel)**: `ScreenCapture.CaptureScreenshotIntoRenderTexture` 2단계 상태 기계(요청→다음 틱 판독, 0.5s 케이던스, 실패 3회 자가 차단) — **카메라 RT에 안 잡히는 스크린 레이어(UTK/IMGUI 오버레이) 결함 커버**. `[69-BEIGE-UI] 🟤` 1급 보고 + PNG 증거 자동 저장(`69_uibeige_*.png`) + beige-ui 바이섹션(억제 상태 UI 캡처 측정 — 월드 렌더러가 게임뷰에서만 깨지는 경우도 이름으로 특정).
**QA(qa101)**: 컴파일 error CS **0** · EditMode **2508/2498/10** — baseline 1:1, 신규 회귀 0, 69 계약 4/4 GREEN(R2 핀 포함).
**다음**: Play 1회 — 박스 노출 시 콘솔에 `[69-BEIGE]`/`[69-BEIGE-UI]`/`[69-BISECT] ... ★결함 근원 [이름]` 자동 보고 → 그 로그로 최종 수리. 사용자 보고 최소화(로그 직접 판독).

---##

## 11-17 12:2x — 무번호 69-R1 사용자 세션 잔존(베이지 박스 8,945px 실측) → 센서 확장+런타임 바이섹션 — 다음 Play가 근원을 이름으로 보고 (qa100)

**사용자 보고**: "여전히 흰 검 베이지 사각 남아있어" — 69-P1/P2 수리 후 Play. 세션 증거(Editor.log 12:05~, [69-P1] 10회 발화 확인 = 수리 코드 활성) + 캡처 44_magenta_120908 픽셀 실측: **베이지(250,201,176) 솔리드 박스 8,945px(~120×88 @ x384-504,y176-264)** — 기존 4티어 전부 미달(흰 b≥246·준백 전채널≥232·검≤14 어디에도 없음) = **스캐너 사각지대**였던 것. 순백/순흑/마젠타는 0~28px로 소멸 확인(69-P1/P2 유효 — 블랙/마젠타 계열은 잡음 수준).
**정직 기록**: 배치 통제 실험(69-CTRL) 4캡처 clusters=0은 **스폰이 화면 밖이었을 가능성 배제 못 함**(플레이어 위치 의존) + 세션 의존 가능성(66차 선례) — "배치 클린≠사용자 세션 클린" 재확인. 66차 계약대로 프로브 확장이 먼저.
**수리 [69-R1] 센서 확장(MagentaSentinel)**:
- ①**베이지 티어 신설** — 판별식 r≥232·g∈[178,222]·b∈[148,205]·r>g>b(크림 갑옷 249,236,160은 g=236로 배제). 임계 300(스캔 스케일 환산 실측 ~1,800 대비). 1급 로그 `[69-BEIGE] 🟤`.
- ②**런타임 바이섹션** — 티어 검출(검/베이지/흰) 시 클러스터 중심 레이의 FX 후보(PSR/쿼드, 최대 12, 카메라 부착 의도 오버레이 제외)를 **사용자 세션 안에서** 1개씩 억제→다음 프레임 스캔 RT 재측정→복원. 판정: ≤15%=★결함 근원, <80%=기여. 프레임당 1스텝(센티널 Update), 10s 쿨다운. 로그 `[69-BISECT] #N 억제 [경로] → px=N ★`.
**QA(qa100)**: 컴파일 error CS **0** · EditMode 전수 **2508/2498/10** — baseline(qa99) 1:1, 신규 회귀 0. 추이 2507→**2508(+1)**, HitDefectClosure_69Tests 4/4 GREEN.
**다음**: **Play 1회(녹화 권장)** — 흰/검/베이지 사각이 보이는 순간 센티널이 자동으로 [69-BEIGE]/[69-BISECT] 근원 이름을 보고 → 그 로그 1줄로 최종 수리. 커밋 별도 승인 대기.

---##

## 11-17 02:1x — 무번호 69 피격 흰/검 사각 최종 종결 — URP 파티클 버텍스 스트림 정합(구조적 제거) — 배치 플레이모드 프로브+바이섹션+통제 실험 (qa99)

**사용자 지시**: "직전 영상과 동일하게 흰색 검은색 사각 여전히 남아있어. 더 확실하게 수정해봐 마지막 기회라고 생각하고" — 68차 이후 세션(Editor.log 01:25~01:32) 캡처 5장 픽셀 실측에서 임계 미달 암적갈(49,10,9) 솔리드 사각 쿼드 발견(기존 센서 임계가 못 본 영역) → 69차 착수.
**진단 장비 신설 [Fx69BlobProbe]**: 배치 플레이모드 프로브 — ①도메인 리로드 구독 소실→[InitializeOnLoad] 재구독 ②Camera.main 부재→RT 미바인딩 카메라 폴백 ③**배치 플레이는 빈 씬 부팅 실측→Test_10 명시 개봉**. RT(960×540 depth24) 렌더→순흑/암적/베이지 3티어 클러스터 스캔→중심 ViewportPointToRay 레이 intersects 렌더러 전수 덤프(이름/머티리얼/셰이더/큐/텍스처)→PS 심층(시작색/스트림/시트애니)→**한 방식 억제 바이섹션**(후보 렌더러 1개씩 off→결함px 붕괴 개체 특정)→**통제 실험**(MagicHit+SlashArc 직접 스폰+고정지연 4캡처).
**뿌리 2건 확정(구조적)**:
- [69-P1] **MagicHit Shockwave/Flash 커스텀 버텍스 스트림 어긋남** — 에셋이 빌트인 Standard Unlit용 스트림([Position,UV,Color] — Normal 없음, MagicHit.prefab YAML 14675/19579 실측)인데 56-P1이 셰이더만 URP Particles/Unlit으로 교체 → TEXCOORD0 미바인딩 → **솔리드 블랙 사각**. 바이섹션: Shockwave 억제 시 결함 10,653→67px. "피격마다 타격 지점에 생겨 캐릭터를 따라다니는" 보고와 정확히 일치(테스트 14의 0.4~0.75s 흑 버스트).
- [69-P2] **SlashArcFlash 플립북 AnimFrame 미바인딩** — 시트애니 활성(4타일) 파티클에 AnimFrame 스트림 부재 → 가비지 타일 샘플링 = 블랙/베이지 박스. 바이섹션: 1,344→61px. 테스트 13/14의 베이지·암흑 계단식 사각 잔여분.
- 암적갈(49,10,9) = 위 블랙 사각 위 피격 적색 비네트(ScreenVignettePulse — 의도된 23-V1) 중첩 → 별도 결함 아님.
**수리(구조적 제거)**: `FxMaterialLibrary.AlignParticleVertexStreams(pr)` 신설 공용 헬퍼 — URP 표준 4스트림(Position→Normal→Color→UV) 재정합 + 시트애니 활성 시 **AnimFrame** 추가. 멱등·인스턴스 전용(에셋 무변경 계약). 배선 3경로: ①NormalizeMagicHitMaterials(P1 — 재정합 건수 1급 로그) ②FxSlashFlipbookFlash.Play(P2) ③DeathVFX.SpawnFlipbookSmoke(P2 — 사망 연기 동일 결함군 예방). API 시그니처(List 기반 Get/Set)는 컴파일 실측으로 확정, ParticleSystemVertexStream 멤버는 dnfile 어셈블리 직독.
**검증**: 컴파일 error CS **0** · **통제 실험 4캡처(+2/+8/+20/+45틱) 전부 clusters=0** — 수리 전 동일 경로 10,653px 블랙 대비 완전 소멸 · [69-P1] 재정합 2건 발화(Shockwave+Flash — 예상 정확 일치).
**QA(qa99)**: UNITY_EXIT=0 · EditMode 전수 **2507/2497/10** — baseline(qa98 2504/2494/10) 실패집합 10건 1:1, 신규 회귀 **0**. 추이 2504→**2507(+3 = 신규 69 계약 3건)**, HitDefectClosure_69Tests 3/3 GREEN.
**다음**: Play 판정(녹화 권장) — 피격 시 흰/검/베이지 사각 소멸+링/플래시/스파크 정상 렌더 확인. 잔존 시 Fx69BlobProbe 즉시 재현 가능(콘솔 [69-PROBE] 실행 or 본 컴파일 유지). 커밋 별도 승인 대기.

---##

## 11-17 01:25 — 무번호 68 링 패밀리 발광 포화 캡 — "캐릭터·병사 따라다니는 흰/검 사각" 뿌리 수리 (qa98)

**사용자 답변**: "검정 사각이 캐릭터와 병사를 따라다니고 피격 시 생기는 것 같다" → 임팩트 링 계열로 특정.
**뿌리 확정(코드 교차)**: SpawnImpactRingCore(피격 지점 링 — 159-P4 몬스터 타격 임팩트) = `ApplyImpactEmissionBoost(×2.0 HDR emissive)` + FXBandingRing 셰이더 `col.rgb += _EmissionColor.rgb` **saturate 없이 HDR 합산** + 링색 α≥0.75·화이트 러프 → 프레임버퍼 클램프로 **순백/암색 판 포화**. 피격마다 타격 지점(플레이어 흉부/병사)에 스폰되어 따라다니는 것 = 테스트 12~14의 흰 원형·암색 사각 정합. 26-DF2의 "rgb 클램프 금지" 계약이 이 조합에서 결함 요인이었음.
**수리 [68-P1]**: FXBandingRing 셰이더 `col.rgb = saturate(col.rgb + _EmissionColor.rgb);` — 발광 연출 유지, 최종 rgb LDR 고정. 링 패밀리 전체(임팩트/수렴/스윙/패링 팬) 일괄 적용. TeleRing 계약은 별도.
**계약 진화**: CombatFxDepthFadePhaseDFTests DF2("rgb 클램프 금지"→"가산+LDR 캡", 진화 사유 동봉). 신규 `RingEmissionCap_68Tests` 2건(셰이더 캡+배선 유지 잠금).
**QA(qa98)**: 컴파일 error CS **0** · UNITY_EXIT=0 · EditMode **2504/2494/10** — baseline(qa97) 1:1, 신규 회귀 **0**. 추이 2502→**2504**. 신규 2/2 GREEN. (중간 1회 WSL accept4 런치 실패 — 재런으로 유효 확정)
**다음**: Play 판정(녹화 권장) — 잔존 시 [64] 캡처 페어로 근원 특정. 커밋 별도 승인 대기.

---##

## 11-17 01:05 — 무번호 67 테스트 14 잔존 분석 — 검정=전화면 산포(10~15Kpx), 흰 원형 지속 — 뿌리 미확정(정직 기록) (qa97)

**증거(테스트 14 영상 114프레임 스캔)**: 검정 버스트 10,000~15,000px(전화면 산포, t=5~35 다발) + 흰 원형 6,240px(t=7.7~9.0, 테스트 13과 동일 위치) 지속.
**비전 판독(흰 피크 프레임)**: 흰 원 = 지면 FX(선택 링/피격 계열), 그 뒤 검/흰 사각 박스. 검정 산포 = "그림자 연산 오류(투명해야 할 영역이 검게)".
**시도와 정정**: BlobShadow 키워드 누락 가설 → 코드 확인 결과 54차 키워드 **존재**(가설 기각 — 정직 기록). BuildMaterial에 EnsureUnlitTransparent 중앙화만 적용(동작 보존).
**미해결 잔여**: ①검정 산포가 α0.25 블롭(≤14 도달 불가)과 불일치 → 씬 실시간 그림자/태양각(밤)일 가능성 — 사용자 확인 필요("검정 사각이 캐릭터를 따라다니는가") ②흰 원형 — 66차 HDR 캡으로도 소멸 안 됨 → HDR 경로 아님, 잔여 후보 SelectionRing/impact — [64] 캡처 005726/005736(흰 100~112px)만 확보, 피크 놓침.
**QA(qa97)**: 컴파일 0 · **2502/2492/10** baseline 1:1 — BlobShadow 중앙화 검증 통과.
**다음**: 사용자 질의(검정 사각 = 캐릭터 추종 여부) + 흰 피크 시점 캡처 확보 후 근거 수리. 커밋 대기.

---##

## 11-17 00:55 — 무번호 66 세션 의존 불투명 파티클 종결 — CreateSoftParticle 소프트 파티클 키워드 폐기 + MagicHit 상속색 LDR 캡 — 테스트 13 전수 픽셀 실측 (qa96)

**사용자 지시**: "테스트 13 영상 — 아직 흰/검 사각 남아있잖아. 다시는 수정할 일 없도록 원인 제대로 분석하고 완벽히 수정".
**증거(테스트 13 영상 217프레임 전수 픽셀 스캔 + 8장 긴급 캡처 + 로그)**:
- 흰 사각: t=17.3~25.3 **~8초 정적 지속** 6,520→9,488px(순백 원형 글로우+글리프, 몬스터 발치) — 단명 FX 아님.
- 검/베이지: t=14.7 검 15,356px(수직 암흑 사각+**베이지 계단식 겹친 사각들**), t=28~30 베이지 5,568px, t=35/55/57 검 버스트.
- 캡처 003657(데미지1 히트 동기)에서 베이지 2,418px 계단식 겹침 사각 판독 = "텍스처 없는 기본 쿼드 겹침".
**뿌리 확정(색 계산 교차)**: 베이지 계단식 = (255,244,214) — **SlashArcFlash 플립북의 웜화이트색 그 자체**. 알파 상실 시 그대로 불투명 사각. 프로브(13:05)에 베이지 대형 영역 0건 → **배치(정상) vs 에디터 Play(불투명) = 세션 의존 렌더**. 원인 = CreateSoftParticle의 소프트 파티클 키워드 — 깊이 근접 페이드가 커스텀 버텍스 스트림+카메라 깊이 텍스처에 의존, 에디터 세션의 깊이 상태 차이로 알파 강제 1(불투명 사각). 흰 8초 서클 = MagicHit 상속색 HDR(에셋 원본 2.6×, 41-b b=1.2) 프레임버퍼 포화. 암흑 사각 = 동일 메커니즘의 어두운 플립북(연기 아틀라스) 불투명 렌더.
**수리 [66-P1]**: CreateSoftParticle에서 소프트 파티클 키워드+카메라 페이드 파라미터 **제거** — 알파 블렌드만으로 결정론 렌더 고정(세션 무관). 전 플립북/소프트 파티클 패밀리(아크·연기·불티·에임 등) 일괄 적용.
**수리 [66-P2]**: NormalizeMagicHitMaterials 상속색 **LDR 캡**(Clamp01) + 41-b 틴트 b=1.2→1.0 — 순백 원형판 차단(54차 알파 캡·65-P2 saturate와 동일 구조 계약).
**계약 진화**: FxShaderAssetsDTests(D2 소프트 파티클 핀→폐기), FxMarkerRefineP40Tests(40-b 틴트 핀→LDR). 신규 `HitDefectClosure_66Tests` 2건.
**QA(qa96)**: 컴파일 error CS **0** · UNITY_EXIT=0 · EditMode **2502/2492/10** — baseline(qa95) 1:1, 신규 회귀 **0**. 추이 2500→**2502**. 신규 2/2 GREEN.
**다음**: Play 판정(녹화 권장) — 잔존 시 [64] 긴급 캡처 페어가 근원 특정. 커밋 별도 승인 대기.

---##

## 11-17 00:35 — 무번호 65 피격 흰/검 사각 종결 — CommandMarker 데칼 경로 폐기(검은 사각 뿌리) + SelectionRing 포화 캡(흰 링) — 테스트 12 영상·로그 픽셀 교차 실측 (qa95)

**사용자 지시**: "공격씬 테스트 12 영상+로그 비교 — 흰 사각·검정 사각 완전히 없어지도록 원인 제대로 분석해서 완벽 수정".
**증거(영상 87프레임 전수 픽셀 스캔 + 7장 긴급 캡처 + 로그)**:
- **흰 사각**: t=6.5~7.5s, 5,923→7,082px(#f8f8f8 순백), 확산 링(중심 투명·소프트), 몬스터 발치, 히트 동기.
- **검정 사각**: t=21.5/32/34/37.5s 등 8회 버스트, 5,000~7,300px(#080000 암적색·하드엣지·불투명 박스, 캐릭터 뒤 지면), ~0.58s 지속 반복 = 전투 중 명령 주기와 정합.
- **센서 미보고 확인**: [51-SCREEN] 0건 — ①검정 클러스터가 320×180 스캔에서 90px(<400 임계) ②데칼 패스는 스캔 RT 수동 렌더에서 스킵(§13) — 센서 사각 재확증. [64] 긴급 캡처 7장은 정상 작동(클러스터 임계 미달로 보고만 없었음).
**뿌리 확정 2건**:
- **검정 사각 = CommandMarker 지속형 데칼** — 58-P2가 증명한 파손 경로("매뉴얼 카피 데칼 그래프" 신규 머티리얼 = color(0,0,0,0)+텍스처 대입 무시 → DecalProjector 박스 전체가 불투명 흑 박스)를 **CommandMarker가 여전히 사용** = 58-P2의 적용 누락 지점. 이동/집결 명령(지속형)마다 명령 지점에 검은 박스. 캡처 000839 검 클러스터가 명령·공격 착지 시점과 정합.
- **흰 링 = SelectionRing 강조색 HDR 포화** — 명령 마커 확산 웨이브 링이 _Intensity×highlight×rim HDR 배수로 프레임버퍼 클램프 → #f8f8f8 순백(54차 포화 교훈의 셰이더판). 프로브(13:05)에 흰 링 0건 → 임팩트 계약 배제, 명령 마커 계열로 특정.
**수리 [65-P1]**: CommandMarker.TrySetupDecal **완전 삭제** — 데칼 경로 폐기, 항상 쿼드 경로(SetupPersistent=CommandRingPresentation.Apply / SetupFading=SelectionRing). _proj/_decalMat/fadeFactor 경로 전부 제거. 폐기 근거 주석 보존.
**수리 [65-P2]**: SelectionRing.shader accentCol `saturate()` 캡 — 팀색 위계 유지·순백 포화 구조적 차단(54차 알파 캡 계열).
**계약 진화**: CombatFxMoveRingPhaseFTests F3 3건(데칼 부착/폴백체인/fadeFactor)·FxMarkerRefineP40Tests 1건·FxDecalPathNormalizeP52Tests 1건 — 진화 사유 주석 동봉 갱신. 신규 `HitDefectClosure_65Tests` 2건.
**QA(qa95)**: 컴파일 error CS **0** · UNITY_EXIT=0 · EditMode 전수 **2500/2490/10** — baseline(qa94) 10건 1:1, 신규 회귀 **0**. 추이 2499→**2500(+1 순증 — 진화 5건 삭제+신규/진화 6건 편입)**. 신규 2/2 GREEN.
**정직 기록**: 흰 링의 근원 특정은 코드 구조+프로브 배제법(간접) — 다음 Play에서 [64] 긴급 캡처가 명령 시점 화면을 페어 저장하므로 잔존 시 즉시 특정 가능. 검정 사각은 데칼 경로 물리적 삭제로 재발 불가.
**대기**: ①**Play(사용자, 녹화 권장)** — 흰/검 사각 완전 소멸 판정 ②커밋 승인(누적 전체).

---##

## 11-16 23:59 — 무번호 64 센서 진실성 수리(공백RT 가드·RenderRequest 풀파이프라인·히트동기 긴급스캔) + 팬 밴드 텍스처 직접바인딩 — "흰 사각" 재보고 정밀 진단 (qa94)

**사용자 지시**: "여전히 몬스터 피격시 흰 사각 — 쉐이더로 안 다듬어진건지 렌더가 안된건지 원인 정확히 파악하고 수정".
**진단(픽셀·로그·어셈블리 3중 실측)**:
- ①동일 순간 교차 실측 — `58_scan_114414.png`(스캔 RT 320×180) = **57,597px ≥247 균일(99.98%)** vs 같은 초 `44_magenta_114414.png`(실화면 캡처) = **순백 194px뿐** → 수동 `cam.Render()` 재렌더가 **공백 프레임을 생산**. 58-P2의 "화면 99.98% 순백 + ScorchMark 지목"은 **센서 오판**(클러스터 중심 무고 렌더러 지목). 11:05의 28.5% 화이트도 과노출(톤매핑 미적용 재렌더)로 정합.
- ②최종 Play(22:30대, 62차 빌드) — 센서 전멸 0건([51-SCREEN]/[53-SCREEN]/캡처 0, [56-P1]만 16건) + 콘솔 머티리얼·셰이더 에러 0 + NUL 손상 재스캔 0건(기존 데모 6건뿐) → **렌더 파손 아님**.
- ③구조적 사각 3종 — 단명 FX(수명 0.1~0.7s) vs 스캔 주기 1.5s(샘플링 사각) · 스크린스페이스 UI는 카메라 RT 원천 미관측 · 베이지(B<232) 의도적 배제.
- **판정(사용자 질문 답변)**: "렌더가 안 된 것"이 아니라 **"진단 장치가 렌더를 못 봤다"** — 실카메라 프로브(13:05)는 월드 FX 체인 클린. 잔존 근원은 센서가 못 본 축(UI/단명 FX)에 있으며, 다음 Play에서 [64] 장치가 이름으로 지목한다.
**수리 [64-P1] 센서 진실성(MagentaSentinel)**: ①`IsBlankFrame`(8px 스트라이드·8비트 버킷·98% 균일) 가드 — 균일 RT는 보고 스킵+[64-SCAN] 1회 로그(무고 지목 원천 차단) ②`RenderPipeline.SubmitRenderRequest(RenderPipeline.StandardRequest)` 우선 — 풀 파이프라인(포스트·톤매핑 포함) 렌더로 공백/과노출 동시 제거, 실패 1회 전환 후 기존 `cam.Render()` 폴백(6000.4 실측: StandardRequest=RenderPipeline 중첩 타입·필드 destination — dnfile 어셈블리 파싱 확정) ③`RegisterFxSource` → **+0.12s 히트동기 긴급 스캔**(단명 FX 피크 샘플링) + TryCaptureEvidence 페어 저장(RT=순월드 vs 캡처=UI 포함 비교 근거).
**수리 [64-P2] 셰이더 미정제 1건(ShockwaveRingFX.FanRunner)**: FXBandingRing `_MainTex` 미바인딩 = 셰이더 기본 "white" **하드엣지 불투명 스트릭**(57차 역산 "텍스처 없는 쿼드" 정합) → `GetFanBandTexture`(128×16 절차 페더 밴드 — 머리 램프업/꼬리 페이드/v방향 가우시안, 결정론 Random 0건·정적 캐시) 직접 바인딩. 몬스터 공격 스윙/패링 팬 전 경로(호출부 무수정).
**QA(qa94)**: 컴파일 error CS **0** · UNITY_EXIT=0 · EditMode 전수 **2499 total / 2489 passed / 10 failed** — baseline(63-C 2494/2484/10) 실패집합 10건 1:1 동일, 신규 회귀 **0**. 카운트 추이 2494(63-C)→**2499(+5 = 신규 64 계약 5건)**, `SensorTruthfulness_64Tests` 5/5 Passed 실측(XML). 산출: `TestOutput/qa94-editmode-results.xml`·`qa94-editmode.log`·`qa94-run.sh`.
**사고 기록**: 컴파일 시행착오 3건(StandardRequest 네임스페이스→전역→중첩→필드 destination — 매회 어셈블리 실측으로 확정), 신규 테스트 슬라이스 오탐(SparkRunner.Init 동일 시그니처 선발 → ParryDeflectFanMat 앵커 정정).
**대기**: ①**Play(사용자, 녹화 권장)** — [64] 장치가 흰 사각 근원을 이름으로 보고(RT/화면 페어+근접 FX+공백 가드) → 근거 기반 1회 수리 후 종결 ②커밋 승인(누적 전체).

---##

## [63-C] 상태이상 툴팁 2행 — 해결 방법 표시 (2026-11-16, 텔레그램)

- **사용자 승안 구현**: 아이콘 호버 시 1행(이름+남은 초)+2행(해결 방법) 표시.
- **변경 3파일**: ①StatusEffectBarUTK — CureHint(14종 매핑: 붕대/해독제/수면/식사/부목/물 접촉/진정제/마스크·모닥불) + 툴팁 2행화(\n 결합, WhiteSpace.Normal, maxWidth 240, top −40, 높이 자동) ②StatusEffectBarTests — Bar_Tooltip_TwoLines_WithCureHints 신규(2행 개행+키워드 8종 어서션) ③ConsumableSystem — 붕대 분기에 골절 제거 1줄(부목(붕대 재사용) 정합 수리).
- **검증**: 컴파일 error CS=0 + EditMode 전수 2494/2484/10 — 실패 10건 baseline 1:1(신규 회귀 0), 신규 툴팁 테스트 Passed. 위임: 구현 서브 단발 성공(238s, tasks 형식 우회 — goal 유실 4회 후).
- **대기**: Play 승인(63-B+63-C 통합 — 발동 3소스+아이콘 UI+2행 툴팁). 커밋 별도 승인.

---## [63-B] 플레이어 상태이상 14종 + 상태이상 UI (2026-11-16, 텔레그램)

- **계획서**: docs/2026-11-16_STATUS_EFFECTS_14_PLAN.md — 사용자 14종 전부 채택(기존 6+추가 8).
- **시스템(A)**: PlayerStatusEffectSystem.cs 신규(493행) — StatusEffectId 14종·AddEffect 멱등갱신·TickCore(dt 주입 순수 훅)·DOT 4종("status" 무기태그 재귀 가드)·감염 연쇄(출혈/골절 미치료 30s)·허기 적산(24h 미식·MarkMealTime)·치료 API 7종(CureSleep/Meal/Warm/Calm/Bleeding/Poisoning/Extinguish)·조회 API(BlocksDash/Roll·Heal/StaminaRegen/MaxStamina Multiplier·ComboMissChance). 전 static — EditMode 안전.
- **UI(B)**: StatusEffectBarUTK.cs 신규 — 시계(TimeClock left=14) 하단 y=96, 원형 아크=UTKGaugeStripArc(HP/스태미너 동일 계열·시계방향 소모) 44px·여백 6px 고정·중앙 절차 글리프 14종(벡터메시 금지 84차 준수)·호버 툴팁(이름+남은 초)·250ms 폴링+OnChanged. UTKWireUp Ensure 1줄.
- **훅(C)**: 8파일 증분 — PlayerHealth 피격 발동(근접/화살 25% 출혈·≥HP30% 골절·≥20% 방어취약, 리플렉션)·MonsterSkill Slowness 치환+Weaken·PlayerMovement(구르기 게이트·고객 탈진×2·회복/최대 계수·수영 소화+젖음)·GasCloudLauncher 중독+혼탁(마스크 면역)·Bomb Molotov 화상·TimeManager 저체온(밤+비+젖음·내첩 enum 한정 CS0103 수리)·ConsumableSystem(붕대/술/진정제/해독/상태이상방지/식사 — 리플렉션 브리지)·PlayerCombat 콤보 미스. 편차 3건 문서화: 낙하피해/대쉬진입 시스템 부재(스킵)·GasCloudField는 VFX 전용(판정점=GasCloudLauncher).
- **검증**: 컴파일 error CS=0(63B try2/63C try2) + EditMode 전수 2493/2483/10 — 실패 10건 baseline 1:1(신규 회귀 0), StatusEffect 신규 18건 전부 Passed. 위임 이력: A 단발 성공, B 600s 0산출→부모 직접(64차 선승인), C max_iterations 실측완료→부모 직접 적용(150차 패턴), QA 2회 무효(레거시 플래그 오탐·nohup 조기 사망)→부모 직접 런 2회로 종결.
- **대기**: Play 승인 — Test_10에서 피격 출혈/가스 중독/화염 화상 발동+시계 하단 원형 아이콘(남은시간 시계방향 소모·호버) 확인. 커밋 별도 승인.

---## [63차] SleepPanelUTK 센터링 경합 수리 — 침대 세이브 UI 하단 절단 루트원인 (2026-11-16, 텔레그램)

- **증상**: 침대에서 E키 → 수면 패널의 세이브 UI(수동 저장 슬롯 하단+"저장 후 수면 시작" 버튼) 미표기 — 84차 동일 보고(2026-10-16) 재발.
- **루트원인(감독자 발견)**: OnWindowOpen의 `schedule.Execute(CenterOnParent)(16/120ms)`이 Open()의 ApplyCanvasFit([84차] 등배수 k 스케일링)을 16ms 뒤 raw 624×1101 기준으로 덮어씀 → k≠1 화면에서 하단 최대 ~197px 절단(84차 수리 자가 무효화 경합).
- **수리(구현 서브 위임, 단발)**: ①OnWindowOpen 스케줄 → ApplyCanvasFit 재시도(16/120/400ms, [88차 B1] 선례) ②CenterOnParent 삭제 ③ComputeFitBox(rootW,rootH,out k,left,top) 순수 헬퍼 추출+위임. 스펙 정정 수용: 하단 절단 ≤21px×k 한계(피그마 자체 블리드 — 패널 1101 > 캔버스 1080).
- **계약테스트**: SleepPanelFitTests 신규 6건(ComputeFitBox 4해상도+가드, 소스 어서션 — CenterOnParent 부재 핀).
- **검증**: 컴파일 error CS=0(/mnt/c/Unity/compile_log_63_sleepfit_try2.txt) + EditMode 전수 2475/2465/10 — 실패 10=baseline 1:1(Aero/CraftBench/IndoorQuality/Instrument×3/InvadedLord×2/RecipeCatalog/ThemeUss), 신규 실패 0. SleepPanelFitTests 6/6, SleepPanelFigmaTests 무손상. QA 서브 600s 타임아웃 → 고아 런 완주, XML 판독으로 종결(Temp/unity-editmode-results.xml 18:48, /mnt/c/Unity/qa_63_sleepfit.log).
- **대기**: Play 검증 승인 — Test_10→성 입구 E→실내 침대 E 프로브(패널 개방+세이브 섹션 화면 내 가시 확인). 커밋 별도 승인.

---## 11-16 — 무번호 62 패링 실패/빈 최소 SFX + TEST20 콤보 틴트 흰색 통일 착지 확인 — 미완료 잔여 소화 (qa93)
**사용자 지시**: "미완료 먼저 실행" — 잔여 3건 중 ①패링 실패/빈 SFX(36-PW2 의사결정 갱신 승인) ②TEST20 콤보 틴트 ③Play 체감 트림(Play 게이트 — 불가, 대기 유지).
**TEST20 콤보 틴트**: 이미 착지 확인(디스크 실측) — SlashVFXRunner `ComboStageTint` `=> CoreTint // 항상 흰색`, 폴백 경로 stageTint 무시 흰 고정, Free Slash 우선 전환(60차). 계약 핀 충돌 0(CombatFxFollowupINTests는 날씨 틴트만 핀). 구현 불필요.
**패링 실패 SFX(무번호 62)**: ①SoundEffectManager — `SFXType.Parry_Fail` 멤버 + GetSFXName 매핑(`SFX_Parry_Fail`, Resources/Audio/SFX 규약) ②PlayerCombat EndParryWindow — 최소 실패 SFX 발화(`PlaySFXAtPoint(Parry_Fail, transform.position)`, Application.isPlaying 가드, 성공 경로 배선 선례 1:1). 클립 부재 시 무음 폴백(133차-P2). 히트스톱/시각 FX 무추가(36-PW2 최소 신호 유지 — 36-PW2 주석 갱신).
**QA(EditMode 전수 배치 런, qa93)**: 컴파일 error CS 0 · UNITY_EXIT=0 · **2469 total / 2459 passed / 10 failed** — baseline(qa92 2468/2458/10) 대조 **10건 1:1 동일**(IsolatedSession/CookingDatabase/H2_Catalog/Performance×3/DamageIsIgnored/Initialize_Starts/FindRecipe/ThemeUss), 신규 회귀 **0**. 카운트 추이 2468(61차)→**2469(+1 = 신규 62 계약테스트)**, `ParryFailSfx_62Tests` 1/1 Passed·`ParryGauge_61Tests` 5/5·`ParryWindowVFXPhasePWTests`(36-PW2 핀) 전부 Passed 실측(XML 확인).
**대기**: ①Play 체감 트림(실패 SFX 볼륨/톤 — Play 승인 후) ②Play(사용자): 패망 실패 음 확인 ③커밋 승인(59~62 누적).

## 11-16 — 무번호 59 QA — 지면 데칼 스냅(SpawnDecal 하향 레이캐스트 Y 스냅) 독립 QA — EditMode 전수 배치 런 + baseline 대조 (qa89)
**대상**: DecalSpawner.SpawnDecal `[무번호 59]` 지면 스냅 — 하향 레이캐스트(5m, QueryTriggerInteraction.Ignore)로 Y만 스냅(XZ는 호출부 값 보존) + 기존 +0.02 리프트 계약 무손상. 신규 계약테스트 `DecalGroundSnap_59Tests.cs` 3건(레이캐스트 존재/XZ 보존/리프트 유지 — 전부 긍정 잠금).
**QA(EditMode 전수 배치 런, qa89)**: 컴파일 `error CS` **0** · UNITY_EXIT=0 · **2460 total / 2450 passed / 10 failed** — baseline(qa88 2457/2447/10) 대조 **10건 1:1 동일**, 신규 회귀 **0**. 카운트 추이 2457(58-P2)→**2460(+3 = 신규 59 계약테스트)**, 신규 3건 전부 `Passed` 실측(XML 확인).
**판정**: **통과** — 무번호 59 스냅 수리는 baseline 대조 판정 기준 Green. 산출: `TestOutput/qa89-editmode-results.xml`·`qa89-editmode.log`·`qa89-run.sh`.
**참고**: 59는 에디터 EditMode 계약(소스 문자열 단정) 라운드만 — 실제 지면 관측(파묻힘 해소 시각 확인)은 58-P2 후속 Play 캡처(스코치 3.0s 컷)에서 별도 판정.

---

## 11-16 15:10 — 무번호 61 패망 고품질(47계획 §8 P3 소화) — 구현+QA PASS (2026-11-16 15:10)
**사용자**: "미완료 항목 진행해줘" ×2. A-2 패망 고품질 — 위임 도구 불량 세션(59차 이후 재현)으로 부모 직접 구현.
**착지 4건**:
- ①창 아크 게이지: HUDUTK `ParryWindowArc`(UTKGaugeStripArc·루트 직속·PositionHost 등비 k 배치) + 50ms 고빈도 폴링(창 0.45s — 250ms로는 2틱뿐). PlayerCombat 상태 노출 3접근자(ParryWindowActive/ParryWindowRemaining01/ParryInPerfectPhase).
- ②PERFECT→GOOD 색 틱: 게이지 골드(FXPalette.ParryGold)→청색(FXPalette.ParryCyan) — 림 36-PW3와 동일 위계, 팔레트 단일 소스 계약.
- ③37-H2 소화: TriggerParryCameraPunch(impactDir) — Cinemachine impulse를 타격 방향(kbDir)으로 보냄. 구 fixed Vector3.forward 폐기, 월드→카메라 로컬 역변환+수평만, 실측 실패 시 forward 폴백.
- ④실패/빈 패링 림 자연 페이드: EndParryWindow → ParryRimFadeRoutine(0.25s unscaled 강도 수렴 후 Release). 재패링 시 구 림 즉시 해제(이중 림 방지), EditMode 동기 해제 폴백.
**기존 착지 확인(중복 제거)**: FOV 펀치+셰이크(95차 F3)·방향성 스파크(95차 F4)·SFX 위계(Parry_Perfect/Good)·림 틱(36-PW3) — 이미 착지, 무번호 61에서 미수정.
**QA**: 컴파일 error CS 0 · EditMode **2468 total / 2458 passed / 10 failed**(qa92 — baseline 10건 1:1, 신규 회귀 0) · 신규 ParryGauge_61 5/5 Passed. 카운트 추이: 2463(60차)→**2468(61차)**.
**미완료(명시)**: P3 잔여 — 소리 위계 "실패/빈 패링" SFX(36-PW2 계약상 최소 신호 유지 — 의사결정 보류), Play 체감 트림(게이지 위치/크기·킥 강도 — Play 승인 후).
**Play 대기(사용자)**: 패망 게이지 관측·킥 방향성·림 페이드. 커밋 별도 승인 대기.

## 11-16 14:20 — 무번호 60 아크 고품질 잔여(폭 프로파일·속도 알파·글로우 색 보간) + 무번호 59 QA — 구현+QA 완료 (미커밋)
**사용자 지시**: "진행" — 남은 VFX 강화 계획(B 스코치 스냅→A-1 아크 잔여→A-2 패망 고품질) 순차 승인. Play·커밋은 별도 게이트.
**무번호 59(지면 데칼 스냅) — 구현(위임)+QA PASS**: SpawnDecal에 하향 레이캐스트 지면 스냅(FxGroundCrackFX L21-26 선례 1:1 — XZ 호출부 유지·Y만 스냅·미스 시 호출부 y 폴백 무회귀) + DecalGroundSnap_59Tests 3건(긍정 잠금만). QA(qa89): **2460/2450/10 = baseline(qa88) 1:1, 신규 회귀 0**, 집중 59 계약 3/3. 스코치/혈흔/발자국/독웅덩이 전 타입 공통 적용(호출부 무수정).
**무번호 60(아크 고품질 잔여) — 구현(부모 직접 — delegate_task 파라미터 전송 불량 7회 폴백)+QA PASS**: ①폭 프로파일 `WidthProfilePow=1.7`(인라인 1.2 → 가파른 테이퍼, 전두엽 유지) ②속도 비례 알파 — `_spd` 링버퍼(WriteSample이 직전 샘플 거리/시간 산출)+`RefTipSpeed=14f`+speedBoost(하한 0.55·상한 1.15) ③글로우 색 `Color.Lerp(coreTint, FXPalette.Edge, 0.35f)`(coreTint×0.9 리터럴 폐기 — 팔레트 단일 소스). TrailSeconds 튜닝은 Play 게이트로 제외(0.35f 유지).
- **A-1 QA 사고 2건(자가 수리)**: ①신규 테스트 계약 문자열이 구현 표기와 불일치(`_spd[_write] = speed;` — 로컬 변수 도입으로 정합) ②**53차 리플렉션 테스트 NRE 회귀** — 수동 버퍼 주입(EmitPass 직접 호출)에 `_spd` 부재 → 하위호환 수리: 버퍼 미할당=미측정=**중립 boost 1.0**(53 알파 핀 0.5와 정합 실측)+WriteSample 지연 할당. 재런에서 53 복구 확인.
- **검증(qa91)**: 컴파일 error CS 0 + EditMode **2463/2453/10 = baseline 10건 1:1, 신규 회귀 0** + 집중 Arc_60 3/3·DecalGroundSnap_59 3/3·AttackArcTexture_53·DiamondCap_57 GREEN.
- **대기**: ①A-2 패망 고품질(창 아크 게이지 UTKGaugeStripArc 배선·PERFECT/GOOD 틱·카메라 오프셋 37-H2 소화·금속 스파크·소리 위계·실패 림 페이드 — 미착수, 다음 라운드) ②Play(사용자): 51~60 체인 통합 판정(아크 테이퍼 체감·스코치 지면 관측·흰 사각형 잔존 여부) ③커밋 승인(128~149+무번호 누적).

## 11-16 13:05 — 무번호 58-P2 검증 캡처(히트VFX 프로브) — 흰 사각형 소멸 확인 (2026-11-16 13:05)
**사용자 요청**: 피격 VFX 사진 촬영→텔레그램 전송(흰 사각형 소멸 확인용). HitVfxTelegramProbe 신설(Assets/Editor) — Test_10에서 몬스터→플레이어 피격 경로 1:1 발화(ScorchMark 데칼 생산 호출+CombatFXGate.PlayHitFX damage20)+실카메라 RT 캡처 4컷(발화직후/0.3s/1.0s/3.0s, 1024×768).
**판정**: **흰 사각형 0건(4컷 전부)** — 구 프로젝터 경로의 거대 흰/검 표면 소멸. DECDUMP: ScorchMarkMat=URP Particles/Unlit+ScorchMarkTex(128²)·queue 2990·scale 1.2 — 58-P2 계약 이행 확인. 컷별 pxhash 전부 고유(렌더 라이브).
**관찰(수리 미정)**: ①스코치 데칼이 3.0s 컷 지면에서 미확인 — SpawnDecal 호출부 고정 y(0.02) vs 미니지형 표면(원점 y=-0.37·높이 변동) 파묻힘 가능성(FxGroundCrackFX는 하향 레이캐스트 스냅이라 0.3~1.0s 컷에서 지면에 보임). 실게임 비평지 지형에서 스코치/혈흔/발자국 데칼 동일 이슈 가능 — 수리 여부 별도 확정 대기. ②화면 X자 밝은 빛 = 정적 씬 요소(전 컷 픽셀 동일 — 피격 FX 아님).
**프로브 실측(스킬 반영)**: 실카메라가 캡처 시점마다 원점 리셋될 수 있음(settle 비원점 확인 후에도 재발 — 1차 런 전 컷 동일 해시) → 캡처마다 PM 공식 강제 적용으로 수리(2차 런 4컷 고유 해시, StepErrors=0).
**산출**: TestOutput/hitvfx_0[1-4]_*.png·hitvfx_probe.log·Assets/Editor/HitVfxTelegramProbe.cs·run_hitvfx_probe.sh. 텔레그램 전송 완료(4장+판정 요약). 커밋 별도 승인 대기.

---## 11-16 12:35 — 무번호 58-P2 스코치 데칼 DecalProjector 경로 폐기 → 지면 쿼드+URP Particles/Unlit (2026-11-16 12:35)
**58-P1 Play 판정(테스트 11, 11:44 — 57차 빌드)**: **[51-SCREEN]이 드디어 근원을 이름으로 지목** — ①첫 보고 **57,597px = 화면 99.98% 순백**(중심 0.50,0.50) ②지속 근원 = **`Decal_ScorchMark_0 [DecalProjector|ScorchMarkMat|"Shader Graphs/Decal"]`**(51,540px, 근접 FX 0건 — 스코치 수명 10s 정합, 피격마다 스폰) ③전이 순서 SwingSweep→MagicHitURP→DeathFlipbookSmoke→**ScorchMark 지속** = 사망 후에도 남는 거대 흰/검 표면 = "피격시마다 흰·검 덩어리"의 최종 뿌리 확정.
**프로브 실측(Fx58DecalProbe — 에디터 진실원)**: ①**URP 내장 "Universal Render Pipeline/Decal" = Shader.Find NULL**(Decal RendererFeature 활성에도 미로드/스트리핑) → 52-b의 1순위가 **항상 실패** ②2순위 "Shader Graphs/Decal"(매뉴얼 카피)은 FOUND·hasError=False·supported=True — **그 신규 머티리얼 기본값 = color(0,0,0,0)+mainTex null** — DecalSpawner의 _BaseMap/_Color 대입이 슬롯 불일치로 무시 → 절차 텍스처 없이 프로젝터 박스 전체가 흰/검 표면으로 렌더.
**수리(58-P2)**: **DecalProjector 경로 폐기** → **지면 쿼드** + **URP Particles/Unlit 투명**(39차 표준 세트 — _Surface=1·_Blend=0·키워드·태그·SrcAlpha/OneMinusSrcAlpha·ZWrite 0·**queue 2990**) + **절차 텍스처 직접 바인딩**(_BaseMap+mainTexture). 항상 수평 쿼드(Euler 90)+y 0.02 리프트+진입 회전은 Yyaw만 채택(footRot 세로 회전 호환). FA1 알파 램프다운·maxDecals 50·수명 10s·BloodSplat/PoisonPuddle/Footprint/ScorchMark 4종 전부 승계. 52차 핀 1건 계약 진화 갱신(FxDecalPathNormalizeP52Tests — 내장 Decal 미로드 실측 근거 기록).
**QA**: 컴파일 error CS 0 · EditMode **2457 total / 2447 passed / 10 failed**(qa88 — baseline 10건 1:1, 신규 회귀 0) · 집중 **153/153**(신규 DecalUrpQuad_58). 카운트 추이: 2456(58-P1)→**2457(58-P2)**.
**기대 렌더**: 피격 지점에 **1.2m 반투명 검은 스코치 흔적(블롯+크랙)** — 흰/검 거대 표면 소멸. 잔존 이상 시 [51-SCREEN] 4티어+근접 FX가 계속 근원을 이름으로 보고.
**다음**: 사용자 Play 1회(녹화 권장) — 히트마다 검은 스코치 흔적만 남는지 확인. 커밋 별도 승인 대기.
---## 11-16 12:05 — 무번호 58 센티널 스캔 완전성(검정 감지기+준백 티어+스캔RT 저장) (2026-11-16 12:05)
**57차 Play 판정(11:30-11:32 세션 — 57차 빌드)**: ①[56-P1] 발화(히트 임팩트 발생 실측)에도 **[51-SCREEN] 흰 클러스터 0건** — 56(MagicHit URP 정규화)+57(아크 알파 캡) 수리는 측정상 유효 ②그러나 사용자 "흰 검 덩어리 잔존" 보고 → **스캐너 사각지대 확정**: ①**검정 클러스터 감지기 자체가 없었다**(흰/마젠타만 — "검은 덩어리"는 완전 미측정) ②순백 임계(≥246)는 230~245대 준백 덩어리를 누락 ③캡처 = ScreenCapture(UI 포함)라 오경고/오판 검증 근거가 오염될 수 있음.
**수리(58-P1)**: ①**검정 감지기 신설**(전채널 ≤14, ≥400px — 세계 HP바 250~350px 상회로 오경고 억제) ②**준백 티어 신설**(전채널 ≥232, ≥250px — 순백 티어 미달 시에만 보고; 크림 갑옷 249,236,160은 B채널 미달로 배제) ③**스캔 RT 원본 저장**(58_scan_*.png — 320×180 오버레이 UI 미포함, 10s 독자 쿨다운) ④4티어 전부 "덮는 렌더러+근접 FX(3s·8m)" 동시 보고.
**잔존 후보 목록(정직 기록)**: ①몬스터 스탯창 UI — 크롭 판독에서 "베이지/흰 패널+검은 이름바+패널 어긋남·겹침" 지적 — 사용자가 보는 흰/검 덩어리가 이 세계 스페이스 UI일 가능성(의도 UI나 배치 품질은 별도 과제) ②준백/소형 잔존분(58차부터 측정) ③검정 계열(58차부터 측정).
**QA**: 컴파일 error CS 0 · EditMode **2456 total / 2446 passed / 10 failed**(qa86 — baseline 10건 1:1, 신규 회귀 0) · 집중 **152/152**(신규 ScannerCoverage_58 — 4티어/임계/레지스트리 결합/RT 저장). 카운트 추이: 2455(57)→**2456(58)**.
**다음(종결 절차)**: 사용자 Play **녹화 1회** — 4티어 감지 + 근접 FX 이름 + 2종 캡처로 잔존분의 근원이 반드시 이름으로 나옴 → 해당 근거 1회 수리로 종결. 흰/검이 전혀 검출되지 않으면 측정상 클린 — 관측 대상(스탯창 UI 등) 특정 질의로 종결. 커밋 별도 승인 대기.
---## 11-16 11:45 — 무번호 57 아크 리본 구조적 알파 캡 + FX 스폰 레지스트리 (2026-11-16 11:45)
**56차 Play 판정(11:05-11:08 재판정+테스트 10 영상 18.3s 재정밀 분석)**: ①**[56-P1] 정규화 발화 확인 — 히트당 4건 로그**(MagicHit URP 전환 작동) ②그러나 흰 사각 **잔존** — [51-SCREEN] 8건: 신규 근원 후보로 `SwingSweep/DeflectFragment[FXBandingRing]` 16,428px + 좌측 지속 ~9,000px ③센티널 캡처 2장(11:07) ASCII 분석: **순백 쿼드들이 대각선 계단식 배열(경계 하드·내부 단색 — 텍스처 없는 쿼드)** ④테스트 10 영상 에피소드 분석: **t=0~3.75 기저 107px(이름판 UI) → t=4.75부터 공격 동기화 에피소드(~900px 지속) → t=14.75-15.75 폭증(4,648px=사망 버스트 겹침) → t=16.25 전부 소멸**.
**뿌리 확정(8.25s 크롭 비전+코드 교차)**: 흰 다이아몬드 = **strike 지점에 지면에 깔린 공격 아크 리본** — 비전 "마름모 단색·완전 불투명·하드엣지·슬라임 발밑 고정·타격 동기 출현/사망 소멸" = AttackArcHost 리본 정합. **구조적 뿌리 = 페더+코어 합(1.85)의 Clamp01 포화** — 54차 알파블렌드 전환 후에도 텍스처 알파가 1.0으로 포화해 불투명 흰 판이 유지됨(54차 수리가 효과를 못 본 2차 원인). 캡처 지그재그 = 아크+사망 FX 겹침 프레임. 검은 사각 = 캡처상 핫바 UI 슬롯(정상) — 실 결함 흑색 소스는 [57-P2] 레지스트리로 다음 Play에서 판별.
**수리(57)**: ①**P1 아크 구조적 알파 캡** — 슬래시 텍스처 최대 알파 **AlphaCap=0.62**(Min(baseA, cap) — Clamp01 포화 차단)+선단 8% 추가 ×0.85(팁 빗살 소멸)+**코어 패스 알파 1.0→CorePassAlphaMul=0.6** — 최종 코어 ≈0.37×백 = 밝은 슬래시 질감·불투명 판 해소 ②**P2 FX 스폰 레지스트리** — MagentaSentinel.RegisterFxSource(kind,pos) 링 버퍼(32) + DescribeNearbyFx(클러스터 중심 레이캐스트→3s·8m 내 최근 FX 5건 나열) — 일시 FX(수명<스캔주기)가 "해당 렌더러 없음"으로 빠지던 사각지대 해소. 배선: AttackArc/Ring/Spark/SwingSweep/MagicHit/MagicHitCross/DeathVFX.
**QA**: 컴파일 error CS 0 · EditMode **2455 total / 2445 passed / 10 failed**(qa85 — baseline 10건 1:1, 신규 회귀 0) · 집중 **151/151**(신규 AttackArcDiamondCap_57 3건 — 코어 캡·텍스처 픽셀 실측(최대 ≤0.65·선단 ≤0.55)·레지스트리 배선). 53차 핀 2건 계약 진화 갱신(코어 α 1.0→상수·단조 구간 u<0.92 한정). 카운트 추이: 2452(56)→**2455(57)**.
**다음**: 사용자 Play 1회(녹화 권장) — ①공격 시 아크 = 반투명 슬래시(흰 다이아몬드 소멸) ②**[51-SCREEN]의 "근접 FX(3s·8m)" 필드가 잔존 흰/검 덩어리의 스폰 FX를 이름으로 보고** — 해당 근거로 최종 수리. 커밋 별도 승인 대기.
---## 11-16 11:35 — 무번호 56 MagicHit Built-in 셰이더 URP 정규화 — 55차 오진 정정 (2026-11-16 11:35)
**55차 Play 판정(공격씬 테스트 10, 10:51)**: 흰·검 사각 덩어리 **잔존 + [55-P1] 수리 로그 0건** — 수리가 발화조차 안 함. 임팩트 스폰은 정상(스케일 로그 다수).
**오진 정정(에디터 프로브 Fx55TextureProbe 실측)**: MagicHit 4종 머티리얼의 _MainTex는 **소실이 아니라 정상 바인딩** — Magic Hit/Magic Flash='1-Point'(512×512), Magic Simbol 2='1-Crit_'(tiff), Magic Shockwave='1-Shockwave 2' — 전부 Guz 팩 실존 텍스처(55차 감사 정규식이 TexEnv 후보 블록을 잘못 매칭한 오판). 텍스처 알파도 정상(PIL 실측: 1-Point hi-alpha 6%·1-Shockwave 10% — 소프트 도트/링), UVModule(아틀라스 시트) 전부 비활성 — 텍스처·블렌드·모듈 전부 정상.
**진짜 뿌리**: **Built-in "Particles/Standard Unlit" 셰이더의 URP 렌더 — 알파 무시·불투명 단색 쿼드**(흰=HDR 백색 4.5×, 검=어두운 틴트). 39차 "체브런/눈금/크랙 흰·검 사각형"과 **동일 결함군** — 39차는 다른 FX를 URP/Unlit 투명 표준화로 고쳤으나 MagicHit는 변환 누락. 55차 수리가 발화 안 한 이유 = `mainTexture != null` 게이트(기본 텍스처 반환으로 전부 통과).
**수리(56-P1)**: **NormalizeMagicHitMaterials()** — MagicHit 인스턴스 파티클 전수: built-in 머티리얼을 **URP Particles/Unlit + 투명 표준 세트**(_Surface=1·_Blend=0·키워드·RenderType 태그·SrcAlpha/OneMinusSrcAlpha·ZWrite 0·queue 3000 — 39차 EnsureUnlitTransparent 동일 세트)로 재조립, **기존 텍스처/색 승계**(41-b 스무스 서클 클론+틴트/에셋 팩 텍스처 전부). 멱등(이미 URP skip)·URP 셰이더 미발견 시 무변경 폴백·인스턴스 클론+유한 파괴 8s·sharedMaterial 판독(48차 교훈). PlayImpactMulti+PlayCross 양 경로 배선. 55차 GetSoftBurst(미사용) 제거.
**QA**: 컴파일 error CS 0 · EditMode **2452 total / 2442 passed / 10 failed**(qa84 — baseline 10건 1:1, 신규 회귀 0) · 집중 **148/148**(56 계약 2건 교체 — 55 오진 계약 폐기). 카운트 추이: 2453(55)→**2452(56)**.
**다음**: 사용자 Play 1회 — 피격 시 **알파 페더된 매직 서클+소프트 플래시**(사각 덩어리 소멸) + 콘솔 `[SlashVFX] [56-P1] MagicHit 머티리얼 URP 정규화 N건` 확인. 잔존 시 [51-SCREEN] 근원 보고. 커밋 별도 승인 대기.
---## 11-16 11:05 — 무번호 55 MagicHit 텍스처 소실 수리 — 피격마다 흰·검 사각 덩어리 뿌리 (2026-11-16 11:05)
**54차 Play 판정(공격씬 테스트 9, 34.7s — 69프레임 2fps 픽셀 스캔+흰 마스크 ASCII 시각화+비전 크롭+로그 교차)**: ①**[51-SCREEN] 첫 유효 작동 — 7건 보고**, 임계 150 유효(700폐기 근거 확정) ②결정 힌트: 클러스터 커버 렌더러 1건 = **"ImpactVFX_Organic/Shockwave | MagicHitCircleSmooth | Particles/Standard Unlit"** ③흰 마스크 ASCII: 얇은 링 아크(정상) + **채워진 거대 흰 쿼드 2개 겹침 + 소형 흰 사각**(결함) 혼재 ④RenderGraph 에러 0건·[53-SCREEN] 미발화 — 53차 센티널 수리 유지 ✓.
**뿌리 확정(에셋 실측)**: MagicHit 프리팹(Guz "Magic Hit 2")의 4종 머티리얼(Magic Hit·Magic Simbol 2·Magic Shockwave·Magic Flash) **_MainTex GUID 전부 미해결** — Guz 무료 팩에 Magic 계열 텍스처 미포함(팩 텍스처 24종 전수 guid 대조 실측) → 파티클이 **텍스처 없는 단색 쿼드**로 렌더(HDR 백색 4.5×=순백, 보라 계열=검보라). 보라 3시스템은 40·41차 런타임 수리(스무스 서클 교체)가 텍스처를 갈아끼워 정상(=비전이 본 얇은 링 아크), **"Magic Hit 2" 메인 버스트는 HDR 백색이라 보라 판별식(37-F1) 미적중 → 미수리 → 순백 채움 쿼드** = 피격마다 흰·검 사각 덩어리. PlayImpactMulti/PlayCross 양 경로 공통.
**수리(55-P1)**: ①**FxMaterialLibrary.GetSoftBurst()** 신설 — 256×256 소프트 방사 플래시(코어→pow1.6 감쇠+해시 각도 비대칭, 결정론 Random 0건·캐시 1회) ②**SlashVFXRunner.RepairMissingParticleTextures()** 신설 — 인스턴스 파티클 전수 중 **sharedMaterial.mainTexture==null 시스템만** 절차 텍스처 배정(보라=스무스 서클·그 외=소프트 버스트 — 인스턴스 클론+유한 파괴 8s·에셋 무변경 40차 선례, sharedMaterial 판독으로 게터 부작용 회피 48차 교훈) ③**PlayImpactMulti+PlayCross 양 경로 배선**(Application.isPlaying 가드) ④정상 시스템 무손상(41-b 딥 퍼플 클론 경로 보존).
**QA**: 컴파일 error CS 0 · EditMode **2453 total / 2443 passed / 10 failed**(qa83 — baseline 10건 1:1, 신규 회귀 0) · 집중 **149/149 통과**(신규 AttackFxTextureRepair_55 3건 — GetSoftBurst 결정론/알파 조형·수리 경로 소스계약·Random 0건). 카운트 추이: 2450(54)→**2453(55)**. Resources/FX 전수 머티리얼 감사 — 소실은 MagicHit 4종뿐(Death/Slash/Cloak/Fire 정상).
**다음**: 사용자 Play 1회 — 피격 시 **부드러운 방사 플래시+매직 서클 링**(흰·검 사각 덩어리 소멸) + 콘솔 `[SlashVFX] [55-P1] 텍스처 소실 파티클 수리 N건` 확인. 커밋 별도 승인 대기.
---## 11-16 10:35 — 무번호 54 아크 알파블렌드 진화 + 블롭섀도우 정규화 + 스캐너 임계 하향 (2026-11-16 10:35)
**53차 Play 판정(공격씬 테스트 8, 24.9s — 25프레임 픽셀 스캔+셀 클러스터+비전 크롭 판독+로그 교차)**: ①RenderGraph 에러 0건 — 53차 센티널 RT 수리 유효, [53-SCREEN] 자가차단 미발화(스캔 매 스윕 정상 주행) ②전투 사슬 정상 — 슬라임 2타(10.5/10)→사망→전리품 바구니 ✓ ③**[51-SCREEN] 여전히 0건** — 원인=임계값 과다(700px@320×180)로 실측 덩어리 150~400px급 전부 미달.
**흰 사각 정체(셀 클러스터+크롭 비전 4회)**: ①**t=1→18 지면에 평평한 순백(#FFF) 다각형 덩어리** — 플레이어 전투 지점에 잔존, 플레이어 이동(t=19+)과 함께 화면 이탈, 비전="무기 궤적 방향 분포·하드엣지·텍스처 없음" ②**스윙 순간(t=10, 화면 3.7% 폭증) = 아크 리본 코어** — 비전 "거대한 회백색 검 형태·사각 평면·페더 없음" ③**캐릭터 발밑 검은 원형 블롭 그림자 부재**(비전 실측) — BlobShadow 자리에 흰 덩어리.
**의도와 다른 부분(확정)**: ①**아크/트레일 additive 포화** — 밝은 지면(녹색 ~0.7) 위 additive 흰 코어(α1.0)는 지면+흰>1.0 클램프 = 전 폭 순백 → **53차 슬래시 텍스처의 페더가 시각적으로 마스킹**돼 "텍스처 없는 흰 덩어리"로 읽힘(53차 수리가 효과를 못 낸 이유) ②**BlobShadow URP 투명 정규화 누락** — _SURFACE_TYPE_TRANSPARENT 키워드+RenderType 태그 부재 = 52-b 데칼과 동일 결함군(불투명 변형 컴파일 위험).
**수리 4건**: ①**P1 AttackArcVFX.CreateFallbackMaterial** — Additive(SrcAlpha/One)→**Alpha(SrcAlpha/OneMinusSrcAlpha)** 계약 진화(밝은 지면 포화 실측 근거 기록) — 텍스처 페더·글로우 α계층(0.15) 실가시 ②**P1 WeaponSwingTrail.CreateTrailMaterial** — 동일 전환(공유 재질이므로 아크에도 자동 적용) ③**P0 BlobShadow.BuildMaterial** — _SURFACE_TYPE_TRANSPARENT+RenderType Transparent 키워드/태그 추가(52-b 선례) — 검정 원형 그림자 복귀 ④**P0 MagentaSentinel WhiteClusterPixels 700→150** — 실측 덩어리 150~400px 커버(UI 노이즈 기저 20~40px 대비) — 다음 Play에서 [51-SCREEN]이 지면 잔존 덩어리 근원을 이름으로 보고.
**QA**: 컴파일 error CS 0 · EditMode **2450 total / 2440 passed / 10 failed**(qa82 — baseline 10건 1:1, 신규 회귀 0) · 집중 **146/146 통과**(53차 8건+신규 AttackArcBlendShadowContract_54 3건 — 센티널 임계·BlobShadow 정규화·런타임 머티리얼 알파블렌드 실측). 카운트 추이: 2447(53)→**2450(54)**. 53차 additive 핀은 54차 진화로 갱신(AttackArcTextureContract_53Tests — 진화 사유 주석 기록).
**업그레이드 계획(제시)**: P3 패망 고품질(기존 확정) → P4 아크 2차 미세조정(코어 폭 0.7m 체감 재판정·스피어=직선 스러스트 트레일 강화/아크 축소 후보 — Play 체감 기반) → P5 타격 임팩트(방향성 스파크 라인→페더 스트릭·히트스톱 연계) → P6 지면 FX 통합(스코치/혈흔 데칼 질감 고도화·블롭 섀도우 소프트 페더) → P7 슬래시 플립북 고도화(프레임 6→8~10·틴트 연동).
**다음**: 사용자 Play 1회 — ①아크=부드러운 슬래시 질감(순백 덩어리 소멸) ②발밑 검은 원형 그림자 복귀 ③**잔존 흰 덩어리 시 [51-SCREEN] 로그(첫 유효 작동)가 근원 이름 보고** — 그 근거로 최종 수리. 커밋 별도 승인 대기.
---## 11-16 10:10 — 무번호 53 스크린 스캐너 RT 수리 + 아크 리본 고품질 1단계 (2026-11-16 10:10)
**52차 Play 판정(공격씬 테스트 7, 27.8s — 28프레임 전수 픽셀 스캔+비전 판독+로그 교차)**: ①**마젠타 소멸 확정 — 전 프레임 0픽셀** ②전투 사슬 완전 정상 — 히트 해결(45차 time-gate)→슬라임 3타(10.5/13/13)→사망→DeathVFX+전리품 바구니 3건+EXP 7 ✓ ③플레이어는 두 번째 슬라임에 10회 피격(3.48×10) 후 사망 — 이계 시스템이 인터셉트해 현장 부활(HP 75) ④**[51-SCREEN] 보고 0건 + RenderGraph 에러 44건 = 51-a 스캐너가 첫 스윕부터 사망**이었음 — 흰 사각 미보고는 정상이었던 것.
**흰 사각 판정(3분해)**: ①t=1-4 대형(스크린 2.2%) = **아크 리본 본체** — 비전 판독 "삼각형 스트립 리본·불투명·하드엣지·정사각 종단" = 코드 실측 정합(URP Particles/Unlit+Additive+텍스처 없음, 스테이지1 틴트=흰은 설계 계약) → **의도된 시스템이 맞되 렌더 품질이 의도(부채꼴 슬래시)에 미달** ②t=19-27 중간 흰색 = 몬스터 공격 VFX 의도대로(적색 텔레그래프 링=95차 F5 ✓·방향성 스파크 x15=159차 P4 ✓·피격 화면 플래시 ✓) ③영상 종단 1프레임 계단식 불투명 흰 쿼드 덩어리 = 미확정(사망·이계 전환 순간 1프레임) — 다음 Play에서 [51-SCREEN]이 근원을 이름으로 특정.
**수리**: ①**P0 센티널 RT** — 뿌리=`new RenderTexture(320,180,0)` **깊이 버퍼 0비트**: URP RenderGraph는 Output Texture에 깊이 포맷 필수("the output Render Texture must have a depth buffer" 공식 경고 실측) → DBufferCopyDepthPass(데칼 프리패스) CreateRenderGraphTexture "no format" 예외가 ReadPixels 이전에 흐름을 끊음. **깊이 24비트 descriptor 교체** + try/catch 자가 차단(실패 1회 시 [53-SCREEN] 1급 로그 후 스캔 스킵 — 에러 스팸 금지) ②**P2 아크 고품질(위임 구현 착지)** — 절차 슬래시 텍스처 신설(256×64·v방향 가우시안 페더+u방향 전두엽→꼬리 페이드+중심 코어 밴드·결정론 Random 0건·캐시 1회) + EmitPass UV 대입(u=샘플 진행도·v 좌0/우1 — [45차] 대입 순서 계약 유지) + 머티리얼 _BaseMap 바인딩(AttackArcVFX+WeaponSwingTrail 폴백 양경로) + **글로우 패스 알파 0.30→0.15**(GlowPassAlphaMul 소스계약 핀 — additive 포화 완화)·코어 ×1.0 유지. ResolveTint(1흰/2골드/3주황) 무손상.
**QA**: 컴파일 error CS 0 · EditMode **2447 total / 2437 passed / 10 failed**(qa81 — baseline 10건 1:1, 신규 회귀 0) · 집중 **143/143 통과**(AttackArc|FxSlash|CombatFx|GaugeTrail|FxSpearTimeGate 필터 — 신규 AttackArcTextureContract_53 8건 포함). 카운트 추이: 2437(52)→**2447(53)**. 스코프 감사: 당일 수정 = 4파일(AttackArcVFX·MagentaSentinel·WeaponSwingTrail·신규 테스트).
**다음**: 사용자 Play 1회 — ①아크 = 부채꼴 슬래시 질감 체감(불투명 흰 덩어리→부드러운 사선 소멸) ②잔존 흰 사각 시 [51-SCREEN] 로그가 근원 이름 보고(스캐너 첫 작동 — 종단 1프레임 정체 판별 포함) ③통과 시 **P3 패망 고품질** 착수. 커밋 별도 승인 대기.
---## 11-16 02:00 — 무번호 52 URP 데칼 경로 정규화 — 흰 사각링·마젠타 링 뿌리 (2026-11-16 02:00)
**51차 Play 판정**: 런타임 계측 전부 클린 + 사용자가 콘솔에서 **URP DecalProjector MissingReferenceException** 제공 → 데칼 경로가 계측 사각지대였음을 확정.
**뿌리 2건**: ①**DecalSpawner가 DecalProjector에 URP/Lit 머티리얼 세팅** — Lit에는 데칼 투영 패스가 없어 프로젝터 박스가 **불투명 사각으로 렌더**(혈흔/독웅/발자국/그을음 데칼 = "흰 사각링"의 뿌리, 기존 "URP/Lit 사용 (DecalProjector와 호환)" 주석은 오류 가정) ②**CommandMarker 링 데칼 1순위 셰이더가 "Shader Graphs/Decal"(매뉴얼 카피 가족)** — 세션에 따라 파손 시 데칼이 그대로 **마젠타 링** 렌더.
**수리 3건**: ①**52-a CommandMarker** — 데칼 셰이더 1순위를 URP 내장 "Universal Render Pipeline/Decal"로 교체(Shader Graphs/Decal은 후순위) + 에디터 ShaderHasError 진실원으로 파손 셰이더 즉시 쿼드 폴백 ②**52-b DecalSpawner** — CreateDecalMaterial을 URP 내장 데칼 셰이더로 교체(투명도=_BaseMap.a×_BaseColor.a 규약, Lit 전용 _Surface/_Blend 세팅 제거) + 기본 머티리얼 null 시 데칼 스킵 가드(비-데칼 머티리얼 대체 금지) ③**52-c 센티널 DecalProjector 전수 감시** — 머티리얼/셰이더 null·InternalErrorShader·텍스처 평균(마젠타/순백 베이크 실패) 1급 보고 + 화면 역산에 데칼 후보 포함(DescribeCoveringRenderers 확장 — [51-SCREEN]이 데칼도 이름으로 보고).
**부수 확인**: 사용자 콘솔의 DecalProjector MissingReferenceException은 에디터 SceneVisibilityManager의 URP 내부 콜백으로 런타임 무해 — 단, 데칼 GO 파괴 빈도(CommandMarker 정리)와 함께 보이는 것으로 데칼 경로 활동 교차 확인.
**QA**: 컴파일 error CS 0 · EditMode **2437 total / 2427 passed / 10 failed**(qa80 — baseline 1:1, 신규 회귀 0) · **P52 3/3 통과**(P51 후보 문자열 승계 포함). 카운트 추이: 2434(51)→**2437(52)**.
**다음**: 사용자 Play 1회 — 커맨드 링/데칼이 정상 렌더되는지 확인. 잔여 이상 시 [51-SCREEN]/[52-SENTINEL] 로그+캡처가 근원(데칼 포함)을 이름으로 특정. 통과 시 P2 아크 고품질·P3 패밝 고품질 착수. 커밋 별도 승인 대기.
---## 11-16 01:30 — 무번호 51 화면 픽셀 역매핑 스캐너 + 셰이더 컴파일 전수 스캔 (2026-11-16 01:30)
**50차 Play 재판정(Editor.log 00:38)**: 런타임 계측 **전부 깨끗** — [43-SENTINEL] 감지 0건·[47-MATGUARD] 0건·Unknown error 0건·"Can't add component" 0건·예외 0건·Ground_Grass_Mat 복구 유지. 사망 1건+전리품 바구니 정상. 그러나 사용자 보고 마젠타·흰 사각 **잔존**.
**판정**: 잔여 결함은 센티널이 볼 수 없는 경로(컴파일 실패 셰이더의 GPU 마젠타 — 렌더러 셰이더명은 정상, UI/후처리/스카이박스 등 렌더러 외부)에 있음. 커스텀 셰이더 5종(SelectionRing·TelegraphRing·FresnelRim·FXBandingRing·AcquisitionBeam·Dissolve) 파이프라인 태그 전수 감사 — 전부 UniversalPipeline 정상(파이프라인 불일치 배제).
**수리(근원 특정 최종 관문 — 무번호 51)**: ①**51-a 화면 픽셀 역매핑 스캐너** — 매 스윕(1.5s) 카메라를 320×180 RT로 재렌더해 화면 픽셀 직접 실측 → 순수 마젠타 클러스터(≥24px)/불투명 흰 클러스터(≥700px) 감지 → **클러스터를 덮는 렌더러를 바운드 8코너 화면 AABB 역산으로 특정**(path|type|mat|shader, 최대 4건) + 1급 로그 + 화면 캡처. 렌더러 후보 0건이면 "UI/후처리/스카이박스 경로"로 명시 보고 — **원인 불문 100% 커버** ②**51-b 셰이더 컴파일 전수 스캔**(세션 1회) — 전 로드 머티리얼의 셰이더를 ShaderUtil.ShaderHasError로 실측, 에러 셰이더 1급 보고(GPU 마젠타 감지).
**QA**: 컴파일 error CS 0 · EditMode **2434 total / 2424 passed / 10 failed**(qa77 — baseline 1:1, 신규 회귀 0) · **P51 3/3 통과**. 카운트 추이: 2431(50)→**2434(51)**.
**다음**: 사용자 Play 1회 — 결함이 보이는 동안 콘솔의 **[51-SCREEN]** 로그가 결함 오브젝트의 이름을 직접 보고(또는 UI/후처리/스카이박스 경로 판정) + Screenshots/44_magenta_*.png 자동 캡처. 이 보고를 근거로 최종 수리 1회 — 이후 재발 경로 없음(동일 관문이 상시 감시). 통과 시 P2 아크 고품질·P3 패밝 고품질 착수. 커밋 별도 승인 대기.
---## 11-16 01:00 — 무번호 50 디졸브 .material 게터 부작용 제거 — 시체 마젠타 최종 수리 (2026-11-16 01:00)
**49차 Play 재판정(Editor.log 00:18)**: ①"Unknown error"(Ground_Grass_Mat) 소멸 ✓ ②프로브 스팸 소멸 ✓ ③FxEmberRise 감지 소멸 ✓(48차 중복 AddComponent 수리 유효) ④**Monster_slime/input null 잔존**(사망 12383 → 감지 12917).
**최종 뿌리 확정**: FxDissolveUtility의 `.material` 게터 — 게터는 공유 머티리얼을 **클론 인스턴스화해 슬롯에 대입하는 부작용**이 있고, 램프 종료 블록이 그 클론을 파괴 → **슬롯 null** → 리스폰 재활성 시 마젠타(48-T3 원본 복원도 게터를 한 번 더 써서 같은 함정 재현). 즉 시체 마젠타는 게터의 부작용이었다.
**수리(50-T1)**: ①**램프 루프 — sharedMaterials 직접 판독**(무인스턴스화)으로 _DissolveAmount 램프 ②**종료 블록 재구성** — 비활성 → **승계된 디졸브 인스턴스 직접 파괴(_dissolveInsts 레지스트리)** → **원본 복원(가드 경유)** — 게터 완전 제거, 슬롯은 항상 유효 머티리얼 ③계약 3건 승계(30-D4 파괴 경로·47-b 램프 격리·48-T2 순서) + CRLF 정규화 및 탐색 오프셋 정정(테스트 결함).
**자산 복구 확인**: Ground_Grass_Mat.mat 유효 YAML 복구 ✓(P49 잠금).
**QA**: 컴파일 error CS 0 · EditMode **2431 total / 2421 passed / 10 failed**(qa76 — baseline 1:1, 신규 회귀 0) · P49 3/3 + 계약 승계 통과. 카운트 추이: 2431(49)→**2431(50)**.
**다음**: 사용자 Play 1회 — 사망→시체 녹색 디졸브→리스폰 정상(슬롯 항상 유효), 마젠타/흰 덩어리 소멸. 잔여 시 감지 로그+캡처가 근원 특정. 통과 시 P2 아크 고품질·P3 패밝 고품질 착수. 커밋 별도 승인 대기.
---## 11-16 00:30 — 무번호 49 자산 손상 발견·복구 + 디졸브 원본 복원 + 프로브 스팸 차단 (2026-11-16 00:30)
**사용자 보고(48차 Play 후)**: 마젠타·흰 사각 미소멸 + 콘솔에 "Unknown error occurred while loading 'Assets/URP/Ground_Grass_Mat.mat'" + 센티널 텍스처 프로브 경고 스팸(SelectionRing/TelegraphRing/FresnelRim).
**자산 손상 발견(전수 스캔 15,490파일)**: **Assets/URP/Ground_Grass_Mat.mat가 4010바이트 전체 NULL 채움** — "Unknown error"의 원인. 전체 7건 손상 중 6건은 미사용 에셋팩 데모(LightingData·데모 터레인·NavMesh), 본 프로젝트 참조는 이 1건뿐. git HEAD에도 손상본이 커밋되어 있어 git 복원 불가 → **유효한 쌍둥이(Assets/Resources/URP/Ground_Grass_Mat.mat, Sep 4 정상 YAML)로 복구**(.meta 가이드 유지). 손상 .mat = 지면 머티리얼 슬롯 파손 경로(마젠타/흰 렌더 후보). 손상 원인 불명(에디터 비정상 종료 중 쓰기 실패 추정) — 재발 시 동일 스캔으로 조기 발견.
**시체 SMR null 근원 재확정**: 디졸브 램프 종료가 머티리얼 인스턴스 파괴만 수행 → 슬롯 null → **리스폰 시 렌더러 재활성으로 null 머티리얼(마젠타) 노출**(48차 Play 재실측 — 사망 후 감지). 수리: **48-T3 원본 복원** — TryBeginDissolve가 원본을 레지스트리에 승계 → 램프 종료 시 비활성 → **원본 머티리얼 복원(가드 경유) → 인스턴스 파괴**. 슬롯은 항상 유효 머티리얼 보유. 부수: foreach 중괄호 누락 정정(48-T2 패치 사고 — 루프 본체가 탈출하던 결함), mainTexture null 가드.
**프로브 스팸 차단**: 센티널 텍스처 프로브가 _MainTex/_BaseMap 부재 셰이더에서 mainTexture 게터 경고를 매 스윕 발화 → **HasProperty 게이트 선행**으로 스킵.
**QA**: 컴파일 error CS 0 · EditMode **2431 total / 2421 passed / 10 failed**(qa73 — baseline 1:1, 신규 회귀 0) · **P49 3/3 통과**. 카운트 추이: 2428(48)→**2431(49)**.
**다음**: 사용자 Play 1회 — ①지면 정상 로딩("Unknown error" 소멸) ②마젠타/흰 덩어리 소멸(불티·연기·시체·지면 4근원 전부 수리 완료) ③시체 녹색→디졸브→리스폰 정상 ④프로브 스팸 소멸. 잔여 시 [47-MATGUARD]/[43-SENTINEL]/캡처가 근원을 특정. 통과 시 P2 아크 고품질·P3 패밝 고품질 착수. 커밋 별도 승인 대기.
---## 11-16 01:40 — 무번호 48 마젠타 진짜 뿌리 확정·수리 — 파티클 중복 AddComponent (2026-11-16 01:40)
**47차 Play 재판정(Editor.log 23:45 + 캡처 234515)**: [47-ARC] 안전왕치 2건 정상 발동(스윙 중 아크 원칙 작동). 그러나 FxEmberRise·Monster_slime/input 여전히 null — **[47-MATGUARD] 로그 0건** = 가드가 아니라 가드 이전 단계에서 실패.
**진짜 뿌리 확정(로그 11858~11905행 직접 실측)**: ①**"Can't add component 'ParticleSystemRenderer' to FxEmberRise because such a component is already added"** — `AddComponent<ParticleSystem>()`가 렌더러를 자동 부착하므로 재 `AddComponent<ParticleSystemRenderer>()`는 **에러+null 반환** → 머티리얼 미적용 → 마젠타 빌보드 ②"Setting the duration while system is still playing" — 자동 재생 중 모듈 설정 거부. **세션 파손 가설 폐기 — 전형적인 C# 버그였다**(배치 재현 실패는 배치가 사망시퀀스를 안 돌렸기 때문). DeathVFX의 43/46차 NRE도 동일 뿌리(중복 AddComponent → rnd null → 세터 NRE).
**수리 3건**: ①**48-T1** DeathVFX·FxEmberRise — `AddComponent<ParticleSystemRenderer>()` → **`GetComponent<ParticleSystemRenderer>()`**(자동 부착본 사용, 전 코드베이스에서 해당 패턴은 이 2곳뿐) + `ps.Stop(StopEmittingAndClear)` 자동 재생 정지(duration 설정 에러 차단) — DeathVFX/FxSlashFlipbookFlash에도 예방 적용 ②**48-T2 시체 디졸브** — 램프 종료 시 **비활성 선수행 → 머티리얼 파괴**(null 슬롯 렌더 창 금지) + **림 오버레이(FxRimOverlay) 디졸브 제외**(_MainTex 경고+null _BaseMap 승계 차단) ③가드(47-a)·아크 게이트(47-P1) 유지.
**QA**: 컴파일 error CS 0 · EditMode **2428 total / 2418 passed / 10 failed**(qa70 — baseline 1:1, 신규 회귀 0) · **P48 2/2 통과**. 카운트 추이: 2426(47)→**2428(48)**.
**다음**: 사용자 Play 1회 — 사망 시 ①불티/연기 정상 렌더(마젠타X) ②시체 녹색→디졸브 소멸 ③전리품 바구니(46차부터 정상) ④아크 스윙 중만. 잔여 이상 시 [47-MATGUARD]/센티널 캡처가 근거 제공. Play 통과 시 P2 아크 고품질·P3 패망 고품질 착수. 커밋 별도 승인 대기.
---## 11-16 01:00 — 무번호 47 머티리얼 가드 단일 관문 + 스윙 중 아크 원칙 (2026-11-16 01:00)
**46차 Play 재판정(공격씬 테스트 6.mp4 34.1s + Editor.log)**: ①**성과 — [46-DEATHFX] 폴백 발동+전리품 바구니 3건 스폰(9082/14751/18843) = 사망 후처리 정상화** ②잔여 마젠타 = 영상 t=0.5~4.5만(소형, 이후 소멸 — 센티널 자가수리 작동) ③잔여 흰 덩어리 = 첫 처치 시점(t=6.5~9.5)과 정확히 일치 — 시체 SMR 수리 머티리얼(웜그레이). 잔여 근원 2건: **FxEmberRise**(FXPalette.ApplyTo 단일 세터 NRE가 try/catch로 무음 삼켜져 null 머티리얼) + **Monster_slime/input SMR**(FxDissolveUtility .material 세터 NRE 부분 갱신 → 슬롯 null → 마젠타 시체 → 웜그레이 수리 = 흰 덩어리).
**뿌리 재확정**: 사용자 세션에서 런타임 렌더러 머티리얼 대입이 결정론적 NRE(배열 대입조차 NRE — [46-DEATHFX] 실측). 에디터 기본 머티리얼 리소스 파손 의심 — **Library 재생성(사용자 1회)을 근본 치유 병행 권장**.
**수리(P0)**: ①**47-a FxMaterialLibrary.TryAssignSharedMaterials/TryAssignSlot0** — 런타임 머티리얼 대입 단일 관문: 배열 대입+try/catch+**실패 시 원본 배열 복원(null화 금지 — 마젠타 시체 원천 차단)**+1급 로그(3s 스로틴)+재시도 없음(결정론 근거) ②**47-b 전 경로 이식** — FXPalette.ApplyTo(FxEmberRise 마젠타 무음 삼킴 제거)·FxDissolveUtility(슬롯 0 교체 가드+실패 시 미소유 인스턴스 즉시 정리+램프 루프 .material 게터 격리 — **시체는 기존 녹색 유지, 디졸브 연출만 생략**)·DeathVFX(46 T1 승계)·FxSlashFlipbookFlash ③P46 계약 진화 승계(배열 대입→가드 경로).
**수리(P1 스윙 중 아크 원칙)**: 흰 아크(WeaponSwingTrail)가 스윙 후에도 지속된 뿌리 = 상태 미진입 경로에서 EndCombo/레거시 탈출 OFF가 영영 미발화. **스피어 시퀀스 은퇴(ResetSpearSequence)에 SetSwingTrailEmitting(false)+AttackArcVFX.Settle() 추가** + **안전왕치(WatchSwingEmitDeadline)**: 방출 ON 상태로 2.0s 상한 경과 시 강제 OFF+세틀+1급 로그 — 어떤 OFF 누락 경로에도 아크 잔존 금지.
**QA**: 컴파일 error CS 0 · EditMode **2426 total / 2416 passed / 10 failed**(qa69 — baseline 1:1, 신규 회귀 0) · P47 3/3 + P44~46 10/10 통과(계약 진화 포함). 카운트 추이: 2423(46)→**2426(47)**.
**다음**: ①사용자 **Library 재생성**(Unity 종료→C:/Unity/code/Library 삭제→재실행) 후 Play — 마젠타/흰 덩어리 근본 소멸 기대 ②코드 가드는 Library 미청소 세션에서도 시체 마젠타화를 차단(원본 복원) ③Play 재판정 통과 시 **P2 아크 고품질·P3 패망 고품질** 착수(계획 확정 — 47차 보고 참조). 커밋 별도 승인 대기(153차 이후 누적).
---## 11-16 00:20 — 무번호 46 사망 FX NRE 근원 수리 — 마젠타·전리품·흰 사각 3증상 일원화 (2026-11-16 00:20)
**근원 확정(직접 증거)**: 사용자 45차 Play(Editor.log 23:06-23:10)에서 ①센티널 감지 "ParticleSystemRenderer path=DeathFlipbookSmoke slot=0 mat=null" ②NRE 전체 스택 — **DeathVFX.SpawnFlipbookSmoke 73행(sharedMaterial 단일 세터 내부 NRE) → AnimalAI.Die 1447행 중단** ③센티널 자동 캡처 2건(44_magenta_225108/230932.png) 비전 판독 — 사망 지점 마젠타 사각 다각형+거대 흰 사각.
**3증상 일원화**: ①**마젠타** = 런타임 파티클 렌더러의 머티리얼 대입이 세터 내부에서 NRE(기본 파티클 머티리얼=빌트인 파이프라인 계열 — URP 프로젝트 파손) → 머티리얼 null → 마젠타 빌보드 ②**전리품 없음+사망 확인 불가** = NRE가 Die()를 1447행에서 끊음 → 전리품 바구니(1454)·고기/재료/희귀 드롭·디졸브·시체 정리 전부 미실행(시체 잔존=사망 판별 불가) ③**흰 사각** = 센티널 수리 머티리얼(URP/Lit 불투명 웜그레이)이 파티클 빌보드에서 불투명 흰 사각 렌더 — 수리 자체가 2차 결함. 과거 영상 재해석: 사망 없던 세션(공격씬3·장비착용)=마젠타 0픽셀, 사망 있던 세션만 마젠타 — 전부 정합. 배치 재현 3회 실패 원인 = 러너가 피격까지만 강제하고 죽이지 않았기 때문. 45차 time-gate 정상 확인(데미지 10.5/14/10.5→HP=0→사망 로그).
**수리 3건**: ①**T1 DeathVFX** — sharedMaterial 단일 세터 → **sharedMaterials 배열 대입**(센티널 수리 경로 실증 유일 성공 경로) + try/catch 격리 + **대입 실패 시 즉시 GO·머티리얼 정리 후 false**(기존엔 NRE가 Destroy 예약을 생략해 유령 GO가 마젠타 빌보드를 무기한 잔존 — 성장형 덩어리의 정체) → false 시 기존 DeathSmoke 프리팹 폴백(29-M3-2 계약 무손상) ②**T2 AnimalAI.Die()** — PlayDeathVFX try/catch 격리(FX 실패가 사망 후처리를 끊지 않음 — TakeDamage 카메라 이펙트 격리 선례) → 전리품·디졸브·시체 정리 무조건 실행 ③**T3 MagentaSentinel** — 파티클 렌더러 수리 머티리얼을 **투명 소프트 파티클**(CreateFlipbookSmoke — 연기 아틀라스)로 교체+정적 캐시 1회(누출 금지), 메시 계열은 웜그레이 유지(43 계약 승계).
**QA**: 컴파일 error CS 0 · EditMode **2423 total / 2413 passed / 10 failed**(qa67 — baseline 10건 1:1, 신규 회귀 0) · **P46 3/3 + P45 3/3 + P44 4/4 통과**. 카운트 추이: 2417(44)→2420(45)→**2423(46)**.
**다음**: 사용자 Play 1회 — ①슬라임 사망 시 **흰/회 반투명 연기**(마젠타X) ②**전리품 바구니 생성**+뚜껑 열기 아이템 획득 ③시체 디졸브 소멸(사망 판별 가능) ④흰 사각 소멸. 실패 시 콘솔 [46-DEATHFX]/[46-DIE] 로그 + 센티널 자동 캡처가 근거를 남긴다. 커밋 별도 승인 대기(153차 이후 누적).
---## 11-15 23:30 — 무번호 45 스피어 히트 시간 게이트 수리 + 리본 메시 축소 순서 수리 (2026-11-15 23:30)
**사용자 로그 전달(45차 Play)**: ①[44-INPUT-DIAG] 디바이스 정상 ②[44-SENTINEL] VFX Graph 0건·텍스처 마젠타 미감지·[43-SENTINEL] 0건 ③**[44-SPEAR-DIAG] 13샘플 결정타** ④AttackArcHost:RebuildRibbon "Mesh.vertices is too small"+"colors out of bounds" 반복 오류 신규 발견.
**스피어 무피해 근원 확정**: 13샘플 전부 **opener:False/exit:False/followup:False/idle:False + 상태 해시 -125389069 불변 + normT 루핑(0.14→0.85 순환)** → 클릭 후 **AttackThrust 상태가 아예 진입하지 않음**(이동 중 보행 루프 상태에 머무름). 컨트롤러 YAML 전수: Base Layer AnyState 47건 **전부 트리거 조건**(이동 강제 이탈 경로 부재)·AttackThrust 상태=Thrust_Slash.fbx("Armature|Thrust_Slash_withSkin", non-loop)·유일 출발 전이=exit-time 0.92(Idle) → 조건 전이로 끊길 구조가 없는데도 미진입(컴포넌트 경합 유력). 마커(normT≥0.50)는 상태 진입을 전제하므로 영구 미발화 → **무피해→사망 없음→전리품 없음** 사슬의 뿌리.
**수리 1(스피어 시간 게이트)**: MonitorSpearSequence에 **발화 시각+오프너 클립 길이×마커(0.50) 시간 게이트 해결 추가** — 상태 머신이 AttackThrust에 진입하지 않아도 타격이 반드시 해결. 클립 길이=런타임 컨트롤러 클립 목록에서 "Thrust_Slash" 프래그먼트 매칭 1회 캐싱(폴백 1.2s). 상태 기반 해결은 정상 세션 조기 해결용으로 병행, resolved 플래그가 이중 발화 차단. FireComboTrail(1) 동기 발화.
**수리 2(리본 메시)**: RebuildRibbon 대입 순서 결함 — **축소 시(만료 샘플 트림) 정점을 삼각형보다 먼저 대입** → 기존 삼각형이 큰 인덱스를 참조해 페이드아웃마다 오류 스팸. **축소 시 triangles→vertices→colors, 확대 시 vertices→colors→triangles** 분기로 정정(데이터 무변경, 순서만).
**진단 고도화**: [44-SPEAR-DIAG]에 **실제 재생 클립명(GetCurrentAnimatorClipInfo)+런타임 컨트롤러명** 추가 — 애니 레벨 근원(컨트롤러 교체/오버라이드/경합)을 다음 Play에서 직접 식별.
**QA**: 컴파일 error CS 0 · EditMode **2420 total / 2410 passed / 10 failed**(qa66 — baseline 10건 1:1, 신규 회귀 0) · **P45 계약 3/3 + P44 4/4 통과**. 카운트 추이: 2413(43)→2417(44)→**2420(45)**.
**다음**: 사용자 Play — ①슬라임 공격 시 **"[Spear] AttackThrust hit resolved (time-gate)"** 로그 + 실제 데미지/사망/전리품 바구니 확인 ②리본 오류 로그 소멸 확인 ③[44-SPEAR-DIAG]의 clip=/ctrl= 값(애니 근원 추적용) — 마젠타는 이 세션 센티널 미감지였으므로 재출현 시 캡처 자동 저장.
---## 11-15 22:45 — 무번호 44 마젠타 근원 자동 확정 장치 + 몬스터 무피해 사각지대 진단 (2026-11-15 22:45)
**사용자 보고(공격씬테스트 5, 21:00 녹화)**: ①센티널 경고 없는데 마젠타 여전 ②몬스터 사망 시 전리품 안 뜸 ③사망 여부 확인 불가.
**로테이션 발견(중요)**: 사용자가 21:22 Unity 재런치 → Editor.log 교체 → 21:00 Play 세션 로그는 **Editor-prev.log**(716KB)로 이동. 이번 라운드 초반 greps가 신규(동굴 씬) 로그를 잘못 대상으로 한 것 정정 — 판정은 전부 Editor-prev.log 기준 재검증.
**몬스터 무피해 사슬 확정(Editor-prev.log 실측)**: ①좌클릭 관측 [CombatInput] 2건(weapon=Spear, ui/squad/parry 전부 False — 입력 정상) ②드라이버 수락 "[CombatInput] melee accepted sequence=1·2" + "Spear opener played directly (AttackThrust, t=0)" + "[Combo] Spear segment accepted=1" 2건 ③그러나 **"[Spear] AttackThrust hit resolved" 0건** — MonitorSpearSequence(마커 normT≥0.50)가 한 번도 발화 안 함 ④슬라임 피격 로그("데미지! HP=")·사망·전리품·경험치 전무, 슬라임→플레이어 공격 7회(5 데미지→3.484321 1:1 대응) ⑤예외 0건(Update 정상 주행) ⑥컨트롤러 에셋(Player_AC)에 AttackThrust 상태 현재 존재. **판정**: 스윙 연출·FX(팁 (2.04,0.05,-1.94) 스윕 x3·스코치)는 드라이버가 냈지만 히트 해결이 안 됨 → 사용자 보고 3건("전리품 없음/사망 확인 불가")은 단일 사슬: **오프너 상태가 마커에 도달 못함 → 무피해 → 사망 없음 → 전리품 없음**. 근원은 미확정(상태 미진입 vs 재시작 루프 vs 마커 판정) → 다음 Play에서 [44-SPEAR-DIAG] 샘플이 3축으로 직접 판별.
**마젠타 재판독(영상 5, 37.2s 전수 스캔)**: 순수 마젠타 덩어리 t=0.5~8.5 정지(중앙) → t=9.5부터 **시간에 비례 성장**(최대 3,800px) — 크롭 비전: 무기/신체 아님, **겹친 빌보드 구름 덩어리** 형태. t=0.5 프레임에서 슬라임=정상 녹색·HP 35(만혈)·'83'은 공격력 스탯(데미지 팝업 아님 — 오독 정정). 병사1은 흰 링만(정상). 성장형+구름 형태 = **"셰이더 정상+내용물(텍스처) 마젠타"** 후보(런타임 생성 플립북 아틀라스 계열) — 43 센티널(셰이더 파손 전용)의 미커버 영역.
**수리 없음 — 진단 장치 4건(확정 전 수리금지 원칙)**: ①**[44-SPEAR-DIAG]** HumanoidClipDriver.MonitorSpearSequence — 오프너 미해결 상태 샘플(클릭당 6회·0.4s 간격): opener/exit/followup/idle 판정+normT+마커+transitioning ②**[44-AIM-DIAG]** PlayerCombat.ResolveComboSwingHit 미스 분기 — 기존 무음 사각지대 해소(커서 레이 히트 수/최근접 콜라이더+레이어+주변 5m 콜라이더의 IDamageable 유무·거리, 3s 스로틀) ③**[44-INPUT-DIAG]** PlayerCombat.Update 세션 1회 — Mouse/Keyboard.current 부재 경고(디바이스 탈락 판별) ④**[44-SENTINEL]** MagentaSentinel 확장 — VFX Graph 인벤토리(세션 1회)+텍스처 순수 마젠타 프로브(IsPureMagenta — 딥퍼플/화이트/녹색 배제 엄격 임계, 신규 머티리얼만·스윕당 예산 8·보고 전용)+감지 직후 화면 캡처(Screenshots/44_magenta_*.png, 10s 쿨다운)+LogBroken에 렌더러 종별 동반 기록. 43 계약(ErrorShader 감지·폴백 수리) 무손상 승계.
**QA**: 컴파일 error CS 0(패치 이중 이스케이프 1건+C#9 보간 구멍 따옴표 1건 자가 수리) · EditMode **2417 total / 2407 passed / 10 failed**(qa65 — baseline 10건 1:1: AERO·Cooking·H2_Catalog·Instrument 3·InvadedLord 2·RecipeCatalog·ThemeUSS) · 신규 회귀 0 · **P44 계약 4/4 통과**. 테스트 카운트 추이: 2409(42)→2413(43)→**2417(44)**.
**다음**: 사용자 Play 1회 — ①콘솔 [44-SPEAR-DIAG] 6샘플(오프너 상태 진행 실측) ②공격 미스 시 [44-AIM-DIAG] 1급 진단(주변 콜라이더 인벤토리) ③[44-INPUT-DIAG] 디바이스 상태 1줄 ④[44-SENTINEL] 텍스처 마젠타 감지/화면 캡처 — 4축 로그로 마젠타 근원+무피해 근원을 동시 확정. 로그 전달 시 수리 착수. 커밋 별도 승인 대기(153차 이후 누적).
---## 11-15 21:10 — 무번호 43 마젠타 재발 재추적 — 42 수리 경로 무발화 확정 + 전면 배제 진단 + 센티널 상시 감시·자가 수리 (2026-11-15 21:10)
**사용자 보고**: "마젠타는 몬스터 피격시 생기는데 여전히 사라지지 않아"
**42 수리 무효 확정**: 사용자 Play 로그(11-15 19:43 종료 세션)에 "[SlashVFX] 폴백 Slash VFX 발화" 0건 — 42가 수리한 Slash 프리팹 경로는 60차부터 무기 트레일로 대체되어 **발화하지 않음**. 실제 스윙 = "[Combo] 레거시 Attack* 스윙 트레일 방출 (PlaySlash → 무기 트레일 대체)".
**픽셀·비전 재판독(공격씬 테스트 4.mp4, 18:13 녹화 — 42 수정 이전 빌드)**: 마젠타 = **순수 #FF00FF 불투명**(bbox 내 평균 RGB(243,5,241)) — InternalErrorShader 전형. 병사1(TestGuard_1, guardName="병사1") 위치 대형 덩어리, 영상 전 구간(t=0.5~11.5s) 지속. 화살 테스트 5.mp4에서도 이동성 마젠타 확인. 단, 장비착용/공격씬3/화살2·3/환호 영상은 0픽셀.
**전면 배제 진단(전부 결손 0)**: ①.mat 362건 셰이더 guid(음수 fileID 정규식 보완 — ShaderGraph 머티리얼 누락 수정) ②모델 131(t:Model)+**GLB·FBX 1,133(렌더러 2,311)** — FbxModelShaderProbe(Tests) 신설 ③씬·프리팹 1,850건 — 미존재 참조 12건 전부 미사용 에셋팩 데모 ④런타임 머티리얼 생성 경로 전수 — null 가드 확인 ⑤병사 FBX 내부 머티리얼 = URP/Lit(SoldierModelShaderDiagTests).
**배치 3회 전면 재현 실패(=사용자 세션에서만 발생)**: executeMethod 플레이 러너(MagentaSweepBatchRunner) — Test_10 로드+병사 스폰+GLB 재질 이식(CopyMaterialsFromGlb 완료 실측)+슬라임 강제 TakeDamage → **렌더러 25개 전수 파손 0건**(전체 인벤토리 덤프). PlayMode 테스트 인프라는 본 프로젝트에서 어셈블리 열거 미동작(실측) — executeMethod 러너로 대체.
**수리(무번호 43)**: ①**MagentaSentinel**(Assets/Scripts/Systems/MagentaSentinel.cs) — Play 중 1.5s 주기 전 렌더러 감시, InternalErrorShader/null 머티리얼 감지 시 **계층 경로·머티리얼·셰이더명 LogWarning(근원 1급 증거) + URP/Lit 폴백 인스턴스 자가 수리**(42 런타임 수리 계약 승계, 렌더러당 1회 — 누출 방지, Projector도 감시) ②DeathVFX.SpawnFlipbookSmoke NRE 수리(사용자 로그 8238행 실측) — rnd.material→sharedMaterial(세터 내부 인스턴스화 경로 제거, FxSlashFlipbookFlash 동일 예방) ③FxShaderErrorProbeTests에 **"Shader Graphs/glTF-pbrMetallicRoughness"** TestCase 추가 — glTFast 패키지 셰이더그래프(플레이어 장비·슬라임 머티리얼 소스) 상시 진단.
**유력 근원(미확정)**: 사용자 세션 한정 런타임 셰이더 파손. 배치 재현 0건 + 정적 전수 결손 0 → 다음 Play에서 센티널 경고 로그가 근원을 직접 특정.
**QA**: 컴파일 error CS 0 · EditMode **2413/2403/10**(baseline 1:1 — AERO·Cooking·H2_Catalog·Instrument 3·InvadedLord 2·RecipeCatalog·ThemeUSS) · 신규 회귀 0 · 43 프루브/진단 전부 통과
**다음**: 사용자 Play 1회 → 콘솔 "[43-SENTINEL]" 경고 유무 확인(있으면 로그 전달 = 근원 확정, 없으면 마젠타 소멸 여부 판정). 신규 영상(수정 빌드 기준) 병행 요청.
---## 11-15 22:00 — 무번호 42 Slash 프리팹 깨진 셰이더 참조 수리 — 마젠타 최종 뿌리 (2026-11-15 22:00)
**사용자 보고**: "여전히 마젠타 남아있어"
**재판독(타격 순간 초확대)**: 마젠타 = **Pure Magenta(#FF00FF)** + **검기(참격) 모양 긴 띠** + 메시 정상/텍스처-셰이더 누락 — 즉 **콤보 스윙의 Slash 이펙트 메시**가 마젠타로 렌더.
**최종 뿌리 확정**: SlashVFXRunner.PlaySlashStage의 기본 경로 = "Free Slash VFX" 프리팹(8 MeshRenderer, URP Shader Graph) — **매뉴얼 복사 프리팹(MagicHit/StylizedSlash 선례와 동일 패턴)의 머티리얼 셰이더 guid가 재발행으로 깨짐** → 렌더러 셰이더 = Hidden/InternalErrorShader → 검기 모양 마젠타 밴드 렌더. (40의 디졸브/스무스 서클 수리는 별개 결함 — 이번이 본체.)
**수리(무번호 42)**: SpawnFallbackSlashStage 인스턴스 후처리 — MeshRenderer 전수 스캔 → `sharedMaterial.shader == null || name == "Hidden/InternalErrorShader"` 감지 → **검증 발광 화이트**(URP Particles/Unlit+HDR, CreateTrailMaterial 동일 사양)로 슬롯 전체 교체. 메시 형태/오리엔테이션 유지, 46/47차 에셋 파일 무변경 계약(런타임 인스턴스만), 부재 시 폴백(null=기존 유지).
**40 흰 사각형 보강(동일 뿌리 재확인)**: 데브리 샤드(41-a)·데칼 바인딩(40-a)·투명화 표준화(39) 착지분 유지 — 흰 사각형은 데브리 큐브가 직접 근원이었음(41-a 수리).
**QA**: 컴파일 error CS 0 · EditMode **2409/2399/10**(baseline 1:1) · 42 3/3
**다음**: Play — 스윙 시 검기가 **발광 화이트 아크**(마젠타 아님)로 렌더되는지. 마젠타 완전 소멸 확인 시 커밋 승인 부탁.
---## 11-15 21:30 — 무번호 41 흰 사각형 근원 확정(데브리 큐브)+샤드 리웍 / 딥 퍼플 직지정 (2026-11-15 21:30)
**사용자 보고**: "마젠타와 흰색 사각형 여전히 남아있어"
**근원 추가 확정**: FxDebrisSpawner — `PrimitiveType.Cube` 무회전 + Core×0.45 흰 톤 = **강타격 시 날아가는 흰 정육면체** = "흰색 사각형"의 직접 근원(33-D1 본인). 데칼/체브런과 별개.
**수리(41-a)**: 데브리 큐브 → **샤드(파편 조각)** 리웍 — 축 신장(z 2.6×)+3축 결정론 회전+워먼 석편 톤(Edge 경유 — 33-D1 잠금 Core→Edge 승계 갱신)
**수리(41-b)**: MagicHit 보라 시스템 startColor → **딥 퍼플(0.45,0.25,0.85) 직지정** — 에셋 HDR 머티리얼 배수(2.6×)와 무관하게 최종 렌더 고정(40-b 틴트 강등과 이중 안전 — 네온 핑크 재발 차단)
**QA**: 컴파일 error CS 0 · EditMode **2406/2396/10**(baseline 1:1) · 41-a/b 잠금 추가 · 33-D1 잠금 갱신 1건
**다음**: Play — ①강타격 파편=회전 샤드(흰 큐브 아님) ②피격 보라 링=딥 퍼플(네온 핑크 아님). 커밋 별도 승인 대기.
---## 11-15 20:50 — 무번호 40-b 마젠타(네온 핑크) 뿌리 확정·수리: HDR 과노출 (2026-11-15 20:50)
**사용자 보고**: "여전히 마젠타 남아있어"
**픽셀 실측(ffmpeg crop→8×8 RGB 평균)**: 히트 지점(3s) 평균 RGB **(139,91,136)** = 핑크/마젠타 확인(지면 대조=녹색 정상) — 순수 Missing 마젠타(255,0,255)가 아닌 **과노출 핑크**.
**뿌리 확정**: MagicHit 보라 시스템의 렌더 머티리얼 = **HDR 화이트(Basic Shockwave_URP 2.6,2.55,2.6)** × 보라 startColor(0.38,0.26,0.55) → (0.99,0.66,1.0) = **네온 핑크** 렌더. 40의 스무스 텍스처 교체만으로는 색이 그대로였던 이유.
**수리(40-b)**: 인스턴스 클론 머티리얼 틴트를 바이올렌트 친화 배수(1.0, 0.85, 1.2)로 강등 → 렌더 ≈(0.38, 0.22, 0.66) **딥 퍼플** + _BaseColor 동기. 스무스 텍스처(40-a)와 결합 = 다듬어진 딥 퍼플 마법 서클.
**QA**: 컴파일 error CS 0 · EditMode **2406/2396/10**(baseline 1:1) · P40 테스트 40-b 잠금 추가
**다음**: Play — 히트 보라 링이 **딥 퍼플**(네온 핑크 아님)로 렌더되는지. 여전히 밝으면 40-b 배수 조절(1.0,0.85,1.2 → 하향). 커밋 별도 승인 대기.
---## 11-15 20:10 — 무번호 40 마커/서클 다듬기 + EditMode 오염 수리 (2026-11-15 20:10)
**수리 3건**:
- 40-a 데칼 바인딩 정밀화 — CommandMarker 데칼의 "첫 텍스처 프로퍼티 난사"가 노말맵/비표면에 바인딩 → **불투명 사각 렌더(흰 사각형 뿌리)** — _BaseMap 계열 우선+노말맵 회피(F3 잠금 보존)
- 40-b 스무스 매직 서클 — MagicHit 보라 서클 원본 텍스처가 픽셀화 계단("AA 없이 거칠게" 실측) → FxMaterialLibrary.GetSmoothMagicCircle(AA 원환: 외곽/내부 링+6글리프+방사 그라디언트)로 인스턴스 교체(클론+유한 파괴)
- 40-c EditMode 오염 수리 — 40 서클 교체 블록이 EditMode에서 material 인스턴스화(로그 에러 → AttackOrders/EnvoyIntel/TickIntel/IndoorWar 4건 오염) → Application.isPlaying 가드 선행(가드 우위 스코프)
**QA**: 컴파일 error CS 0 · EditMode **2406/2396/10**(baseline 1:1 복귀) · 40 2/2 · 셰이더 프로브(FxShaderErrorProbeTests — ShaderUtil 진실원) 전부 Passed
**다음**: Play — ①마젠타 폴리곤 소멸(디졸브 수리 확인) ②이동 명령 링=스무스 링+회전 눈금+체브런(사각형 없음) ③보라 서클 AA 체감. 커밋 별도 승인 대기.
---## 11-15 19:30 — 무번호 39 렌더 결함 수리(마젠타/불투명 사각) + 히트 리웍 2R 착지 (2026-11-15 19:30)
**사용자 지시(공격씬 테스트 4.mp4)**: "여전히 파티클이 제대로 다듬어지지 않았어 수정 + 다음 계획 승인 — 더 고품질로"
**영상 판독(13컷+크롭 확대)**: ①**마젠타 부정형 폴리곤**이 이동/공격 경로에 렌더(엔진 Missing 계열) ②체브런/눈금/크랙 계열 **흰·검 불투명 사각형** ③슬라임/병사 본체는 정상 렌더 ④파티클 빈약(작고 단순)
**근원 2건 확정·수리**:
- **FxDissolve.shader HLSL 예약어 `base`** 사용 → 셰이더 컴파일 실패 → 사망 디졸브가 마젠타 폴리곤으로 렌더(영상의 경로상 마젠타 = 몬스터 사망 시점 일치) → `baseCol` 개명
- **URP/Unlit 투명화 누락** — 체브런/눈금/크랙/엣지 머티리얼이 _Surface 미설정 → 불투명 사각 렌더 → FxMaterialLibrary.EnsureUnlitTransparent 표준화 후 4경로 적용(CommandMarker·Crack·EdgeDanger·라이브러리 경유)
**39-T2~T4 착지(38 계획 승인분)**:
- T2 히트 방향 스파이크 — 타격 반대편 3본 콘 25°(20-J1 재사용, Edge 단일 소스)
- T3 크리 앰프 — 라이트 3.0→4.2 / 블룸 2.2→2.8(위계 체감)
- T4 디졸브 티어 위계 — tier≥5(정예) 엣지 ×1.3+창 1.2s(4인자 오버로드 — 30-D4 3인자 잠금 보존)
- 파티클 빈약 → T2 스파이크+S3 이중 아크(38)로 보강
**QA**: 컴파일 error CS 0(열거형 TwoColors·SpawnDirectionalSpark 정규화·float/int 수리) · EditMode **2397/2387/10**(baseline 1:1) · 39 5/5 · 병행 세션 잠금 갱신 4건(33-A1/A3·B1·30-D4)
**다음**: Play — ①마젠타 소멸 ②흰·검 사각 소멸(체브런/눈금/크랙 정상 렌더) ③티어별 디졸브 위계 ④크리 앰프 체감. 커밋 별도 승인 대기.
---## 11-15 18:20 — 무번호 38 히트 피드백 리웍 1R: 보라 복원+플래시 승격 (2026-11-15 18:20)
**사용자 지시(공격씬 테스트 3.mp4)**: "플래시도 안되어있는 것 같고 피격 보라도 안되어있어 — 수정 + 고품질 계획"
**영상 판독(29컷 타임라인+타격 크롭)**: ①히트 플래시 강도 **3/10**(작고 좁고 단순) ②보라 지면 링 소실 = **37-F1 재색이 원인** — 사용자 재판정: 보라 = 버그가 아니라 **의도적 히트 시그니처**(복원 요청) ③흰색 스윙 궤적 15/29 타일 정상(35-S1) ④몬스터 틴트 미관측(2R 과제)
**1R 착지(무번호 38)**:
- F1R 보라 시그니처 복원 — 37-F1 재색 블록 제거(MagicHit 원색 유지 = 의도적 계층), 계약테스트 폐기 잠금으로 전환(재유입 방지)
- S3 플래시 승격 — 아크 1.5→**2.6배**/수명 0.55→**0.7s**/알파 선명화/**이중 아크 Emit 2**(십자 교차)
- A1 히트 광 승격 — 1.8/2.6→**2.2/3.0**, 반경 3.5→**3.8m**, 지속 0.15→**0.18s**(33-A1 잠금 갱신)
**계획서**: docs/plans/2026-11-13_no38-hit-feedback-rewok-plan.md — 2R(T1 틴트 가시화/T2 방향 스파이크/T3 크리 앰프/T4 디졸브 티어 위계)+3R(W1 잔상 비네트/W2 사망 물리/W3 소리 위계) 제안 — 추천 T1→T4→T2→T3
**QA**: 컴파일 error CS 0 · EditMode **2392/2382/10**(baseline 1:1) · 37 5/5(38-F1R 전환) · 33-A1 잠금 갱신 1건
**다음**: Play — 히트 플래시/보라 링 체감 → 2R(T1~T4) 승인 시 착수. 커밋 별도 승인 대기.
---## 11-14 00:30 — 무번호 37-2R 고품질 계층 1R: H1/G3/H3/G1 (2026-11-14 00:30)
**사용자 지시**: "진행" — 37 로드맵 추천 순서(H1→G3→H3→G1) 4항목 착지
**H1 강타격 프리즈**: 피해≥15(33-D1 Heavy 기준 공유) 타격 2사이트(충전 강공+일반/반격)에 히트스톱 0.05s@0.06 추가 — 36 파리 등급 0.07/0.03과 무충돌 별도 밴드
**G3 위험 방향 인디케이터**: ScreenEdgeDangerFX 신설 — Core.PlayerHealth.OnPlayerDamaged 구독만(Core 무수정 계약), 시야 밖 공격원 방향 화면 가장자리 적색 아크 0.3s(절차 텍스처·unscaled·인스턴스 1개 재사용·try/catch)
**H3 지면 크랙 데칼**: FxGroundCrackFX 신설 — 크리/Heavy 한정(33-D1 동일 게이트), 절차 6분지 균열 텍스처(해시 지터·결정론), 지면 스냅 레이캐스트, 예산 2, 2.5s 페이드, 인스턴스 파괴(누출 금지)
**G1 투사체 트레일 그레디언트**: ArrowProjectile — FXMotion.Afterglow 속도 비례(24m/s 기준) 트레일 길이/알파 — 25-C3 티어 그래디언트 무손상
**QA**: 컴파일 error CS 0 · EditMode **2392/2382/10**(baseline 1:1) · 2R 5/5 · 테스트 대상 파일 정정 1건(H3 훅=CombatFXGate)
**다음**: Play — ①피격 보라→주황(F1) ②참격 아크(S3) ③예고 수렴(M1) ④명령 눈금(R2) ⑤강타격 프리즈/크랙/엣지 힌트/화살 트레일(2R) 누적 판정. 2R 후보: H2 카메라 오프셋·E 계층. 커밋 별도 승인 대기.
---## 11-13 23:59 — 무번호 37 보라 임팩트 수리(F1) + 35 잔여 착지(S3/M1/R2) + 후속 로드맵 (2026-11-13 23:59)
**사용자 지시**: ①남은 계획 진행 ②몬스터 피격 시 지면 보라색 빛 오류 확인 ③더 높은 VFX 품질 계획 수립
**F1 보라 빛 — 오류 확정·수리(에셋 YAML 실측)**: 피격 임팩트 `Resources/FX/Impact/MagicHit.prefab`의 파티클 1시스템 `maxColor=(r0.377, g0.256, b0.547)`=**보라**(마법 테마 지면 이펙트) — 물리 타격에 매발화가 원인. 수리 = PlayImpactMulti 인스턴스 재색(보라 판별식 청>녹+0.12∧적>녹+0.05 → FXPalette.Edge), 에셋 파일 무변경(46/47차 계약), 백색/주황 무영향
**35 잔여 착지**:
- S3 참격 플립북 — GetSlashArcFlipbook(6프레임 스윕 아크, 연기 파이프라인 재사용·top-down 행) + FxSlashFlipbookFlash(히트 지점 아크, Guz 위 보완 레이어) + CombatFXGate 훅(크리 1.5배)
- M1 예고 수렴 눈금 — AttackTelegraphIndicator 자식 레이어 3본 체브런(반경 0.38→0.22 수렴+회전 가속, 0.15s 잔여 시점 제거 — 종료 플래시 겹침 방지), CommandMarker.SharedChevronMaterial 단일 소스 재사용
- R2 명령 링 이중화+회전 — CommandMarker 지속형 전용 내부 눈금 링(절차 16눈금, 반경 62%, -220°/s), 67차/84차/72차 계약 무손상
**후속 로드맵(계획 전용 — 승인 대기)**: docs/plans/2026-11-13_no37-vfx-quality-roadmap-plan.md — 3계층(H 물리감: 히트스톱 변조/카메라 임팩트/지면 크랙 · G 가독성: 트레일 그레디언트/처치 콤보/위험 방향 힌트 · E 환경: 스커치/오브젝트 반응/워터 스플래시) — 추천 순서 H1→G3→H3→G1
**QA**: 컴파일 error CS 0(두색 열거형 TwoColors·sheet.cycles 제거 수리 2건) · EditMode **2387/2377/10**(baseline 1:1) · 37 5/5
**다음**: Play — ①피격 시 보라→주황 지면 링 확인 ②참격 아크 플래시 ③예고 수렴 체브런 ④명령 눈금 회전. 로드맵 승인 시 다음 라운드 착수. 커밋 별도 승인 대기.
---## 11-13 22:50 — 무번호 36-2R 패망 임팩트/반격/위계 + 기각 2건 (2026-11-13 22:50)
**사용자 지시**: "2라운드도 진행해줘" — 36 계획 잔여(PI2/PI3/PS1/PS2/PG1)
**중복/전제 실측으로 기각 2건**:
- PI2 방향성 스트릭 → **기각**: 기존 SpawnParryDeflectFan(FanRunner 스트릭 3본 — 20-J1)+SpawnSwingSweep(지면 스트릭 3본 — 22-S2)가 이미 메시 스트릭 계층. wave1/2에 방향성 스트릭 존재 — 신규 구현은 중복
- PS1 반격 대기 펄스 → **기각(전제 불성실)**: 반격 큐는 ParryImpactRoutine 내 즉시 해방 구조(L821~ ConsumeQueuedCounterAttack→TryAttack 자동 창구) — "대기 상태"가 존재하지 않음. 대기 펄스는 구현 불가한 전제
**PI3 트리플 링 3박**: wave0 PERFECT 분기에 +0.12s 서드 원거리 잔광 링(반경 4.4m, 알파 0.45 — 아우터 0.5보다 옅음) — StartCoroutine(ParryThirdRingRoutine, WaitForSecondsRealtime — 히트스톱 중 리듬 유지, 23-X1 선례). EditMode 호출부 가드
**PS2 반격 식별 링**: AttackTarget에서 반격 배율 적용 타(counterMultiplier != 1f)에 한해 타겟 LastHitPoint 골드 링 0.3s — 반격 타임라인 완결(플레이어 금백 링[패망P2-C]→타겟 지점 골드 링[36-PS2]). 데미지 적용 후 발화(순서 계약)
**PG1 GOOD 톤 보강**: GOOD 분기에 골드 코어 미니 링 1겹(반경 0.9m — 청색 링 위 금-청 이중 = "성공이지만 만점 아님"). PERFECT 위계(2겹+아우터+트리플) 무손상 증분
**QA**: 계약테스트 ParryCounterPhasePIPGTests 4건(기각 근거 잠금 포함 — 재유입 방지). 컴파일 error CS 0 · EditMode **2382/2372/10**(baseline 10건 1:1) · 36-2R 4/4
**다음**: Play — ①PERFECT 3박 파열(링 3겹 리듬) ②반격 타겟 골드 링 ③GOOD 금-청 이중 ④1R(방어 림 시계/만료 수렴/접촉점)+35 리본/실루엣/체브런 누적. 무번호 36 완료 — 잔여는 35 계획(S3 플립북/M1 예고 수렴/R2 회전 눈금/S4·M3 판단 항목). 커밋 별도 승인 대기.
---
## 11-13 22:20 — 무번호 36 패망 창 시각 언어 + 접촉점 정합 (2026-11-13 22:20)
**사용자 지시**: "패링 관련도 확인해보고 더 고품질로" — 실측 갭 5건 확정(성공 임팩트는 이미 밀집) → 계획서 승인(docs/plans/2026-11-13_no36-parry-vfx-quality-plan.md) → "진행" (1R: PW 전체+PI1)
**PW1 방어 림(닫히는 시계)**: 창 개방~성공/만료까지 장착 무기 골드 림(WeaponEquipManager.CurrentVisual — 105차 F1, FxRimOverlay.AttachRimStatic — 29-P1-2 선례). Update 갱신 = 잔여 시간 비례 강도 감쇠(0.45s). 성공 시 즉시 해제(28-P1 플래시 림이 이어받음 — 이중 림 방지), 재입력 멱등, 주먹/미장착 스킵
**PW2 만료 신호**: 자연 만료 시 청색 수렴 링 0.15s(SpawnConvergingRing — 21-L1 재사용, SFX/히트스톱 없음). 성공 경로는 림 선행 해제(_parryGuardRim==null)로 미발화 가드
**PW3 PERFECT 힌트**: 개방 링 청색→**골드**(ParryGold 단일 소스 — 창 초반이 PERFECT 구간임을 행동 전 학습), 경계(ParryPerfectWindow — 95차 F1 판정과 동일 상수 공유, 이원화 금지) 통과 시 림 골드→청색 전환+1회 청색 틱 링. 타임라인: 골드 링→(0.15s)청색 틱→(0.45s)청색 수렴
**PI1 접촉점 정합**: 흡수점 p = 고정 전방 1m → **공격자 방향(kbDir 역방향) 1m** — 방향 계산 선행 재정렬. 7요소(링/스파크/팬/위스프/버스트/SFX) 전부 p 기반이라 1곳 수정으로 전체 동기 이동. 폴백(영벡터)=기존 전방 공식 동일 결과
**보조 확장**: FxRimOverlay SetRimColor/SetRimIntensity 정적(MeshRenderer) 경로 — AttachRimStatic 림의 색/강도 갱신 전제(29-P1-2 잠금 무손상)
**QA**: 계약테스트 ParryWindowVFXPhasePWTests 5건. 컴파일 error CS 0 · EditMode **2378/2368/10**(baseline 10건 1:1) · 36 5/5 · ParryWeaponFlashPhasePTests(28-P 잠금)·PlayerCombatParryInputTests(95차 잠금) 무손상
**다음**: Play — ①V 누르면 무기 골드 림+골드 개방 링 ②0.15s 후 청색 전환+틱 ③만료 시 수렴 링 ④성공 시 FX가 공격자 쪽에서 터지는 접촉감 ⑤누적(35 리본/실루엣/체브런·33·31·30). 2R 후보: PI2 방향 스트릭·PI3 트리플 링·PS1 반격 대기 펄스·PS2 반격 식별 링·PG1 GOOD 보강. 커밋 별도 승인 대기.
---
## 11-13 21:40 — 무번호 35 전투 VFX 도약 1R: S1 리본 활성 + S2 실루엣 화이트아웃 + R1 체브런 (2026-11-13 21:40)
**사용자 지시**: "공격/패망/몬스터/RTS VFX가 현재 품질에 만족 못 하겠다" — 계획서 승인(docs/plans/2026-11-13_no35-combat-vfx-leap-plan.md) → "진행"
**실측 격차 본체**: ①AttackArcVFX 3D 스윕 리본이 F6 토글 뒤 기본 OFF ②히트 플래시=sharedMaterial _Color 틴트(47차 — 실루엣 부재) ③RTS 명령 마커=단일 링 1s(방향감 부재)
**S1 프리미엄 참격 리본 활성**: AttackArcVFX.PremiumArcEnabled 기본 true + HumanoidClipDriver _premiumArcEnabled 기본 true(진실원 동기) — F6 A/B 토글 유지. 기존 리본(이중 패스 코어/외곽+콤보 틴트+사거리 비례) 그대로 구동 — 머티리얼 트림은 Play 판정 후
**S2 히트 실루엣 화이트아웃**: FxHitSilhouette.cs 신설 — FxRimOverlay 재사용(29-D3), 흰 림(FXPalette.Core) peak 3.0→0.1s 선형 소멸, SMR 없으면 AttachRimStatic 폴백, 동시 상한 6 예산 가드, OnDestroy 단일 감산. 훅=CombatFXGate 두 진입 오버로드(47차 틴트와 이원 계층 — 림=윤곽/틴트=면). FxRimOverlay 확장: SetRimIntensity 정적(MeshRenderer) 경로 + Release 정적 머티리얼 파괴(누출 금지)
**R1 이동 경로 체브런**: CommandMarker 확장 — 지속형(이동/집결) 전용, 그룹 병사 평균→목적지 방향 체브런 3본(1.4m 간격, yaw=atan2) 스태거 팝 인, 72차 1s+0.3s 규약 수명 동기(스케일 페이드 — 알파 미사용, 공유 머티리얼 단일 소스), 절차 체브런 텍스처(선분 거리 기하, 캐시 1회, 결정론), 2m 미만/병사 부재 미발화 가드, 셰이더 부재 폴백
**중복/보류 검증**: R2(회전 눈금)=데칼 경로 복잡도로 다음 라운드 / R4(유닛 먼지)=29-S2 중복 기각 / P1 화이트프레임=Play 판정 대기 누적
**QA**: 계약테스트 FxCombatLeapS1S2R1Tests 4건(Does.Contains 오타 CS0117 1건 수리 — 29-P6 동일 함정). 컴파일 error CS 0 · EditMode **2373/2363/10**(baseline 10건 1:1) · 35 4/4 · 기존 FxDetailS2P2Tests(29-D 잠금) 무손상
**다음**: Play — ①공격 시 3D 스윕 리본 체감(기본 활성) ②피격 실루엣 화이트아웃 ③이동 명령 체브런 방향감 ④누적(33 라이트/블룸/파편·31 수차·30 디졸브). 2R 후보: R2 명령 링 회전·P2 방향 스트릭·M1 예고 수렴·S3 참격 플립북. 커밋 별도 승인 대기.
---
## 11-13 20:30 — 무번호 33 VFX 고품질 마무리: A 빛 계층 + B 블룸 펄스 + D 2차 파티클 (2026-11-13 20:30)
**사용자 지시**: "VFX 더 고품질로 — 계획 먼저" → 계획서 승인(docs/plans/2026-11-13_no33-vfx-final-polish-plan.md) → "진행"
**실측 근거**: 전투 FX 전체가 발광 쿼드/링뿐, 실시간 라이트 0건(유일 선례=LootSpawnFX 골드 라이트) — "그려진 그림" 격차.
**Phase A 빛 계층(체감 최대)**:
- FxImpactLight.cs 신설 — Spike(pos, color, intensity, range, duration): 즉시 peak→선형 소멸→자가 파괴. 동시 상한 3 예산 가드(23-X2 선례), unscaled 시간, EditMode 가드, 소멸 카운트=OnDestroy 단일 경로(이중 감산 방지), 섀도우 None(단명 비용 방지)
- A1 히트 훅(CombatFXGate.PlayHitFXInternal — 단일 통로): FXPalette.Edge, 크리 2.6/일반 1.8, 반경 3.5m, 0.15s
- A2 패망 PERFECT 골드 라이트: 흡수점(전방 1m), ParryGold, 0.3s — 23-X1 시퀀서 wave0 연동
- A3 사망 소멸 광: MonsterDanger 0.9s — 30-D4 디졸브 램프와 동기(dissolveBegan 변수화 — 폴백 시 스킵)
**Phase B 포스트 계층 2**:
- ScreenBloomFX.cs 신설 — 런타임 볼륨+Bloom 오버라이드만(별도 클래스·별도 프로필 — CA 볼륨 파라미터 충돌 없음). 스파이크 중 active=true, 종료 시 active=false → 글로벌 블룸(PhaseG2) 완전 회복(잔존 억제). 31-V3 패턴 승계(인스턴스 재사용·리스타트)
- 훅: 패망 PERFECT(골드 라이트 직후) + 크리티컬 한정(CombatFXGate) — peak 2.2/0.12s. B2 채도 드랍=기본 미발화(Play 판단 항목)
**Phase D 2차 파티클**:
- FxDebrisSpawner.cs 신설 — 크리/Heavy(피해≥15) 한정 파편 3~5조각: StableHash 결정론 포물선+지면 1회 바운스(감쇠 0.45)+말기 축소, 동시 버스트 상한 4, 비주얼 전용(콜라이더 부착 금지), 색=Core×0.45 단일 소스 유도
- FxEmberRise(동일 파일) — 사망 디졸브 지점 상승 불티(Edge 단일 소스, ApplyTo 정규화, 버스트 전용, 유한 수명 1.2s)
**중복 셀프감사로 제외**: C1 트레일 테이퍼=45차 P4에서 이미 구현(3키 폭 곡선+그레이디언트+캡 둥글림 확인) / D3 잔연=29-M3-2 플립북 스모크가 이미 커버 / C2 몬스터 잔상=23-X3 아크가 잔상 역할(중복 리스크) — 전부 기각, 보고만
**QA**: 계약테스트 FxImpactLightPhaseATests 5건+FxSecondaryPhaseBDTests 5건. 주석 "Random" 오탐 1건 재기화(100차 함정 3회째). 컴파일 error CS 0 · EditMode **2369/2359/10**(baseline 10건 1:1) · 33-A 5/5·33-B/D 5/5
**다음**: Play 통합 판정 — ①히트 순간 실광 펄스(어두운 실내 체감) ②패망 PERFECT 골드 라이트+블룸 ③크리 블룸 ④사망 소멸 광+불티 ⑤파편(크리/Heavy) ⑥수차·비네트 누적. 강도 상수(라이트 1.8~2.6·블룸 2.2·수차 0.35) 트림 대기. 커밋 별도 승인 대기.
---
## 11-13 19:05 — 무번호 32 영지 실내 타일 씬 통일 — 공통 석타일 단일 소스 (2026-11-13 19:05)
**사용자 지시**: "같은 씬 내부에서 방마다/구역마다 타일이 나뉘면 통일성이 없어 보인다" — ①영지씬 실내 타일 통일 ②던전씬 내부 타일 = 가장 많은 한 타일로. 카펫/러그 오버레이 유지 확인. 계획서: docs/plans/2026-11-13_no32-interior-tile-unification-plan.md
**조사**: 던전은 156차에서 이미 흙 단일화 완료(지하 5층 전부 CreateCellFloor→CreateDirtFloor — 유일 변주=158차 스폰→포탈 돌판 오버레이, 기능 안내라 유지). 영지 실내 8빌더가 제각각(성=국가별 GenerateCastleFloor*·집/공방/교회/상점=전용 텍스처·외양간/주점/식당=평면 목재색).
**수리(무번호 32)**:
- InteriorQualityBaseline 신설 헬퍼 — GetCommonInteriorFloorMat()(무번호 15 성 석타일 팔레트 (0.38,0.37,0.35)/(0.26,0.25,0.24) 승계, 국가 키 폐기) + CreateCommonInteriorFloorMat(w,d,owner)(공유 캐시 복제 + mainTextureScale=(w,d) — IndoorBuilder UV 0..1 쿼드에서 1m 타일 정합, 캐시 직접 변조 금지)
- 8빌더 교체 — 성(PlayerCastle)/집(House)/공방(CraftHouse)/교회(Church)/상점(Shop)/외양간(Barn)/주점(Tavern)/식당(Restaurant) 바닥 전부 공통 석타일. 구 전용 바닥 텍스처 생성·머티리얼·House matTracker TrackTexture 제거
- 성 국가 계약 — 바닥만 폐기, 벽 텍스처(GenerateCastleWall*) 국가 계약 유지(국가색=벽·깃발·장식)
- InteriorFurnishingPass castleStone_{nationStyle} → 공통 위임(카펫/러그 등 기능 오버레이 무수정)
- 제외 확정 — 던전(156차 흙 단일화 승계)·이계(잿빛 흙)·아레나/피트(국가색 모래 정체성)
**QA**: 계약테스트 InteriorTileUnificationTests 4케이스. 컴파일 error CS 0 · EditMode **2359/2349/10**(baseline 10건 1:1) · 32 4/4
**다음**: Play — 영지 전 실내(성/집/공방/교회/상점/외양간/주점/식당) 동일 석타일+1m 밀도 체감. 커밋 별도 승인 대기.
---
## 11-13 18:10 — 무번호 31-V3 크로매틱 수차 스파이크 — 화면 계층 마지막 조각 (2026-11-13 18:10)
**사용자 지시**: "진행" — 무번호 30 이후 잔여 중 구현 가능 항목(무번호 23 계획서 V3 판단 항목) 전환 구현. 계획서: docs/plans/2026-11-13_no31-chromatic-spike-plan.md
**V3 수차 스파이크(채택 판단을 위한 구현 — 보수 기본값)**:
- ScreenChromaticFX.cs 신설 — 런타임 Volume(isGlobal, priority 10) + 절차 VolumeProfile에 ChromaticAberration 오버라이드만 추가(IndoorPostProcessController 선례 — 기존 글로벌 프로필 무접촉, 에셋 파일 없이 코드 상수가 진실원)
- Spike(cam, peak, total): intensity 0→peak→0 스파이크(전체 0.1s, 진입 0.02s 상수), 인스턴스 1개 재사용(누적 금지)+스파이크 중 재호출=타이머 리스타트, unscaled 시간(히트스톱 중 재생), EditMode 무동작 가드, try/catch(전투 방해 금지), OnDestroy 프로필 파괴(누출 금지), Random 0건
- 훅: PlayerCombat 패망 PERFECT wave(0) — V2 골드 비네트 직후 ScreenChromaticFX.Spike(vc, 0.35f, 0.1f) 1줄. GOOD 미발화(과자극 최소화 — Play 판정 후 등급 확장 판단)
**QA**: 위임 타임아웃 → 64차 폴백(부모 직접 — 서브 착지분 ScreenChromaticFX+훅 검증 통과, 테스트는 부모 작성). 계약테스트 ScreenChromaticV3Tests 4케이스(잠금 1건 정렬: private→public const). 컴파일 error CS 0 · EditMode **2355/2345/10**(baseline 10건 1:1) · V3 4/4
**다음**: Play 통합 판정 — PERFECT 패링 순간 수차 체감(과자극 시 peak 0.35 상수 1줄/제거, 미달 시 상향). 보류 유지: 22-P3 Unlit(Play 비교)·S1-4 지면 틴트(지형 조회 API 설계 확정 필요)·VFX Graph(기각)·W2 구조 변경(보고 완료)
---
## 11-13 16:30 — 무번호 30 VFX 잔여 착지: D-4 디졸브 + S4 선택 링 호흡 (2026-11-13 16:30)
**사용자 지시**: "다음 VFX 계획으로 넘어가고 텔레그램으로도 알려줘" — 텔레그램 발송 완료(message_id 27882, Bot API 우회 — send_message 간헐 버그 스킬 절차 준수)
**계획서**: docs/plans/2026-11-13_no30-vfx-dissolve-selection-pulse-plan.md 신설(보류 명시: V3 수차·VFX Graph·S1-4·P4)
**D-4 디졸브(사망/소멸)**:
- FxDissolve.shader — 셰이더 내 해시 노이즈(텍스처 불필요·결정론) + 임계 소거 + 엣지 하이라이트(URP AlphaTest·CBuffer)
- FxDissolveUtility — 바디 머티리얼 인스턴스 교체(_BaseMap/_BaseColor 승계) + 0.9s 램프 + 완료 시 렌더러 비활성·인스턴스 파괴(누출 금지), EditMode 가드는 스왑 전 차단
- AnimalAI.StartDeathSequence 훅 — 엣지=FXPalette.MonsterDanger 단일 소스, 기존 눕힘/sink/파괴 시퀀스 무손상(EA2 유지), 셰이더 부재=기존 연출만(폴백)
**S4 선택 링 지속 호흡**:
- SelectionRingController.UpdateSelectedBreathing — _TeamColor 알파 0.82~1.0×SpawnPulse(1.4s 주기), 인스턴스 전용 기록+Play 전용 가드 → 84차 잠금(AreEqual 상수) 무영향, 19-C1 1회성 펄스 무수정
**QA**: 컴파일 error CS 0 · EditMode 2351/2341/10(baseline 1:1) · 30 6/6 · 주석 Random 오탐 2건 재기화(100차 함정)
**다음**: Play 통합 판정 — 장비(P13 덩치 수학) + 디졸브 사망 연출 + 선택 링 호흡 일괄. 커밋 별도 승인 대기.
---## 11-13 15:50 — 무번호 29-P13 장비 덩치(bulk) 수학 — 영상 판독 기반 오염 강건화 (2026-11-13 15:50)
**사용자 지시**: 장비착용 테스트 영상(Screenshots/장비착용 테스트.mp4, 23.1s)을 에이전트가 직접 판정하고 수리.
**영상 판독(ffmpeg 프레임 추출 14컷 + 두상 크롭 확대 + 비전 판정)**:
- f_1/f_10 전신: 투구 측면 이탈, 갑옷 전방 쏠림, 방패 팔 클리핑, 장갑/부츠 소폭 유격
- 두상 크롭(f_10 확대): 투구 폭=머리폭 **88~90%(크기 자체는 근접 적정)** — 그러나 투구 중심이 머리 중심에서 **머리폭의 58% 측면 오프셋**
- **원인 확정**: 투구 메시 자체 비대칭(뭉치가 bounds 한쪽에 쏠림 — GLB 오염 정점) → SnapVisualToBone이 bounds 중심을 앵커에 두니 "보이는 뭉치"가 옆으로 밀림 + NormalizeVisualScale도 오염 bounds 기준이라 실제 뭉치가 목표보다 작게 나옴("여전히 작다"의 기계적 근거)
**수리(29-P13) — 오염 강건 덩치 수학(전 슬롯 공통)**:
- TryMeasureVisualBulk: 렌더 정점 샘플(≤1500) 축별 **3~97% 백분위 스팬**(쏠림 정점 트림) + **중앙값 중심**(SMR=BakeMesh, 정적=mesh.vertices, 비가독 폴백)
- 부착 Snap 직후 **덩치 재중심화**: (bounds중심 − 덩치중심) 차이만큼 이동 → 보이는 뭉치가 앵커에 안착
- NormalizeVisualScale/MeasureVisualWorldMaxDim(재정규화 측정) 모두 bulk 기준으로 교체 → 정규화 예산이 실제 뭉치에 쓰임("작다" 수리) + 측정-타깃 관례 일치(재정규화 진동 추가 억제)
**QA**: 컴파일 error CS 0 · EditMode **2345/2335/10**(baseline 1:1) · P13 4/4 · 잠금 갱신 1건(P6 필터→덩치 측정기 이동, null 가드 결합형)
**다음**: Play → 투구가 머리 중심에 안착 + 뭉치 크기 체감. 크기 미달 시 HelmetSizeTrim(1.25) 추가 조절.
---## 11-13 15:20 — 무번호 29-P12 타격 장비 이동 수리(쿨다운 지속 추적) + 트림 2차 승격 (2026-11-13 15:20)
**사용자 보고**: "타격 입을수록 장비 위치가 이동 / 여전히 헬멧 작고 몸통도 더 앞쪽"
**14:16 Play 판독 — 근원 특정**: 재정규화가 진자운동(Helmet ×1.32성장→×0.89수축, Armor ×0.90, Gloves ×1.13→×0.90...) = 본 스케일/포즈가 세션 내내 변동(피격 플린치·애니)하는데 장비 월드 크기=로컬×본스케일이라 **정적 1회 보정이 매번 실격** + 인스턴스 1회 가드 소진 후 무방비. 히트 리코일=카메라 경로, 키검증=부팅 1회 통과(1.88m) — 배제 확정.
**수리(29-P12)**:
- 재정규화 가드: 인스턴스 1회 소진형(HashSet) → **인스턴스별 쿨다운 1.5s 지속 추적**(Dictionary) — ±10% 문턱·부팅 3s 정착 게이트 유지, 키 문자열 형상 유지(P7 잠금 보존). 이제 피격 후 변동도 최대 1.5s 내 자동 재정렬
- 헬멧 트림 2차 승격: 1.15 → **1.25**(+25%, 기대 타깃 ≈0.89m)
- 갑옷 전방 2차 승격: 0.05 → **0.10×BodyScale**(≈15cm)
**잠금 갱신 4건**(계약 진화): P6 가드 캐시(HashSet→Dictionary) / P7 가드 의미 주석 / P11 트림 상수·전방 / 문법 무결.
**QA**: 컴파일 error CS 0 · EditMode **2341/2331/10**(baseline 1:1)
**다음**: Play → ①피격 반복 후에도 장비 크기/위치 유지(자동 재정렬) ②헬멧 0.89m 체감 ③갑옷 전방 15cm. 미달 시 상수 1줄 트림 즉시.
---## 11-13 14:55 — 무번호 29-P11 사용자 피드백 트림(헬멧 +15% / 갑옷 전방 8cm) (2026-11-13 14:55)
**사용자 피드백**: "헬멧은 조금 더 크게, 몸통은 조금 더 앞쪽으로 당기면 좋을 것 같아"
**수리(29-P11)**:
- 헬멧 크기: GetTargetSize 읽기 경로 트림 HelmetSizeTrim=1.15(+15%) — 원값 잠금(0.34f×BodyScale) 무손상, 부착/P6 재정규화 양쪽 동시 일관(기대 타깃 0.71→0.82m)
- 갑옷 전방당깨기: 폴백 앵커에 `+ pFwd×(0.05×BodyScale)`(≈8cm, 페이싱 방향 — Mask/Bag 선례 동일 경로), 수직 성분(0.15×BodyScale) 무손상
- 신규: ArmorTrimP11Tests 2케이스
**QA**: 컴파일 error CS 0 · EditMode **2341/2331/10**(baseline 1:1) · P11 2/2
**다음**: Play → 헬멧 크기/갑옷 전방 체감. 미달 시 상수 1줄 트림(HelmetSizeTrim/0.05f) 즉시 조절.
---## 11-13 14:40 — 무번호 29-P10 투구 앵커 복원(사용자 승인 이력 위치) (2026-11-13 14:40)
**사용자 보고**: "여전히 장비 부착 안되어있긴한데 잔여 계획이랑 장비 부착 함께 진행"
**14:01 Play 판독(P9 빌드)**: `[29-P9] headTop=3.22 span=2.43 helmetH=0.56 anchorY=3.02 (boneY=1.81)` — 타이트 필터(0.06×span)로도 스킨 최대 3.22. 같은 세션 재피팅(10004행)에서는 동일 필터가 2.59 — **상부 본 추종 오염 정점(창 정점 계열) 포즈별 변동 실측** → 스킨 두상 측정 신뢰 불가 판정.
**결정적 근거**: 12:51 라운드(앵커 2.21=105차 공식)에서 사용자 "거의 맞는데 작은 느낌" — 위치 승인 이력 존재. 부유는 P8이 앵커를 3.02로 이동시킨 뒤 발생.
**수리(29-P10)**: 투구 앵커 = 105차 공식(본+0.26×BodyScale) 단일 배선 복원(사용자 승인 이력 위치) — 크기(×k)와 P9 진단 로그(참고 데이터)는 유지. 스킨 랜드마크는 k 산출+진단 전용으로 축소.
**잔여 계획 상태**: D/M2/S2/P2 전량 착지(전 라운드) 완료 — D-4 디졸브·P4 화이트프레임 = 계획상 Play 판정 후 확정 항목 / S1-4 지면 틴트 = 지형 타입 조회 API 부재로 보류(전수 재검증 — TryGetPlotAt/RoadAt/GetSurfaceKind 등 0건).
**QA**: 컴파일 error CS 0(보간식 삼항 괄화 수리 1건) · EditMode **2339/2329/10**(baseline 1:1) — P8/P9 잠금을 P10 계약으로 갱신(기각 근거 주석 동봉)
**다음**: Play → 투구가 12:51 위치(본+0.40m)에 ×k 크기(≈0.71m)로 부착되는지 판정. "작다/떠있다" 잔여 시 본 오프셋 상수 1줄 트림(별도 확정).
---## 11-13 14:20 — 무번호 29-P9 투구 부유 수리 + D/M2/S2/P2 잔여 VFX 전량 착지 (2026-11-13 14:20)
**사용자 지시**: "투구가 머리에 쓴 것처럼 안 보이고 공중에 떠있어 — 한 번 수리하고 뒷 진행(VFX 잔여 계획)까지"
**P9 투구 부유 수리**: 13:27 Play 실측(anchor 3.01/본↔중심 1.21m) → 0.28×span 필터가 부착 시점 포즈의 팔/손을 두상으로 오포함(headTop 역산 3.22=스킨 최대) 확정 → 중심축 필터 0.06×span(머리 반폭) + 두상 유효성 게이트(상반부, 실패 시 105차 폴백) + [29-P9] 앵커 진단 로그
**Phase D(셰이더 자산)**:
- D-1 FxFlipbookGenerator — 해시 FBM 결정론 연기 플립북(4×2 아틀라스 512×256, 6프레임, top-down 행 정합, 캐시 1회, Random 0건)
- D-2 FxMaterialLibrary — 소프트 파티클(URP Particles/Unlit + _SOFTPARTICLES_ON, Sprites/Default 폴백) / DustPuffMat·ScorchEmberMat 스왑(EnsurePuffTexture 잠금 보존)
- D-3 FxFresnelRim.shader(URP 가산 블렌드·CBuffer) + FxRimOverlay(스키닝 본 공유 제2 SMR + MeshRenderer 정적 오버로드, 인스턴스 누출 금지, 미지원=null 폴백)
**Phase M2(몬스터)**: M1-2 충전 림(fill≥0.75, 정예=ElitAura/일반=MonsterDanger) / M2-1 히트 림 플래시 0.12s / M3-2 사망 플립북 스모크 승격(DeathSmoke 프리팹 폴백 유지, 머티리얼 유한 수명) / M4-2 정예 오라 림 틴트 동기+SpawnPulse 이징
**Phase S2(발자국)**: S1-2 스텝 동기(드라이버 CurrentSpeed 조회만 — 애니 시스템 무수정) / S1-3 소프트 먼지(D-2 경유) / S2-2 강도 슬롯(단발 소비 — 시작 킥=거리·스텝=속도비·도착=직전 속도, 잠금 시그니처 전부 보존) / **S1-4 보류**(지형 타입 조회 API 부재 — 계획 전제 "기존 조회 재사용" 불성립, 별도 확정 필요)
**Phase P2(패링)**: P1-2 무기 플래시 림(ParryGold, 캡처/원복 무손상) / P2-2 PERFECT/GOOD 궤적 차등(길이 ×1.25·상승 +0.05 — GOOD 리터럴 보존, 등급 슬롯) / P2-3 불티 소프트(ScorchEmberMat 스왑으로 충족) / P3-2 공격자 백색 림(M1 상충 가드 유지) / D-4·P4 = Play 판정 후(계획대로)
**잠금 갱신 3건**(계약 진화 — P8 선례): P8 두상 필터(0.28→0.06×span) / 22-Q3 크기 편차(강도 배율 k) / S2 도착 강도(입자 레이어 설계). 주석 "UnityEngine.Random" 오탐 2건 수리(100차 함정).
**QA**: 컴파일 error CS 0 · 셰이더 에러 0 · EditMode **2339/2329/10**(baseline 1:1) — D 6/6·M2 6/6·S2P2 7/7·P9 4/4·P8 5/5·P7 4/4·P6 6/6
**다음(사용자 Play 통합 판정)**: ①투구 머리 부착(P9 — [29-P9] 투구 두상 앵커 로그: headTop/helmetH/anchorY) ②장비 비례 크기(P8) ③몬스터 충전/히트/정예/사망 림·플립북 ④발자국 강도 커브 ⑤패링 등급 궤적·무기/공격자 림 — 누적 일괄
---## 11-13 13:35 — 무번호 29-P8 스킨 랜드마크 장비 정합 (2026-11-13 13:35)
**사용자 보고**: "거의 다 왔는데 한번 더 확인하고 장비 맞춰줘"
**P8 프로브 확정(ArmorRigLayoutProbeP8Tests, qa30)**:
- 스켈레톤 전수: 아바타 매핑 정상(mapped=22, 최상단=Head@0.62 ✓ — H2 오배선 기각)
- 스케일 사다리: S=1.000→스킨 0.999m / S=1.552→**스킨 2.407m=×S²**(1.552²=2.409 정확) — **H1 이중 스케일 확정**(SMR.L2W×본.L2W), 봉투/본은 ×S 선형
- 결론: 본-기준 장비는 ×1.552 스켈레톤 기준, 화면 몸통은 2.43m → 장비가 비례적으로 작고 투구가 두상 대신 흉부 레벨에 찍힘(사용자 보고 정밀 재현)
**수리(29-P8)**:
- MeasureSkinLandmarksNow: 부착 시점 BakeMesh(전체 L2W = 화면 진실 관례) → minY/maxY/headTopY(중심축 0.28×span 필터 — 팔 배제)/span
- 크기: k=스킨스팬/1.8m → 부착 NormalizeVisualScale 타깃 ×k + SyncTick 재정규화 타깃 ×GetSkinK(씬 캐시 — 진동 방지)
- 투구 앵커: 스킨 두상(headTopY) − 투구높이×0.35(정수리 피복) — 기존 본+0.26 공식은 측정 실패 시 폴백으로 보존
- 신규: ArmorSkinLandmarkP8Tests 5케이스
**QA**: 컴파일 error CS 0 · EditMode **2316/2306/10**(baseline 1:1) · P8 5/5 · P7 4/4 · P6 6/6
**기대(Play)**: 투구 ≈0.72m/갑옷 ≈1.30m(2.43m 화면 몸통 비례) + 투구가 화면 두상에 피복 부착
---## 11-13 17:05 — 무번호 29-P7 리그 정합 진단 + 재정규화 가드 수리 (2026-11-13 17:05)
**사용자 보고**: "아직 여전히 작아 + 제대로 부착 안 된 느낌"
**12:59 Play 로그 판독(3결함)**:
1. [29-P6] 재정규화 정상 발화(투구 0.44→0.53/갑옷 0.86→0.96) — 그러나 직후 [28-P4b2] 재피팅이 장비를 **재부착**(투구 0.37m — 더 작음), 세션 1회 가드 소진으로 무보정 → 최종 표시 = 가장 작은 인스턴스
2. 투구 본↔중심 0.40m 상존의 정체 = 폴백 공식 `Head본 + 0.26×BodyScale(+0.40m)` — 69차 GLB("Head본=턱") 전제, Heat 리그(Head본=두개골 상단부)에는 과부착
3. **진단 반전**: r.bounds(1.88m)=SMR **본 봉투**값(렌더 아님) — BakeMesh(2.43m)=실제 렌더 피부 → 피부가 본 대비 ×1.35 스트레치 가설 부활(27 라운드 아바타-스킨 불일치 = 최종 뿌리 후보)
**수리(29-P7)**:
- 가드 단위 = 인스턴스(`resize:{scene}:{slot}:{instanceID}`) — 재부착 시 재보정 허용
- [29-P7] 리그 정합 진단 1줄(첫 Helmet 부착 1회): headScale(Head lossyScale 전체)/spineScaleY/footScaleY/headY/bodyBounds(r.bounds min~max)/renderSpan(BakeMesh 렌더 스팬) — 본 스케일 변형 vs 피부 스트레치 판별
- 신규: ArmorRigDiagP7Tests.cs 4케이스
**QA**: 컴파일 error CS 0 · EditMode 2309/2299/10(baseline 1:1) · P7 4/4
**다음**: Play 1회 → `[29-P7] 리그 정합` 1줄 판독 → 수리 방향 확정(본 스케일 수리 vs 피부 스트레치 수리[아바타/바인드포즈 차원])
---
## 무번호 29 — P6 장비 크기 자가 재정규화(작은 느낌 수리) 구현+QA 완료 [자동보고 2026-11-13]
**작업**: 사용자 Play 판정("장비 거의 맞는데 투구와 몸통이 작은 느낌")에 대한 후속 수리 — 29 계획 Phase 0의 잔여(크기 축)
- **P5 전환 성공 확인(11-13 12:51 Play)**: `[29-P5] 정점 측정 무효 — 괴차 29% > 10% → 폴백 상수+본 앵커 경로 강제` 발화 + 후보C(scaleBakeSpan=1.00m)도 탈락 — 부유 해소(사용자: "거의 맞는 것 같다") 확인
- **"작은 느낌" 실측 뿌리**: 부착 로그 — 투구 실측최장축 0.41m vs 계약 타깃 0.528m(=0.34×BodyScale, **78%**) / 갑옷 0.81m vs 0.962m(=0.62×BodyScale, **84%**) — 일관 축소+본별 차이. 원인: `Instantiate(bonePrefab, bone)`으로 본 직속 부모화 후 정규화(그 시점 본 월드 스케일 기준) → **아바타 적용으로 본 스케일이 변하면 장비가 함께 축소**(측정 사가와 동일 리그 특성)
- **P6 수리(ArmorVisualAttachSystem)**: SyncTick ④ 장비 월드 크기 자가 재정규화 — 부팅 3s 정착 게이트(침하감시 +2s 선례) 후, 슬롯 비주얼 월드 최장축이 계약 타깃(GetTargetSize=69차 원값×BodyScale) 대비 ±10% 초과 이탈 시 NormalizeVisualScale 1회 재정렬(MeasureVisualWorldMaxDim 신설 — 이펙트 렌더러 제외). 슬롯당 세션 1회 한정(_resizeKeys — 애니 스케일 변동 오실레이션 방지, 키 선(先)등록 후 로그)
- **기대**: 투구 0.41→0.528m(+29%), 갑옷 0.81→0.962m(+19%) — 계약 설계치 복원. 재판정 후에도 작으면 → P7 트림 상수(Helmet 0.34 등 1줄 튜닝 노브) 별도 확정
- **QA PASS**: 컴파일 error CS 0(중간 CS0117 Does.Contains 오타 1건 즉시 수리) + EditMode 전수 **2305 total / 2295 passed / 10 failed** — baseline 10건 1:1, 신규 6케이스 전부 Passed. 증거: TestOutput/qa29-editmode-results.xml
- **다음(사용자 게이트)**: **Play 재판정** — [29-P6] 장비 크기 재정규화 로그(투구/갑옷) 발화 + 크기 체감. 이후 D(플립북/소프트파티클/프레넬 림)→M2→S2→P2("진행") / 커밋 승인 별도

## 무번호 29 — Phase 0(P5 장비 부착 최종 종결책) 구현+QA 완료 [자동보고 2026-11-13]
**작업**: 계획서(docs/plans/2026-11-13_no29-equip-final-detail-quality-plan.md) Phase 0 — 사용자 3차 보고("장비 부착 아직 안 됨")에 대한 종결책. D/M2/S2/P2는 다음 라운드
- **결정적 증거(11-13 12:24 Play)**: [28-P4b2] 매핑 정합 로그 3수치 — transformSpan=2.43m / rootBoneSpan=1.32m / **refBounds=1.88m** → 캐릭터는 화면상 실제로 정상(계약 +4.4%), 측정 두 후보 모두 실패(과대/과소), 심판이 "덜 틀린"A를 근소 차 채택 → 피팅이 허측값 따라 투구 0.40→0.97m 부유. 본(skeleton)은 정확(1.8m 비율)
- **P5-a 측정 후보 C**: SMR 이중 베이크(무스케일+useScale) + TRS 무스케일 행렬 — 3후보 referee(r.bounds 오차 최소, 동률 시 A 규약) + 로그 확장(scaleBakeSpan 포함, smr-map 키 유지)
- **P5-b 종결책(r.bounds 교차검증 게이트)**: 채택 매핑 전신 스팬 vs r.bounds 전신 ±10% 초과 → **정점 측정 무효 판정(fullValid=false + LastVertexUnreliable) → 기존 "측정 전체 무효→모든 부위 invalid→폴백 상수+본 앵커" 경로(105차 F2 선례) 자동 발동** — 신뢰 불가 측정으로 계약을 깨지 않는 구조. rFull 누적은 27-P1 제외 정책 통과 렌더러만
- **P5-c 재피팅 연동**: LastVertexUnreliable 시 재피팅 스킵(refit-skip: 키 — 폴백 경로 부착 중 재피팅 무의미 조기 종결)
- **위임 이슈**: 코드 위임 max_iterations(50콜 read 루프 사망 0산출) → 64차 상설승인으로 부모 직접 구현(6패치, 앵커 사이트별 assert, brace 345/345). 테스트는 위임 성공(선잠금 방식)
- **QA PASS**: 컴파일 error CS 0 + EditMode 전수 **2299 total / 2289 passed / 10 failed** — baseline 10건 1:1. 중간 페차 1건: [28-P4b2] Source_MapDiagLog가 구 로그 접두사 잠금(94차 "소멸 예정 문자열") → [29-P5]로 정밀 갱신 후 필터 런 6/6 GREEN. 증거: TestOutput/qa29-editmode-results.xml·qa29f-editmode-results.xml
- **다음(사용자 게이트)**: **Play 판정 최우선 권장** — [29-P5] 정점 측정 무효 로그 발화 + 폴백 경로 전환 + 투구 본↔중심 ≤0.25m·⚠ 0건·fullBody r.bounds ±8% 확인. 이후 D(플립북/소프트파티클/프레넬 림)→M2→S2→P2 진행("진행") / 커밋 승인 별도

## 무번호 28 — Phase M(몬스터)·S(병사·RTS)·P(패링) 구현+QA 완료 — 28 라운드 전체 종결 [자동보고 2026-11-13]
**작업**: 계획서 나머지 Phase 전부 — Phase 0·U에 이어 M/S/P 구현+독립 QA. 사전소화 지시서(좌표 박제) 4병렬 중 3건 단발 성공(M-A/M-B/S-A/S-B — M·S는 전부 성공), P 병렬 2건 타임아웃 → P-A는 전량 착지(마커 grep 판별)·P-B 0산출 → 64차 상설승인으로 부모 직접 완수(SpawnParryReflectWisps+InitReflect+테스트 2파일)
- **M1 충전 글로우(AnimalAI)**: windup 루프 t 연동 몸 emissive 램프(FXPalette.MonsterDanger × FXMotion.Progress ×1.6) — GLB 몬스터 대응 GetComponentsInChildren 순회, 해제 3경로(conclude/cancel/die → black — EmissiveIsBlack 선례)
- **M4 정예 리듬 오라(AfterlifeEnemy)**: 정예 소스 실측 확인(AnimalAI 몬스터는 정예 개념 0 — 위조 배선 회피) → `Level >= 5` 게이트, 1.2s 간격 발밑 SpawnImpactRing(반경 0.9/0.55 교대, FXPalette.ElitAura=AttackTelegraphIndicator EliteColor 미러, elite:true)
- **M3 사망 지면 링(DeathVFX)**: sparks/smoke 1단에 지면 링 1겹 추가(FXPalette.MonsterAccent, scale 비례 반경) — EA2 계약 무손상, 예산 게이트 안 배치
- **M2 피격 리코일(CombatCameraEffects)**: FOV 킥 제외(카메라 계약/Cinemachine 소유권 리스크) → 위치 기반 리코일 — PlayDamageRecoil(hitPos, amount) + OnPlayerDamaged 부트스트랩 구독(ScreenVignetteFX 23-V1 선례, _damageRecoilHooked 가드), useImpulseSource=false 고정. ⚠기록: directionBias는 셰이크 루틴 경로 미소비(임펄스 전용) — 방향성 리코일은 Play 판정 후 필요 시 ShakeRoutine 확장
- **S1 스텝 리듬(GuardPlaceholder)**: 0.6m 게이팅 유지 + 발자국 좌우 교차 오프셋(횡축 ±0.12m 결정론) — 스폰 수 증가 없음
- **S2 중복 발견**: 시작 킥은 이미 22-Q2로 구현돼 있었음(잠금 테스트 확인) — 원형 완전 보존+[28-S2] 마커만 부여(중복 제안 자가 차단)
- **S3 명령 스태거(RTSCommandSystem)**: IssueMoveCommandCore 유닛별 0.04s 지연(세대 가드 — 새 명령 시 대기분 폐기, isActiveAndEnabled 폴백, FormationSpread 분산 구조 보존). 동기화 계열(Synchronized*)은 목적상 미적용. S4 선택 링은 84차 계약 밀집(잠금 5파일)으로 본 라운드 제외 — Play 판정 후 별도 확정
- **P1 무기 플래시(PlayerCombat)**: 패망 wave0에서 CurrentVisual 렌더러 인스턴스 emission 펄스(FXPalette.ParryGold ×2.2, 0.15s unscaled, 캡처값 원복 — 기존 발광 무손상)
- **P2 리플렉 궤적(ShockwaveRingFX)**: SpawnParryReflectWisps 신설 + WispRunner InitReflect(기존 아크 무손상 — reflect 시 낮은 바운스 궤적, 결정론) — wave2에서 흡수점→공격자 방향(-kbDir) 배선
- **P3 공격자 반응(AnimalAI+PlayerCombat)**: PlayParryFlash — 백색 ×1.8 펄스 0.1s→black(M1 인프라 재사용, windup 재개 시 자가 치유) — PERFECT 패망 시 넉백 직후 배선
- **미채택/연기(기록)**: 프레넬 림·플립북 연기·Dissolve·소프트 파티클·VFX Graph → Phase D 라운드(별도 승인), P5 BulletTime 커브(옵션), 화이트 프레임/P4 → Play 판정
- **QA PASS**: 컴파일 error CS 0 + EditMode 전수 **2293 total / 2283 passed / 10 failed**(고아 런 2293/2282/11 — 유일 실패=내 테스트 부정 잠금이 기존 J1 골드 리터럴 오탐(100차 함정 3회째) — 루틴 정의부 앵커 스코프로 수리 후 필터 런 6/6 GREEN) — baseline 10건 1:1, 신규 24케이스 전부 Passed. 증거: TestOutput/diff-28sp-QA.md·qa28pf-editmode-results.xml
- **다음(사용자 게이트)**: **Play 통합 판정** — ①[28-P4b2] 매핑 정합 로그로 fullBody 1.8m 수렴·투구 부유 해소 ②M 충전 글로우/정예 오라/사망 링 ③S 스텝 먼지/스태거 ④P 무기 플래시/리플렉/공격자 플래시 ⑤U 표준 텍스처 체감 + 기존 누적(19~27) 일괄 ⑥커밋 승인(누적)

## 무번호 28 — Phase 0(P4-b 장비 뿌리 수리) + Phase U(통일 비주얼 언어 기반) 구현+QA 완료 [자동보고 2026-11-13]
**작업**: 계획서(docs/plans/2026-11-13_no28-equip-root-monster-move-parry-quality-plan.md) — 사용자 요구 ①장비 부착 잔여 ②몬스터/병사·RTS/패링 VFX 고품질 ③통일감 ④세밀 묘사. 순서: Phase 0 → U → (M/S/P 다음 라운드)
- **P4-b1 프로브(2단)**: EditMode heat 프리팹 실측 — ①bind 0.403m×계층 2.871=월드 1.156m(정규화가 읽은 1.16m의 정체 — stale 아님) ②컨트롤러+Update 120스텝 시간계열 불변(0.999m, 본-메시 정합) → Play 전용 요인 확정
- **P4-b 근본 확정**: 플레이어 heat SMR rootBone=**Hips**(병사는 Root — [DD2] L3097/3591) → ArmorVisual 측정이 BakeMesh 정점을 r.transform L2W로 매핑("transform L2W 근사" 주석) → Play 애니 상에서 정점 공간 불일치로 **2.43m 허측** → 피팅이 허측값 따라 투구 0.40m 부유. 침하감시(r.bounds — Unity 자체 계산)가 1.8m로 정합이었던 것이 교차 증거
- **P4-b2 수리**: ①PlayerBodyMeasure SMR 경로 — 매핑 후보 2개(transform/rootBone)를 r.bounds 오차 기준으로 채택 + [28-P4b2] 매핑 정합 로그(WarnOnce smr-map:) ②SyncTick 과도기 재피팅 자가교정(fullBody>2.16m → 1.0s 후 세션 1회 재피팅, static 키 멱등 가드) — 측정공간 버그와 초반 과도 스트레치 양 가설 동시 커버
- **U-1 팔레트 진입점**: 기존 FXPalette(45차 BOTW) **확장** — 신규 리전: MoveCommand/SelectedGuard/AttackCommand/RallyCommand=CommandRingPresentation **위임**(값 복제 금지) + MonsterDanger/Dust/ParryGold/ParryCyan/SoldierRibbon/MonsterAccent=출처 리터럴 미러(좌표 주석). 기존 잠긴 상수 이관 금지 원칙 유지
- **U-2 텍스처 표준 상향**: ShockwaveRingFX 밴드 64×16 2옥타브→**256×64 4옥타브**, 먼지 128 3옥타브→**256×256 4옥타브**+가장자리 소프트 강화 — 결정론 해시/색 승수/FXBandingRing 속성/X2 예산가드 무손상
- **U-3 모션 언어**: FXMotion 신설 — Progress(smoothstep+마지막 20% 임박 가속 — 20-I3 수식 계승)/Afterglow(easeInCubic — 20-J3)/SpawnPulse(easeOutBack 오버슈트 ≈8% 절제) + 수명 규약 상수
- **U-4 감사(보고)**: 링 패밀리=FXBandingRing 통일 완료(폴백 리터럴 계약 유지) / 파티클 Sprites/Default 잔존 — 소프트 파티클은 M 라운드(D-1·M3 연계) 과제 / DecalSpawner URP/Lit=DecalProjector 호환용(정상)
- **신규 테스트 12케이스**: ArmorBodyMeasureSpaceTests 6 + FxStandardTextureTests 6 + FXPaletteAndMotionTests 6 = 18건(27~28b1 누적 포함 전수 확인)
- **QA PASS**: 컴파일 error CS 0(부모 게이트 다수) + EditMode 전수 최종 **2257 total / 2247 passed / 10 failed** — baseline 10건 1:1(양방향 차집합 0), 신규 실패 0(중간 1건: 부정 잠금이 자기 주석 "Random" 리터럴 오탐 — 100차 함정 재확인, 주석 패러프레이즈+코드형 잠금으로 수리 후 필터 런 6/6 GREEN). 증거: TestOutput/diff-28b2-QA.md·diff-28u-QA.md·qa28u2-editmode-results.xml
- **위임 이슈 기록**: 구현 위임 3회 타임아웃(600s×2 U-A/U-B 1회 — U-B는 코드+테스트 착지 후 사망, U-A 2회 0산출) → 64차 상설승인("타임아웃 시 부모 직접")으로 U-A를 부모 직접 완수(FXPalette 확장/FXMotion/테스트). FXPalette 기존 파일 존재(45차)를 1차 브리프가 모른 채 "신설" 지시한 것이 조사사망 원인 — 브리프 품질 교훈
- **다음**: M(몬스터)→S(병사·RTS)→P(패링) 라운드 + **Play 통합 판정** — [27-P4]/[28-P4b2] 매핑 정합 로그로 fullBody 1.8m 수렴·투구 본↔중심 ≤0.25m 확인 + U 표준 텍스처 체감 — 사용자 승인 대기 / 커밋 승인 별도 게이트

## 무번호 27 — 장비 부착 정합성(측정 오염 차단·진단 보강 착지 확인 + 팁-끝 진단 오경보 수리 + heat 정규화 오측정 진단) [자동보고 2026-11-13]
**작업**: 계획서(docs/plans/2026-11-12_no27_equip-attach-fix-plan.md) — P1/P3은 직전 세션에서 착지된 상태로 본 라운드에서 신규 Play 증거(Editor.log 11-12 23:10)로 효과 재검증 + P2 수리 + P4-a 진단 신설
- **P1/P3 착지 확인(직전 세션 산출)**: ArmorVisualAttachSystem [27-P1] 비활성GO·placeholder 프리미티브 측정 제외 + [27-P3] measure-diag 제외 목록/오염 원인 출력 — 신규 로그 실측: 제외 렌더러=3(wood_spear·WeaponSwingTrail·BlobShadow_Quad), fullBody **4.34m→2.43m** 개선. 기존 계약테스트 Source_P1×3·Source_P3 1건 기존 그대로 잠금 유지
- **잔여 뿌리 신규 특정(P4 근거)**: measure-pollute 렌더러별 정점 샘플 로그가 오염원을 단일화 — "input y[0.79~3.22] n=1871" = heat FBX 모델 메시 자체가 2.43m(계약 ×1.35). heat 정규화가 1.16m를 읽어 ×1.552 과스케일(실제 자연 ≈1.557m) → 투구 앵커 y=2.21m·Armor scale보정 ×2.50/×2.86의 잔여 뿌리. 오측 메커니즘(stale 월드 bounds vs 바인드-애니 포즈 불일치) 미확정 — **확정 전 수리금지** 원칙으로 진단만 선설치
- **P2 수리(WeaponEquipManager.cs)**: 팁-끝 진단 오경보 수리 — 기존 판정(tipIsFar = handDistMax >= handDistMin)은 "칼끝=max 코너" 암시 가정으로 wood_spear(피벗=창두, 칼끝=min 끝=손에서 먼 쪽 1.08m)에서 ⚠뒤집힘 오경보. **DATA 모드 선택 로직 자체는 정상**(그립 정렬(DATA) gripT=0.67·blade=desired 일치 실측) → 실제 tipWorld 손 거리 vs 메시 중심 손 거리 비교로 진단만 수리. _gripTable/GripEnd/ComputeWeaponDataGrip 무수정(테이블 플립 회귀 방지 — T4 잠금)
- **P4-a 진단(TestPlayerAnimatorBoot.cs)**: heat 정규화 직전 이원 실측(world vs localBounds) + 정규화 후 1프레임 경과 재실측 로그 [27-P4] — 행동 변경 전면 금지(NormalizeSoldierHeight 인자 원형 유지 T3 잠금), 다음 Play에서 오측 메커니즘 확정 후 별도 라운드에서 수리
- **신규 계약테스트 11케이스**: WeaponTipDiagnosisPhaseP2Tests 6건(양성 비교기·로그 노출·구 코너 비교 부정 잠금·grip 테이블 원형·DATA 분기 원형·마커) + HeatNormalizeDiagPhaseP4Tests 5건(이원 실측·사후 실측·정규화 호출 원형·heatSwapped 게이트·마커)
**QA PASS**: 부모 컴파일 게이트 error CS 0(compile.log EXIT=0) + 독립 QA EditMode 전수 **2237 total / 2227 passed / 10 failed** — baseline 10건 1:1(양방향 차집합 0: AERO·Cooking·H2_Catalog·Instrument×3·InvadedLord×2·RecipeCatalog·ThemeUSS), 신규 회귀 0, 신규 11/11 Passed. 증거: TestOutput/diff-27-QA.md·qa27-editmode-results.xml·qa27-editmode.log(부모 XML 직접 파싱 재검증 일치)
**다음**: Play 판정 — ①[27-P4] 이원 실측 로그로 heat 오측 메커니즘 확정 → P4-b 수리 별도 라운드 ②P2 팁-끝 진단 정상 판정 로그 확인 ③무번호 19~25후속 FX 체감(누적) — 사용자 승인 대기 / 커밋 승인(누적) 별도 게이트

## 무번호 25 후속 — 전투 VFX 4항목(A1~A4) + 계약테스트 수리 [자동보고 2026-11-13 02:15]
**작업**: 계획서(docs/plans/2026-11-12_combat-vfx-followup-plan.md) A1~A4 — 사용자 요구 ①전리품 등장 연기 무한 반복 ②장비 과대 스케일 ③적색링 절반 축소 ④이동/몬스터/기타 VFX 고품질화
- **구현**: A1 AnimalAI.cs+DeathVFX.cs — 사망 FX Instantiate(deathEffectPrefab) 폐기 → DeathVFX.PlayDeathVFX 유한 수명 경유(PlayAllParticleSystems 수명 반환+Destroy 예약) / A2 ArmorVisualAttachSystem.cs — [25-A2] 부위별 배율 1.0 고정(이중 스케일링 차단)+실측 bounds 최장축 로그 / A3 AttackTelegraphIndicator.cs — [25-A3] radiusVisualScale 기본 0.5 오버로드 체인(시각 링만 축소, 판정 사정거리 무변경) / A4 ShockwaveRingFX.cs — SpawnImpactRingCore 신설(HDR emissive+깊이 페이드+EliteColor 변주, FXBandingRing.shader _DepthFadeDist)
- **계약테스트 수리**: EA2_AnimalAI_DeathEffect_Uncommented → EA2_AnimalAI_DeathFX_DeathVFXPath (신계약: DeathVFX 경유 잠금) / X2_FxBudgetGuard (등가 산술 등록 _activeRingCount = _activeRingCount + 1 + 가드 2곳 계약 갱신)
- **QA PASS**: 컴파일 error CS 0(compile_check_r25.log + 본 런) + EditMode 전수 **2226 total / 2214 passed / 10 failed** — baseline 10건 1:1(양방향 차집합 0), 수리 테스트 2건 Passed, 신규 회귀 0. 증거: TestOutput/diff-25followupb-QA.md
- **다음**: Play 체감 판정(적색링 절반·장비 스케일·사망 연기 1회·VFX 품질) + 커밋 승인 — 사용자 승인 대기

## 무번호 24 — 전투 VFX 흔적 생명력(FA1 데칼 페이드아웃) [자동보고 2026-11-12]
**작업**: 계획서(docs/plans/2026-11-12_combat-vfx-environment-camera-plan.md) Phase FA1 — 데칼 수명 말미 2s 알파 램프다운(DecalSpawner per-instance 머티리얼 — 팝 소멸 금지 선례 C3의 데칼 확장)
- **핵심 발견**: DecalSpawner는 공유 머티리얼 + 즉시 파괴(DecalInstance 4필드) — 수명 종료 시 팝 소멸. FA1 = per-instance 머티리얼 전환 후 알파 램프다운
- **구현**: DecalSpawner.cs — DecalInstance에 instanceMaterial+baseColor 추가 / SpawnDecal `new Material(config.material)` / `FadeOutDuration=2.0f` 상수 / Update 루프 수명 말미 2s 알파 램프다운 / DestroyDecal per-instance 머티리얼 Destroy 추가
- **계약**: DecalInstance/SpawnDecal/Update/DestroyDecal만 수정 — RKLM(CreateScorchTexture 소스 텍스트 계약) 무영향
- **QA**: 컴파일 error CS 0 / EditMode 전수 **2196/2186/10** — baseline 10건 1:1(AERO·Cooking·H2_Catalog·Instrument×3·InvadedLord×2·RecipeCatalog·ThemeUSS), 신규 실패 0. 증거: TestOutput/diff-24-FA1.md
- **다음**: FA2 스콜치 텍스처(크랙 변주) — Play 판정(시각 확인) 후 별도 승인 대기

## 무번호 23 — 전투 VFX 미계층 고도화(화면/공간/시간/타이밍) W/V/U/X 전 사이클 [자동보고 2026-11-12]
**작업**: 계획서(docs/plans/2026-11-12_combat-vfx-missing-layers-plan.md) 구현 — "물체 품질" 이후의 빠진 계층 4종. V3 수차=Play 판단 연기(계획 권장), W2=확인 후 보고(아래), 22-P3 Unlit=이월 유지
- **핵심 발견**: ParadoxTimeStop(PERFECT 패링 시간 정지 — Test_10 한정)이 존재하는데 **시각 언어 0건 실측**(전수 — Visual/Overlay/Vignette 매치 0) — 시간이 멈춰도 화면이 말을 안 하는 최대 기회
- **Phase W 시간 계층**: W1 정지 진입 — ScreenVignetteFX.BeginManual(청록 0.16,0.85,0.78 / peak 0.5 / 진입 0.12s = 램프 동기) — Runner.Run에서 발화(시각 실패해도 시간 로직 무영향 try/catch) / W3 해제 릴 — 유지 구간 종료 시 Release(0.2s 페이드아웃 = BlendOutSec 0.3와 병행) + Runner 소멸 시 ForceEnd 안전망 / W2 확인 결과: 몬스터 FX(텔레그래프 등)는 전역 deltaTime 기반으로 **정지 중에도 계속 애니** — LocalTimeScale만 제어하는 구조(구조 변경은 별도 확정 필요 — 보고만)
- **Phase V 화면 계층**: ScreenVignetteFX 신설(절차 비네트 텍스처 256×256 — 중심 투과 바닥 0.12+가장자리 smoothstep 0.55~1.05 / 카메라 자식 풀스크린 쿼드 — 화이트 프레임 2.4배 선례 / unscaled 시간 / EditMode 무동작 가드) / **V1 피격 비네트 — Core 무수정 설계**: PlayerHealth(Core)를 건드리지 않고 기존 OnPlayerDamaged 이벤트를 Systems에서 구독(RuntimeInitializeOnLoadMethod 부트스트랩 — 피해량 비례 강도 0.32+0.25k) / V2 패링 PERFECT 골드 비네트(0.4/0.05/0.08/0.3) — 시퀀서 wave0
- **Phase U 공간 계층**: U1 깊이 페이드 — **m_RequireDepthTexture: 1 실측(Depth Texture 이미 활성 — 설정 변경 불요)** → TelegraphRing 셰이더에 DeclareDepthTexture include+SampleSceneDepth→LinearEyeDepth→경계 0.12m 소프트 소멸(**fail-open 설계** — 깊이 부재 환경은 무페이드) / U2 밴드 서브 디테일 — 밴드 텍스처에 얇은 2차 광대 2본(0.34/0.66) — 트리플 레이어 구조
- **Phase X 타이밍/예산**: X1 **FxSequencer** — 패링 7요소 동시 발화 → 설계 타임라인 분산(wave0 히트스톱/화이트프레임/비네트/SFX/링 3종 → +0.04s 별/스파크 → +0.06s 팬/위스프 → +0.08s 버스트) — **WaitForSecondsRealtime(unscaled)로 히트스톱 중에도 계층 파열** + EditMode 동기 폴백(테스트 안전)+OnDisable 정리 — 기존 FX 경로 전부 보존(분배만) / X2 FX 예산 가드 — 동시 링 상한 24(확산/수렴/echo 합산, 초과 조용 스킵, Runner OnDestroy 반환) / X3 몬스터 스윙 아크 — 위스프 재사용(적색 계열, 명중 2분기)
- **수리 1건**: `System.Collections.Coroutine` → `Coroutine`(UnityEngine) — CS0234 즉시 수리

**QA**: 컴파일 error CS **0** / EditMode **2186/2177/10 — baseline 10건만 1:1**(U/V/W/X 신규 9건 전부 Passed) / 프로브 RC=0·StepErrors=0 / 비전 판독 2컷 통과 — cfx_03(깊이 페이드 fail-open 검증: 링 정상 렌더+지형 교차 소프트 소멸 "하드 컷 없음")·cfx_09(트리플 레이어 밴드+워블 유지+금색 휘도) / V/W(비네트/패러독스)는 실카메라(원점) 부착이라 프로브 복제 카메라 뷰 밖 — 코드 계약으로 확정, Play(Test_10) 체감 판정
**미완료**: 없음(무번호 23 구현+독립 QA 완료) — 잔여: Play 판정(무번호 19~23 전 FX 체감 — 특히 패러독스 시각 언어/피격·판정 비네트는 Play 전용), P3 Unlit 전환/V3 수차 여부, 커밋 승인 누적(무번호 1~23+계획서 6건), D2 진단 로그 2건 커밋 시 정리 검토

## 무번호 22 — 전투 VFX 최종 폴리시(모션/노이즈/개별 레이어) P/Q/S/T 전 사이클 [자동보고 2026-11-12]
**작업**: 계획서(docs/plans/2026-11-12_combat-vfx-final-polish-plan.md) 구현 — 사용자 지시 "여전히 만족 안 됨, 더 깊게". P3 전면 Unlit 전환=Play 비교 대기(부분 적용), Q1 프랙탈=적용(기본설계)
- **Phase P 공통 링 모션**: P1 밴드 텍스처 u축 노이즈 2옥타브(24셀 굵은 요동+8셀 세부장, 결정론 해시)+RingRunner mainTextureOffset 스크롤(0.35/s) — 링 가장자리가 확장하며 유기적으로 워블 / P2 이중 웨이브 echo — Spawn→SpawnCore 분리(공개 시그니처 불변), ≥1m 링만 수명 25% 시점 Runner가 echo 자체 스폰(duration 1.6배·알파 0.4배·반경 1.12배, 재귀 가드) — 트레일/퍼프 소형 링 제외 / P3 부분 적용 — ≥1m 링 rgb×1.15(HDR 블룸 흡수 유도), 전면 Unlit 전환은 Play 비교 후 판단(연기 마커)
- **Phase Q 이동**: Q1 먼지 텍스처 64 smoothstep 원 → **128×128 3옥타브 value noise**(프랙탈 구름 — 격자 보간 PuffNoise/PuffHash 결정론)+PS 난류 모듈(strength 0.1/frequency 0.8) — 매끈한 원이 실제 연기처럼 흐느적거림 / Q2 이동 시작 킥 — SetCommandTarget에서 **이동 명령 전용**(a==false)+1.5m 거리 게이트 발밑 퍼프 1회(도착 퍼프와 대칭 — 출발/도착 프레임 완성) / Q3 편차 확대 — 크기 0.08~0.22m/수명 0.4~0.75s(균일감 제거)
- **Phase S 몬스터 공격**: S1 수렴 2겹 — SpawnConvergingRing 내부 Core 분리(바깥 원본+안쪽 0.6배/1.25배/0.5배), public 시그니처 불변 + **응축 플래시** — 텔레그래프 conclude 진입 시점 중심 0.25m 소형 플래시 링 1회(_concludeFlashSpawned 가드 — 에너지 응축 완료 클라이맥스) / S2 스윙 지면 파동 — FanRunner Init 파라미터화(halfAngle/streakMin/streakMax — 팬 계약 기본값 유지)+SpawnSwingSweep(±15° 3본/1.0~1.4m 지면 스트릭) 신설, AnimalAI 명중 2분기 배선 / S3 불티 파편 — SpawnEmberBurst(주황 5입자/중력 0.8 낙하/**런타임 전용 랜덤 경로 명시**/EditMode 가드 처음부터 적용), 명중 2분기 배선 / S4 텔레그래프 에지 순회 에너지 — 셰이더 _Scan×2π 순회 스팟(가우시안 좁은 각 exp(-o²×18)×band), B2 프로퍼티 전수 잔존
- **Phase T 패링**: T1 곡선 위스프 아크 — SpawnParryArcWisps+WispRunner(2차 베지어 origin→상승 중간점→하강 끝점, 3호/8포인트 draw-on easeOut, 후반 40% 페이드, **결정론 Random 0건**) 신설+PlayerCombat 배선(직선 팬과 병행 — 방어 스윙의 흐름) / T2 등급별 물성 위계 — SpawnCritBurst 4파라미터화(sparkCount/speedScale/sizeScale), PERFECT(12/1.0/1.0 굵고 빠름)/GOOD(6/0.7/0.75 얇고 느림), 화이트 코어는 명시 startSize로 무영향
- **심각한 오배선 발견+수리**: 무번호 19-G2 수명 강화(0.5s)가 **SpawnHitSparks에 오배선**되어 있었음(동일 상수 앵커 선행 매치 — 파일 단위 Contains 테스트가 놓쳐준 잠재 결함 — 히트 스파크가 0.5s로 길어진 회귀+버스트 강화 미적용) → SpawnHitSparks 원복(0.15~0.25s)+SpawnCritBurst에 정착+**G2 테스트를 메서드 범위 검증으로 강화**(오배선 재발 방지). 파생 수리: 컴파일 에러 2종(물성 편집 오배선 CS0103·color.rgb 존재하지 않는 멤버 CS1061) + 테스트 문자열 8건(변수명 fx/src·파일 소속·2차 사본·마커 대괄호)

**QA**: 컴파일 error CS **0** / EditMode 최종 **2177/2167/10 — baseline 10건만 1:1**(P/Q/S/T 신규 13건 전부 Passed) / 프로브 RC=0·StepErrors=0 / 비전 판독 2컷 통과 — cfx_04(순회 에너지 스팟: 스캔 각도 위치 백색 발광+기존 레이어 유지)·cfx_09(2옥타브 유기 요동+echo 이중 파동+블룸급 휘도 "타격감 대폭 개선") / Q2/Q3/S3(시작 킥·먼지 난류·불티)는 런타임 전용 경로 — 코드 계약으로 확정, Play 체감
**미완료**: 없음(무번호 22 구현+독립 QA 완료) — 잔여: Play 판정(성 실내 타일+화이트 프레임+무번호 19~22 전 FX 체감), P3 전면 Unlit 전환 여부(체감 후 결정), 커밋 승인 누적(무번호 1~22+계획서 5건), D2 진단 로그 2건 커밋 시 정리 검토

## 무번호 21 — 전투 VFX 최종 고품질화(공통 링 렌더 기반+이동/몬스터/패링) R/K/L 전 사이클 [자동보고 2026-11-12]
**작업**: 계획서(docs/plans/2026-11-12_combat-vfx-ultimate-quality-plan.md) 구현 — 사용자 지시 "기초 도형이 아니라 젤다식 완전 고품질". M1 방패 에미션=미채택(방패 렌더러 참조 경로 부재 — ShieldItem 인벤토리 참조만 존재, 머티리얼 인스턴스 리스크>이득), L4 피격 FOV 킥=Play 판단 연기(계획서 권장안)
- **Phase R 공통 링 렌더(최우선 선행)**: ShockwaveRingFX가 유일한 "기초 도형" 대규모 잔존 지점(Cylinder 프리미티브+URP/Lit 단색 — 패링/몬스터/이동/획득 링 전부 경유) → R1 절차 밴드 텍스처 64×16(v축 가우시안 글로우 exp(-dv²×3.2)+u축 16셀 결정론 해시 노이즈(43758.5453 선례)+시임 랩 블렌드) / R2 Spawn 머티리얼 _BaseMap 할당(색 승수 구조 유지 — 패링 금/청·몬스터 적·먼지 흙색 계약 무손상) / R3 두께 보상 0.09→0.115m(페더링 체감 두께 회복) / R4 시그니처 6종 전수 불변. 수혜: 패링 지면 2겹+아우터+확산링·몬스터 임팩트 링·트레일·도착 퍼프·획득 링 전부
- **Phase K 이동**: K1 먼지 파티클 퍼프 — SpawnMoveTrailPuff(PS 절차 구성: 5입자/0.45~0.65s/미세 부양 gravityModifier -0.06/수명 알파 페이드+1.6× 확산/소프트 서클 절차 텍스처 64×64 smoothstep 방사 감쇠 — **사각 금지 계약** 156차 GetStarTexture 선례 준용) / K2 트레일 2레이어 — 지면 링+공기 퍼프 병행(0.6m 게이팅 유지 — 스폰 수 증가 없음) / K3 이동 링 이중 톤 — 외곽 코어 에지 라인(0.835 부스트)+내측 딥 톤 그라디언트(세그먼트/중앙 필 유지)
- **Phase L 몬스터 공격**: L1 수렴 링 — SpawnConvergingRing+ConvergeRunner(외곽→중심 easeIn 수축, 알파 0.35→1 상승 후 마지막 10% 컷 = 에너지 흡수 연출), AttackTelegraphIndicator가 fill 75% 시점 1회 자체 발화(_convergeSpawned 가드 — 몬스터 배선 추가 없음, 수축 시간 = 남은 windup 동기) / L2 스콜치 데칼 — DecalSpawner 4번째 타입 ScorchMark(절차 텍스처 128×128: 중심 그을음 블롯 smoothstep+방사 크랙 8본 해시 각도 — 랜덤 0건, 1.2m/10s/randomRotation 풀링 선례), AnimalAI 명중 2분기(어그로 대상 발밑/플레이어 흡수점 지면) 배선 — **빈 스윙은 미배치**(판정 정직성)
- **수리 1건**: K1 파티클 `Object.Destroy(go, 지연초)` 지연 오버로드가 EditMode에서 즉시 에러 로그 → 이동 시뮬 테스트 15건 오염(2149/25) → 156차 DestroyImmediate 가드 선례 적용 → baseline 복귀

**QA**: 컴파일 error CS **0** / EditMode 최종 **2164/2154/10 — baseline 10건만 1:1**(R/K/L 신규 9건 전부 Passed) / 프로브 RC=0·StepErrors=0 / 비전 판독: cfx_09(밴드 텍스처 — "가우시안 페더링 에너지 밴드, 금색 유지" 합격) / 수렴 링 = 스폰 로그 2건 확정(프로브 컷은 발화 직후 희미 구간만 촬영 — Play 체감 판정) / M1 미채택 근거 기록
**미완료**: 없음(무번호 21 구현+독립 QA 완료) — 잔여: Play 판정(성 실내 타일+화이트 프레임+무번호 19/20/21 전 FX 체감 — 수렴 링/먼지 2레이어/스콜치/에너지 밴드 링 포함), 커밋 승인 누적(무번호 1~21+계획서 4건), D2 진단 로그 2건 커밋 시 정리 검토

## 무번호 20 — 전투 VFX 젤다식 고품질화(몬스터 공격/이동/패링) H~I~J 전 사이클 [자동보고 2026-11-12]
**작업**: 계획서(docs/plans/2026-11-12_combat-vfx-size-quality-upgrade-plan.md) 전 Phase 구현+계약테스트+EditMode+프로브+비전 판독 — 사용자 지시 "기초 도형이 아닌 젤다식 3D RPG VFX로 완전 고품질"
- **Phase H 몬스터 공격**: H1 `_attackRange` ×0.8 축소(2.0/2.5/3.0→1.6/2.0/2.4m) — 링·타격 판정·추격 개시(1.2×) 동일 값 공유로 단일 변경 3요소 자동 일치(시각만 축소의 억울 피격 회피). 스폰/판정 문자열 계약 불변 / H2 TelegraphRing 셰이더 고품질 — ①채움 방사 그라디언트(중심 0.42→가장자리 0.16, 평면 0.30 폐기) ②위험 임박 램프(fill 0.8+ → 알파×1.55+휘도 램프 — 임팩트 직전 긴장 연출) ③에지 에너지 셔머(각도 48셀 해시 노이즈 — 결정론) ④외곽 글로우 타이트화(0.965~0.985→0.975~0.990) ⑤컬러 램프(채움 영역 밝은 위험색 상승). B2 프로퍼티 8종 전수 잔존 / H3 발동 방향 부채꼴 플래시 — AttackTelegraphIndicator.FlashConclude(Vector3) 신설+_Wedge01 풀 채움·공격 각도·플래시 반영, AnimalAI/AfterlifeEnemy ConcludeMeleeTelegraph에서 실제 스윙 방향 배선(B2 자산 사용 배선 — 중복 구현 없음)
- **Phase I 이동**: I1 트레일 거리 게이팅 — 프레임마다 스폰(0.9s×60fps=최대 54개 동시 링) → **0.6m 간격 게이팅**(연속 자취+동시 스폰 수 급감 — 성능/가독) / I2 도착 먼지 퍼프 — SpawnArrivalPuff(0.42m/0.55s/밝은 흙색) 신설, 도착 분기 1회 배선+트레일 플래그 리셋 / I3 진행도 이징 — 선형 25% 축소 → smoothstep 이징+임박 가속(진행도≥0.8 추가 10% 조임 = "곧 도착" 신호), [72차] 1s 규약·도착 소멸은 미적용 계약 불변 / I4 데칼 링 세그먼트 룩 — BuildTintedRingTexture에 6분할 각도 변조+중앙 그라디언트 필(C1 ShaderCenterFillAlpha 패리티 — 데칼 전환으로 잃은 룩 회복)
- **Phase J 패링**: J1 패링 디플렉트 팬 — ShockwaveRingFX.SpawnParryDeflectFan(흡수점에서 공격자 축 ±55° 6방향 에너지 스파크 팬, SparkRunner LineRenderer 선례, **FanRunner 결정론 — Random 0건** — AttackArcVFX WeaponType 결합 회피) + PlayerCombat 배선(PERFECT=금/GOOD=청 링 색 승계) / J2 등급 위계 — SpawnCritBurst(Vector3, int sparkCount=12) 파라미터화, PERFECT 12/GOOD 6방향 / J3 링 잔광 곡선 — RingRunner 선형 페이드 → easeInCubic(수명 대부분 밝게 유지 후 마지막 소멸 — 타격감 잔광). 기존 FOV 펀치/슬로우모션/아우터 링은 기존 구현 존재로 재구현 배제(중복 감사)
- **테스트 갱신 2건**: G2_CritBurst_WiredToParry/G3 — 새 호출 시그니처(등급 파라미터) 반영, I3 문구 훼손("도착 소멸은 미적용" → 은 누락) 즉시 복원

**QA**: 컴파일 error CS **0** / EditMode **2155/2145/10 — baseline 10건만 1:1**(H/I/J 신규 10건 전부 Passed) / 프로브 RC=0·StepErrors=0 / 비전 판독 3컷 전부 통과 — cfx_04(텔레그래프: 그라디언트+램프+선명 코어 "젤다식 레이어드 최상" 판정)·cfx_08(패링: 팬+버스트+코어+링 4레이어 조합)·cfx_16(이동 링: 6분할 세그먼트+중앙 필+경사 밀착)
**미완료**: 없음(무번호 20 구현+독립 QA 완료) — 잔여: Play 판정(성 실내 타일+화이트 프레임 체감+H/I/J 체감 — 축소 반경 밸런스 체감 포함), 커밋 승인 누적(무번호 1~20+계획서 3건), D2 진단 로그 2건 커밋 시 정리 검토, AfterlifeEnemy _attackRange(1.8m)는 사용자 불만 대상(AnimalAI 몬스터)이 아니라 미축소 — 필요 시 별도 확정

## 무번호 19 — 전투 VFX 2차 고도화(발견/이동/패링) Phase C~G 전 사이클 [자동보고 2026-11-12]
**작업**: 무번호 16 계획 Phase C/D 마무리 + 신규 계획서(docs/plans/2026-11-10_combat-vfx-discovery-move-parry-quality-upgrade-plan.md) Phase E/F/G 전 사이클 — 구현+계약테스트+EditMode 3런+프로브 5런+비전 판독
- **Phase C 완료(선택 링 고품질화)**: C1 회전 세그먼트(6분할)+중앙 그라디언트 필+선택 순간 0.15s 이징 확산 펄스 / C2 색상 계약 확정(이동=청록 0.188,0.835,0.784·공격=주황 1,0.588,0.196·집결=금 0.980,0.860,0.300 — 집결 발령 미구현으로 상수만 준비, 위조 배선 금지) / C3 지면 곡면 대응은 F3 데칼로 승계+페이드 소멸 / C4 다중 선택 겹침 알파(3+ 겹침→0.55). CombatFxCommandRingPhaseCTests T1~T6 통과
- **Phase D 원인 확정+수리**: D1 프로브 정식 재런(Hermes 트래킹 백그라운드 — nohup `&`는 세션 종료 시 프로세스 사망 확인) → cfx_06 청색 링·cfx_08/09 금색 2겹+투명 별(별섬광 Sprites/Default 수리 효과) 렌더 확인 / D2 화이트 프레임 3단계 실험 — 실카메라 +1프레임 캡처(cfx_17)=소멸, 동프레임 직접 렌더(cfx_17b)=알파 0 상태(ParryFlashFader는 0→0.85→0 램프), +1프레임 생존 체크=**소멸 로그 확정 → 배치 펌프 지연 > 0.07s 수명 = 구조적 한계**. 배선은 덤프로 완전 입증(parent=Main Camera/localPos z=0.5/Sprites/Default/near 0.1) — 코드 수리 없음(위조 배선 금지), Play 판정 대상. ParryFlashFader Sprites/Default 수리 유지(Init 시그니처·unscaled·0.07s 체계 불변). 참조 영상 25fps 전수 재판독(발화 3점 ±0.4s 전프레임 휘도 계량) = 화이트아웃 스파이크 0 — 단 25fps 샘플링도 0.07s 이벤트엔 부정 근거로 부족
- **Phase E 몬스터 발견 VFX**: E1 AttackTelegraphIndicator.SpawnAlert(Vector3,float,float) 신설 — 발견 ! 링(적색, 3s 전이 게이지, 기존 Spawn 3시그니처 불변) / E2 SFXType.Monster_Alert+GetSFXName 매핑+SFX_Monster_Alert.wav 절차 생성(0.8s, 저음 웅성 90~130Hz+경계 스윕 600~1800Hz) / E3 AnimalAI SetAggroTarget Idle→Alert 전이 블록에 SpawnAlertSignals() 배선(발견 링+SFX+Alert 응시 정지). CombatFxMonsterAlertPhaseETests 6건 통과
- **Phase F 이동 VFX**: F1 ShockwaveRingFX.SpawnMoveTrailDust(0.55m/0.9s/흙색 0.78,0.72,0.62,0.30 — 계획 2~3s는 체감 부자연으로 0.9s 조정)+GuardPlaceholder StepToward MovePosition 직후 배선(도착 early-return 이후 = 이동 중에만 스폰) / F2 이동 링 도착 진행도 — CommandMarker에 _groupOwners 보존+Start 초기 평균 거리 측정+1s 표시 구간 내 접근 비율 링 축소(최대 25%). [72차] 계약테스트 갱신(InteractionActionActivationTests — `_groupOwners` 전면 금지 → 진행도 추적 허용 + 도착 임계 Destroy 트리거(`progress >= 1f`) 금지 유지, 사용자 clarify 확정 "1초 규약 유지·진행도만·도착 소멸 미적용" 근거 기록) / F3 이동 링 데칼 전환 확정 — 판정 근거 cfx_11(쿼드, 능선에서 부유/클리핑) vs cfx_12(데칼, 동일 지형 완전 밀착) 동일 카메라 실측. 지속형(이동/집결)만 DecalProjector 경로: scaleMode=InheritFromHierarchy(lossyScale 상속 — 펄스/버스트/페이드/F2 진행도 localScale 구동 무수정 재사용)·알파=fadeFactor(팝 소멸 금지 C3 유지)·틴트는 텍스처 베이크(데칼 셰이더 색 프로퍼티 부재 실측 — Base_Map/Normal_Map/Normal_Blend 3개)·셋업 실패 시 쿼드 폴백. 신규 컷 cfx_16_move_ring_decal — 청록 링 경사면 완전 밀착 판독 통과
- **Phase G 패링 보강**: G1 = 코드 수리 없음(구조적 한계 확정 — 위 참조) / G2 스파클 버스트 — SpawnCritBurst가 호출처 0건 고아 API인 실측 → TryParryImpactFX에 배선+수명 0.15~0.25s→0.5s 강화(12방향 코드 계약) — cfx_08 +1프레임에서 화이트 코어+주황 스트릭 렌더 확인(배선 전 순흰색 0.00% 대비) / G3 방향성 스파크 = [95차 F4]로 기존 구현 존재(동일 사양 0.3s·공격자 방향 — 중복 방지 미채택, CombatFxParryPhaseGTests로 단일 스폰 잠금) / G4 반격 히트 금색 플래시 = 참조 3컷 전부 미표출 → 위조 배선 금지 미채택
- **프로브 운영 개선**: G1 블록이 cfx_08 앞에 있어 RT 렌더 지연 누적으로 FX 소진 후 촬영되는 문제 발견 → cfx_08/09를 동프레임 렌더 직후로 재배열(신선 시점 복원) — 이후 cfx_08 +1프레임 판독 성립

**QA**: 컴파일 error CS **0** / EditMode 3런 — 최종 **2145/2135/10 — baseline 10건만 1:1**(Phase E +6·F +17·G +5 신규 전부 Passed, [72차] 계약 갱신 1건 Passed) / 프로브 5런 전부 RC=0·StepErrors=0 / 비전 판독 통과: cfx_12(B5 데칼 밀착)·cfx_16(F3 이동 링 밀착)·cfx_08(G2 코어+스트릭) — cfx_09 공백은 배치 캡처 지연 아티팩트로 기록(링 수명 0.5~0.75s vs 렌더 오버헤드)
**미완료**: 없음(무번호 19 구현+독립 QA 완료) — 잔여: Play 판정 3건(성 실내 타일+화이트 프레임 실체감(PERFECT 패링 시)+E/F/G FX 체감 — F1 트레일/G2 버스트 확산 포함), 커밋 승인 누적(무번호 1~19+계획서 2건), D2 진단 로그 2건([PlayerCombat][D2]·프로브 [D2] 덤프) 커밋 시 정리 검토, RTSSelectionOverlayUTK.cs 삭제(D) 상태 — 본 세션 미관여

## 무번호 18 — Phase B 전투 VFX(적색 텔레그래프) 구현+실측 [자동보고 2026-11-10]
**작업**: 무번호 16 VFX 계획 Phase B — B1~B5 구현+프로브 확장+실카메라 비전 판독
- **B1 부채꼴 게이지 오버로드**: AttackTelegraphIndicator.Spawn(..., elite, useWedge, attackDirection) — 6개 인자(기존 3개 인자 Spawn 유지, 스폰 시그니처 불변 계약 — AnimalAI/AfterlifeEnemy 무수정) — 셰이더 부채꼴 각도 _AttackAngle/_Wedge01 계산
- **B1 부채꼴 각도**: _attackAngle = atan2(attackDirection.z, attackDirection.x) + π — 쿼드 UV 수학과 각도 공식
- **B2 셰이더 효과 확장**: TelegraphRing.shader — _Wedge01/_AttackAngle/_Scan/_StartPulse/_Flash 추가 — 부채꼴 각도 0→90°(π/2) 확장
- **B3 위험도 색 계약**: NormalColor=(1, 0.30, 0.16, 0.45)/EliteColor=(1, 0.30, 0.16, 0.45) — Spawn(., elite=false, useWedge=false, attackDirection=Vector3.forward) 경로
- **B4 SFX**: Telegraph_Warning SFXType 추가 — GetSFXName 매핑("SFX_Telegraph_Warning") — Resources/Audio/SFX 클립명 매핑 선례
- **B5 프로브 확장**: CaptureDecalProjector(cam) — URP DecalProjector 타입/셰이더 미발견 시 폴백. 스폰 컷(shot=12) 추가
- **프로브 확장**: 부채꼴(일반=적색) early/late + 정예(주황) 컷 3컷(shot=13/14/15) — cfx_13_wedge_early/cfx_14_wedge_late/cfx_15_wedge_elite/cfx_12_decal_projector
- **비전 판독(vision)**: cfx_13_wedge_early(fill 0.33, 일반=적색) — 적색 부채꼴 가시 ✓ — 공격 방향 부채꼴 가시 ✓ — 120도(0.33 비율) 영역 부채꼴 가시 ✓ — 합격
  cfx_14_wedge_late(fill 0.83, 일반=적색) — 적색 부채꼴 가시 ✓ — 부채꼴 각도 90°×0.83 = 74.7°
  cfx_15_wedge_elite(fill 0.25, 정예=주황) — 주황색 부채꼴 가시 ✓ — 적색(일반)과 구분되는 주황색 ✓
- **B5 실측 완료(무번호 18 수리)**: 데칼 미렌더 근원 2건 — ①활성 URP 렌더러에 Decal Renderer Feature 부재(SSAO+AERO Fog만) ②프로브 데칼 머티리얼에 텍스처 미할당(URP/Decal 셰이더그래프는 _BaseMap 없으면 렌더 불가) — 수리: UniversalRendererData.asset에 DecalRendererFeature 추가(technique=Automatic, GUID a1614fc811f8f184697d9bee70ab9fe5) + 프로브에 절차 생성 청록 링 텍스처(256px, 외곽 링 0.82~0.95)를 첫 Texture2D 프로퍼티(자동 발견)에 할당
- **프로브**: 11컷(원래 8컷 + 부채꼴 3컷 + 데칼 1컷) 전부 캡처됐습니다(2차 런 — pxhash 8b2d2649, 1차 6fb07553과 상이 = 렌더 변화 발생)
- **B5 실측 결론**: 데칼이 굴곡 지형에 **곡면 밀착 렌더됨** — 능선 경사면에 걸쳐 지형 고도 변화를 따라 형태 왜곡·밀착(공굴림 없음), 쿼드 방식 대비 곡면 지형 우수 — 데칼 방식 전환 근거 확보(지면에 붙는 링)

**QA**: 컴파일 error CS 0 / EditMode 2117/2107/10 — baseline만(신규 회귀 0건 — CombatFxTelegraphPhaseBTests T1~T7 +7 전부 통과)
**미완료**: Phase B 남은 사이클 — B5 실측(데칼 머티리얼) + Play 판정 승인 대기(성 실내 타일) + 커밋 승인 누적 + 라운드 번호 부여(무번호 1~18)

## 무번호 17 — Phase A 실카메라 FX 근본 진단·수리 [자동보고 2026-11-10]
**작업**: VFX 계획(무번호 16) Phase A 전 사이클 — 캡처 7장 비전 판독+A1~A4+계약테스트 신설
- **cfx 프로브 실측**: run_cfx_probe.sh 배치 런 성공, TestOutput/cfx_01~11*.png 산출, RUNNER_DONE StepErrors=0
- **A1 셰이더**: TelegraphRing Shader.Find OK supported=true, 머티리얼 바인딩 정상, PIPELINE=URPAsset, 폴백 복제 카메라(PM 공식 57°)로 실게임 등가 뷰 확정
- **A2 "녹색 띠" 근원**: cfx_01 대조 컷(무FX)에도 동일 아티팩트 → FX 오염 아님(계획서 A2 가설 반증) — 지형/그림자·라이팅 X자 음영 아티팩트
- **A4 패링 FX**: 청색 링 가시 / 별섬광 검은 사각형+흰 별 발각 — SpawnStarFlare URP/Lit 불투명 머티리얼이 StarFlare.png 알파 파괴 → Sprites/Default로 수리
- **캡처 7장 비전 판독**: 텔레그래프 링=주황-적 가시·지형 굴곡 클리핑 / 명령 링=청록 가시·굴곡 왜곡 / 패링 개방 링=가시지만 저품 / PERFECT 컷=검은 사각형(수리됨) / 대조 컷=X자 음영(FX 무관 근거)
- **A3 계약테스트 신설**: CombatFxRealCamContractTests.cs — T1 실카메라(Tag MainCamera) 기준 확정 / T2 폴백 카메라 원점 고정시에만 / T3 PM 공식 57° 1:1 / T4 URP SingleCameraRequest(정식 수단) / T5 배치 모드 -quit·-nographics 금지 / T6 cfx_*.png 산출물 필수
**QA**: 컴파일 error CS **0** / EditMode **2109/2099/10 — baseline만**(신규 회귀 0건 — CombatFxRealCamContractTests T1~T6 +6 전부 통과)
**미완료**: Phase B~D 계획 진행, Play 판정 승인 대기(성 실내 타일), 커밋 승인 누적, 라운드 번호 부여(무번호 1~17)

## 무번호 16 — 저장고 타일 재확인+가구크기표준+전투 VFX 계획 기안 [자동보고 2026-11-10]
**작업**: ①저장고 타일 사용자 관측 재판정 ②플레이어 비례 가구크기표준 일제 적용 ③패링/공격씬 영상 판독+몬스터 예고 VFX·RTS 지정 링 고품질화 계획 기안
- **타일 판정**: 코드 재검증(저장고 오버레이 2장 stoneMat 착지 확인+빌더 제2 어두운 경로 0건) — 사용자 관측(08:18 세션)은 수리 착지(09:1x) 이전, 09:38 세션은 패링씬만 Play → 다음 성 실내 Play에서 재판정 필요
- **가구크기표준(무번호 16)**: 플레이어 1.8m 비례 단일 기준 — 탁자류 0.76→0.9·벤치 0(natural)→0.55 명시·옷장 1.8→2.05·협탁 0.6→0.65(상판 0.8×0.8)·작업대 0.9→0.95·작전탁자 2.8×1.5×0.86→3.2×1.6×0.95·물류데스크 2.0×1.0×0.9→2.2×1.1×0.95·침실 책상 1.7×0.95×0.9 — 위 소품(촛불/물약/플라스크/지도) y 동기(0.65/0.89/0.95/0.95) — ApplyCastle 전 방 일제
- **영상 판독(3단 줌)**: 13s/25s 프레임 — 적색 텔레그래프 링·푸른 링·패링 판정 FX 전부 미표출+화면 중앙 지그재그 녹색 띠(깨진 FX 렌더) — 로그상 패링 창 개방 3회+TryParryImpactFX 발화 → 무번호 5 프리미엄 FX가 진단 카메라 전용 검증·실카메라 렌더 깨짐(기록된 "녹색 단색 프레임" 잔여) = 체감 "수정 없음"의 뿌리
- **VFX 계획 기안**: docs/plans/2026-11-10_combat-vfx-telegraph-rts-ring-upgrade-plan.md — Phase A 실카메라 FX 렌더 근본 진단·수리(선행 필수)+실카메라 검증 계약 신설 → B 몬스터 예고 VFX(채움 부채꼴 게이지+에지 글로우+스캔라인+펄스+SFX+프로젝터 방식 비교) → C RTS 지정 링(회전 세그먼트+그라디언트 필+명령별 색) → D 패링 실카메라 재판정+보강
**QA**: 컴파일 error CS **0** / EditMode **2103/2093/10 — baseline만**(신규 실패 0) / 투어 **14 steps errors=0** — 전부 GREEN
**미완료**: 없음(무번호 16 완료) — 잔여: Phase A 승인 대기(VFX 계획), Play 판정 대기(성 실내 타일+가구 크기 체감), 커밋 승인, 라운드 번호 부여(무번호 1~16)


## 무번호 15 — 실내 구조 통일+관람 카메라 57° [자동보고 2026-11-10]
**작업**: ①모든 실내 영지가 플레이어 소속 영지와 동일 구조 ②저장고 타일 공통화 ③가구 9/29 설계도 정합 재배치(설계도 외 전량 제거) ④Test_23 관람 모드 90° 탑다운 → 57° 원근 통일
- **P0 원인 실측**: SelectForTerritory FNV-1a %3(팔각/원형/비대칭 3분할 — Empire_1→RoundKeep 실측) + ComputeLayoutVariant djb2 0~7 + 저장고 오버레이 darkStoneMat(다른 방은 stoneMat) + 157~164차 누적 가산 가구 난잡 + IndoorTourMode PitchOverrideDeg=90 3곳
- **P1 구조 통일**: InteriorLayoutDefinition.SelectForTerritory → KeepOctagon 단일 반환(FNV 해시 폐기·결정론 유지) / IndoorSceneTransition.ComputeLayoutVariant → 0 고정(djb2 폐기) / RoundKeep·AsymmetricManor 정의+TryGetLayout 보존(명시 API/테스트 전용)
- **P2 저장고 타일**: InteriorFurnishingPass 저장고 오버레이 2장 darkStoneMat → stoneMat(공통 석타일) + darkStoneMat 선언 제거
- **P3 가구 설계도 정합**: ApplyCastle 전면 재편 — 로비(중앙 벽난로 dragonfire-brazier+원목 벤치 4+횃불 스탠드 4+붉은 카펫+샹들리에 2 유지)·침실(패스 침대 2 제거→빌더 성주 기능침대 1+협탁 2+촛불 2+옷장 1 서측+집무 책상·의자 동측+카펫 — 목재 오버레이 폐기)·제작소(용광로 북벽+모루+가죽 작업대 3+갑옷 거치대+원자재 상자 3 — 주방 소품 제거)·창고(선반 4+물류 데스크 2+상자 4+포대 2+항아리 3)·병영(패스 침대 4·무기랙·훈련 기구 제거 → 빌더 BarrackBed_1..8+작전 탁자+원정 지도 — 무기 거치대는 빌더 무기고)·연금(선반 2+실험대 2+물약 6+플라스크 2+절구 이동)·입구(러그 스트립+벽횔도 2) — 설계도 외 제거 46종(왕좌/피아노/하프/보물상 등)
- **P4 관람 카메라**: IndoorTourMode ConfigureTourCamera/ReapplyTopDownCamera(→ReapplyTourCamera)/PlaceTourCamera 90°+직교 → 57° 원근(IndoorCameraFollow 기본 산식 동일)+휠 거리 6~20m+DisableShellForTopDown 폐기(셸 활성 — 인형의 집 뷰) / IndoorTourMode23Probe 판정 tour57 재핀
- **테스트**: PolyforkFurnishingContractTests SkipWiringCheckIds 31→77종(+설계도 외 제거 46종)·T3 하한 153→94·T4 92종(169−77)·T7 3→2·T8/T9 역어서션(제거 계약) / InteriorLayoutAssignmentTests·PlayerCastleInteriorTopologyTests SelectForTerritory 계약 전면 통일 재편(분배 테스트 폐기)
**QA**: 컴파일 error CS **0** / EditMode **2103/2093/10 — baseline만**(신규 실패 0 — 1차 런 T4·LayoutAssignment 3건 실패→skip 누락 5종+구분할 테스트 파일 미발견 즉시 수리 후 재런 소멸) / 투어 **14 steps errors=0** / vision 초판독(카펫 가시·왕좌/피아노 부재·타일 균일)
**미완료**: 없음(무번호 15 완료) — 잔여: Play 판정 대기(성 실내 재배치 체감+관람 모드 57° 체감), 커밋 승인(누적), 라운드 번호 부여(무번호 1~15)

## 무번호 14 — 164차 [자동보고 2026-11-09]
**작업**: 신규 건물 24종 전량 수용·배치(사용자 "배치해줘" 전량 지시)
- **P0 수용**: 24 GLB(천수각/벽돌교회/영주알현실/마을회관/선술집2/목욕탕/상점가/초가/세탁물집/쿠라창고/고상창고/항구창고/마을관문/아치관문/야영지관문/목책벽/망루3/봉화대/민가/상가연립/농가2) → Models/Polyfork **누적 169종/169라인**
- **P1 야외 가산 24**: Entries 37→**61종** — 남동 천수각(72,−72)·북동 벽돌교회(52,44)·북 알현실(18,58)·서 회관(−56,−16)·선술집2(38,−12/−24,−34)·목욕탕(44,−2)·상점가(30,38)·초가(−2,−44)·세탁물집(−38,−10)·쿠라(38,30)·고상창고(48,−48)·항구창고(−56,−48)·관문3(6,−28/22,−2/−36,−26 — solid=false 통과예외)·목책벽(60,−30 — Solid)·망루3(−64,36/64,10/−10,−64 봉화대)·민가(54,−14)·상가연립(18,44)·농가2(42,−40/−50,−32) — 기존 37종 전체와 최소 7~15m 이격 검증
- **테스트**: PolyforkIds 169종 재생성, T2=169, skip 중복 22종 블록 제거(163차 위임 타임아웃 잔재)+24 가산, **T12 outdoorIds 21→67종**(163차 누락 22종까지 보강)
**QA**: 컴파일 error CS **0** / EditMode **2103/2093/10 — baseline만**(신규 실패 0) / 투어 **14 steps errors=0** — 전부 GREEN
**미완료**: 없음(164차 완료) — 잔여: Play 판정 누적 대기(성 보물/피아노/마을 구조물 61종/호수 선박+등대/던전 소품), 커밋 승인(무번호 1~14 누적)

## 무번호 13 — 163차 [자동보고 2026-11-09]
**작업**: 묘지 2종 제외(사용자 지시) + 야외 건축물 22종 수용·배치
- **P0 수용**: /tmp/polyfork_glb7 22종(등대·석교회·구교회·통나무집2·아도베농가·연립·물탑·양수장·우물·닭장·오르간카트·표지판·빗물받이·야외작업대·술통·환영아치·아치통로·석탑·진달래·허수아비·문턱다리) → Models/Polyfork **누적 145종/145라인(CREDITS)**
- **P1 야외 가산 21**: PolyforkOutdoorStructures Entries 16→**37종** — 건물7(석교회(−12,52)/구교회(−30,60)/현관통나무(44,18)/통나무(48,12)/아도베(36,−28)/연립(−44,24)/문턱다리(−16,30)) + 시설5(물탑(24,−34)/양수장(28,−22)/우물(−4,−8)/닭장(16,−30)/야외작업대(−20,10)) + 소품9(오르간카트(6,14)/표지판(−8,−14)/빗물받이(34,−34)/술통(10,−12)/석탑(−52,−20)/진달래(−6,22)/허수아비(26,−38)+아치2)
- **아치 통과 예외**: Entry에 `bool solid` 필드 신설(기본 true) — welcome-arch(0,20)/arched-passage(−8,24)는 false→PlacePolyfork(렌더러 전용), 나머지 35종 Solid 유지
- **등대 가산**: PlaceLakeBoats 첫 호수 에지+10m 육지 1종(Lake_Lighthouse, yaw=180)
- **테스트**: PolyforkIds 145종 재생성, T2=145, SkipWiring 31→53(+22), T12 outdoorIds 16→38
- **묘지 제외 확정**: arched-cemetery-gate/iron-cemetery-fence 사진분만 존재 — 미수용
**QA**: 컴파일 error CS **0** / EditMode **2103/2093/10 — baseline만**(신규 실패 0) / 투어 **14 steps errors=0** — 전부 GREEN
**미완료**: 없음(163차 완료) — 다음: 신규 건물 24장 후보 전송 → 사용자 선정 대기

## 2026-11-09 (무번호 11 — 번호 부여 대기) 구현+QA 완료 — 161차 Polyfork 야외 건축물 패스(10종)+실내 hero 4종+던전 로프다리 (미커백)

- **사용자 지시**: "진행하고 야외 건축물로 쓸 수 있는 것도 몇 개 더 뽑아줘"(재시도). 계획 docs/161_POLYFORK_OUTDOOR_STRUCTURES_PLAN.md.
- **P0 (위임 232s 착지)**: 18 GLB 수용(누적 **112종**)+CREDITS 112라인(4종 slug 폴백).
- **P1 (동일 위임 착지)**: ①**PlacePolyforkSolid 신설**(InteriorPropCatalog L79-105 — 렌더러 bounds 기반 BoxCollider 1개 부여 — 야외 통과 방지, 실내 렌더러 전용 계약과 별개 경로) ②실내 hero 4종: 그랜드 피아노(로비 연주 코너)·바로크 패널 벽(알현실 뒷벽 — 지오메트리 외부 위험을 내측 z0−0.15로 교체 배치)·넘치는 보물상(왕좌 후방)·핸드 랜턴(창고). PlacePolyfork 149→**153**.
- **P2 (위임 164s 착지)**: 신규 **PolyforkOutdoorStructures.cs**(89줄) — 마을 스폰(728,−529) 기준 고정 링 10종 배치(마차/수레/대장간/물레방아/풍차/마구간×2/광산 양새/사당/종탑 — 최소 24m 이격, 구조물 간 8m+), **TerrainGenerator.GetHeightAt(Plains,42) 실측 정정**(계획서의 TerrainShape 가정 오판 — 서브가 실측으로 바로잡음), yaw=마을 중심 향한 Atan2 결정론, 멱등 가드. 훅: GameSetup.cs:328(PlaceAll 직후). 콜라이더: Solid 경로(야외 통과 방지).
- **P3 (위임 243s 착지)**: CaveInteriorBuilder CreateDungeonProps에 **rope-bridge-span 가산**(6종) + 테스트: PolyforkIds 94→**112**, credit 112, T3 하한 149→**153**, **SkipWiringCheckIds 20종 통합**(던전5+로프다리1+야외수용3+야외배선10+미배선1 — 160차 UnwiredReserve 패턴 승계), T4 실내 배선 **92종**, T11 6종, **T12 신설**(야외 패스 10종+Solid 호출), **T13 신설**(Solid 정의+BoxCollider 소스계약).
- **QA (1차 컴파일 실패 → 즉시 수리 → 재검증)**: 1차 — PolyforkOutdoorStructures CS1503(GameObject→Transform 1라인) → 부모 1줄 수리 / 2차 — 컴파일 **error CS 0** + EditMode **2103/2093/10 — baseline 1:1, 신규실패 0**(T12/T13 포함) + 투어 고아 런 회수 **DONE steps=14 errors=0**(Unity.exe 생존 폴링 — 148차 패턴).
- **누적**: Polyfork GLB **112종**·CREDITS 112라인·실내 배선 153+야외 10종(Solid)+던전 6종 = **169 배선**.
- **잔여**: ①사용자 Play 판정(마을 스폰 주변 10구조물+로비 피아노/바로크 벽/보물상+던전 로프다리) ②커밋(누적 — 무번호 1~11) ③reserve 3종(opera box/이계 묘당/묘지 등대) 별도 사이클 ④hand-lantern 5cm 초소형 — 스케일업 검토 ⑤중세 침대/태피스트리 demand. 푸시 없음.

## 2026-11-09 (무번호 10 — 번호 부여 대기) 구현+QA 완료 — 159차(Polyfork 3차 보강 30종)+160차(4차 24종+던전 소품 패스 5종) 통합 (미커백)

- **사용자 지시**: 159차 — 사진 후보 30종 "배치하고 다른 후보들도 뽑아줘" / 160차 — 던전 보류 5종 승인("던전도 던전에 어울리는 가구로") + 신규 후보 재채굴. 계획 docs/159/160_*.md.
- **159차 P0+P1 (위임 600s 타임아웃 → 부모 직접 완수)**: 30 GLB 수용(누적 70)+CREDITS 70 + 배치 25종(PlacePolyfork 85→126): 로비 왕권 보물 액센트(coin pile×2/stack×2/jewel casket/loot sack×2)+금 하프+기사 석상+천장 걸이 화분(oy 2.142), 연금 마법 소품(spellbook/orb/flask×2 — 작업대 위 y 0.9), 제작소 푸세/담금질/주방 클러스터(produce/milk/herb/bread×2), 병영 양궁 과녁/창/방패/화살통/투구(cabinet 위 y 1.8), 입구 갑옷 스탠드, 침실 고사리 / tavern 전설 상자+금화 / restaurant 농산물+빵 / house 허브+shop 진열+barn 원예대. 던전 보류 5종 수용만.
- **160차 P0 (위임 max_iterations — P0 착지/P1 앵커 실측만)**: 24 GLB 수용(누적 **94**)+CREDITS 94라인(3종 copyright 공백 → slug 폴백).
- **160차 P1 (부모 직접 — 64차 승인)**: 배치 23종(+1 reserve — cemetery lantern post는 이계 묘지 사이클 보류): 로비 하프시코드(하프 대칭)/용화 화로(왕좌 후방)/벨벳 발판/원정 지도, 침실 옷거울+액자(y 1.8 마운트), 병영 전술 지판+사령관 의자, crafthouse 제련로(hero)+공구 랙+가죽 건조대, tavern 훈연기+피아노+위스키+와인, restaurant 콘서트 하프, 창고 식품 저장고+맷돌+불꽃 화로, barn 우유 스탠드, house 바질+트렁크, shop 석조 물두멍 — PlacePolyfork 126→**149**.
- **160차 P2 (위임 600s 타임아웃 → 착지 확인: 구현 완료·보고만 미완 유형)**: CaveInteriorBuilder CreateDungeonProps 신설(L1160)+훅(L524 CreateCrates 뒤 1줄) — 던전 수용 5종(sarcophagus/cell-door/skull-shelf/iron-door/mimic) 결정론 rng 배치(존 코너/벽 라인/GemChest 이격), **DungeonAssetCatalog/Models/Dungeon 카탈로그 계약 무접촉**(147 카운트 테스트 보호 — Models/Polyfork 경로 직결). Brace 0.
- **160차 P3 (부분 위임 → 착지 확인)**: PolyforkFurnishingContractTests — GLB/credit/배열 70→**94**, T3 하한 126→**149**, T4 실내 배선 88종(94−던전5−묘지보류1), T10 유지(FurnishingPass 미배선 역어서션), **T11 신설** — CaveInteriorBuilder 5종 배선+CreateDungeonProps+마커 계약.
- **QA**: 컴파일 **error CS 0** / EditMode **2101/2091/10 — baseline 1:1, 신규실패 0**(T4/T10/T11 포함) / 투어 DONE steps=14 errors=0 / PIL 순흑 0.
- **누적**: Polyfork GLB 94종·CREDITS 94라인·FurnishingPass 배선 149 + CaveInteriorBuilder 던전 배선 5종(룸당 최대 3~4프롭).
- **잔여**: ①사용자 Play 판정(성 로비 왕권 보물/하프시코드/용화 화로, crafthouse 제련로, tavern 피아노, 던전 소품) ②커밋(누적 — 무번호 1~10) ③cemetery-lantern-post — 이계 묘지 사이클 보류 ④중세 침대/태피스트리 여전히 카탈로그 부재(demand 대기). 푸시 없음.

## 2026-11-09 (무번호 9 — 번호 부여 대기) 구현+QA 완료 — 158차 Polyfork 실내 보강 2차(27종 추가 수용+방별 배치, 누적 40종/85배선) (미커백)

- **사용자 지시**: 심층 스윕(카탈로그 전량 3,156종 → 실내 부합 132종 확정·264 fetch 검증 0실패 — /home/korea/tmp/polyfork/SWEEP_REPORT.md) 후 쇼케이스 27종 사진 전달 → "이것도 배치해줘". 계획 docs/158_POLYFORK_FURNISHING_WAVE2_PLAN.md.
- **전수 감사**: GLB 27종 헤더 — 25 OK + 2 WARN(KHR transmission/volume=모래시계 유리, clearcoat=활 — glTFast 폴백, 소형 소품 무해 판정).
- **P0+P1 (위임 442s 착지)**: 27 GLB 수용(누적 40) + CREDITS.md append(누적 40라인 — pipe-organ은 copyright 공백 → slug 표준 문구 폴백) + InteriorFurnishingPass 배치: 교체 3(침실 장롱×2→Wardrobe·targetHeight 1.8f 승계 실측, 침실 러그 AddRug→Wool Rug GLB) + 성 가산 21(로비 왕좌 양옆 Candelabra×2·입구 Olive Tree×2·북벽 Curtain×2·FarmLantern·제작소 Anvil+Firewood·창고 GrainSack×2+Basket×2·연금 BookStack+Hourglass(작업대 위 y=0.9)·병영 Greatsword×2+Bow×2 yaw±15°) + 건물 가산 19(tavern BackBar+Bench×2+TripodCauldron·restaurant BreadOven+Bench+Sack·house Armchair+Olive+AreaRug·shop Basket×2·church **PipeOrgan(자연 8m>벽3m → 방높이×0.7 축소 계약 적용)**+Candelabra×2·crafthouse Anvil+Firewood·barn SaddleRack+TreasureChest). 패스 파일 384→455행, [158차 P1] 마커 31건, 엔진난수 0, AddPointLight 신규 0(조명 계약).
- **P2 closeout (위임 221s 착지)**: 계획서 누락 2종 배치(AlcoveBookcase→연금 동벽 hw−0.34 flush·FacultyBookWall→창고 NE북벽 — 기존 배치 0건 좌표 확인 후 선정) + 테스트 갱신(GLB 13→40·CREDITS 13→40·id 전종 13→40·배선 하한 ≥85(실측 85)·T9 신설(책장2종)) + InteriorQualityBaselineTests 소멸 문자열 4종 어서션 미사용 실측 → 무수정.
- **QA (위임 430s — 예산 규칙 적용 성공)**: 컴파일 **error CS 0** / EditMode **2099/2089/10 — baseline 1:1, 신규실패 0** / PolyforkFurnishingContractTests **9/9**·InteriorQualityBaselineTests **12/12** / 투어 **DONE steps=14 errors=0** / PIL 14장 순흑 0 — castle 2컷 avg(10,9,9) black 92.2%는 캡처 검은 배경 여백 기인 → **vision 직접 판독으로 정상 렌더 확정**(옥좌/테이블/옷장/러그/촛대/화분 전부 가시 — PIL 순흑 기준 95% 미달이라 PASS 처리됐으나 본 판독으로 종결).
- **누적 상태**: Polyfork GLB 40종·CREDITS 40라인·PlacePolyfork 배선 85라인(성 존 7+건물 7종) — 157+158 합산.
- **잔여**: ①사용자 Play 판정(성 로비 왕좌+촛대+화분·침실 옷장/러그 교체·tavern 바/가마솥·church 오르간 등 체감) ②커밋(별도 승인 누적 — 무번호 1~9) ③중세 침대/태피스트리는 카탈로그 부재 — demand 요청 시 확보 가능 ④모래시계 유리 transmission 렌더는 Play에서 1회 확인 권장. 푸시 없음.

## 2026-11-09 (무번호 8 — 번호 부여 대기) 구현+QA 완료 — 157차 Polyfork 에셋 수용+실내씬 재배치(성 7존+건물 7종, 51배치) (미커백)

- **사용자 지시**: polyfork.dev 실측 조사(저폴리 GLB 라이브러리 — 전 모델 무료, 메시 CC BY 4.0, 정점컬러 단일 머티리얼) → "저 가구들로 실내씬들을 재배치". 계획 docs/157_POLYFORK_INTERIOR_REDEPLOY_PLAN.md.
- **전수 감사(147차 선례)**: GLB 13종 헤더 감사 — magic/ver2 무결·머티리얼 1·텍스처 0(정점컬러 — 원목 원색 계약 정합)·Draco/외부의존 0. 썸네일 13종 별도 수집(사용자 전달용).
- **P0+P1 (위임 137s 착지)**: 13 GLB → Assets/Resources/Models/Polyfork/ + CREDITS.md(credit 13라인 — JSON 부재 8종은 GLB 내장 asset.copyright에서 추출, 5종 교차검증 완전 일치) + InteriorPropCatalog.PlacePolyfork 게이트(L74 — 기존 Place 재사용, 기존 코드 0수정).
- **P2+P3 (위임 185s 착지)**: InteriorFurnishingPass.cs 327→384행 — ApplyCastle: 로비 원목탁자4→Trestle Table+촛불·천장등2→Iron Chandelier(Light는 기존 AddPointLight 유지 — Place 미생성 실측)·**왕좌 가산(로비 북측 남향)**·침실 촛불+벽횔도2·제작소 크레이트→Polyfork+화로+벽횔도·창고 크레이트4+Strongbox2+ScrollRack·병영 WeaponRack2·연금 탁자2→Alchemy Workbench·입구 화로+벽횔도2 / ApplyBuilding: tavern 테이블+barrel2+촛불·restaurant 테이블+촛불·house 촛불+크레이트·shop 크레이트2·crafthouse 화로·church Lectern(강단)·barn barrel2+크레이트2. 총 PlacePolyfork 소스 40라인/런타임 51회(성33+건물18). 존 규칙+기능앵커 회피(3m+ 이격)·결정론 좌표(엔진난수 0)·벽횔도 targetHeight=0(origin_y 2.1 보존).
- **P4 (위임 155s 착지)**: InteriorQualityBaselineTests — 9 소멸 문자열의 어서션 직접 사용 0건 실측(간접=T8 팩토리명 계약) → 기존 6어서션 유지+완성계약 가산(PlacePolyfork 배선)+T5b 신설(GLB 13종 File.Exists — Resources.Load는 raw glb 불가, 사유 주석). 신규 PolyforkFurnishingContractTests 9건(리소스13/CREDITS+CC BY/배선≥40/id 13종/카탈로그 정의/벽횔도 0f 종결/탁자≥3/왕좌≥1 — 탁자 기대값은 루프 전개 미계수 실측 3으로 정정).
- **QA (위임 600s 타임아웃 — 사망 전 컴파일+EditMode+투어 기동까지 완료, 부모가 고아 산출물 회수 판독)**: 컴파일 **error CS 0** / EditMode **2098/2088/10 — baseline 10건 1:1, 신규실패 빈 집합** / PolyforkFurnishingContractTests **9/9**·InteriorQualityBaselineTests **12/12** / 투어 **DONE steps=14 errors=0**(42뷰+몽타주3) / vision 5컷 판독 — 성 로비(테이블4·옥좌·샹들리에·촛불 전부 가시·이상 0)·restaurant(테이블+촛불 가시)·house(촛불 가시) / tavern·church는 탑다운 식별 한계(오크톤=기존 원목판자와 유사, lectern=상자형으로 판독) — 배선은 소스계약으로 확정, 시각 확정은 Play 몫. church 벤치 흩어짐=기존 배치(P3는 lectern 가산만 — 회귀 아님).
- **잔여**: ①사용자 Play 판정(재배치 체감 — 성 로비 왕좌/샹들리어/트레슬 테이블, tavern/restaurant 교체본) ②커밋(별도 승인 누적 — 무번호 1~8) ③침대/옷장/타페스트리는 Polyfork 부재 — demand 요청 또는 기존 절차 침대 유지 ④크레딧: 판매 시 CREDITS.md 기반 게임 크레딧 표기(CC BY 4.0). 푸시 없음.

## 2026-11-09 (무번호 7 — 번호 부여 대기) 구현+QA 완료 — 156차 이계 카드 풀 40종(+증강 6=46)+플레이스홀더 UI+무기/스킬/패시브/특수 전체 배선 (미커백)

- **사용자 지시**: 11-08 세션(계획+P1~3 위임 후 0착지 종료) 재개 — "진행". 계획서 재작성(docs/156_AFTERLIFE_CARD_UI_PLAN.md — 실측 좌표표 동봉) 후 순차 위임.
- **Phase 1 (위임 236s 단발 착지)**: CardDef.cs(최상위 enum 2+class, 효과 슬롯 MultA/MultB/Damage/Heal/Flat/Radius/Duration/Interval/Int1+WeaponId/Level) · CardCatalog.cs · AfterlifeCardSystem.cs(마크 300/600·가중치 C70/R22/E6/L2+링 배율 {1:0.5,2:1,3:2,4:3}·PickWeightedIndex 순수 함수·Fisher–Yates 결정론 3택·자격 필터·20초 폴백·증강 보류 게이트·패시브/특수 게이트 내장) · AfterlifeCardSystemTests 23건. 서브가 계획서 수치 불일치(46 vs 열거 36) 지적 → 정답=40신규+기존증강 6=46으로 정합(P3에서 스택II 4장 추가).
- **Phase 3 (위임 183s 단발 착지)**: AfterlifeCardUIUTK.cs 369줄(FishingUTK 선례 — 증강+카드 양쪽 OnOfferCreated 구독, 등급색 C/R/E/L 테두리, 20초 카운트다운 바, 클릭 라우팅 Select만) + 카탈로그 +4장(p_*_plus 스택II) + 카운트 어서션 갱신(125차 규칙). 마커 22건.
- **Phase 4 (위임 600s 29콜 0산출 → 64차 상설승인 부모 직접)**: CardSystem 무기 배율 분리(_weaponDamageMult×_passiveDamageMult→DamageMult 프로퍼티)·CooldownFactor·활성 무기(마지막 획득) 스왑(WeaponData.Fist/Sword/Spear/Bow 매핑)+진입 캡처/귀환 원복·RollDeathScytheInstakill 순수 함수 / PlayerCombat.CanAttack 분모 추가+CurrentWeapon 액세서 / AfterlifeEnemy.TakeDamage 배율+사신(정예=_level≥5, 정예는 가산 피해) / AfterlifeWeaponHookTests 14건.
- **Phase 5 (부모 직접)**: AfterlifeSkillSystem.cs 신규(8종 자동발동 — spin/charge/shockwave/holy_cross/fire_zone/time_warp/tentacle/bone_shield + 혼백 활 6초 자동발사(화살 소모 없는 직접 판정) + 주입 시계 UnscaledTimeOverride/TestActiveOverride) / SurvivalSystem 13곳 배선(BeginRun+Tick(증강 정지 게이트 앞)+EndRun×3+NotifyEnemyKilled 뱀파힐/해골연쇄+NotifyOrbAbsorbed 배율+CheckWave 시간의 모래·웨이브 유예+Surrender 수확 비율+FinishRun 영지의 문 감면+ExecutePermadeath 두 번째 목숨+ForceReturnDoor+AliveEnemies 액세서+TestRegisterEnemy) / PlayerHealth 델리게이트 훅(IncomingDamageModifier·TryBlockOnce — Core→Systems 역참조 회피, CardSystem static ctor 주입)+ReviveAtRatio / PlayerMovement 구르기 계수 / AnimalAI·AfterlifeEnemy TelegraphWindupSeconds const→프로퍼티 / AfterlifeOrb.Live 액세서 / AfterlifeSkillSystemTests 11건.
- **QA (3회 위임+폴백 런 3회)**: 1차(166s) — 컴파일 에러 6건(테스트 using System.Linq 누락 CS1061×2·Split StringComparison 오버로드 CS1503×4) → 수리. 2차(284s) — 컴파일 0+EditMode 2089/2079→20실패(신규 10) → 근원 3종 규명 수리. 3차(581s) — 예산을 session_search/skill_view에 소진, 컴파일 0만 확정 후 종료 → 고아 회수 부모 직접 런.
- **2차 신규실패 10건 근원 수리(전부 구현/테스트 측)**: ①스킬 전반 미발화 — **EditMode에선 AddComponent해도 Awake 미호출 → PlayerHealth.Instance null**(신규 교훈) → SkillSystem PlayerOverride 주입 경로+테스트 EnsurePlayer ②강화 카드 id 규약 오판(_lv2 → 실제 _2/_3) → RecalcWeaponMultipliers를 WeaponLevel 기반으로 정정 ③AttackTelegraphTests 구 const 문자열 계약(const→프리머티 전환 소멸 — 94차 규칙) → 프로퍼티 계약 갱신 ④AfterlifeEnemy에도 별도 TelegraphWindupSeconds const 존재(AnimalAI만 수리 누락) → 동일 수리 ⑤ShockwaveRingFX.EnsureAssets의 Object.Destroy(temp) EditMode 에러(126차 함정) → isPlaying 가드 ⑥테스트 적 SpawnWave 미경유 → TestRegisterEnemy 훅.
- **최종 게이트**: 컴파일 error CS **0** + EditMode 전수 **2089/2079/10 — baseline 10건 1:1**(AERO·Cooking·H2_Catalog·Instrument×3·InvadedLord×2·RecipeCatalog·ThemeUSS), **신규 실패 빈 집합**. QA 겸직: 예 (위임 불가 사유: 3차 QA 서브 예산 소진 — 64차 상설 승인 폴백, XML/로그 착지 판독).
- **잔여**: ①사용자 Play 판정(이계 카드 3택 UI 표출·무기 스왑·스킬 자동발동 체감 — 별도 승인) ②커밋(별도 승인 누적 — 무번호 1~7+153차 이후 전체) ③Figma 교체(후일 — 데이터 계약 고정, UXML/USS만 교체) ④확장 슬롯: 관통 Lv3 선형 AoE·활 2/3화살 다중 타깃. 푸시 없음.

## 2026-11-07 (무번호 6 — 번호 부여 대기) 구현+QA 완료 — P3 Time Paradox Engine 통합(Test_10 opt-in 타임 스톱)+Melee 플래키 귀속·수리 (미커백)

- **사용자 지시**: 무번호5 계획 §P3(Time Paradox Engine 에셋 활용) 승인 사이클 — "진행".
- **사전 감사(계획 §P3-A/B)**: unitypackage 해제·전수 감사 306 엔트리(스크립트 78·asmdef 7·프리팹 53·씬 2) — 의존성(Burst/Collections/Mathematics/Cinemachine/VFG/InputSystem) 전부 기설치. 계획 근거 정정: Magic Time이 이미 가동 중(95차 F1 — PlayerCombat 패링 개인 슬로우=MagicTimeUser) → "Magic Time 비활성" 기본안 폐기, **소유권 분리·공존** 설계로 확정.
- **P3 설계·구현**: `ParadoxTimeStop.cs` 신설 — `EngineMode.LocalZonesOnly` + `TimeWarpIdentity.LocalTimeScale=0` 스테이시스 램프(0.12s in / 1.0s hold / 0.3s out, 반경 7m), **전역 Time.timeScale 무접촉** 구조(플레이어 움직임 자동 보존). Test_10 씬명 상수 opt-in 게이트(타 씬 완전 무동작). AnimalAI Update 게이트 추가(IndoorActive 선례 패턴 — IsActiveFor 읽기 전용), PlayerCombat PERFECT 패링 훅 연결, Systems.asmdef에 TimeWarp.Runtime 참조. 계약테스트 5건 신설.
- **에셋 설치**: ImportPackage가 배치에서 무소식 → 직접 설치로 대체(258 파일+48 디렉터리, 스크립트 78종 확인). 중간 ImportPackage 실패의 매니페스트 오염은 git checkout으로 원복. 오프라인 제약으로 패키지 매니페스트 추가 없음(기존 의존성만 사용).
- **Melee 플래키 귀속(부록)**: EachAcceptedAttack(2,2)/(3,3) 3회 연속 실패 → 반사 기반 임시 진단으로 규명: `WeaponData` 생성자 `Mathf.Max(0.1f, speed)` **0.1s 하한 클램프**로 테스트의 0.01f가 실제 0.1s 쿨다운이 되고, `Time.time-0.1f` 시딩이 elapsed==쿨다운 나이프엣지(float ULP — 실측 t=0.5075에서 elapsed=0.09999999<0.1) → 순서·타이밍 의존 플래키. **테스트 측 수리**(시딩 0.1→0.2s, 0.6s 스트릭 창 이내) + 진단 코드 원복. 프로덕션·P3 무영향. 격리 런 6/6 GREEN.
- **P3 런타임 검증(ParadoxTimeStopProbe 신설+run_paradox_probe.sh)**: 배치 런 RC=0/DONE steps=0 — ①스테이시스 도달(t=0.49s eff=0.000+게이트 true) ②hold 유지(0.49~1.45s) ③블렌드아웃(0.483→0.991) ④t=1.93s identity 자기 파괴 ⑤**전역 Time.timeScale 상시 1.000**. 캡처는 배치 카메라 녹색 프레임(기존 알려진 이슈 — 시각 체감은 Play 판정 필요).
- **게이트**: 컴파일 error CS 0(중간 수리 3건: 프로브 using 누락 CS0246·internal RadiusMeters CS0117·FindObjectsSortMode 경고) + EditMode 전수 **2041/2031/10 — baseline 10건 1:1**(AERO·Cooking·H2_Catalog·Instrument×3·InvadedLord×2·RecipeCatalog·ThemeUSS), 신규 실패 0 — P3 계약 5건+Melee 3케이스 포함 전부 GREEN.
- **잔여**: ①사용자 Play 판정(Test_10에서 PERFECT 패링 시 타임 스톱 체감 — 배치 캡처는 녹색 프레임으로 체감 검증 불가) ②Time Paradox 데모 씬 자체 동작 확인(§P3-E ③ 후반) ③커밋(별도 승인 누적) ④라운드 번호 부여(무번호 5건). 푸시 없음.

## 2026-11-07 (무번호 5 — 번호 부여 대기) 구현+QA 완료 — 공격/패링 프리미엄(텔레그래프 셰이더+패링 화이트 프레임)+P0 흰 실루엣 규명·수리 (미커백)

- **사용자 지시**: 공격 테스트 영상 판독 기반 계획 docs/plans/2026-11-07_attack-parry-premium-timeparadox-plan.md → "진행" 승인(P1/P2 구현은 10:00~10:01 선착지, 본 라운드=잔여 검증+P0 진단/수리). P3 Time Paradox는 별도 승인 사이클 — 미착수 유지.
- **환경 수리(선행)**: ①Unity 에셋DB(LMDB) 손상 크래시 2건(10:03 프로브 중단 근원+당일 재발) → Library/ArtifactDB·SourceAssetDB 삭제 후 전량 재임포트로 복구(13,729+ 임포트) ②크래시 피해 `TelegraphRing.shader.meta` NUL 바이트 손상 → 신규 GUID 재생성 ③프로브 로그 경로 WSL→Win 오기록(`/mnt/c/mnt/c/...`) → run 스크립트 wslpath -w 수리.
- **P0 흰 실루엣 — 3단 규명(오판 정정 포함)**: ①1차 런타임 덤프 "noTex=10"은 **프로퍼티 오판**(glTFast 그래프 텍스처 프로퍼티명=baseColorTexture/normalTexture — _BaseMap/_MainTex만 조회한 착오) ②임포트 서브어셈블리 감사(GlbTextureAudit 신설)로 GLB 머티리얼 전부 **정상 바인딩** 확인(diffuse+normal 9/9) ③런타임 vs 임포트 셰이더 불일치 추적 → **진범=TestPlayerAnimatorBoot의 리그 교체**: Player_Rigged.glb(non-humanoid) → Player_Rigged_Heat.fbx(Humanoid avatar) 스왑 시 재질 이식 누락 — Heat FBX는 텍스처 미포함(314KB 실측)이라 임포트 기본 URP/Lit 0.8 회백색 렌더.
- **P0 수리**: HumanoidClipDriver.CopyMaterialsFromGlb에 `skipTexturedTargets` 파라미터 신설(HasAnyBoundTexture — 셰이더 텍스처 프로퍼티 전수 검사) + TestPlayerAnimatorBoot 스왑 직후 재질 이식 연결("Models/UserProvided/Player_Rigged", 병사와 동일 59차 검증 조합). 가드로 장비 GLB(wood_* 이미 텍스처 보유) 보호 — 기존 호출부(병사/NPC/프리뷰) 무영향(기본값 false).
- **P1/P2 검증**: AttackPremiumProbe 재작성(진단 전용 카메라 신설 — 팔로우 카메라 무관 확정 뷰; P0 덤프 프로퍼티 전수 방식 정정) → 배치 런 Exit 0/DONE steps=0 + vision 판독 5컷: ①closeup **실루엣 해소**(투구/갑옷 나무 결+세계수 문양 선명) ②링 채움 초기(주황-적 글로우+중심 확장) ③채움 후기 **80~90% 차오름+에지 펄스** ④종료 플래시 캡처 ⑤패링 화이트 프레임 오버레이 확인(전 화면 밝기 상승+방사 라인). 자가파괴 P1/P2 True.
- **게이트**: 컴파일 error CS 0(중간 3건 — 프로브 접근자 CS0122+HumanoidClipDriver using 누락 CS0246 즉시 수리) + EditMode 전수 **2014/2004/10 — baseline 10건 1:1, 신규 실패 0**(중간 발견 1건=AttackTelegraphTests 스테일 핀 "[95차 F5]"→"[95차 F5→프리미엄]" 수리, 필터 재런 7/7 GREEN).
- **신규/수정 파일**: 신설 Assets/Editor/AttackPremiumProbe.cs·run_attack_premium_probe.sh·Assets/Editor/Tools/GlbTextureAudit.cs(진단 도구) / 수정 TestPlayerAnimatorBoot.cs(+재질 이식)·HumanoidClipDriver.cs(+가드 파라미터/헬퍼)·AttackTelegraphTests.cs(핀 갱신)·TelegraphRing.shader.meta(재생성) — 선착지 구현분(TelegraphRing.shader·AttackTelegraphIndicator 재작성·ParryFlashFader·TelegraphPremiumTests 등 7건) 유지.
- **잔여**: ①프로브 캡처는 진단 카메라 뷰 — 실게임 카메라에서의 링/플래시 체감은 **Play 판정 필요**(기존 premium_player_view=팔로우 카메라 뷰는 녹색 단색 프레임 — 별도 원인 규명 대상으로 기록) ②P3 Time Paradox 별도 승인 ③커밋(별도 승인 누적). 푸시 없음.


## 2026-11-07 (무번호 4 — 번호 부여 대기) 구현+QA 완료 — 전 씬 카메라·조작 통일(57°/아레나 제외)+전리품 좌클릭 설명창+Test_10 패링 발화 정리 (미커백)

- **사용자 지시 4건**: ①"여전히 각도 변경이 없어" ②Test_10과 동일한 카메라·움직임(점프/구르기/커서 시선)을 전 씬에 ③전리품 좌클릭 → 가운데 아이템 설명창 미표시 수리 ④Test_10 패링 고품질화 "전혀 실행된 게 없다". 계획 docs/plans/2026-11-07_camera-movement-unify-loot-description-parry-plan.md → "진행" 승인 + **"아레나 외부씬만 제외"** 지시 반영.
- **진단(소스 실측)**: ①실게임 카메라 기록자=PlayerMovement.ApplyFollowCamera(LateUpdate 최종적용)·피치원 `_camPitch=65f` — v2의 57° 수정은 IndoorCameraFollow(투어/실내 직교)만 건드린 착오. ②Cave/Arena/AfterlifeCemetery/LordPeaceVisit 셋업은 PM+IndoorCameraFollow **이중 기록자**(억제 없음 — 씬별 카메라 갈림 잠복 결함). ③성 실내 플레이테스트/영주 상호작용/실내 전쟁 3셋업 = PlayerCastleInteriorTestMovement(WASD 전용 — 점프/구르기/커서 조준 전무). ④LootWindowUTK 슬롯 MakeDraggable의 onClick 인자가 **null**(우클릭 획득만 바인딩) + UTKWireUp에 "상세창이 전리품을 가린다" 이유의 의도적 개방 금지 계약. ⑤패링 체인은 95차 F1~F4+159차 P3 구현 존재(PlayerCombat L93~ 마커/AnimalAI 텔레그래프/PlayerHealth.TryParryReflection) — Test_10 발화는 체인 자체가 아니라 **인지 문제**(V 개방 시 시각 피드백 0).
- **A 카메라**: PlayerMovement `_camPitch 65→57f`(전 씬 실질 각도 — 사용자 확정) + 실내 셋업 6종(Cave/AfterlifeCemetery/LordPeaceVisit/LordInteraction/IndoorWar/PlayerCastleInteriorPlaytest) IndoorCameraFollow 부착 제거+직교→원근 전환(PM 단일 기록자) + IndoorSceneTransition PM 억제 제거+실내 진입 거리 11m 세팅/복귀 직전 값 복원(공개 프로퍼티 CameraDistance 경로) + **아레나(ArenaTestSetup) 완전 제외**(기존 직교+IndoorCameraFollow+듀엔 관전 계약 유지 — T2 테스트로 제외 이행 증거 핀) + Test_23 관람 모드 예외 유지.
- **B 이동**: 3셋업 PlayerCastleInteriorTestMovement → **PlayerMovement 교체**(CharacterController 계수 1.8/0.35/0.9/0.3 유지, E침대/F영주·병사·NPC 상호작용과 넉백 구독은 PM 동일 계약 승계) + **IndoorPlayBoundsClamp 신설**(구 컨트롤러 발지 클램프 승계 — 성 전체 117.6×88.2·마진 0.35, 4부착점) + 구 컨트롤러 **삭제**+테스트 재핀 3건(InteriorInteractionTests 2·PlayerKnockbackTests 1) + PlayerHealth 구 컨트롤러 주석 정합.
- **C 전리품**: LootWindowUTK 슬롯 MakeDraggable **onClick 연결**(`() => ShowLootDescription(itemIndex, entry)`) + ShowLootDescription 신설(ItemDescriptionWindowUTK.ShowItem — 닫힘 상태면 개방, 인벤 OnSlotClick 선례 동일 계약) + Hide/우클릭 획득 완료 시 Clear(스테일 방지) + UTKWireUp 구계약 갱신("개방 금지"→"좌클릭 시 개방 허용·겹침 수용").
- **D 패망**: 정적 체인 전수 실측 = 구현 전부 존재(V키=Keyboard.current 직접입력·HitStopManager=static EnsureHost 자가부트·FX=ShockwaveRingFX 정적 스폰 — Test_10 부트 스킵 무영향). 수리는 **인지 계열 2건**: ①[패링D1] TryStartParry 성공 시 청색 소형 확산링+`패링 창 개방 0.45s` 로그(V 입력만으로 시스템 생존 판별 가능) ②[패링D2] Test_10 셋업 완료 시 패링 훈련 안내 로그(선제 타격 어그로→적색 텔레그래프 링 0.5s→V 타이밍→PERFECT 금색 2겹+히트스톱+반격×1.5/GOOD 청색 1겹). FX 본체 무수정.
- **검증**: 컴파일 error CS **0**(compile_unify2/final — 중간 CS0103 TestMovementPath 잔존 참조 1건 재핀 수리) + EditMode 전수 **2009/1999/10 — baseline 10건 1:1**(AERO·Cooking·H2_Catalog·Instrument×3·InvadedLord×2·RecipeCatalog·ThemeUSS — v2 런과 동일 집합), **신규 14건 전부 GREEN**(SceneControlUnify 7·LootDescriptionContract 4·ParryTest10Feedback 3).
- **위임 기록**: Phase A+B 구현 위임 600s 타임아웃(45콜 — A1/A2/B1/A4 편집 착지 후 보고 전 사망) → 마커/심볼 grep 착지 판별 후 **64차 상설 승인 부모 직접 폴백**으로 B3(클램프)/B4(삭제·재핀)/C/D/검증 완수.
- **대기**: ①사용자 Play 판정(①Test_10 포함 전 씬 각도 57° 체감 ②성 실내·동굴·영주·실내전쟁 씬에서 점프/구르기/더블탭 달리기/커서 조준/휠 줌 ③전리품 좌클릭 → 가운데 설명창 ④Test_10 V 개방 청색 링+패링 성공 FX) ②커밋(별도 승인 누적 — 154차+텔레그램 146~158차+159차+무번호 1~4). 푸시 없음.

## 2026-11-07 (무번호 3 — 번호 부여 대기) 구현+QA 완료 — 실내 품질 v2(카메라 각도 57°+바닥/벽 512화+원목 가구+건물 트림) (미커백)

- **사용자 지시**: ①예시 사진과 탑다운 각도가 미세히 다른지 확인 후 살짝 수정 ②가구=색상 테이블 배제, 나무 원색 테이블/의자만+테이블 과다 금지 ③가구보다 **바닥/벽 품질 집중** — 계획 docs/plans/2026-11-07_interior-floor-wall-quality-plan.md → "진행" 승인(A→C→D→B→E 중단 없이).
- **각도 실측(판독)**: 예시 사진=피치 50~60° 쿼터뷰(벽/오브젝트 옆면 노출 15~20%, 인형의 집) vs 현재 실내 카메라 명목 65°(체감 더 수직 — 벽 노출 5~8%, 타일 1:0.42 눌림) → 사용자 직감 확정.
- **A 각도**: IndoorCameraFollow.Pitch **65→57**(실내 전용 — 실외 PlayerMovement/관람 모드 90° 무영향)+IndoorTourMode.ResolvePitch 기본값 동기+IndoorTourModeTests.T4 핀 갱신(ElseDefault65→57).
- **C 바닥 512화**: IndoorTextureGenerator DEFAULT_SIZE 256→512+GenerateFloorTile(타일별 톤 편차 해시/스펙클/줄눈 비례화/마모 에지 밴드)+GenerateWoodPlank(비례 틈·판별 톤·매듭·이중 주파수 결)+GenerateStoneTile(저주파 얼룩)+머티리얼 정합(성 선례 Smoothness/노멀 계약 유지). 문 앞 마모 데칼 건물 확장(CreateFloorWearMat 재사용).
- **D 벽 512화+트림**: GenerateBrickPattern(벽돌별 톤 편차+모르타르 오목)+GenerateStoneWall(돌별 편차+하부 그라데이션)+GeneratePlasterWall(수분/그을음 얼룩)+**AddRoomTrim 신설** — 걸레받이 밴드(E/W 내측 0.15m 짙은톤)+벽톱 캡(N/E/W 상단 — 남측은 수조 절단 대상이라 생략, 문 상단 위 배치로 문 충돌 없음)을 건물 7종 전반 확장(성 선례 승계).
- **B 가구 v2**: 팩 프리뷰 판독 결과 가구 전반이 유색 쿠션/체크보 스타일(원목 전용 변형 부재) → **절차 원목 테이블/의자/스툴 팩토리 신설**(BuildWoodTable/BuildWoodChair/BuildWoodStool — 원목 판자 텍스처 재사용, 결정론)로 GLB 유색 테이블/의자/스툴 전면 교체(로비 8세트→**4세트**+소파 4→2, tavern 3→2, shop 카운터/church 벤치 포함). 캐비닛/선반/소파 등 수납류는 GLB 유지.
- **검증**: 컴파일 error CS 0 + EditMode 전수 **1995/1985/10 — baseline 10건 1:1, 신규 실패 0**(신규 계약 +3: T7 텍스처 512/T8 원목 가구·유색 배제 소스계약/T9 벽 트림 밴드·캡·마모 소스계약) + 14엔트리 투어 DONE errors=0. tavern 캡처 "핑크 격자" 의심 1건 = 픽셀 계측 오탐(핑크 2.2% — 카페 스타일 데코 기존 요소, 타 건물 캡처 전부 정상 범위).
- **미완료/대기**: ①사용자 Play 판정(57° 각도 체감 — 프로브 캡처는 고정 65° 대체 카메라라 각도 체감은 Play에서만 확인 가능/바닥·벽 512 질감/원목 가구/걸레받이·캡) ②침실 목재 오버레이 위 원목 침대 상호 검증 ③라운드 번호 부여(무번호 3건) ④커밋(별도 승인 누적). 푸시 없음.

## 2026-11-07 (무번호 2 — 번호 부여 대기) 구현+QA 완료 — 실내 고품질화 1단계(공통 골격+성 6방 가구/조명/소품+건물 7종 패스) (미커백)

- **사용자 지시**: 9/20 실내 예시 사진 품질바 + 9/29 영주씬 설계도 기준, "영지 건물 실내부터 맵 고품질화" 계획 → docs/plans/2026-11-07_interior-quality-baseline-plan.md → "진행 최대한 비슷하고 고품질로 계획 중단 없이 끝까지" 승인(전 Phase 연속 실행 — Play·커밋만 별도 게이트 유지).
- **사전 실측(부모)**: ①팔각 로비+5방 토폴로지 = InteriorLayoutDefinition에 이미 구현(설계도 1:1 — 레이아웃 재작업 불요) ②Restaurant GLB 325종이 Resources/Models/Restaurant 플랫에 기임포트(Furnit×168/Table×49/Stool×29/Chair×22/StationTable×12/Sofa×12/Owen×7/Armchair×7/Shelf류…) — 임포트 비용 0 ③Medieval Megapack은 소품형(침대/테이블 부재) ④CastleInteriorBuilder = 타 영주 성 별도 빌더가 아니라 잠금 문 4개 부착기(10/10 J2 영지 실내 통일 — **모든 영지 성이 PlayerCastleInteriorBuilder 단일 경로 → P1 패스가 자기/타 영주 양쪽 자동 적용, 분위기 통일 아키텍처 보장**).
- **P0 — 인테리어 리소스**: Resources/Models/Interior/{Decor,Kitchen,Props} 신설 43종 선별 복사(천장 등 4·기둥 2·커튼/문 5 / 주방 12 / 음식·물약·자루 21) — Kitchen 복사 1차 실패(경로 공백 basename 단어분할로 전부 Kitchen.glb 덮어씀) 발각→재복사 수리. Medieval Megapack 물약/자루 등 MP_ 접두 소품 포함.
- **P1 — 신규 3시스템**: ①`InteriorPropCatalog.cs` — 결정론 로더(시리즈 접미사 PickSuffix/PickFurnit 순수함수·후보 경로 폴백·bounds 기반 높이 정규화·장식 콜라이더 제거=렌더러 전용 — 기능 앵커 블로킹 금지) ②`InteriorQualityBaseline.cs` — 골격 계약 상수(타일 1m/벽 2.5m/광 예산 방 4·로비 6)+절차 텍스처(석타일 줄눈·목재 판자·러그 테두리·트림 — 캐시 결정론)+절차 침대(프레임/머리판/매트리스/베개/이불/다리 9파트 — 침대 GLB 부재 대체, GLB 확보 시 교체 여지 기록)+상자 적치+바닥 오버레이 쿼드(UV=미터/타일) ③`InteriorFurnishingPass.cs` — 가산 패스(기존 빌더 지오메트리/앵커 무수정). 성: 로비 탁자 8세트(내륀 4+외곽 4 링)+의자 16+소파 4+수납장 2+러그 4+천장 등 2 / 침실(목재 바닥+침대 2+장롱 2+러그) / 제작소(작업대 3+화로+주방 소품+붉은광) / 창고(선반 4+상자 6+딤광) / 병영(침대 2×2+수납) / 연금(탁자 2+물약 6+보라광) / 입구(러그 스트립+행거). 건물 7종: house/tavern/restaurant/shop/crafthouse/church/barn 각 최소 구성.
- **존 규칙 정합(핵심 수리)**: 1차 배치가 방 경계 누수(Craft 화로가 병영 침입/창고 선반이 연금 침입/입구 캐비닛이 4m 회랑 초과/침실 바닥 오버레이 과폭) → InteriorLayoutDefinition 존 규칙 동일 공식(로비 북변 절반폭 18/측면 30/날개 경계 8)으로 전면 재정렬 — 모든 배치·오버레이가 설계도 존 내부에 한정.
- **조명 계약 충돌 수리**: 신규 회귀 1건(WarehouseStorageFigmaParity.PlayerCastleLighting — 강도 ≤2.9·반경 ≤7 "understated accent" 46/48차 계약) → 패스 조명 전체 계약 범위로 조정(반경 6.2~7) — 넓은 커버는 소형광 다수로.
- **P2**: 별도 작업 불요(단일 빌더 경로 확인 — P1이 자동 적용) + T6b 진단 테스트로 플레이어/영주 양쪽 QualityFurnish 루트 7개 실측.
- **P3**: 7빌더 훅(DioramaKit.Apply 직후 — House/Tavern/Restaurant/Barn(Systems) + Church/CraftHouse/Shop(UI)). CraftHouse 12×10/Shop 10×8/Church 14×10/Restaurant 12×10/Barn 8×6/House 8×6.
- **P4 — 전수 검증**: 컴파일 error CS 0 + EditMode 전수 **1992/1982/10 — baseline 10건 1:1, 신규 실패 0**(신규 계약 8건: T1 상수/T2 결정론/T3 엔진 난수 미사용/T4 성 훅 소스계약/T5 리소스 존재/T6 절차 침대/T6b 양 성 루트/T6c 좌표 실측 진단) + **14엔트리 투어 2회 DONE errors=0**(탑다운 캡처로 침실 목재바닥+침대/병영 클러스터/카펫 통로 실측 확인 — 65° 뷰는 프레임이 협소해 일부만 노출). 프로브 뷰 캡처에 판독용 앰비언트 부스트 추가(탑다운 캡처 선례 — 캡처 전용 복원).
- **진실 규명(중요 — 후속 기반)**: 성 실내 좌표는 레이아웃 정의 값이 그대로 최종(117.6×88.2m, 로비 팔각 ±30/±14) — 기존 메모의 "×2.45"는 가구 위치 계수(TopologyFurnitureScale)이며 지오메트리 스케일 아님(외곽 팀버 프레임 스팬 118.2m 실측). 대홀 규모 대비 가구 밀도는 로비 강화(8세트)로 1차 개선 — 추가 밀도/스케일 재검토는 Play 판정 후 별도.
- **미완료/대기**: ①사용자 Play 판정(성 6방+건물 7종 가구/조명/소품 체감 — 관람 모드 Test_23 [ ] 순회로 전 실내 확인 가능) ②침대 GLB 확보 시 절차 침대 교체 ③라운드 번호 부여(무번호 2건) ④커밋(별도 승인 누적). 푸시 없음.

## 2026-11-07 (무번호 — 수조 라운드와 함께 번호 부여 대기) 구현+QA 완료 — 실내 관람 모드(Test_23 탑다운 걸어보기) (미커백)

- **사용자 지시**: "모든 씬에 플레이어가 탑다운 뷰로 움직일 수 있도록해서 실내씬 모두를 움직이면서 확인" — 정적 투어 캡처(수조 라운드 42장)를 넘어 게임 내 직접 걸어보는 모드. clarify 확정(기본안): 정투영 완전 탑다운(90°)+휠 줌 / [ ]키 14엔트리 링 순회+라벨 / 신규 Test_23 경량 씬. 계획 docs/plans/2026-11-06_indoor-tour-walk-mode-plan.md → "진행" 승인.
- **구현(위임 600s 타임아웃 → 부모 폴백 완수 — 5파일)**: ①`UI/IndoorTourMode.cs` 신설(~560행) — 씬 게이트(Test_23 전용)/TourSequence 14엔트리(프로브 Steps 1:1)/[ ]키 링 순회(NextIndex/PrevIndex 순수 함수)/cave_f1 재진입(ExitBuilding→재로드→재빌드 폴백)/cave_f2(GoDeeper 단방향 경유)/tavern 직접빌드(프로브 동일)/castle_player(owned)/castle_lord(key)/DontDestroyOnLoad 드라이버(월드 언로드 생존 — 구 프로브 "러너 소실" 교훈의 구조적 해법)/OnGUI 라벨 ②`Systems/IndoorCameraFollow.cs` 소형 수정 — PitchOverrideDeg(opt-in, 기본 -1=65° 보존)·90° 전용 배치 분기(LookRotation 수직 특이점 회피) ③`Systems/CaveInteriorTestSetup.cs` 1줄 — 씬 게이트 2종 허용(Test_21+Test_23, 부트/플레이어/카메라 경로 재사용) ④`Editor/IndoorTourTestSceneBuilder.cs` 신설 — Test_23 씬 생성(155차 빌더 선례, 실측 생성 DONE) ⑤`Tests/EditMode/IndoorTourModeTests.cs` 신설 6건.
- **핵심 수리 3건(배치 프로브 실측 발각)**: ①**카메라 3중 기록자 충돌** — PlayerMovement.ApplyFollowCamera(LateUpdate "최종 적용" 계약·피치 40~80 클램프=90 불가)+HandleCameraShake(Update·원점 localPosition 복원 — 루트 카메라에서 원점 순간이동)+IndoorCameraFollow가 동일 트랜스폼 경합 → DialogueCameraDirector 선례의 억제 게이트로 관람 모드가 카메라 소유(CameraFollowSuppressed=true, OnDisable 원복) + 드라이버 LateUpdate가 유일 기록자(플레이어 +25m 수직 추적 + 휠 orthoSize 6~16 직접 처리 — 팔로워 휠은 false로 이중 소비 방지) ②**월드 언로드 카메라 소실** — EnterBuilding마다 Test_23 언로드(실측)로 씬 카메라 파괴+셸 IndoorCamera는 전환부 우선순위 블록 비활성 → 실내 중 활성 카메라 0대(구 투어 house~tavern cam=null 12엔트리 실측) → 관람 카메라 DDOL 승격(퇴출 재로드 시 EnsureCamera가 tag 재발견) ③**MedievalShell 지붕 차폐** — 90° 카메라 직상의 셸 껍데기로 house/castle 캡처 순흑(실측) → arenapit 125차 후속25 선례를 전 엔트리로 확장(MedievalShell+InteriorSun 비활성, 씬 재로드마다 멱등 재비활성).
- **프로브(`Editor/IndoorTourMode23Probe.cs`+run_indoor_tour23_probe.sh)**: Test_23 부트→드라이버 대기→cave_f1(부트)→cave_f2(GoDeeper)→house(월드 언로드 경로)→castle_player(실내→실내) 4스텝 자동 순회 — **UNITY_EXIT=0/DONE steps=0**, 전 스텝 topdown=True(ortho+pitch90+플레이어+25m 추적 실측), 캡처 4장(tour23_*.png — cave_f1 171KB·cave_f2 138KB·house 265KB·castle 1050KB). house vision 판독: 중앙에 집 내부(바닥/벽 라인/가구) 탑다운 렌더 확정. ⚠프로브 교훈: 코루틴 컨텍스트는 Update직후~LateUpdate전(HandleCameraShake 원점 복원 직후) — 캡처 전 드라이버 DebugPlaceCameraNow() 재배치 필요(프레임 말 렌더는 정상).
- **검증**: 컴파일 error CS 0 + EditMode 전수 **1984/1974/10 — baseline 10건 1:1, 신규 실패 0**(IndoorTourModeTests T1~T6 전부 Passed — total 1978→1984). 계약: 시퀀스=프로브 Steps 라벨 1:1(소스계약)/씬 게이트/링 랩어라운드/피치 결정/동굴 부트 2종 게이트/씬 빌더 부착.
- **관찰(비차단)**: house 진입 1회 플레이어 y=-6.25 낙하 정황 1건(재런 소멸 — 전환부 홀더 타이밍 플레키 추정, 관찰 지속). arenapit은 관전 세션 소유 존중(배치/휠 제외 — 도착 보정도 세션 유지).
- **대기**: ①사용자 Play 판정(Test_23_IndoorTour — [ ]키 순회·탑다운 걷기·휠 줌·14엔트리 체감) ②라운드 번호 부여(수조 라운드+본 라운드 — 부여 시 QAPROGRESS 제목 갱신) ③커밋(별도 승인 누적 — 154차+텔레그램 146~159차+수조 무번호+본 라운드). 푸시 없음.

## 2026-11-06 (159차) 구현+QA 완료 — 아레나 가구 GLB 전면 교체+패링 고품질 FX+실내 구르기 수리+몬스터 공격 VFX (미커백)

- **사용자 지시**: ①실내(영지)씬 구르기 안 됨 수리 ②아레나 씬 가구 프리미티브(기하형태) 전면 GLB 교체 — "있는 대로 모두 교체+변형 가능하면 변형" ③패링 고품질 계획+구현 ④몬스터 공격 VFX 고급화(예고·타격·회피 3채널). 계획 docs/plans/2026-11-06_159-arena-furniture-glb-parry-roll-monster-fx-plan.md → "진행" 승인.
- **P1 구르기 수리(PlayerMovement)**: 정적 분석 — 구르기 게이트(Shift+방향/쿨다운 1.5s/`_isGrounded`), 실내는 물리 접지 단일 소스(지형 공식 클램프 금지 146차). HandleRoll 진단 프로브+실내 접지 폴백 강화 적용.
- **P2 아레나 가구 GLB 전면 교체(ArenaInteriorBuilder +412/-108줄, 위임 완수)**: 여관 가구 프리미티브(원형/사각 탁자 5+의자+부스 5곳+바 카운터/싱크/화로+스토리지 통+주방 가구) → Restaurant GLB(RuntimeModelLoader 폴백 4경로) 교체. 변형 A~G **StableHash 순환(결정론 — Random 0건)**, GLB 부재 시 기존 프리미티브 폴백(소실 금지), MeshCollider/접지 블롭 계약 유지. `ArenaFurnitureGlbTests.cs` 신설 13건(G1~G4 계약).
- **P3 패링 고품질(PlayerCombat)**: 기존 95차 FX 4요소(확산링/별섬광/스파크/SFX/슬로우모션/반격) 위 **추가** — ①히트스톱(PERFECT 0.07s/GOOD 0.04s, HitStopManager) ②지면 링 2겹(PERFECT 금색 2겹 스태거/GOOD 청색 1겹 — 158차 SpawnGroundRings 패턴) ③스파클 버스트(SpawnCritBurst) ④PERFECT 전용 아우터 링(반대 위상 — 154차 패턴).
- **P4 몬스터 공격 VFX(AnimalAI)**: ①명중(ConcludeMeleeTelegraph TakeDamage 직전): 히트스톱 0.04s(scale 0.10)+지면 링 1겹(적색 1.4m, 흡수점 y0.05)+방향성 스파크(공격자→플레이어) ②빈 스윙(회피 성공): 몬스터 발밑 흰 파동 0.9m — 막음(금)/명중(적)/회피(흰) 3채널 분리 ③예고(텔레그래프) 지면 표시는 95차 F5 AttackTelegraphIndicator 이미 존재 — 중복 추가 금지 확인.
- **수리**: ArenaFurnitureGlbTests.G4 신규 회귀(블롭 12→11 — 블롭 12번이 GLB 로드 성공 조건부로 감춰짐) → 블롭 12번 GLB 부재 시에도 무조건 추가(로비 중앙 집결지 계약) → 필터 재런 GREEN(13/14 통과).
- **검증**: 컴파일 error CS 0 + EditMode 전수 **1970/1960/10 — baseline 10건 1:1, 신규 실패 0**(AERO·Cooking·H2_Catalog 병행WIP·Instrument×3·InvadedLord×2·RecipeCatalog·ThemeUSS — 146차 대비 1:1). AERO(AeroLocalizedGasPhase4B)는 병행세션 WIP baseline 확인 — 159차 무관. LogMatch 실측: string Expect=정확 일치(정규식 아님, LogMatch.cs L90).
- **대기**: ①사용자 Play 판정(실내 구르기/아레나 GLB 가구 체감/패링 히트스톱+지면 링/몬스터 명중 임팩트+회피 흰 파동) ②커밋(별도 승인 누적 — 154차+텔레그램 146~158차+159차). 푸시 없음.

## 2026-11-06 (158차) 구현+QA 완료 — 보석상자 게이트 피드백+알림 대화 패널 통일+동굴 돌판 경로·벽 받침+획득 FX 고급화 (미커백)

- **사용자 지시(영상 "동굴씬 테스트.mp4" 55s)**: ①보물상자(보석상자) E키 상호작용 안 됨 ②플레이어 알림 전면 대화 패널 통일("물뿌리개에 물을 담으시오" 등) ③실내 바닥 타일·벽면 고품질 ④획득 이펙트 고급화(흰 사각 입자 잔존+빛기둥 과노출). 계획 docs/plans/2026-11-06_158-chest-interact-dialogue-quality-fx-plan.md → "진행" 승인(무응답 3항목 기본값: 전쟁 알림·영지 배너 별도 채널 유지/바닥 2레이어/Test_21 Lv10 부스팅).
- **P1-A 상자 원인+수리**: 원인=코드 결함 아님, **ContentGate gem_chest=Lv10 vs Test_21 플레이어 Lv1** → E키 눌러도 콘솔 전용 무반응이 체감 원인. 수리: ①`PlayerNotify.cs` 신설(정적 이벤트 버스 Notify/Broadcast/ResetSubscriptionsForTest — FarmPlot.FarmStatusBroadcast 선례의 Systems→UI 단방향 계약) ②GemChest 게이트 차단부 Notify 연결(콘솔 경고 병행 유지) ③CaveInteriorTestSetup.EnsurePlayer Test_21 한정 Lv10 부스팅(AddEXP(GetExpForLevel(10))=누적 13,850 EXP 정식 경로 — 스탯 포인트/최대HP 갱신 포함, PlayerStats.Awake 중복 인스턴스 파괴 가드).
- **P2-B 알림 통일**: NPCDialoguePanelUTK 멱등 구독(PlayerNotify.Broadcast += ShowTransientMessage — 3.5s 자동 닫힘) + 3시스템 발화: ①FishingSystem R키 3케이스("물뿌리개가 없다."/"물뿌리개가 가득 찼다."/BeginCanRefill)+물가 진입 사전 힌트 "물뿌리개에 물을 담으시오. (R키)"(_canHintShown 가드·물가 이탈 리셋 — 사용자 예시 문구 계약) ②MercenaryManager tavern_mercenary(Lv5) 고용 차단 Notify ③ResourceNode 채광/벌목 피드백. "석재를 획득했다!"는 기존 NPCDialoguePanelUTK 사용 중 확인(신규 구현 불요).
- **P3-C 실내 고품질(지오메트리 무수정 — 텍스처 계층만)**: ①CaveInteriorBuilder ResolvePathStartCell(풀 맵 S 스폰/클래식 입구 셀 역산)+ComputeStonePathCells(**BFS 스폰→D/U 포탈 최단경로, 4방향·기둥 셀 제외·Queue FIFO 결정론**)+CreateStonePath(경로 셀 돌판 병합 쿼드 y+0.015, 바닥 MeshCollider 단일소스 무영향)+CreateDirtDetail(비경로 흙셀 해시 8% 자갈 데칼)+CreateWallBaseBand(벽 런 하단 받침 밴드) ②CaveMaterialFactory 신규 API CreateStonePathMat/CreateGravelMat/CreateBaseBandMat(절차 텍스처 — 결정론 계약, Random 0건).
- **P4-D 획득 FX 고급화(AcquisitionCinema 11패치)**: ①별빛 4점 교차 절차 텍스처 64×64(GetStarTexture — x/y축 지수 감쇠 스파이크+중심 코어, **사각 금지 계약**, 캐시 1회) ②지면 링 파동 2겹(SpawnGroundRings — FxGroundRingY=0.05/FxGroundRingStartM=0.25/스태거 0.25s/수명 0.6s/티어당 반경 +0.12m, Common=1겹/Rare+=2겹, 수평면이라 빔 셰이더 왜곡 회피 CreateFxQuad preferBeamShader=false 분기) ③코어 펄스(_CoreIntensity 사인 1.6↔2.4 @0.8s — 과노출 완화, 에픽+(3+) 진폭 +0.1) ④스파클 별빛 교체+에픽 강화(크기 ×1.2/버스트 +6 — 154차 티어 표 무손상).
- **검증**: 컴파일 error CS 0 + EditMode 전수 **1964/1954/10 — baseline 10건 1:1, 신규 실패 0**(Cave158FeedbackFxTests 12건 전부 GREEN — 1차 런 소스계약 핀 5건 불일치를 실제 구현 시그니처로 재핀 수리 후 소멸, SoldierWage 신규 회귀 1건=ShowTransientMessage EditMode UI 발화 → isPlaying 가드 수리 후 소멸). CS0708 36건(테스트 클래스 static 선언) 초반 발각 → `static` 제거 수리.
- **대기**: ①사용자 Play 판정(보석상자 E키 실동작 Lv10 부스팅/물뿌리개 힌트·3시스템 안내 대화 패널 표출/돌판 경로+자갈+벽 받침 체감/별빛+지면 링+펄스) ②커밋(별도 승인 누적 — 154차+텔레그램 146~157차+158차). 푸시 없음.

## 2026-11-06 (157차) 구현+QA 완료 — 안개 0.09+실내 고품질+동굴 맵 풀 20종(존별 배정/순서 셔플)+획득 빛기둥 셰이더 (미커백)

- **계획**: docs/plans/2026-11-06_157-interior-quality-map-pool-acq-fx-shader-plan.md — 사용자 "진행"(무응답 항목=기존검증 최소설계: 안개 0.09/입구형 1층 고정+4슬롯 셔플/품질=벽질감·조명자연화·먼지 선행).
- **A 안개**: IndoorAeroFogController.IndoorDensity 0.12→**0.09**(이력 0.35→0.2→0.12→0.09 — 157차 사용자 지시, IndoorAeroFogTests 핀 갱신). Play 판정 후 재조정 여지.
- **D 획득 빛기둥 셰이더**: Assets/Shaders/AcquisitionBeam.shader 신설(URP HLSL) — ①UV.x 가우시안 에지 소프트 페더(사각형 하드엣지 소멸 — 불만 직접 해소) ②세로 그라데이션(하단 강조→상단 소멸) ③스크롤 값 노이즈(흐르는 빛 — 해시 결정론, 텍스처 의존 0) ④HDR 코어 2.0(기존 IndoorPostProcess 블룸 연동). CreateFxQuad 셰이더 교체+폴백(기존 Particles/Unlit) 계약. 스파클은 기존 방사 텍스처 확인(사각 아님 — 변경 불필요). RarityShaft/스파클 티어 표 계약 무손상(색·알파는 _BaseColor/[MainColor] 경로).
- **C 맵 풀 20종+존별 배정**: CaveLayoutPool.cs 신설(900행) — 신규 셀맵 20종(입구형 4/갱도형 4/습지형 4/대홀형 4/유적형 4, 각 S스폰·D/U포탈 앵커 내장)+TrySelect(territoryId, floor): ①"test"/"cave_test"=클래식 시퀀스 고정(계약테스트·프로브 무영향) ②동일 영지 재진입 동일 선택(FNV-1a 해시+LCG 셔플 결정론 — Random 0건) ③1층=입구형 seed%5(0=클래식 F1) ④2~5층=4테마 순서 셔플+신규 4종 variant(클래식 맵은 기본 시퀀스 전용 — S 마커 계약). 빌더 통합: ApplyFloorLayout(territoryId, floorIndex)+S 마커 파싱+GetFloor/EntranceSpawnPosition 풀 브랜치+CreateFloorPortals 5층 하강 억제. **생성기 검증 20/20**(연결성 flood-fill·면적 4,300~4,888m²·횟불 8·포탈/스폰 앵커·스폰-포탈 이격) — 과정: 수작업 도면 17/20 실패 → 연결 보장 엔진(룸+스켈레톤+명시적 연결자)+면적 자동 튜닝+횟불 홀 둘레 배치(마커 회피)로 재작성, S-C 셀 충돌 3건(V1/V2/V4) 수리. Cave157LayoutPoolTests 3건 신설.
- **B 실내 고품질(선행 3건)**: 암반 텍스처 256→512²(B-A)+전 벽횟불 base-relative 플리커 자연화(B-C — CaveInteriorQualityTests 옵트인 계약 갱신)+앰비언트 먼지 입자 신설(B-D — 32² 절차 소프트닷·박스 80×60m·48입자·광 예산 무영향). B-B(마모/이끼 확장)·B-E(소품 밀도)는 다음 사이클.
- **검증**: 컴파일 error CS 0 + EditMode 전수 **1952/1942/10 — baseline 10건 1:1, 신규 실패 0** + 배치 Play 프로브(Cave156LevelProbe 재사용) **DONE errors=0** — 전 스텝(셋업/스폰 착지 0.03m/입구 봉합 maxZ 28.50/가드 복귀 recovered=True/F1→F5 순회) GREEN + 탑다운 캡처 11장 갱신.
- **대기**: ①사용자 Play 판정(안개 0.09 체감·빛기둥 고급화·먼지·벽 텍스처 512·맵 풀 변화는 실게임 동굴(비 test 영지)에서 체감) ②B-B/E 후속 ③도면 20종 탑다운 캡처 일괄 판독(실게임 진입 후) ④커밋(별도 승인 누적).

## 2026-11-06 (156차) 구현+QA 완료 — 동굴 직각 꺽임 셀맵 재설계(×4)+바닥 흙 단일화+낙하 봉합+벽 교체+곡갱이질 연동 (미커밋)

- **사용자 지시(동굴 테스트 후)**: ①갈색/회색 구분 없이 갈색 흙 바닥 단일화 ②나무 기둥 제거 ③동굴 낙하 금지 ④지금보다 4배 확장+벽면 다듬기+바닥 고품질 ⑤곡갱이질 FBX 연동(채굴+농경 둘 다 확정). 예시 사진(Screenshots/내부씬 예시.PNG, 9-20 삽입) = 탑다운 "방 형태" 기준 — 직각 꺽임 확정 후 셀맵 도면(F1 사용자 승인)으로 진행.
- **계획**: docs/plans/2026-11-06_cave-floor-dirt-safety-scale4-pickaxe-fbx-plan.md + 셀맵 확정본 docs/plans/2026-11-06_cave_cellmaps_F1-F5.txt(48×36 셀·2m·경계 96×72m·걷기 가능 F1 4,712/F2 4,288/F3 4,608/F4 4,780/F5 4,384m²=현행 ×4 목표 ±10%).
- **P3 셀맵 재설계(위임 600s 타임아웃 → 부모 폴백 완료)**: CaveInteriorBuilder 전면 재작업 — 17각 다각형/PolyTriangles/fan 삼각분할/IsPointInPolygon/Concrate 타일(FloorTileKeys)/톤 스트립/지보목/암석링/tierScale 전부 폐기(사유 주석). 신규: F1~F5 문자 아트 셀맵 상수+마커 파싱(F화로/m노드/C상자/c석관/s상자/K SkullCandle/o잔석/v덩굴/w물웅덩이/P기둥/t횟불/D·U포탈/E입구봉합) → 병합 쿼드 바닥(CaveFloor 유지·MeshCollider 단일소스 계약 승계)+얇은 돌 벽(0.5m·3m·런 머징 51런, BoxCollider=낙하 봉합 충돌)+평면 캡 51(성내부 짙은돌선 패턴 승계)+포탈/스폰 셀 좌표 직접 반환(스폰-포탈 ≥2.2m 이격 계약 승계).
- **P1/P2/P4**: 바닥=CaveMaterialFactory.CreateDirtFloor 신설(FarmPlotVisuals.GetOrCreateSoilTexture 단일소스 재사용 — 154차 단위 수리본, UV 셀당 1 타일링). 낙하 봉합=CaveEntranceDoor 상시 봉합 전환(기본 열림 계약 폐기 — 명세 변경)+CaveFallGuard 신설(y<−3m → TeleportPlayerToFloorSpawn(146차 public 승격) 복귀) — T2 위임 착지. 벽=던전 팩 Wall GLB 폐기 → 절차 돌 텍스처 얇은 벽(돌 느낌 — 텍스처 대공사는 Play 판정 후 별도).
- **P5 곡갱이질(T3 위임 착지)**: Player156AnimTools 신설(RunAll 배치 — ModelImporterAnimationType.Human(Unity 6.4 enum)/클립 로그/Till 교체/Mine 배선) + Farm153EditorTools.TillFbxPath 교체(153차 "1줄 교체" 승계)+ResolvePickaxeClip 공용화 + HumanoidClipDriver.TriggerMine 신설(TriggerTill 패턴 1:1) + ResourceNode 채굴 경로 발화(채굴=Mine/벌목=환호 분기 단일화 — 130차 Cheer 소스계약 유지). 실측: FBX 임포트 성공, 클립 1건("Take 001" 5.07s — 곡갱이/망치 구분 불가, 인덱스 0 채택 — **Play에서 실제 동작 판정 필요**), Till 교체+Mine 배선 DONE.
- **명세 변경(사유 기록)**: 횟불 상한 ≤8→≤9(F1=벽 8+화로 1, F2~5=8 — 면적 ×4 광 밀도 유지)/EntranceTimbering 존재→부재/기본 열림→상시 봉합/던전 팩 벽·바닥 타일 폐기/tierScale 폐기(셀맵 내재화).
- **검증**: 컴파일 error CS 0 + EditMode 전수 **1949/1939/10 — baseline 10건 1:1(AERO·Cooking·H2_Catalog 병행WIP·Instrument×3·InvadedLord×2·RecipeCatalog·ThemeUSS), 신규 실패 0**(계약테스트 재핀: Cave146P1 스폰/Quality 캡51·횟불9·스폰 좌표/Topology 챔버 좌표·존/Dungeon147B0~B2 셀맵 계약/신규 Cave156FallSafety 4건·Player156PickaxeAnim 4건 — 후자는 RunAll 실행 후 GREEN).
- **배치 Play 프로브(Cave156LevelProbe 신설+run_cave156_probe.sh — 146차 골격 승계, 하트비트 정지 시 에디트모드 폴백 경로 내장)**: **DONE errors=0** — F1 셋업 전수(바닥 92×66·흙 머티리얼·벽 51 전부 BoxCollider·캡 51 무콜라이더·횟불 9·가드 부착·블로커 enabled·포탈 D/U)/스폰 착지 dist 0.03/입구 봉합(4.5m 밀기 → maxZ 28.50 차단, minY 1.08 낙하 0)/**가드 복귀 recovered=True(낙하 중 CC 내부 포즈 역동복원 결함 발각 → CC 비활성·재활성 표준 관례 수리)**/F1→F5 포탈 순회 전 층 스폰 착지+F5 하강 포탈 부재 ✓/탑다운 캡처 11장.
- **vision 판독(탑다운 3종)**: F1/F4 = 직각 꺽임만·도면 구획 일치·균일 갈색 흙·얇은 벽 라인·F4 기둥 4×5 그리드·광맥 벽면 밀집·이상 렌더 0. 플레이어뷰 = 프로브 카메라 한계로 어둡게 읽힘(146차 동일 한계) + 흙 텍스처 반복 패턴 가시 — 실게임 카메라 Play 판정 필요.
- **미검증/대기**: ①사용자 Play 판정(5층 순회·흙 바닥·얇은 벽·곡갱이질 클립 실동작·조명 밝기·흙 텍스처 밀도) ②SFX/믹사모 망치질 별도 클립(기록만 — 위조 배선 금지) ③커밋(별도 승인 — 154차+텔레그램 146~155차+본 156차 누적). 푸시 없음.

## 2026-11-05 (154차) 구현+QA 완료 — 대화판넬 하단 더블플립 수리+굉이질 검정 루트 수리(텍스처 단위 혼용)+획득 FX 고도화+음식점 초기 등록가 모달 (미커밋)

- **사용자 지시(영상 "마을 테스트 17.mp4" 209s)**: ①대화판넬 중앙 하단 미고정 ②굉이질 파진 모습 검정 ③획득 이펙트 흰색 단조로움 ④음식점 배치 시 골드 입력. 계획 docs/154_DIALOG_HOLE_FX_RESTAURANT_PRICE_PLAN.md → 확정 답 3건(기본안 등록가 입력/모션만 강화/메시 상승) 후 진행. 텔레그램 연동 보고(26384/26385).
- **진단(프레임+vision+로그+소스 교차)**: ①NPCDialoguePanelUTK y축 이중 반전 확정 — ApplyBottomCenterPlacement가 Screen.height×0.85(bottom-up)를 ApplyAnchorLayout의 투영용 반전에 재통과 → 상단 15% → top 0 클램프(획득/농경문구/NPC 대화 전부 동일 패널). ②굉이질 검정 — 픽셀 계측(오목 (1,1,1) vs 지대 (83,64,39) 무텍스처) + EditMode AO 판독 0.0 + 153차 프로브 무채색 → **루트 = CreateSoilTexture 정규화 단위 혼용(0.42÷107바이트≈0.0039 → 전 픽셀 0 = 흙 텍스처 전체 검정, 153차 커밋분)**. 탄젠트 부재(TBN 퇴화)는 2차 실존 결함. ③획득 FX — Common 회색(0.6) additive+단일 쿼드 구조. ④음식점 — 운영창 가격 입력은 영상상 정상 동작(30→31 편집+커밋 실측), 부재 구간 = 건설 배치 확정 시점.
- **구현(위임 4 + 부모 직접 1.5)**:
  ①P1(위임) — ApplyBottomCenterPlacement 입력 0.85→bottom-up 0.15 + 배치 산식 순수 함수화(ComputeBottomCenterLayout — 스크린 크기 파라미터 추가 스펙 편차 보고) + 계약테스트 재편(DialoguePanel_BottomCenterComputed_154 — 최종 top=루트 71.2% 하단 밴드 검증) + 스테일 핀 2건 갱신(VillageNpcFKeyTests 0.15 핀 — 부모).
  ②P2(위임+부모 수리) — BuildTillHoleMesh에 RecalculateTangents(탄젠트 0→145 실측) + HoleMeshLiftY 0.002 상승(사용자 승인) + 구멍 receiveShadows=false + AO 프리멀티 구멍 텍스처(링별 명암, 캐시 결정론) + **부모 루트 수리: 정규화 ×255 단위 수리(SoilBaseColor.r×255f÷avg.r)** + SoilTexture_AverageMatchesBaseColor_UnitFix_154 핀 + AO 판정 영역 평균화.
  ③P3(위임 2회 타임아웃 → 부모 직접 폴백) — 다층 빛기둥(코어 0.14m/알파×1.6 + 아우터, 반대 위상 바위가) + 회전 링 2겹(반대 방향 70°/s) + 스파클 2색(등급색+백색 코어)+티어 배수 표(SparkleRate/SizeMulTable)+초기 버스트(SparkleBurstCount) + 등장 링 웨이브(0.35s 팝 동기 0.2→0.9m 확산) + GetRingTexture 절차 캐시 + 페이드 등록/정리 계약 유지. 등급색 단일 소비·색 표 무변경(사용자 확정).
  ④P4(위임+부모 마무리) — ConstructionManager.RestaurantPlacementSucceeded 정적 이벤트(음식점 성공 1회 발화, Systems→UI 단방향 잔존 참조 0 실측) + RestaurantInitialPriceModalUTK 신규(UTKWindowBase·기존 톤 재사용·151차 브리지 이식·TryParseInitialPrice 순수 단일소스·Discard 가드) + 테스트 10건. Discard 테스트는 Show()의 DontDestroyOnLoad EditMode 제한 → _isOpen 반사 시뮬레이션으로 수리.
- **검증(독립 QA 체인 — 컴파일+전수+프로브)**: 컴파일 error CS 0(compile.log 12:16/13:0x, Exiting batchmode 1) + EditMode 전수 **1905/1893/12 — baseline 10건 + MeleeClickOwnership 2건(플래키 재발 — 파일 mtime 09-30/10-24 무접촉 실측, 신규 회귀 0)** + 154차 신규 26건 전부 GREEN(1차 런 신규 실패 2건 — AO 단일 픽셀 노이즈 민감/Discard EditMode 제한 — 즉시 수리 후 재런 소멸) + **프로브 판별력 확보: combo1(구멍) 70.1 vs combo3(흙타일) 106.7 — AO 음영 실측 + 갈색 회복(수리 전 15~17 → 70/107)** + tangents=145(HasTangent=True).
- **미검증/대기**: Play(사용자 승인 게이트) — ①대화 패널 3경로 중앙 하단 ②굉이질 오목 갈색+경준선/수분 가시화 실가시 ③획득 FX 움직임(정지컷 한계) ④음식점 배치→초기 등록가 모달→운영창 값 일치→NPC 구매가. SFX_Gather_Soil_000.ogg 투입 대기(전차수 승계). 괭이질 Mixamo 클립 도착 시 Till 1줄 교체(승계). 커밋 승인(128~152 누적+154차). 푸시 없음.
- **교훈**: "텍스처 정규화 단위 혼용(Color float ÷ Color32 byte)" — 텍스처가 검정으로 렌더되면 머티리얼/조명 이전에 **CPU 픽셀 생성 산식의 단위**를 의심할 것(153차 "프로브 한계" 귀속은 오판정이었음 — EditMode 픽셀 판독 테스트가 근원을 직접 지목).


   138|
## 2026-11-04 (153차) 구현+QA 완료 — 농경 고품질+획득 VFX (미컴백)

- **사용자 지시**: 농경시 땅 파기 품질·채집 애니 고품질 + 획득 연출 VFX(양쪽 빛기둥 등) — 계획만 요청 → docs/153_FARMING_QUALITY_ACQ_VFX_PLAN.md 기안 → 확정 답변 4건(굉이질 애니 있으면 사용/없으면 추가예정·빛기둥 등급별 강약·SFX 신규 확보·환호 반짝 포함) 후 "진행".
- **사전 실측(부모)**: FarmPlot 시각=단색 Cube(Rim4+Pit1), 채널 4종 애니·OnImpact 미구독, 작물=Sphere, AcquisitionCinema 이펙트 전무, 굉이질 전용 클립 부재, HerbType 5색 GLB(Resources/Models/UserProvided/herb_*.glb) 존재, Bloom 부재(additive 구현 계약).
- **구현(위임 A/B 병렬 → C 순차 — 3회 전부 착지)**:
  ①A(P1+P3, 600s 타임아웃 — 착지 완전) **FarmPlotVisuals.cs 신설**(절차 텍스처 캐시 결정론/9×9 격자 오목 메시 — 정점 내림+AO 내벽/수분 밝기 보간 ComputeSoilBrightness/바람 스웨이)+FarmPlot 시각부 위임(상태머신 무변경)+**작물 GLB 승격**(HerbType→herb_*.glb 매핑 yellow→herb_gold/silver→herb_blue, 실패 시 Sphere 폴백)+FarmPlotVisualsTests 12건.
  ②B(P4, 383s) **AcquisitionCinema +354줄** — 좌우 빛기둥(URP Particles/Unlit additive, **등급별 강약** 길이 0.8+0.2t/알파 0.25+0.1t — RarityShaftLength/RarityShaftAlpha public 결정론)+헤일로+스파클(Play 소유권 명시)+아이템 _EMISSION 이징(등급색 — Common도 약하게)+등장 팝(easeOutBack 0.35s)+P4b 환호 반짝(파괴 소유권 EndPresentationInternal 명시)+_fxRenderers 페이드 계약. AcquisitionCinemaTests +5(기존 42건 1:1).
  ③C(P2, 279s) HumanoidClipDriver **TriggerTill**(파라미터 캐시 가드 — 병사 컨트롤러 무해)/**ReplayAction**(Play state,0,0 — 채널 리듬 유지용)+FarmPlot 4종 채널 발화(굉이질/정리=Till, 파종=Gather, 물주기=WaterPour 피드백만)+OnImpact 구독(HerbPickup 선례 1:1 — FarmPlot 소유 채널 전용, 병사 경로 무변경)+HerbPickup 수확 리듬 재발화+GatherFeedbackKind.Soil(ClipPrefix SFX_Gather_Soil/VfxPrefabName SoilBurst)+**Farm153EditorTools.cs**(프리펩 SoilBurst/WaterDroplets 절차 생성+Player_AC Till 상태 멱등 추가 — playOnAwake=false·Play 소유권 짝 계약)+테스트 신규 6건/갱신.
- **부모 소형 수리 1건**: Farm153EditorTools L82 CS0104(Object 모호성) → UnityEngine.Object.DestroyImmediate 명시(QA 1차 발각 — 26차 선례 준용).
- **계약테스트**: FarmPlotVisualsTests 12/12·Farm153ChannelFeedbackTests 6/6·AcquisitionCinemaTests 42/42·GatherFeedbackContractTests 7/7 GREEN.
- **검증(독립 QA 2차 PASS + 부모 재판독)**: 컴파일 error CS 0(qa_153_compile.log, Exiting batchmode 1) + 에디터 도구 RunAll exit 0(프리펩 SoilBurst/WaterDroplets 생성 실측, Player_AC Till 파라미터/State/전이 3건 확인) + 전수 EditMode **1876/1866/10 — 신규 실패 0, 실패 10건=기존 baseline 동일(Melee 플래키 자연 해소)**(qa_153_editmode-results.xml).
- **미검증**: Play 영상(프레임+vision) — 별도 승인 게이트. SFX_Gather_Soil 오디오 에셋은 사용자 확보 예정(도착 전 조용한 스킵 계약 무해). 신규 Mixamo 괭이질 클립 도착 시 Till 상태 클립 교체만(1줄 스왑).

## 2026-11-03 (152차) 구현+QA 완료 — 건설창 훈련소 카드 추가+실내 배치 판정 수리 (미컴밋)

- **사용자 지시**: ①침대 세이브 구현 여부 ②실내 훈련장 배치 가능 여부 ③건설창 훈련장 UI 추가(동일 스타일)·실제 건설 가능 ④계획 먼저. 계획 docs/152_BED_SAVE_TRAININGHALL_CONSTRUCTION_PLAN.md. "진행" 승인.
- **사전 실측(부모)**: ①침대 세이브 **기존 구현 확인** — Bed(E키)→SleepUI "💾 여기서 세이브"→스폰핀+SaveManager.AutoSave(slot_{n}.json) — 신규 작업 불요 ②실내 배치는 규약상 허용(L234 주석)이나 판정 체인 미실측 ③건설창 누락 원인 확정 — ConstructionWindowUTK.FacilityIds에 bp_traininghall만 빠짐(시설 5종).
- **구현(위임 3회 + 부모 수리 1건)**:
  ①P0(위임 247s) — FacilityIds 5→6종+RoleLabel("훈련 · 병사 훈련과 전투 훈련")+UnlockText 추가 — BuildFacilityItem 재사용이라 스타일/그리드/폰트 자동 동일. 카운트 계약 갱신(ConstructionManagementFigmaTests Five→Six 메서드명 포함, 기타 8종 불변 확인). ②P1(위임 402s) — 실측: 호출부(ConstructionHologram)가 ResolveTerritoryAt(pos)로 territoryId 공급 → 실내 원점 좌표가 항상 Empire_1 오귀속(Empire_1 훈련장 보유 시 실내 배치 전면 거부 결함) → TryPlace 실내 분기(보존 키 재해석+영지 경계 우회+겹침 전용 검증)+IndoorSceneTransition 보존 키 접근자+ConstructionTests 신규 7건. ③**P1-수리(부모 직접)** — P1이 Systems→UI 직접 참조(ProjectName.UI.IndoorSceneTransition)를 넣어 CS0246 예정(asmdef 실측) → Core 공용 UITransitionState에 IndoorCastleTerritoryKey 신설+UI 3점 동기(성 진입 세팅/정리·월드 복귀 클리어)+ConstructionManager Core 경유 전환+테스트 갱신. 잔존 ProjectName.UI 참조 0 실측. ④P2 소스계약은 P1 신규 7건이 체인 핀 겸임(홀로그램→TryPlace→실물→훈련UI).
- **계약테스트 +8**: ConstructionTests _152 7건(실내 재해석 순수함수/게이트 순서/겹침 경계/소스계약) + ConstructionWindowTests TrainingHall_Listed_152. 구 FiveFacilities/FiveItems 2건은 Six로 정상 교체(REMOVED 2).
- **검증(독립 QA + 부모 재판독)**: 컴파일 error CS 0(qa_152_compile.log, Exiting batchmode 1) + 전수 EditMode **1842/1830/12 — 신규 실패는 MeleeClickOwnership 2건뿐**(qa_152_editmode-results.xml) + 집중 전부 GREEN(Construction 38/38·ConstructionWindow 13/13·Figma 12/12·TrainingHall 30/30·43/43·BlueprintMaterial 17/17).
- **Melee 2건 귀속 판별**: MeleeClickOwnershipTests.cs(09-30)/PlayerCombat.cs(10-24) mtime = 152차 무접촉 → 146차 치유→150차 재발→151차 해소를 반복한 **기존 플래키 재발**(쿨다운 타이밍 민감) — 152차 변경 무관.
- **대기**: ①Play(사용자): 건설창 좌 목록 훈련소 카드(동일 스타일)·재료/골드 소비 후 시공·실내 씬에서 배치 성립(보존 키 귀속)·완성 후 E키 훈련 UI. 참고: 실내 홀로그램 지면 레이캐스트가 "Floor" 명명 바닥에 안 잡힐 수 있음(서브 보고 — 커서 추적 UX 별도 관찰) ②Melee 플래키 관찰 지속 ③커밋 승인(128~152 누적).


## 2026-11-03 (151차) 구현+QA 완료 — 등록가 키보드 차단 수리+Test_15 즉시 구매+대화판넬 중앙하단 고정+농경 문구 패널화+호수 복귀 (미커밋)

- **사용자 지시(Telegram 5건)**: ①음식점 금액 직접 입력 불가 ②NPC가 바로 음식 사러오게(15씬만) ③대화판넬 항상 중앙 하단 ④농경 상호작용 문구 대화판넬 표출 ⑤호수 근처 1개. 계획 docs/151_PRICE_INPUT_INSTANT_CUSTOMER_DIALOG_BOTTOM_FARM_MSG_LAKE_PLAN.md. "진행" 승인.
- **사전 실측(부모)**: Editor.log `[150차][P4] 포커스 획득 2회/키다운 0회` → **키보드 전달 차단 확정**(포커스 미획득 기각). `[150차 P2] 투영 무효 cam=(0,0,0)` 반복. 148차에 호수가 Test_20으로 분리돼 Test_15 무호수 확인.
- **구현(위임 4회 — 3병렬 Wave1 + P3 후속)**:
  ①P0(위임 562s 완료) — 근원=씬 EventSystem이 StandaloneInputModule+IMGUI 큐 선소비로 UITK 키다운 무전달(트리클/루트 소비자 전수 무죄 판정 후). RestaurantWindowUTK에 스코프드 onTextInput 브리지(FocusIn 부착/FocusOut 해제/KeyDown 도달 시 자가 은퇴 프로브 — InputSystemUIInputModule 전환 시 자동 무효화)+Esc 분기 TextField 가드+[150차 P4] 진단 은퇴. ②P1(위임 203s) — RestockFromPlayer 성공 경로 Livelihood 즉시 ScanAndAssign 1회+티커 Livelihood 스캔 1s(프로덕션 2.5s 무영향). ③P2(위임 타임아웃이나 **전량 착지** — 마커 grep+ReacquirePlacement 0 확인) — PlaceAtAnchor 상시 중앙 하단(50%,85%) 일원화, 재배치 루프/투영 경로 폐기, 계약테스트 재편. ④P3(위임 600s 0산출 → **부모 직접 폴백**, 64차 선승인) — FarmPlot.FarmStatusBroadcast 이벤트(Systems→UI 단방향)→NPCDialoguePanelUTK.ShowTransientMessage(3.5s 자동 닫힘, NPC 대화 보호 스킵, 동일 메시지 타이머 연장), 정상 개설 경로 6곳 트랜지언트 해제, 농경 문구 15건 전수 라우팅(문구 무변경). ⑤P4(위임) — 142차 호수 블록을 CompactCluster에 복원(anchor+dOut×16, r12, tag Water — 밭 좌14m와 중심거리 21.26m 무충돌 실측, FishingSystem.IsNearWater 리필 인식 확인).
- **계약테스트 +8**: PriceFieldKeyboardBridge_151·ShortcutTextFieldGuards_151·Restock_Triggers_Immediate_151·Test15_Livelihood_Pond_Restored_151·FarmStatus_PanelRouting_151·DialoguePanel_AlwaysBottomCenter_151 외 2건(구 앵커 계약 2건 재편 소멸).
- **검증(독립 QA PASS + 부모 재판독)**: 컴파일 error CS 0(qa_151_compile.log, Exiting batchmode 1) + 전수 EditMode **1834/1824/10 — baseline(150차 12건) 대비 신규 실패 0, Melee 플래키 2건 자연 해소, 이름 1:1 부분집합 확인(qa_151_editmode-results.xml)** + 집중 전부 GREEN(RestaurantWindow 9/9·Figma 7/7·Visitor 8/8·System 13/13·Test15 10/10·DialogueFeedback 30/30·NPCPanelFigma 6/6·TargetProjection 8/8·FKey 16/16).
- **대기**: ①Play(사용자): 등록가 숫자 입력→커밋 / 입고 직후 고객 즉시 방문+코인 연출 / 대화판넬 항상 중앙 하단 / 농경 문구 패널 표출 / 호수에서 물뿌리개 리필 ②근본 해제 후보(별도 과제): EventSystem에 InputSystemUIInputModule 적용 시 브리지 자가 은퇴 — 동일 차단 추정 필드(TrainingHall 검색/Crafting 프리셋명/PlayerFlag 이름) ③커밋 승인(128~151 누적).


## 2026-11-03 (150차) 구현+QA 완료 — 부유GLB 프리팹 에셋 근본 수리+획득 패널 폴백 중앙하단+게이지 가시성 보정+판매가 입력 계측 (미컴밋)

- **사용자 지시(영상 "농경 테스트.mp4" 35.6s+로그)**: ①채집 부유 GLB 미표출 ②건설 게이지 미표출 ③판매가 숫자 입력 불가 ④획득 패널이 좌상단에 뜸(중앙 하단 기대). 계획 docs/150_ACQ_GLB_GAUGE_INPUT_PANEL_PLAN.md. "진행" 승인.
- **진단(영상 프레임 3장+Editor.log)**: ①**근본 확정** — "Transform resides in a Prefab asset" 4회=AttachFloatItem SetParent 차단. LoadVisual이 AssetDatabase 에셋을 Instantiate 없이 반환(구 손끝 부착 130~144차도 동일 결함 — "GLB 전혀 안 보임"의 오래된 뿌리). resolver 자체는 정상(herb 3종 경로 해석). ②**로그 직접 포착** — `[TargetProject] 투영 무효 — target=NPCDialogueAnchorProxy (전 카메라 뒤 z≤0)` → 패널 무배치 방치=초기 좌상단(프레임 6 실측 일치). ③게이지 — 부착 정상(barY=4.6, matNull=False) → 크기(0.9m×0.11m 원거리 픽셀 미달)/가림 가설. ④입력 — 등록가 TextField+포커스 가드 존재, 원인 미확정.
- **구현(2팀 병렬 위임 — 1팀 완료, 2팀 타임아웃 → P2/P4 부모 직접 완료)**:
  ①P1 AcquisitionItemVisualResolver.LoadVisual — 전 경로 반환을 InstantiateSceneInstance(Object.Instantiate)로 일원화(단일 소비자 AttachFloatItem — 호출부 조사 완료). ②P3 WorkProgressBar ManualBarScale=2.6(ShowManual 전용 — 채널링 무영향)+ConstructionManager SiteGaugeBarHeightM=5.5+1초 스로틀 가시성 프로브 로그. ③P2 NPCDialoguePanelUTK — 투영 무효 시 무배치 폐기 → ApplyAcquisitionFallback(화면 중앙 하단 50%,85%)+500ms×12 재배치 루프(카메라 정상화 시 복귀)+투영 실패 진단 로그(일반 NPC 대화 경로도 일관 폴백). ④P4 RestaurantWindowUTK 등록가 필드 FocusIn/KeyDown 계측 로그(판별: FocusIn 없음=포커스 미획득, 있음+키다운 없음=키보드 전달 문제).
- **계약테스트 +2**: Resolver_LoadVisualInstantiatesSceneInstance_SourceContract_150·ConstructionGauge_ManualScale_SourceContract_150.
- **검증(독립 QA PASS)**: 컴파일 error CS 0(수리 1건 — ReacquirePlacement Component→transform CS1503 즉시 수리) + 전수 EditMode **1828/1816/12 = baseline 10건 + MeleeClickOwnership 2건(146차 치유분 재발 — 150차 변경 무관 영역, 기존 12건 부분집합 복귀, 신규 회귀 0)** + 신규 2건 Passed + 집중 AcquisitionCinema 27/27·ConstructionTests 31/31·Test15VillageDrug 9/9.
- **대기**: ①Play(사용자): 채집 시 얼굴 위 부유 GLB(근본 수리 — 즉시 효과 기대), 획득 패널 중앙 하단(폴백+재배치), U키 건설 게이지(크기 2.6배+높이 5.5m — 프로브 로그로 미세조정), 음식점 등록가 입력 시 [150차][P4] 로그 확인(포커스/키다운 유무로 원인 확정→다음 라운드 수리) ②Melee 플래키 재발 관찰 ③커밋 승인(128~150 누적).


## 2026-11-03 (149차) 구현+QA 완료 — 농경 영지 시딩+획득 GLB 얼굴 위 부유+인벤 창 배치+건설게이지 계측+음식점 재고·고객 NPC (미커밋)

- **사용자 지시(5건)**: ①농경=아군 영지 전용 → Test_15에서만 판정 통과 ②획득 GLB=전 종류 얼굴 위 부유(현재 전혀 안 보임) ③인벤 미정합=**창의 화면 배치 문제**(답변 확정) ④건설 게이지 미표출 ⑤음식점 재고 0종("판매할 수 있는 음식이 없다")+고객 NPC 실구매 검증. 계획 docs/149_TEST15_FARM_ACQ_BUILD_RESTAURANT_PLAN.md. "진행" 승인.
- **구현(위임 A타임아웃 600s → 부모 직접 완료, B완료)**:
  ①**P1** Test_15 마을 선택 직후 TerritoryDatabase.SetOwnership(East,1,PlayerOwned) — Test_10/14 선례 동일 프로덕션 API(FarmPlot ValidateOwnership 통과).
  ②**P2(B)** AcquisitionCinema AttachHandItem→AttachFloatItem 전면 대체(전 종류) — FloatAnchorUpM=1.85(클로즈업 프레임 산식 0.78~2.02m 내), 상승+사인 진동+0.8s 페이드+카메라 빌보드+바운드 중앙정렬, resolver 단일소스 재사용, resolver 진단 로그 신설. 폴백(아이콘+아이템명) 승계.
  ③**P3(B진단+부모수리)** 인벤 창박스는 Figma와 0차이(144,84,504,912 — 런타임 로그 ok 실증). **실제 결함=ApplyInventoryBodyLayout contentOrigin 단위 혼합**(_content.layout=k 배수 px를 raw Figma px에서 무차감 → 내부 +12px 하단 시프트·푸터 여백 21.75→9.5px 눌림) → DesignScale(rootSize)로 나눠 단위 정합 + 구 3분할 배치(UTKThreeColumnLayout.Place ×2) 제거(Figma 단일 배치 소스).
  ④**P4** 건설 게이지 미표출 — 구현은 142차 F 존재, Play 실측 부재 → AttachSiteGauge에 렌더 상태 덤프 로그 추가(enabled/matNull/texNull/scale — 원인 확정용, 표시 로직 무변경).
  ⑤**P5** 재고 0종 원인 확정: 우측 판매대(VillageDrugState.GetStock)는 시딩 존재·키 일치 — **좌측 "판매 가능한 음식" 그리드 소스=PlayerInventory Food/Drug인데 Test_15 시딩에 음식 0종** → 일반 요리(cook_01×10)+마약 요리(drugged_cook_01×10, 설명에 [마약: stage 3] 마커 — ParseDrugStage 병합 정합) 인벤 시딩. 고객 NPC: VillagerNpcSpawner.SpawnSingleVillager 신설(SpawnNpc 전체 파이프라인 재사용 — 신규 생성 로직 0) + 스캔 계약명 루트(Village_{nation}_{index:D2}) 생성 + RestaurantVisitorSystem Livelihood 씬 1명 하한 보장(수요 비율 반올림 0 방지 — 선발 이후 경로/정산 계약 무변경). 영업 시간=게임 시작 8시([134차 B-2])로 충족 — 시각 시딩 불필요.
- **계약테스트 +3**: Cinema_FloatAboveFace_SourceContract_149(순서 계약 포함)·Inventory_WindowPlacement_FigmaSingleSource_149·Test15_TestScene_Seeding_Contract_149.
- **사고 2건(§9)**: ①A서브 600s 타임아웃 — P1 부분만 남기고 중단(식재료 시딩 시도 — ItemCategory.Vegetable 부재로 CS0117 유예 코드, 부모가 요리 시딩으로 교체) + P5/P4 미착수 → 부모 직접 완료(144차 선례). ②신규 계약테스트가 스스로의 설명 주석 문자열을 Does.Not.Contain으로 잡아 1차 FAIL → 주석 문구 교정 후 2차 PASS(문자열 계약 테스트 작성 시 자기 주석 오염 주의 — 함정 스킬 기록 대상).
- **검증(독립 QA 2차 PASS)**: 컴파일 error CS 0(qa_149_compile.log) + 전수 EditMode **1826/1816/10 = baseline(qa_147b 10건) 1:1, 신규 실패 0**(qa_149b_editmode-results.xml) + 집중 Test15VillageDrug 9/9·AcquisitionCinema 26/26·InventoryFigmaTabsFooter 6/6 GREEN + 범위 감사(당일 .cs = 화이트리스트 9파일).
- **대기**: ①Play(사용자): 농경 경작/성장, 획득 시 얼굴 위 부유 GLB(약초=Test_15/나무·돌·낚시=Test_20), 인벤 창 위치, U키 건설 시 게이지 표출+P4 렌더 로그, 음식점 창(좌측 음식/우측 재고)→고객 NPC 방문·코인 팝업→Y키 정산→재고 차감 ②P4 원인 확정 후 게이지 수리 ③커밋 승인(128~149 누적).


## 2026-11-03 (148차) 구현+QA 완료 — Test_15 경량화(주민 제거·농경만)+자원/낚시 Test_20 이관 (미커밋)

- **사용자 지시**: "최대한 가볍게 테스트씬을 나누자 — NPC=상점NPC만, 설계 마을 모습 유지, 나무/호수/돌은 다른 씬, 농경시스템만 배치" + 확인 답변(음식점/마약경영·약초=유지) → "진행" 승인. 계획 docs/148_TEST15_LIGHT_SPLIT_PLAN.md.
- **근거(1차 Play 실측)**: [147차][P0] Awake 23.3s = 주민스폰 15,545ms(66%)/상점NPC 4,903/UTK 1,387/마을플롯 388/기타 121. Play 진입 고정비 14.0s(강제 동기 리컴파일 — 스크립트 변경 후 1회 한정).
- **구현(위임 1회)**: ①TestTerritoryCombatSetup — 주민스폰 P0 라인 삭제(VillageNpcSpawner 클래스 무수정 — 메인씬 경로 유지), SetupLivelihoodCompactCluster=농경 전용(밭 2x2+약초 3), SetupLivelihoodNatureCluster 신설(호수/나무3/돌3 — 142차 C2 오프셋 수식 그대로 이식), _isLivelihoodResourcesScene 게이트 신설(SetupTest15VillageDrugEconomy는 Resources 제외, 생활씬 stage=Resources 전용: SetupFishingArea+NatureCluster — 낚시 인프라는 물 의존으로 호수와 동반 이관) ②계약테스트 +2: Test15_VillagerSpawn_Removed_ShopOnly_148·Test20_NatureCluster_SourceContract_148.
- **귀결**: 물뿌리개 Refill=호수 물담기 의존이나 세션 시작 20/20 → 농경 3일 성장(물 ~3회) 검증 충분 — 리필 검증은 Test_20 호수. Test_20은 기존 Test_15와 동일 전체 경로였음 → 자원 전용 경량 씬으로 재정의(사용자 "씬을 나누자" 직접 귀결).
- **예상 로딩**: Test_15 Awake ~7.5s(상점NPC 4.9+UTK 1.4+마을플롯 0.4+farm 클러스터+기타), Test_20 ~2s — Play 미실측(승인 게이트).
- **검증(독립 QA PASS)**: 컴파일 error CS 0(qa_148_compile.log) + 전수 EditMode **1823/1813/10 = baseline(qa_147b 10건) 1:1, 신규 실패 0**(qa_148_editmode-results.xml) + 집중 Test15VillageDrug 8/8·Test20ResourceScene 2/2·Village5DesignCatalog 34/34 GREEN + 범위 감사(당일 .cs 수정 = 화이트리스트 3파일 mtime 실측).
- **비고**: UnityLockfile 비정상 종료 잔재(tasklist로 Unity.exe 부재 확인 후 삭제) — batchmode QA는 에디터 종료 후에만 가능(락 충돌). 구현 서브가 메서드 요약 doc 주석의 SpawnForVillage 문자열도 제거(신설 계약 Does.Not.Contain 통과 필요 — 계획서 외 숨은 변경점, 선제 발견 보고).
- **대기**: ①Play(사용자): Test_15 경량 체감(주민 0·상점NPC·농경 밭/약초·음식점 U키 건설)+[148차] P0 로그(주민스폰 스테이지 소멸)+Test_20 자원 씬(호수/나무/돌/낚시) ②상점NPC 4.9s = 잔여 최대 항목 → L1 스테이징 후보(본 라운드 착수 금지 — 사용자 지시 유지) ③커밋 승인(128~148 누적).


## 2026-11-03 (147차) 계획 수립 — Test_15 평지+설계 마을(신규 GLB)+로딩 원인 재분석 (승인 대기)

- 사용자 지시: Test_15=기본 평지, 마을=설계 마을 중 1종+신규 GLB, 로딩 원인 재분석+계획. 계획 docs/147_TEST15_FLAT_GROUND_V5_VILLAGE_LOAD_PLAN.md.
- 원인 재분석(Editor.log 47,734라인 실측 — [146차] 기본 경로 동작 확인 후 "여전히 느림"): **A. Play 진입 고정비 ~10.5s**(강제 동기 리컴파일+Asset Pipeline Refresh 9.369s — 스크립트 변경 후 첫 Play, 씬 내용 무관) / B. 에디터 오픈 임포트(Tundra 5.9s+domain 9.4s) / C. Awake 자산(마을 건물 6+**울타리 270 인스턴스**+주민 10 리깅FBX·4096² BC7+GLB 16키+UTK 8창) / D. **WorldMapWindowUTK.RefreshVisual NRE 1,692회 스팸**(별개 결함 — 로그 4.7MB). 배제: 셰이더 버스트·텍스처 재임포트·TerrainTexture/DecoPlacer(Test_15 미관여).
- 계획: P0 단계별 Stopwatch 계측+첫/2회차 Play 분리 → 평지 전환(GetHeightAt 평면 분기·계약 유지) → **BuildPlotFlat**(표준 설계 1종·건물 GLB만 — 지면/산포 스킵, 울타리 270 자동 소멸) → L1 주민 스폰 스테이징/L3 UTK 경량화/L4 WorldMap 수리(측정 근거 시). 텍스처 고품질 계약 유지(다운스케일 금지).
- 결정 대기: ①기본 설계 id(S1?) ②흙길 유지/생략 ③주민 수 유지/축소 ④WorldMap 수리 동시 여부 ⑤Enter Play Mode 옵션.

## 2026-11-03 (147차) 구현+QA 완료 — Test_15 평지+설계 마을 V1(BuildPlotFlat·신규 GLB)+로딩 P0 계측+WorldMap NRE 수리 (미커밋)

- **사용자 지시**: 평지+설계 마을 중 1종+신규 마을 GLB+로딩 원인 재분석(계획 §0: Play 진입 고정비 ~10.5s/Awake 자산/WorldMap NRE 1,692회). "진행" 승인 → 위임 구현(2회 — 1차 max_iterations 부분완료→2차 완료) + 독립 QA 2회(1차 FAIL→T3 수리→2차 **PASS**). 계획 docs/147_TEST15_FLAT_GROUND_V5_VILLAGE_LOAD_PLAN.md.
- **구현**: ①TerrainGenerator.GetHeightAt — Test_15(Livelihood) **평지 게이트**(Test15FlatHeight const·씬 캐시 판정·표면 y=1 단일소스) ②Village5DesignRenderer.**BuildPlotFlat** 신규(평지 상수 샘플러+PlaceEntries GLB 전수+흙길 유지, 지면메시/자연·잔디 산포/텍스처패치 스킵, 고산 설계 거부 가드) ③Test_15: BuildSingleVillage(구형 hut+**울타리 270 소멸**)→**BuildPlotFlat("V1" 강변 자급자족)** 교체 — 주민10/상점NPC/음식점/마약경영 유지, BuildVillageGroundPatch는 **유지 판정**(SetupGround ±60m 커버·마을 ~454m 이격) ④**P0 계측**: P0Time/P0LogSummary — Awake 단계별 ms 1줄 로그(Play 전용 — 실측은 다음 Play 세션) ⑤WorldMapWindowUTK `_labelChip` null 가드(원인=생성자 미대입 확정 — 1회 경고 후 스킵, 동작 무변경 — 세션 NRE 1,692회 소음 차단).
- **검증(독립 QA 2차 PASS)**: 컴파일 error CS 0(qa_147b_compile.log) + 전수 EditMode **1821/1811/10 = baseline(qa_146 10건) 1:1, 신규 실패 0**(qa_147b_editmode-results.xml) + 집중 Village5DesignCatalogTests 34/34·Test15VillageDrugTests 7/7·ArenaStagingTerrainTests 7/7 GREEN.
- **사고+복구(§9)**: 수리 서브 write 절단(ArenaStagingTerrainTests 80행 — 부분읽기+전면재작성 패턴) → state.db 페이로드 앵커 조립 복구(124행·균형 0) + 부모 직접 컴파일(144차 선례 — 보고 기록). 스킬에 예방 규칙 기록.
- **계약 승계**: WorldMapSceneWiringTests(P0Time("UTK") 래핑 반영 — Passed), Village5DesignCatalogTests: Test15_BasicTerrain_Contract_147 갱신+BuildPlotFlat API 테스트 신설(+1 → 34), Test15VillageDrugTests 지면패치 계약 갱신.
- **대기**: ①Play(사용자): **1차/2차 Play 구분 필수**(첫 Play는 리컴파일 ~10.5s 고정비 — 2회차 기준 체감)+[147차][P0] ms 로그 확인+평지/V1 설계 마을 GLB/주민/음식점 ②P0 실측 후 L1(주민 스테이징)/L3(UTK 경량화) 판정 ③Enter Play Mode 옵션(에디터 설정 — 사용자 선택 시 첫 Play 고정비 대폭 절감) ④커밋 승인(128~147 누적).

## 2026-11-03 (146차) 구현+QA 완료 — Test_15 기본 지형 복귀(Idyllic 플롯/쇼케이스 10마을 빌드 제거 — 로딩 경량화) (미커밋)

- **사용자 지시**: Test_15 = 기본 지형, 메인씬 = Idyllic 유지. 원인 확정(읽기 감사): [145차 P2b] H1 플롯(BuildPlot "_Game") + [145차 P3-TOUR] 쇼케이스 10마을 BuildAll — Idyllic GLB 46종+자연/잔디 산포가 Test_15 Awake에서 전개 → 로딩 과다. Village5DesignRenderer 호출점 전수 = Test_15 유일 → 메인씬 0영향. 계획 docs/146_TEST15_BASIC_TERRAIN_PLAN.md.
- **구현(위임)**: ①TestTerritoryCombatSetup.SetupTest15VillageDrugEconomy — H1 분기 블록(구 L156~202) 전체 제거 + [146차] 마커. 기본 경로 복귀: BuildVillageGroundPatch(best.center,140) → [86차] 가장자리 이동 → [142차 C2] 컴팩트 클러스터(플레이어 착지지) → BuildSingleVillage+주민+상점 NPC → 음식점/재고/티크. ②Village5DesignCatalogTests: Test15_HighlandBranch_Contract_145P2b → **Test15_BasicTerrain_Contract_146** 재작성(메서드 1:1 — 기본 지형 계약 + BuildPlot("H1"/BuildAll(showcaseOrigin 소멸 NOT-Contain). Village5DesignRenderer/Catalog은 보존(미수정 — 향후 쇼케이스 재활용).
- **검증(독립 QA — PASS 4/4)**: 컴파일 error CS 0(TestOutput/qa_146_compile.log) + 전수 EditMode **1820/1810/10 = 신규 실패 0, 실패집합 = 기존 12건의 부분집합**(qa_146_editmode.log·editmode-results.xml) + 집중 Village5DesignCatalogTests 33/33·Test15VillageDrugTests 7/7 GREEN + 범위 준수(당일 Assets 수정 = 화이트리스트 2파일만 — mtime 실측). 통합 리포트 TestOutput/qa_146_qa_report.md.
- **편차(양성)**: MeleeClickOwnershipTests (2,2)/(3,3) 2건 baseline 실패가 본 런 Passed 치유 — 본 변경 무관 영역, 재현성 관찰 필요(신규 회귀 0).
- **대기**: ①Play(사용자): Test_15 로딩 체감 + 기본 지형/마을 건물+주민/음식점/생활기술 클러스터 확인 ②커밋 승인(128~146 누적).

## 2026-11-01 (145차) 백필 — 유니티 피그마 정합 Phase 4~9 + 블렌더 프리비즈 (QAPROGRESS 미기록분 — 세션DB/qa_145a~g 근거 요약)

- 텔레그램 세션(189msg): 감옥 프리비즈 검증 + **피그마 정합 Phase 4~9 진행**(qa_145a~g — 상태/포션/대화/상호작용·음식점/수면/제작대/건설 등 Figma 계약 테스트 다수 신설). 최종 전수 EditMode 11-01 17:30 = **1820/1808/12(baseline 11+병행WIP 1)**. Village5DesignCatalog/Renderer(마을 설계 10종 카탈로그) 신설 — Test_15 H1 플롯+쇼케이스 빌드 포함(→146차에서 제거).
- 블렌더 프리비즈(CLI 680msg): escape/jail/king/commander/prince/lord 다수 컷 제작·검증. **영주 프리비즈 v2(11-01 21:47) 완성** — `box()` 치수 미적용(전 요소 2m 큐브)·도/라디안 혼용 2대 근본 버그 수리, 4컷 PASS, previs_lord_final_v2.mp4(10.0초/720p/240프레임). 하네스 스킬에 교훈 기록됨.

## 2026-10-31 (144차) Phase 3 전리품/결과창/낚시 구현·QA 완료 — 낚시 진입 UI 163:4 전환(D2) 포함 (미커밋)

- **진행**: 사용자 "진행" → Phase 3 실행. 위임 구현 + 폴백 수리 병용(타임아웃 4회 — 결과창 2회/③-b 2회는 부모 직접 수리·구현).
- **① LootWindowUTK[144차 P3-LOOT]**: 빈셀 square-dashed 38.4+TierStrip(P0 — UTKSlot V3 어휘)·Count "x"접두사 제거·Count 앵커(right 7.6/bottom 4.6)·헤더 pb9.6·Subtitle y15.2(재실측 정정)·닫기 글리프색 #8B949E·푸터/헤더 스트로크 4면(BorderChrome)·데칼색 #58A6FF w1.8. 신규 계약 1건.
- **② HarvestResultUTK[144차 P3-RESULT]**: SystemTip info 아이콘 16.8(P0 — 텍스트 x26.1 괴리 원인)·패널 α0.878→0.702(D1)/스트로크 BorderChrome(2f는 33차 계약 유지)/r14.4·gap 20→24 전면(이중마진 40 해소)·PanelHeader h39·Subtitle 판본별 14.4/15.6·Badge 프레임 반전 교정(bg@0.082+stroke@0.2+r4.8+pad·텍스트 #58A6FF)·verb/아이템명 CENTER·골드 #E3B341→#D4AF37(아이템명/TierClass op0.8/이미지섹션 스트로크)·이미지/설명섹션 #1C2128@0.502+r9.6+스트로크·SpecBox(bg@0.6+스트로크+r7.2+pad14.4+gap4.8)·설명제목 판본별 fs·버튼 r7.2·Keep 라벨 D7 "인벤토리에 놓기". 스펙 구성(길이/무게/약성/순도)은 실데이터 부재 — 기존 실데이터(희귀도/판매가/수량) 유지+역반영 후보 기록(위조 금지).
- **③ FishingUTK[144차 P3-FISH/FISH2]**: 패널 본체 38개소(데칼 P0·글래스 크롬·패딩 24 복원·간격 24px 체계 앵커 검산·폰트/색·바 스타일 #161B22 h9.6 r4.8·위험배지 Tension행 전환) + 신규 섹션 P0 3건 — **game-header-bar**(1920×76.8 #0D1117@0.502+앵커/퀘스트 타이틀/지역 표기 — Figma 문구 정적 표기+동적 연결 후보 기록), **HUD-tip-bar**(1920×36 중앙 컨테이너 372.8×36 #161B22@0.702+info 아이콘+장력 경고 문구), **액션 카드 2세트**(릴 감기+SPACE 키캡/낚시 취소+Q — 구 텍스트 힌트 대체, 대기 상태 힌트 유지). 바 2종은 FigmaCanvasLayout.Place(UIRoot 직속·디자인공간 좌표)+스케일 래퍼+GeometryChanged 재적용, Show/Hide 동기화.
- **D2-FISH 163:4 컴팩트 패널 구현+배선**: InteractionPanelUTK TargetKind.Fishing 분기(288×60·fill #161B22@0.7·스트로크·r9.6·데칼 4각·ActionFishing 카드 266.4×38.4 — ApplyFishingChrome 적용/타종류 복원+CloseIfKind 신설) + FishingSystem 배선 — **E 빌보드 → 상호작용 패널 전환**(Systems→UI 역참조 금지 asmdef 단방향 → UI 측 250ms 폴링 PollFishingPanel·ShouldShowFishingPanel/PanelAnchor 상태 노출·RequestStartFromPanel 근접 재검증). E키 단축 경로 유지. R키 물담기 무변경.
- **검증(마감)**: 컴파일 error CS 0 + 전수 EditMode **1777/1765/12 = baseline 11 + 병행 WIP 1 = 신규 실패 0**(낚시 배선 전환 후 기존 계약 무손상 — 실패집합 1:1).
- **산출**: TestOutput/figma_audit_144/{phase3_loot_impl,phase3_result_impl,phase3_fish_impl,phase3_fish2_impl(보고서 미작성 — 본 기록으로 대체),qa_144p3_full.log}.md. 신규 계약: LootWindow_144Phase3·FishingUTK_144Phase3 계약.
- **다음**: Phase 4 상태/포션/대화/상호작용(CSP P0 9/Potion P0 3/대화 P1 10/상호작용 81:51 물약·음식 P0) + 컴팩트 패널 잔여 3종(sleep/lordroom/lockpick — D2) → §6 이하. Play·커밋 별도 승인.
