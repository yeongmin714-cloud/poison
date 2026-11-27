# [125차 후속25 계획] 투기장 미가시 뿌리 확정 — RT 캡처 결정 감사 (1회 Play로 종결)

> 상태: **계획 수립 — 구현은 사용자 승인 후.** 근거: 사용자 제공 F1 감사 로그(후속22) + 소스 실측.
> 목표: "이번 만큼은 확실하게" — 남은 가설 전부를 **1회 Play의 물리적 증거(RT PNG)**로 분리·특정한다.

---

## 1. F1 감사 로그 판독 — 이미 확정된 사실 (재검증 불필요)

| 항목 | 실측 | 판정 |
|---|---|---|
| 카메라 ArenaTestCamera | activeInHierarchy=True / MainCamera / ortho 8 / (0,26,0) / mask=Everything / targetTexture 없음 | **정상** |
| 카메라 회전 | 소스 L492 Euler(90,0,0) 수직 하강 + "관전 카메라 확정" 로그 | **정상** |
| 포그 | RenderSettings.fog=False 실측(감사 로그) + ArenaTestSetup.Awake fog=false(후속23 F2) | **렌더 설정 포그는 꺼져 있음 확정** |
| 지형 메시 | Stage/Apron active·rend=True·Default layer·URP/Lit·bounds 정상(35.6×0.3×37.2 / 45.6×0.1×47.8) | **정상** |
| 야간 조명 분기 | 감사 ambient=(0.13,0.15,0.20) = 빌더 달빛값 정확 일치(소스 L218) → 야간 분기 실행 확인 | **생성 코드 실행됨** |
| HQ 개입 | F3 스킵 정상(ambient가 HQ 값이 아닌 빌더 값) | **정상** |
| WallSeg_0 미발견 | 게이트 각도 스킵(IsGateAngle) 가능성 — 벽 링 자체 부재와 구분 필요 | **미확정 — v2에서 WallSeg_1 보조 키로 재확인** |

**결론: 렌더 "조건"은 전부 정상. 미가시 원인은 조건 밖에 있다.**

## 2. 포그 사용자 관찰 답변(현시점 확정분)

- RenderSettings.fog=False가 **투기장 진입 시점 실측**됐다. 즉 Unity 포그는 꺼져 있다.
- 화면에서 "안개처럼 보이는 것"의 후보: ①야간 저앰비언트(0.13,0.15,0.20)+빈 스카이박스 그라데이션의 저대비 착시(후속22 판정 유지) ②화면을 덮는 UI 오버레이 ③전환 후 매 프레임 포그 강제원(BiomeEffectController/DayNightCycle/TestDraculaSetup/PhaseG2_SkyboxSetup — 소스에 fog=true 5곳 존재)이 투기장 씬에 살아있다면 재점화.
- v2가 ③을 컴포넌트 존재 덤프로, ①②를 RT PNG 판독으로 각각 확정한다.

## 3. 남은 가설 매트릭스 (v2가 1회에 분리)

| 가설 | 내용 | 기존 정합 증거 | 판별 수단(v2) |
|---|---|---|---|
| H1 화면 오버레이 | 카메라는 정상 렌더, 화면 위 UI(베팅창 잔존 스크림/기타 Canvas)가 덮음. 영상의 "캐릭터 실루엣"이 사실 UI 초상화(RenderFighterPortrait 회색 배경)였을 가능성 | f_06 실루엣+회색 배경 / f_13 회색 그라데이션 | **RT PNG(오버레이 배제 렌더)** — PNG 정상이면 H1 확정 |
| H2 씬 공존 | Test_18_Arena가 Additive 생존 — DDOL 러너 + Ground(60×60, 원점 y=0) + 스테이징 방향광(1.0)이 투기장(원점)과 공존 | 스테이징/로비/투기장 전부 원점 계열 배치 | **씬 루트 전수 덤프**(살아남은 Ground·잔존 룸 적발) + PNG에 Ground가 보이는지 |
| H3 머티리얼 검정 | 절차 텍스처가 검정 출력 → 어떤 빛에도 알베도 0 → 항상 미가시. 로비(파일 텍스처)=보임 vs 투기장(절차)=미가시 대비 정합 | 로비 주황 바닥(f_02) vs 투기장 전무(f_06) | **머티리얼 프로브** — _BaseColor+텍스처 픽셀 샘플 평균 밝기 |
| H4 라이트 소실/무효 | 달빛/횃불이 감사 시점에 실제로 없거나 강도 0(기존 감사에 라이트 덤프 부재 — 구조적 공백) | 후속18 조명 로그는 구세session 것 | **라이트 전수 덤프**(달빛 방향광 존재·강도·활성) |
| H5 URP 에셋 | 후속19 YAML 인라인 주석 편집 이력 — 파손 시 이상 렌더 | 로비는 정상 렌더 → 가능성 낮음 | PNG 검정+전부 정상일 때만 재검증(주석 제거 복원) |

## 4. R1 — 감사 v2 구현 (승인 시 즉시)

**파일**: IndoorSceneTransition.cs (AuditPitRendering 확장) / ArenaTestSetup.cs (스테이징 시점 1회 감사)

1. **RT 캡처(핵심)**: ArenaTestCamera.Render() → RenderTexture 960×540 → ReadPixels →
   `TestOutput/pit_audit_rt.png` 저장 → 복원. 화면 공간 UI는 RT에 미포함 = **순수 카메라 출력**.
2. **씬 루트 전수**: 활성 씬 전체 + DontDestroyOnLoad 오브젝트 — 이름/active/position 루트 단위 덤프
   (살아남은 Ground/잔존 로비 룸/스테이징 광원 즉시 적발).
3. **라이트 전수**: 이름/타입/enabled/intensity/range/color/좌표 — 달빛 방향광 존재 여부 명시.
4. **머티리얼 프로브**: Stage/Apron/TorchFlame — _BaseColor, mainTexture 유무+크기+픽셀 샘플 평균 밝기(0~1),
   화염 _EmissionColor+키워드. WallSeg_1 보조 키로 벽 링 존재 재확인.
5. **카메라 near/far + 스카이박스 머티리얼명 + 활성 Canvas 목록(sortingOrder)** — H1 보조.
6. **포그 강제원 컴포넌트 덤프**: BiomeEffectController/DayNightCycle/WeatherManager/TestDraculaSetup/PhaseG2_SkyboxSetup의
   로드 씬 내 존재 여부 — "포그 재점화원" 판정.
7. **스테이징 시점 1회**(ArenaTestSetup.Start 지연 1초): 동일 덤프+PNG — 사용자가 본 "포그"가 스테이징인지 투기장인지 분리.

**검증**: 컴파일 0 / EditMode baseline 1:1 / **사용자 Play 1회** → PNG를 내가 직접 vision 판독 + 로그 판독 → 뿌리 확정 보고.

## 5. R2 — 원인별 수리 분기표 (R1 판별 결과에 따라 1개만 실행)

| 판별 결과 | 수리 |
|---|---|
| Case A — PNG 정상 렌더 + 화면 미가시 | 화면 오버레이 수리: 활성 Canvas 덤프로 덮개 특정(베팅창 잔존/기타) → 해당 종료 로직 수리 |
| Case B — PNG에 Ground/이질 물체 | 씬 공존 수리: 투기장 진입 시 스테이징 Ground/이월 오브젝트 정리(또는 투기장 룸 y 오프셋) |
| Case C — 라이트 소실/강도0 | 라이트 생성/파괴 경로 수리(씬 언로드 유실, 덮어쓰기 등 — 덤프가 방향 제시) |
| Case D — 머티리얼 검정 | 절차 텍스처 수리 or A1 팩토리의 파일 우선 강제 전환 |
| Case E — PNG 검정+전부 정상 | URP 에셋 무결성 복원(후속19 인라인 주석 제거 원본 복원) 후 재검증 |

## 6. 전역 계약

- EditMode baseline 11건 1:1 / 컴파일 error CS 0 / 결정론(재방문 동일 결과) / 텔레그램 라운드 보고 병행
- R1은 감사 코드만 추가(렌더/게임플레이 무영향), R2는 R1 판별 전 미착수 — **원인 확정 전 수리 금지(사용자 요구: 확실하게)**
