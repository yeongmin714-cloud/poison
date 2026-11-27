# [무번호 28] 장비 부착 잔여 뿌리 수리(P4-b) + VFX 통일 언어 기반 + 몬스터/병사·RTS/패링 고품질화

> **상태: 계획 — 사용자 승인("진행") 전 실행 금지.** 승인 후 Phase 순서대로 서브에이전트 위임(구현/QA 분리 — docs/AGENT_DELEGATION_POLICY.md).
> 사용자 요구(11-13): ①장비 부착 잔여 수리 ②몬스터 VFX 고품질 ③병사 이동·RTS VFX 고품질 ④패링 고품질 ⑤**VFX 전체 통일감**(각각 다른 느낌 금지) ⑥**기본 파티클 탈피 — 세밀한 묘사**
> 공통 규칙: 문자열 계약 사전검사(편집 전 rg)·결정론(Random 0건)·공유 밸런스값 시각 전용 축소 금지·FX 예산 가드(X2 동시 링 24) 등가 산술 갱신·색 계약 무손상·신규 FX 전부 [무번호 28-*] 마커·141차 텍스처 계약(원본해상도·BC7·q100 — 절차 텍스처는 생성해상도 표준) 준용.

---

## 0. 근거 (로그 실측 — Editor.log 11-13 09:55, Test_10_TerritoryCombat)

### 0-1. 장비 부착 잔여 문제의 뿌리 — P4 진단이 메커니즘을 역산함
```
L3176: [27-P4] heat 키 이원 실측(정규화 전) — world=1.16m localBounds=0.61m
L3456: [27-P4] heat 정규화 후 실측(1프레임 경과) — world=1.80m (계약 1.80m, 편차 0.00m)
L4214: [ArmorVisual] 바디 측정 오염 의심 — fullBody 2.43m — input y[0.79~3.22] n=1871
L4305: ✅ Helmet → Head (anchor y=2.21m, 본↔중심 0.40m) / Gloves anchor y=1.46m(=2.43m의 60% 지점)
L5327: [침하감시] 모델 bounds.center.y=1.522 / 루트 y=0.874 / 편차=0.649m
```
- **정규화 산술은 정상**(사후 1프레임 1.80m, 편차 0.00) — 수리 대상이 아님(27 계획 §1-1 판정 확정).
- 그러나 장비 측정 시점(애니 개시 후)엔 모델 메시 자체가 **2.43m = ×1.35 스트레치**. 증거 체인:
  바인드 메시 스팬 1.16m(×1.552=1.80 ✓) → 휴머노이드 아바타 적용 후 골격이 바인드 대비 1.35배(메시 1.566m)로 늘어남 → ×1.552 스케일이 그대로 곱해져 2.43m.
- 장비 피팅은 **스트레치된 본에 내부적으로 일관되게** 부착됨(Gloves 1.46/2.43=0.60 = 정상 비율) — 사용자가 보는 "부착 불량"의 실체는 **캐릭터가 계약(1.8m) 대비 1.35배 과대**라는 것. 투구 anchor y=2.21m도 2.43m 몸의 머리 위치로는 자기일관.
- 부수 확정: 팁-끝 진단 수리(P2) 정상 작동 — 2건 전부 "먼 쪽(정상)" 판정(L4053/L47339).
- 침하감시 편차 0.649m — SinkWatch 집계에 무기(스피어)가 포함된 의심(ArmorVisual은 무기 제외 후 0.79 발 위치) → P4-b 프로브에서 무기 포함/제외 집계 차이도 확인.

### 0-2. VFX 현황 인벤토리(중복 방지 — 무번호 18~26+25후속 착지분)
- 몬스터: 발견 ! 링+Alert SFX / 부채꼴 텔레그래프(그라디언트·긴장 램프·에지 셔머·컬러램프) / conclude 플래시 / 수렴 링 2겹 / 스윙 지면 파동 3본 / 불티 / 에지 순회 에너지 / 스콜치 데칼 / 임팩트 링(HDR+깊이페이드) / 몸색 혼합 히트컬러 / 사망 DeathVFX(+KillVFX 이중 발화 — Play 판정 대기) / 날씨 틴트
- 병사·RTS: 이동 트레일 먼지+퍼프 2레이어(0.6m 게이팅) / 도착 퍼프 / smoothstep 이징 / 이동·집결 링 데칼 전환(세그먼트+중앙 필) / 선택 링 C1~C4(회전 세그먼트·중앙 필·확산 펄스·3+겹침 알파) / 명령 진행도 링 / 병사 스윙 리본(SoldierSwingTrail). RTSSelectionOverlayUTK는 삭제(D) 상태 — 선택은 GuardSelectionManager+SelectionRingController 경로
- 패링: 디플렉트 팬(±55° 6방향) / 등급 위계(PERFECT 12·GOOD 6) / easeInCubic 잔광 / 스파클 버스트 / 곡선 위스프 아크 / 청록·골드 비네트 / 히트스톱+FxSequencer 7요소 타임라인 / 화이트 프레임(배치 구조 한계 확정 — Play 판정 대기) / 패러독스 타임스톱
- 공용: FXBandingRing(발광+깊이페이드) / FxSequencer / X2 예산 가드 / ScreenVignetteFX / CombatCameraEffects(FOV 펀치 — 24-CA)
- **통일감 진단(사용자 요구 ⑤의 실측 배경)**: 색 값이 파일마다 흩어져 있고(이동 청록/공격 주황/패링 금·청 등 — 계약상 색은 정합, 그러나 단일 소스 부재), 텍스처 품질이 FX별로 제각각(링 밴드 64×16 2옥타브 / 먼지 128 3옥타브 / 스코치 128 / 별·플레어 단순 도형), 모션 언어(이징·수명·소멸 패턴)도 라운드마다 다르게 적용됨 → "각각 다른 느낌"의 구조적 원인.

---

## 1. Phase 0 — P4-b: heat 아바타 스트레치 수리 (선행봉합 — 최우선)

> 원칙: "UI장애=신규Phase 전 선행봉합" 준용 — 장비 뿌리가 남은 상태에서 VFX 신규 Phase 착수 금지.

### P4-b1 — EditMode 아바타 실측 프로브 (Play 불요 — QA 서브에이전트 실행)
- heat 프리팹(Resources "Models/UserProvided/fbx/Player_Rigged_Heat") EditMode Instantiate →
  ①SMR bind 스팬(sharedMesh.bounds.size.y — 예상 1.16m) ②`animator.Update(0)` 후 아바타 T-포즈 월드 스팬(예상 ≈1.566m) ③avatar humanScale/경고 로그 ④스트레치비(②/①) 산출
- 판정 규칙: 비가 포즈 무관 상수(≈1.35)면 **1안** 채택. 포즈별 편차 크면(의심) 2안으로 보고.
- 파일: 신규 프로브(임시 Editor 스크립트 또는 EditMode 진단 테스트 — 증거만 남기고 잔존 여부는 QA 보고에서 판정)

### P4-b2 — 수리(1안: 런타임 재정규화 — 프로브가 상수비 확정한 경우만)
- **위치**: TestPlayerAnimatorBoot 아바타 감시(AvatarArrivalWatch)가 아바타 도착+첫 포즈 적용을 확인한 직후 1회.
- **동작**: animated 월드 스팬(MeasureStandingHeight skinnedLocalBounds:false — 애니 상태 bounds) 재측정 → 계약 1.8m 대비 편차 >5%일 때만 재스케일(멱등 가드 — 2회 정규화 방지 플래그) + 발 접지 재보정(PlayerPlaceholder [125차 후속11] foot-drop 패턴 준용) + [27-P4] 로그 확장(재정규화 전/후 실측).
- **계약**: SoldierTargetHeight 단일 사용(139차 1.8m 고정계약) / CharacterHeightUniformTests 무손상 / NormalizeSoldierHeight 본체 무수정(호출부에서만) / 장비·무기는 본 계층 자식이라 균등 스케일 자동 전파(피팅 비율 보존).
- 2안(포즈 의존 확정 시): FBX 임포트 아바타 재구성 필요 — 에셋 차원 변경이라 별도 사용자 확정 후 별도 라운드.

### P4-c — 장비 피팅 재검증 (Play 1회 — 통합 판정 라운드에서)
- 기준: [F4] fullBody H ≈ 1.80m 수렴 / 투구 본↔중심 ≤0.25m / Armor scale보정 ≈1.0 유지 / Gloves anchor가 손 본 근처 / 침하감시 편차 재판정.

---

## 2. Phase U — 통일 비주얼 언어 기반 (M/S/P 전 적용 — 사용자 요구 ⑤의 정체성 레이어)

> 성격: 신규 FX가 아니라 **모든 FX가 공유하는 규칙·자산의 단일화**. M/S/P의 각 항목이 이 기반 위에서 구현된다. 기존 잠긴 색 상수의 물리적 이동은 계약테스트 파손 리스크가 커서(주석 문구도 잠김 — 2회 재발 선례) 본 Phase는 "신규 전면 참조 + 기존은 문서 단일화"로 스코핑.

| 항목 | 내용 | 파일(예상) | 계약 주의 |
|---|---|---|---|
| U-1 공용 팔레트 단일 소스 | `FXPalette` static 클래스 신설 — 기존 계약색을 그대로 값만 이원(이동=청록 0.188,0.835,0.784 / 공격=주황 1,0.588,0.196 / 집결·패링=금 0.980,0.860,0.300 / 몬스터=적 1,0.30,0.16 / 정예=주황 / 먼지=흙색 0.78,0.72,0.62 / 병사 리본=청백). **M/S/P 신규 FX는 전부 FXPalette 참조 필수.** 기존 잠긴 상수는 이번 라운드 이관 금지(계약 보호) — 이관은 별도 라운드(정밀 계약 갱신 필요) | 신규 FXPalette.cs + M/S/P 소비처 | B3/C2 색 계약 잠금 테스트 rg 선행검사 — 기존 상수 무손상 |
| U-2 텍스처 품질 표준 상향 | 절차 텍스처 생성 표준을 전 FX 통일 — **링 밴드 64×16 2옥타브 → 256×64 4옥타브**(요동 세분+에지 글로우 그라디언트), 먼지 128 3옥타브 → 256 4옥타브(컬러 램프 내장), 스코치 크랙 8본 → 12본+가지. 단순 도형(별·플레어 사각 등) 잔존분 전수 조사 후 표준 텍스처로 교체 | ShockwaveRingFX(R1/Q1 확장) / DecalSpawner / SpawnStarFlare | 색 승수 구조(_Color) 무손상, 기존 컷(pxhash) 재판독으로 품질 비교 증거 |
| U-3 모션 언어 3종 표준화 | 이징 커브를 3종으로 통일 — ①진행류=smoothstep+임박 가속(≥0.8 +10%) ②잔광류=easeInCubic ③출현류=easeOutBack 소폭(스폰 펄스). 신규 FX 전부 3종 선택 사용 + 수명 규약(짧은 FX 0.4~0.9s / 지속 링 1s 표시 구간[72차] / 오라류 루프) + 소멸 패턴 통일(**팝 소멸 금지 — 램프다운**, 24-FA1·C3 선례) | Runner 계열(RingRunner/FanRunner/WispRunner/ConvergeRunner) | [72차] 1s 규약·도착 소멸 미적용 계약 유지, 기존 이징 무손상(신규만 표준 적용) |
| U-4 렌더 파이프라인 통일 | ①지면 링 패밀리 전부 FXBandingRing(HDR emissive+깊이 페이드) — 잔존 Lit/Standard 링 전수 조사(rg Shader.Find/URP Lit) 후 이관, **Lit 폴백 리터럴 잠금 유지**(CombatFxParryFlashPhaseDTests) ②파티클 전부 소프트 파티클(카메라 페이드 — DepthTexture 활성 확인됨) → 기존 M5를 여기로 흡수 | ShockwaveRingFX / FXBandingRing.shader / 파티클 생성 경로 | _Color/_MainTex/_EmissionColor/_DepthFadeDist 속성 계약, rgb saturate 금지(HDR) |
| U-5 레이어드·등급 표준 | 신규 히트/스폰/판정 FX는 **코어 플래시+버스트+팬(또는 방사)+지면 링 4레이어 기본**(무번호 20 품질 바 — 2레이어 미만 신규 금지) + 등급/티어 파라미터 차등 필수(PERFECT/GOOD·Normal/Elite) | 각 신규 Spawn API | 결정론 해시 셔머만(Random 금지), Spawn 시그니처 불변 원칙 |

## 3. Phase M — 몬스터 VFX 고품질화 (U 기반 위에서 — 세밀 묘사 사양 포함)

| 항목 | 내용(디테일 사양) | 파일(예상) | 계약 주의 |
|---|---|---|---|
| M1 충전 글로우+프레넬 림 | 텔레그래프 windup 연동 몬스터 본체 emissive 램프(fill≥0.75 상승, conclude 피크) + **Fresnel 림**(실루엣 가장자리만 밝아지는 위험 예고 — 단색 오버레이 금지) | AnimalAI / AttackTelegraphIndicator | 몸색 소비(gizmoColor)·EA1 MonsterAccentImpactColor 충돌 검사, 머티리얼 인스턴스 누출 금지, 등 위계(정예=주황 림) |
| M2 피격 반응 | 플레이어 피격 시 미세 FOV 킥+비네트(피해량 비례 — 21-L4 재제안) — 비네트는 U-3 이징 표준 적용 | CombatCameraEffects / ScreenVignetteFX | Core(PlayerHealth) 무수정 — OnPlayerDamaged 구독 선례(23-V1) |
| M3 사망 시퀀스 2단+디졸브 | 사망 = ①스모크 **플립북 4~6프레임(절차 생성 — 끓어오름 노이즈)**+소프트 파티클 ②스파크 버스트(등급별) ③지면 링 잔광(easeInCubic) ④(옵션)본체 Dissolve(노이즈+엣지 하이라이트 — U-D). 이중 발화(PlayKillVFX+DeathVFX) Play 판정 후 정리 | DeathVFX / MonsterData / AnimalAI | EA2 신계약(DeathVFX 경로) 유지·유한 수명 불변 |
| M4 정예 오라 | 정예 지속 오라 — 발밑 링 2겹(U-2 표준 밴드 텍스처)+펄스(U-3 출현 이징) + 몸 Fresnel 틴트 상시(주황) | AttackTelegraphIndicator / ShockwaveRingFX | B3 위험도 색 계약 무손상 — EliteColor 승계만 |
| M5 (U-4 흡수) | 소프트 파티클·링 파이프라인 통일은 Phase U에서 일괄 적용 | — | — |

## 4. Phase S — 병사 이동 + RTS VFX 고품질화 (U 기반 위에서)

| 항목 | 내용(디테일 사양) | 파일(예상) | 계약 주의 |
|---|---|---|---|
| S1 스텝 리듬 먼지 | 거리 게이팅(0.6m) 유지 + **스텝 주기 동기 발자국 먼지**(좌우 교차 오프셋 — 결정론 해시, 발 위치 y 스냅) + 먼지 자체는 U-2 4옥타브 표준 | GuardPlaceholder(StepToward L1366~1398) | F1 "이동 중에만" 계약, 도착 퍼프 별개 유지 |
| S2 이동 시작 킥(병사판) | 출발 순간 발밑 퍼프 1회(플레이어 Q2 선례의 병사 적용) — 도착 퍼프와 대칭 완성 | GuardPlaceholder | SpawnMoveTrailPuff 시그니처 불변 |
| S3 명령 수락 스태거 | 그룹 명령 시 유닛별 0.04s 스태거 링 발화(FxSequencer 리듬 패턴 재사용 — 동시 발화 해소) + 링은 U-2 표준 밴드 | RTSCommandFeedback / CommandMarker / ShockwaveRingFX | 72차 1s 규약·도착 소멸 미적용 계약 유지, _groupOwners 계약 갱신 금지 |
| S4 선택 유지 연출 | 선택 유지 중 미세 펄스/회전 속도 변조 + 에지 셔머(U-2 표준) — **84차 SelectionRing 고품질 계약(이중 링/회전 대시/_WaveT) 충돌 사전검사 필수** | SelectionRingController / SelectionRing.shader | 셰이더 파라미터 계약 잠금 — rg 선행검사 후 설계 확정 |
| S5 경로 데칼 트레일(옵션) | 이동 경로 위 데칼 잔존 페이드(U-3 램프다운) — 과밀 우려 → Play 판정 항목 | DecalSpawner | 데칼 per-instance 머티리얼(24-FA1) 선례 준용 |

## 5. Phase P — 패링 시스템 고품질화 (U 기반 위에서)

| 항목 | 내용(디테일 사양) | 파일(예상) | 계약 주의 |
|---|---|---|---|
| P1 무기 본체 플래시 | 패링 순간 무기 GLB 머티리얼 인스턴스 emissive 펄스 0.15s(Fresnel 림 동시 — U-D 자산 재사용) — 21-M1 미채택 사유(방패 렌더러 참조 부재) → 무기 경로 재검토 | PlayerCombat / WeaponEquipManager | CurrentVisual 계약(ArmorVisualFitGuardTests), 머티리얼 인스턴스 원복 보장 |
| P2 리플렉 스파크 궤적 | 흡수점→공격자 방향 반사 베지어 스파크 1~2호 — WispRunner(T1) 재사용 + 스파크에 단면 그라디언트·테일 페이드(U-2/U-3) + 등급 위계 연동 | ShockwaveRingFX / PlayerCombat | 결정론(Random 0건), SpawnParry* 시그니처 불변 |
| P3 공격자 반응 플래시 | 패링 성공 순간 공격자 몸 백색 반전 0.1s → 원복 — EA1 헬퍼(MonsterAccentImpactColor) 재사용 | AnimalAI / PlayerCombat | 몸색 25% 혼합 계약 무손상(기존 히트컬러와 별도 경로) |
| P4 화이트 프레임 Play 판정 | 구조적 한계 확정 상태 — 본 라운드 Play에서 실체감 일괄 판정(코드 수리 없음) | — | 위조 배선 금지 원칙 유지 |
| P5 BulletTime 커브(옵션) | 패링 슬로우모션 진입/해제 커브 미세 튜닝 — 파라미터만 | BulletTime | 별도 수치 확정 후 |

## 6. Phase D — 세밀 묘사 셰이더 자산 (U-2와 별행 — L2 사다리, 범위 확정 필요)

> "기본 파티클 탈피"의 기술적 정체 — M/S/P에서 소비하는 공용 셰이더 자산을 먼저 만들어두면 각 Phase는 조합만으로 세밀해진다.

| 항목 | 내용 | 파일(예상) | 계약 주의 |
|---|---|---|---|
| D-1 절차 플립북 생성기 | 연기/불꽃 플립북 4~6프레임 절차 생성(결정론 — 멀티옥타브 노이즈 시드 고정) — M3 사망 연기·불티가 소비 | 신규 절차 텍스처 유틸(161차 GetStarTexture 선례 계열) | 141차 텍스처 계약 준용, Random 0건 |
| D-2 Dissolve 셰이더 | 노이즈+엣지 하이라이트 디졸브(사망/소멸 연출용 — URP Shader Graph 수기 패턴 or 코드 셰이더) | 신규 shader | FXBandingRing 선례(URP 규약 SAMPLE_TEXTURE2D) 준용 |
| D-3 Distortion(열기) | 스크린 공간 UV 왜곡 — 불티/스코치 잔열 연출 | 신규 shader + FullScreenPassRendererFeature(24-CA 수기 편집 선례) | 렌더러 Feature 추가는 URP 렌더러 데이터 에셋 수정 — B5 선례(DecalRendererFeature 등록) 준용 |
| D-4 VFX Graph(L3) | **본 라운드 보류** — 결정론/프로브 재현성 리스크+도입 비용. 필요성이 Play 판정에서 확인되면 별도 라운드 제안 | — | playbook L3 근거 기록 |

---

## 7. 검증 (Phase 공통 — 스킵 불가 흐름)
1. 구현 서브에이전트: 코드+계약테스트(Phase당 6케이스 관행) + rg 자기검증
2. 부모 게이트: `rm -f Temp/UnityLockfile && timeout 550 bash compile_test.sh` → error CS 0
3. 독립 QA: EditMode 전수 + baseline 10건 1:1(양방향 차집합) + 신규 케이스 전부 Passed
4. cfx 프로브 신규 컷(추가되는 FX별) + 비전 판독 — 판정 기준에 **통일감 항목 추가**: "같은 팔레트·같은 밴드 품질·같은 이징" 교차 FX 비교 컷(예: 이동 링 vs 패링 링 나란히) 합격 필요
5. Play 통합 판정(사용자 승인): P4-c 피팅 + U/M/S/P 체감 + 기존 누적(19~25후속) 일괄
6. 증거 경로: TestOutput/qa28-*(라운드 접미사 — 기존 증거 무덮어쓰기)

## 8. 순서와 승인
- 순서: **P4-b(Phase 0) → U(기반) → M → S → P** — Phase 0 미완료 시 이후 착수 금지, U 미완료 시 M/S/P 신규 FX가 구표준으로 흩어지므로 동일 강제.
- 각 Phase 완료 보고 후 다음 Phase 진행(사용자 "진행" 누적 승인 관행 유지 — Play·커밋은 별도 게이트).
- 미채택/옵션(S5·P5·D-2 이후)은 Play 판정 후 별도 확정.
