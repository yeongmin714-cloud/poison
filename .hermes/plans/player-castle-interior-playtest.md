# 플레이어 성 내부 플레이 테스트 계획

- 공용 `Assets/Scenes/IndoorScene.unity`는 Additive 런타임 진입 씬이므로 씬 YAML과 기존 씬 오브젝트는 변경하지 않는다. 에디터 메뉴에서 `EditorSceneManager.NewScene`/`AssetDatabase` API로 별도 `Assets/Scenes/TestScenes/Test_PlayerCastleInterior.unity`를 생성한다.
- 별도 런타임 부트스트랩은 활성 씬 이름이 `Test_PlayerCastleInterior`인 경우에만 플레이어 성 빌더를 호출하고, 같은 시점에 HQ `IndoorMaterialFactory.ApplyToRoom`(실측 크기) 적용, 테스트 전용 플레이어/카메라 생성한다. 일반 additive IndoorScene 로드에는 개입하지 않는다.
- 테스트 플레이어는 Player tag 기본 캡슐 + CharacterController 및 제한된 WASD 이동을 쓰고, 전용 쿼터뷰 카메라는 플레이어를 추적한다. 별도 테스트 씬만 자산으로 저장하여 런타임 프로덕션 씬에 영향이 없게 한다.
- 기존 `Tools/Indoor/미리보기/플레이어 성 (Castle-Player)` 진입을 테스트 씬으로 안내하고, ROADMAP/QAPROGRESS에 변경·정적검증·에디터 Play 미확인 상태를 기록한다. Unity 편집기/컴파일 상태를 확인하고, YAML scene ID를 직접 편집하지 않는다.
- 검증은 brace balance, 참조/경로, diff check, `compile_test.sh` 또는 사용 가능한 Unity 컴파일 로그로 수행한다. Unity 편집기 Play 검증은 사용자가 에디터에서 진행하도록 남은 항목을 명시한다.

작성: 2026-09-28 (구현 전 계획)
