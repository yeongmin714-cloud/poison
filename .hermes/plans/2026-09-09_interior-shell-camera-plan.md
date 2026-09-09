# 내부씬 기초 지형 상주화 + 실내 카메라 + 중세 감성 계획 (2026-09-09 5차)

> 실행 규약: 코드 위임/부모 직접, CS=0 게이트, 3중 저장+텔레그램. 선행: 2026-09-09 4차(내부 클램프 스킵 수정).

## 0. 유저 요구

1. **IndoorScene에 실제 지형이 존재하게** — 현재 IndoorRoot 빈 오브젝트 1개뿐(빌더는 상호작용 시 런타임 생성 → 씬 뷰에 아무것도 없음)
2. **내부씬 예시 뷰 재현(중세 치환)**: 바닥(거친 돌판/나무판자) + 벽(석벽 하단+회반죽/목재 상단) + 천장 없음(서까래만) + 횃불(따뜻한 오렌지) + 화로/탁자 등 중세 소품 + 쿼터뷰 카메라
3. **실내 카메라 부착** — 씬에 카메라 상주, 전환 시 메인 카메라와 스왑

## 1. 조사 결과

- IndoorScene.unity: **IndoorRoot 빈 오브젝트 1개뿐** — 지형 전무 (빌더 런타임 생성 방식)
- 기존 자산: IndoorBuilder.CreateRoom(방 프리미티브), IndoorTextureGenerator(절차 텍스처), 8종 빌더(House/Castle/PlayerCastle/Church/Barn/Shop/Cave/CraftHouse) — 각자 방을 자체 생성
- 실내 라이트 1개 존재(IndoorScene), 실내 전용 카메라는 없음(메인 카메라 추적 의존 → 4차에 클램프 스킵 수정 완료)
- 프로젝트 한글 폰트 없음 → 텍스처 절차생성은 무관

## 2. 설계

**핵심: "기본 셸(Shell) 상주 + 빌더는 가구만" 구조로 전환**
- IndoorScene에 **중세 기본 셸**을 씬 오브젝트로 상주 저장 → 씬 뷰에서도 보임, Play 즉시 존재
- 셸 규격(표준): 바닥 12x9m(돌판), 벽 4면 높이 4m(하단 석벽 2m + 상단 회반죽/목재 하프팀버 2m), 천장 없음, 벽 상단 서까래 3개, 입구 틈(남측)
- 조명: 횃불 PointLight 3개(오렌지 0xFFA040, 강도 진동 스크립트), 은은한 Ambient(따뜻톤)
- 실내 카메라: 쿼터뷰 45°(pitch 40°/거리 12) 상주, 기본 비활성 → 전환 시 스왑
- 빌더 8종: CreateRoom 스킵(셸 재사용) + 가구/장식만 배치 — 방 치수 표준화

## 3. Phase 계획

### P1 — 기본 셸 생성기 (코드 에이전트)
- [ ] IndoorTextureGenerator 확장: Flagstone 바닥/석벽/회반죽/목재 패턴 절차 텍스처 4종
- [ ] MedievalShellBuilder: CreateShell() — 바닥/벽4(문틈 남측)/서까래3/횃불 홀더3/Ambient 포함 1루트 반환
- [ ] 에디터 메뉴 `Tools/Indoor/중세 기본 셸 생성+저장`: IndoorScene 오픈 → 셸 생성 → 씬 저장 (씬에 영구 상주화)
- [ ] TorchFlicker 스크립트(포인트라이트 강도 사인+노이즈 진동)
- [ ] 배치컴파일 CS=0

### P2 — 실내 카메라 부착 + 스왑 (코드 에이전트)
- [ ] IndoorScene에 `IndoorCamera`(Camera 오브젝트, 쿼터뷰 45°, 기본 비활성) 상주 저장 — P1 저장 시 함께
- [ ] IndoorSceneTransition: EnterBuilding 시 ①메인 카메라(GameObject.FindGameObjectWithTag("MainCamera")) 비활성 ②IndoorCamera 활성 ③빌더 실행 ④플레이어 텔레포트. ExitBuilding 시 역순(메인 카메라 복귀)
- [ ] BuildingTrigger 말풍선: 활성 카메라 자동 인식(기존 Update 재시도 로직이 처리)
- [ ] 배치컴파일 CS=0

### P3 — 빌더 표준화 (코드 에이전트)
- [ ] P0 감사 기반: 8종 빌더의 CreateRoom 호출을 셸 재사용으로 전환(가구만 배치) — 치수 상이한 빌더는 셸 파라미터(폭/깊이) 오버라이드 허용
- [ ] 중세 소품 배치: 화로(돌+장작+붉은 PointLight), 오크 탁자+양피지, 갑옷 거치대(캐슬), 탁자 위 촛불 — 기존 프리미티브 헬퍼 재사용
- [ ] 배치컴파일 CS=0

### P4 — 통합 QA (QA 에이전트)
- [ ] 최종 CS=0 + 정적 검증(셸 중복 생성/RT 해제/스왑 누수)
- [ ] Play 판정 체크리스트:
  1. 씬 뷰(IndoorScene)에 바닥/벽/서까래/횃불/카메라 상주 확인
  2. Tools/Indoor 미리보기 → 셸+가구 렌더, 실내 카메라 뷰
  3. 성문 E → 실내 카메라 스왑 + 실내 렌더(중세 톤) + 가구
  4. Exit → 메인 카메라 복귀
  5. 횃불 깜빡임/따뜻한 톤 확인
- [ ] P5 기록: QAPROGRESS/메모리/git push/텔레그램

## 4. 수정 파일 예상

| 구분 | 파일 |
|:--|:--|
| 신규 | MedievalShellBuilder.cs(또는 IndoorBuilder 확장), TorchFlicker.cs, 에디터 셸 생성 메뉴 |
| 수정 | IndoorTextureGenerator.cs(텍스처 4종), IndoorSceneTransition.cs(카메라 스왑), 8종 InteriorBuilder(셸 재사용) |
| 씬 | IndoorScene.unity(셸+카메라 상주 저장) |
| 문서 | QAPROGRESS/메모리 |

## 5. 리스크/주의

- **빌더 8종 치수 상이**: 셸 표준(12x9)과 개별 빌더 방 크기(8x6 등) 불일치 → 셸 파라미터 오버라이드로 흡수, 가구 좌표는 기존 유지
- **씬 YAML 직접 작성 금지**: 에디터 메뉴로 생성 후 저장(안전)
- **카메라 스왑 누수**: ExitBuilding에서 반드시 복구(기존 카메라 비활성화 방식 보존) — 퇴장 실패 대비 예외 가드
- **URP 절차 텍스처**: 기존 IndoorTextureGenerator 셰이더 파이프라인 재사용
- 실내 천장 없음 → 하늘 비침 → Ambient/서까래로 시야 보정(예시 스타일)

## 6. 검증/배치
- build_stats.bat 재사용, Phase별 CS=0 게이트
- Play 판정: P4 체크리스트 (스크린샷 순번 보고 병행)
