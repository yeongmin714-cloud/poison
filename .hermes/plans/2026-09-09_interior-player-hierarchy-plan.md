# 실내씬 플레이어 완전 작동 + 조명 근본해결 계획 (2026-09-09 6차)

> 선행: 5차(셸 상주화). 유저 요구: 실내에서 플레이어가 등장해 돌아다니며 상호작용, 애니/인벤/미니맵/스탯 전부 동일 작동, 어둠 해소.

## 0. 진단 확정 사항

1. **IndoorCamera 중복**: GameObject.Find는 비활성 오브젝트를 못 찾아, 메뉴 재실행 시 구 비활성 카메라가 누적됨(현재 씬에 구 비활성 + 신 활성 2개 존재). 유저 플레이 시 어느 쪽이 렌더할지 불안정 → **어둠/공백 원인①**
2. **머티리얼/텍스처는 정상 저장** 확인(4 mat + 4 tex + URP Lit 셰이더 guid 유효, null guid 0) — 어둠은 조명/카메라 문제임
3. **플레이어는 MainScene 소유** — IndoorScene은 Additive 로드라 플레이어는 존재하지만 **씬 계층(Hierarchy)의 IndoorScene에는 표시되지 않음** → 유저가 "씬에 없다"고 인식하는 구조적 원인. 또한 일부 시스템이 "활성 씬" 기준으로 탐색할 때 실내 컨텍스트 꼬임 가능
4. 조명: 횃불 3개 + Ambient 자동적용(4차)에도 어두움 → 실내 상주 Directional Light 부재가 근본(횃불만으로 넓은 96x72 공간 커버 불가)

## 1. Phase 계획

### P1 — 셸 메뉴 멱등성 수정 + 실내 Directional Light (코드 에이전트, 소규모)
- [ ] IndoorShellMenu: 기존 카메라/셸 탐색을 `FindFirstObjectByType<>(FindObjectsInactive.Include)` 계열로 교체(비활성도 제거) + 중복 카메라 전부 정리
- [ ] IndoorScene에 상주 **Directional Light 1개**(따뜻톤 0.8, 각도 50/-30) 추가 — 어둠 근본 해소(횃불은 보조)
- [ ] 배치 재실행(build_shell.bat) → 씬 단일 카메라+라이트 상주 확인
- [ ] CS=0

### P2 — 플레이어 하이어라키 실내 이동 (코드 에이전트, 유저 제안 채택)
- [ ] IndoorSceneTransition.OnIndoorSceneLoaded: 플레이어 텔레포트 후 `SceneManager.MoveGameObjectToScene(playerGO, indoorScene)` — **씬 계층의 IndoorScene 아래에 플레이어가 실제 표시**(유저 요구) + 활성 씬 컨텍스트 정합
- [ ] ExitBuilding: MainScene 활성화 후 `MoveGameObjectToScene(playerGO, mainScene)` + 원위 복귀(기존 로직 유지)
- [ ] 주의: 플레이어 자식 중 DontDestroyOnLoad성 오브젝트 없음 확인, CharacterController 텔레포트 패턴 유지
- [ ] CS=0

### P3 — 플레이어 부속 시스템 실내 동작 검증 (QA 에이전트)
- [ ] 애니메이션(HumanoidClipDriver)/이동/상호작용(E)/인벤·미니맵·스탯창·핫바 — 씬 이동 후에도 DontDestroyOnLoad 오버레이라 정상 작동해야 함. 실내에서 꺼지는 시스템 발견 시 목록화(예: 미니맵 IMGUI는 씬 무관 ✓, 미니맵 지형 텍스처 유지 ✓)
- [ ] 카메라: IndoorCameraFollow가 Move 이후에도 Player 태그 재탐색 ✓ 확인
- [ ] CS=0

### P4 — 통합 QA/기록
- [ ] 최종 CS=0 + Play 체크리스트: ①성문 E → 계층에 IndoorScene 하위 플레이어 표시 ②밝은 중세 셸 렌더(Directional) ③이동/애니/상호작용 ④인벤/미니맵/스탯 동작 ⑤Exit → 메인 복귀+플레이어 원위 ⑥횃불 깜빡임
- [ ] QAPROGRESS/메모리/git push/텔레그램

## 2. 수정 파일 예상
IndoorShellMenu.cs(멱등+라이트), IndoorSceneTransition.cs(Move 2곳), 씬 재저장(build_shell.bat)

## 3. 리스크
- MoveGameObjectToScene 후 플레이어 참조를 캐시한 시스템(Instance들)은 오브젝트가 살아있어 무영향 — 단, 씬 언로드가 아닌 이동이라 안전
- MainScene은 Additive 유지(월드 상태 보존) — 유저가 원한 "씬 이동"은 플레이어 계층 이동으로 충족
- Directional Light가 실내 벽 밖 조명도 비추나 셸 벽이 가림(개방형 상단은 밝아짐 — 의도)
