# TDD Workflow — Unity 6000.4.10f1

## TDD 사이클 (RED → GREEN → REFACTOR)

```
1. RED   → 실패하는 테스트를 먼저 작성한다
2. GREEN → 테스트를 통과할 최소한의 코드를 구현한다
3. REFACTOR → 중복 제거, 구조 개선, 테스트는 계속 통과
```

## 명령어

```bash
# RED: 전체 테스트 실행 — 새 테스트가 실패하는지 확인
./run_tests.sh

# GREEN: 구현 후 테스트 통과 확인
./run_tests.sh

# REFACTOR: 리팩토링 후 회귀 테스트
./run_tests.sh
```

## 테스트 구조

```
Tests/
├── EditMode/                          # 단위 테스트 (씬 불필요)
│   ├── ProjectName.Tests.EditMode.asmdef
│   ├── SampleTests.cs                 # 기본 테스트 예제
│   └── PersistentManagerTests.cs      # Core 시스템 테스트
└── PlayMode/                          # 통합 테스트 (씬 필요)
    └── ProjectName.Tests.PlayMode.asmdef
```

## 테스트 작성 규칙

### EditMode Tests (씬 없이 실행)
- 순수 C# 로직 테스트
- MonoBehaviour 테스트는 `new GameObject().AddComponent<T>()` 방식으로 생성
- DontDestroyOnLoad, Instantiate 등은 EditMode에서도 동작
- 네임스페이스: `ProjectName.Tests.EditMode`
- asmdef에서 `ProjectName.Core` assembly 참조 필요

### PlayMode Tests (씬 필요)
- 통합 시나리오, 입력 테스트
- `[UnityTest]` + `yield return` 사용 가능
- 네임스페이스: `ProjectName.Tests.PlayMode`

### 네이밍 컨벤션
```
public class [SystemName]Tests
{
    [Test]
    public void [SystemName]_[Scenario]_[ExpectedResult]()
    {
        // Arrange
        // Act
        // Assert
    }
}
```

예시:
```csharp
public class PlayerMovementTests
{
    [Test]
    public void PlayerMovement_MoveRight_PositionIncreases()
    {
        // Arrange
        var player = new GameObject().AddComponent<PlayerMovement>();
        player.Speed = 10f;

        // Act
        player.Move(Vector3.right);

        // Assert
        Assert.Greater(player.transform.position.x, 0);
    }
}
```

## 새 시스템 개발 TDD 예시

### Step 1: RED — 테스트 먼저
```csharp
// Tests/EditMode/PlayerMovementTests.cs
[Test]
public void PlayerMovement_DefaultSpeed_IsFive()
{
    var player = new GameObject().AddComponent<PlayerMovement>();
    Assert.AreEqual(5f, player.Speed);
}
```
→ `./run_tests.sh` → ❌ 컴파일 에러 (PlayerMovement 없음)

### Step 2: GREEN — 최소 구현
```csharp
// Assets/Scripts/Systems/PlayerMovement.cs
public class PlayerMovement : MonoBehaviour
{
    public float Speed = 5f;
}
```
→ `./run_tests.sh` → ✅ 통과

### Step 3: REFACTOR
- 중복 제거 (Setup 메서드 추출)
- 적절한 접근 제한자 (SerializeField, public)
- 네임스페이스 추가

## 주의사항

- **절대** 테스트 없이 구현 먼저 하지 않음
- 테스트가 실패하는 것을 먼저 확인 (RED 검증)
- 리팩토링 후에도 테스트가 통과하는지 반드시 확인
- Unity Editor가 켜져 있으면 `run_tests.sh`가 실패함 → 에디터 종료 후 실행
- `.asmdef` 파일을 추가/변경하면 Unity 재컴파일이 필요하므로 첫 실행이 오래 걸림

## CI 연동

GitHub Actions에서 자동 실행:
`.github/workflows/test.yml` 참조