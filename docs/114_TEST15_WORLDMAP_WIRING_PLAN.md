# 114차 계획 — 테스트 15씬 월드맵 표시 (UTK 월드맵 배선)

> 2026-10-22 · 사용자 요구: "테스트 15씬에도 월드맵이 보이게 만들어줘"

## 0. 실측 근거 (감독자 사전 조사 — 좌표 박기)

1. **Test_15_Livelihood는 Test_10/13/14/17과 셋업 공유**: `TestTerritoryCombatSetup.Awake()`(Assets/Scripts/Systems/TestTerritoryCombatSetup.cs)가 무조건 `SetupUITestArena()`(75행) 호출 → UTK 창 전수 Ensure.
2. **UTK ensure 목록(2277~2285행)**: Minimap/HUD/Hotbar/Status/Quest/Journal/TimeClock + UTKWireUp.Wire — **월드맵만 누락**.
3. **WorldMapWindowUTK(Assets/Scripts/UI/Toolkit/WorldMapWindowUTK.cs)는 완성·미배선 상태**: UTK GitHub-dark 리스타일(Figma 스펙) 적용, `Ensure()`가 UIRoot 부착 + M키 폴링 기동(399행 `kb.mKey.wasPressedThisFrame`), M키/ESC 자체 토글. 전 코드베이스에서 호출부 0건 — 이번 배선으로 처음 활성화.
4. **M키 충돌 감사 ✅**: 소비자 = TutorialActionDetector(330행, 튜토리얼 "19_world_map" **감지 전용** — 지도 열기를 전제, 배선과 정합) / KeyBindings._mapKey(M, 설정 SO — 런타임 토글 소비자 없음). 토글 충돌 0건.
5. **Test_15 원칙**: "[UTK 전환 2026-09-26] GitHub-dark UTK 기준 재작성 — 구식 IMGUI/UGUI 창 생성 제거"(2241~2253행) → 레거시 MapWindow(IMGUI/UGUI) 생성이 아닌 **UTK 월드맵 Ensure 배선**이 정답.
6. 시스템 asmdef은 UI 참조 불가(순환) → 기존 패턴대로 `InvokeUtkStatic(uiAsm, "ProjectName.UI.Toolkit.X", "Ensure")` 리플렉션 호출.

## 1. 구현 (범위: 2파일)

### 1) `Assets/Scripts/Systems/TestTerritoryCombatSetup.cs`
- SetupUITestArena의 ensure 목록에 1행 추가(TimeClock 다음, UTKWireUp.Wire 이전):
  ```csharp
  InvokeUtkStatic(uiAsm, "ProjectName.UI.Toolkit.WorldMapWindowUTK", "Ensure"); // [114차] 월드맵 — M키 토글(자체 폴링), Test_10/13/14/15/17 공통 셋업
  ```
- 부트 로그(2285행) 갱신: `"...Journal/Clock + wire-up"` → `"...Journal/Clock/WorldMap + wire-up"`.

### 2) `Assets/Scripts/Tests/EditMode/WorldMapSceneWiringTests.cs` (+.meta 신규, GUID 32hex 무하이픈)
소스 계약 테스트(unity-source-contract-tests 규약, 단일행 단언):
- TestTerritoryCombatSetup.cs에 `InvokeUtkStatic(uiAsm, "ProjectName.UI.Toolkit.WorldMapWindowUTK", "Ensure")` 존재 — "WorldMap"이자 "Ensure" 인자 정확 일치.
- 부트 로그 문자열에 "WorldMap" 포함(ensure 목록 계약 갱신 동행).
- WorldMapWindowUTK.cs에 `public static void Ensure()` + `kb.mKey.wasPressedThisFrame` 존재 — 자가부트+M키 폴링 계약(배선이 동작하는 전제).
- [테스트 계약] SetupUITestArena가 LordsWar/Livelihood 분기 이전 base 흐름에서 호출됨 — `SetupUITestArena();` 호출 라인 존재 단언(Test_15 커버 근거).

## 2. 키 배치 자가감사 결과
- M키: WorldMapWindowUTK 토글(신규) + TutorialActionDetector 감지(관측 전용, 충돌 아님 — "19_world_map" 튜토리얼 단계와 정합). 기존 U키(건설)/O키(GameStats)/I키(인벤)/P키(상태)/Y키(일일틱)와 무충돌.

## 3. 완료 기준
- 컴파일 error CS = 0. EditMode 전수: total 1435(신규 테스트 수에 따라 가산) / baseline 11건 1:1 / 신규 GREEN.
- 범위 외 수정 금지(위 2파일만). Play 실행 금지(사용자 게이트).

## 4. Play 확인 목록 (승인 게이트)
1. Test_15 진입 → **M키** → 월드맵 창 표시(영지 81개 마커 + 플레이어 마커 + 범례)
2. M키 재입력 → 닫힘, ESC → 닫힘
3. Test_10/13/14/17에서도 M키 토글 동작(공유 셋업 동반 커버)
4. 지도 열림 상태에서 게임 UI(건설 U키 등) 상호 간섭 없음

## 5. 참고 (보고 사항)
- Test_15에 표시되는 지도는 **UTK 월드맵**(GitHub-dark) — 레거시 MapWindow의 10-21 신기능(저장고/배치 호버 툴팁, 소유주 색상 계약)은 레거시 창 소속이라 UTK 지도에는 미포함. 원하면 다음 라운드에서 UTK 지도에 동일 기능 이식 가능.
- 메인씬(TopDownScene)에는 레거시 MapWindow가 씬 오브젝트로 존재 — 메인씬의 UTK 지도 통합은 별도 논의 필요(이번 범위 외).