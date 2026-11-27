using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [156차 P2] 동굴 낙하 봉합 계약테스트 — 입구 문 상시 봉합 + CaveFallGuard 안전망.
    /// EditMode 원칙(Cave146P1ContractTests 소스계약 + StatusEquipmentFigma84Tests 리플렉션 Update 호출 선례):
    /// ①문 — 소스 어서션. [156차 명세 변경] "기본 열림(진입 흐름 방해 금지)" 계약 폐기 → 상시 봉합 갱신
    ///    (사유: 156차 사용자 지시 동굴 낙하 금지 — 열림 시작 블로커 무효가 입구 개방 통과 → 실내 낙하의 원인).
    /// ②가드 — 행위 어서션(실제 Play 불필요): y < −3m → CaveLevelDirector.TeleportPlayerToFloorSpawn
    ///    경유 현재 층 스폰 복귀 / y ≥ −3m → 무영향.
    /// </summary>
    public sealed class Cave156FallSafetyContractTests
    {
        private const string DoorPath = "Assets/Scripts/Systems/CaveEntranceDoor.cs";
        private const string GuardPath = "Assets/Scripts/Systems/CaveFallGuard.cs";

        private GameObject _player;
        private GameObject _room;

        // ── 1. 입구 문 상시 봉합 — 소스계약 ──

        [Test]
        public void EntranceDoor_AlwaysSealed_ToggleAndOpenDefaultRemoved()
        {
            string src = ReadSource(DoorPath);

            Assert.That(src, Does.Contain("_blocker.enabled = true;"),
                "[156차 P2-3] 블로커 상시 활성 강제 — 기본 열림(비활성 시작) 계약 폐기(사유: 동굴 낙하 금지 사용자 지시).");
            Assert.That(src.Contains("_open"), Is.False,
                "[156차 P2-3] 개폐 상태 변수 잔존 금지 — 토글 경로 완전 제거(블로커 비활성화 경로 0개 계약).");
            Assert.That(src.Contains("KeyCode"), Is.False,
                "[156차 P2-3] 문 E 토글 상호작용 폐기 — 귀환은 기존 상승 포탈(E키, CaveLevelPortal) 경로로 불변.");
            Assert.That(src, Does.Contain("transform.Find(\"CaveDoorBlocker\")"),
                "블로커 이름 계약 유지 — CaveDoorBlocker 1개(콜라이더 단일 소스).");
            Assert.That(src, Does.Contain("if (_panelL != null)"),
                "[156차] 패널 L null 가드 — 빌더 패널 생성 폐기(156차)에 대응하는 NRE 방어.");
            Assert.That(src, Does.Contain("if (_panelR != null)"),
                "[156차] 패널 R null 가드 — 빌더 패널 생성 폐기(156차)에 대응하는 NRE 방어.");
            Assert.That(CountOccurrences(src, "ApplySealed();"), Is.EqualTo(2),
                "Awake+Update 이중 적용 — 외부 비활성화 즉시 복원(상시 봉합 재단성).");
        }

        // ── 2. CaveFallGuard 안전망 — 소스계약 ──

        [Test]
        public void FallGuard_ReusesDirectorTeleport_WithFixedThreshold()
        {
            string src = ReadSource(GuardPath);

            Assert.That(src, Does.Contain("private const float FallThresholdY = -3f;"),
                "[156차 P2-4] 낙하 판정선 −3m 고정 — 실내 바닥(y≈0) 대비 3m 이하 강하 = 지면 이탈.");
            Assert.That(src, Does.Contain("CaveLevelDirector.TeleportPlayerToFloorSpawn();"),
                "[156차 P2-4] 텔레포트는 CaveLevelDirector 기존 경로 재사용(층 스폰 단일 소스 — 신규 좌표 계산 금지).");
            Assert.That(src, Does.Contain("[CaveFallGuard] 낙하 감지 — 스폰 복귀"),
                "[156차 P2-4] 복귀 로그 1행 계약(낙하 재현 프로브 판독 키).");
            Assert.That(src, Does.Contain("FindGameObjectWithTag(\"Player\")"),
                "플레이어 검색은 기존 프로젝트 패턴(태그 \"Player\") — 매 프레임 Find 금지(Awake 1회 캐시+스로틀).");
            Assert.That(src.Contains("FindObjectOfType"), Is.False,
                "FindObjectOfType(Unity 6000 폐기 예정 API) 사용 금지 — FindFirstObjectByType 폴백 계약.");
        }

        // ── 3. CaveFallGuard 안전망 — 행위계약(y<−3 → 층 스폰 텔레포트, EditMode 무Play) ──

        [SetUp]
        public void SetUp()
        {
            ClearTaggedPlayers();   // 선행 테스트 잔존 "Player" 태그 오염 제거 — 캐시 대상 단일화
            _player = new GameObject("Player") { tag = "Player" };
            _room = new GameObject("CaveFallGuardTestRoom");
        }

        [TearDown]
        public void TearDown()
        {
            // DestroyImmediate — EditMode 필수. 룸 파괴 = 가드 자동 해제(룸 부착 계약과 동일 수명).
            UnityEngine.Object.DestroyImmediate(_room);
            UnityEngine.Object.DestroyImmediate(_player);
        }

        [Test]
        public void FallGuard_FallBelowThreshold_TeleportsToCurrentFloorSpawn()
        {
            _player.transform.position = new Vector3(5f, -10f, 5f);   // 낙하 상태(y < −3m)
            var guard = _room.AddComponent<CaveFallGuard>();
            InvokePrivate(guard, "Awake");   // 1회 캐시
            Assert.That(_player.transform.position.y, Is.EqualTo(-10f),
                "사전 조건 — Awake는 위치를 변경하지 않는다(감지는 Update 경로).");

            InvokePrivate(guard, "Update");

            // 텔레포트 목적지는 Director가 사용한 동일 테이블 — tier는 156차 기하 무관이나 1 고정으로 비교.
            Vector3 expected = CaveInteriorBuilder.GetFloorSpawnPosition(CaveLevelDirector.CurrentFloor, 1);
            AssertPosition(_player.transform.position, expected, "y<−3 감지 → 현재 층 스폰 복귀");
            Assert.That(_player.transform.position.y, Is.GreaterThanOrEqualTo(0f),
                "복귀 지점은 바닥 위 스폰(y=1.5) — 재낙하 루프 아님.");
        }

        [Test]
        public void FallGuard_AboveThreshold_NoTeleport()
        {
            _player.transform.position = new Vector3(3f, 2f, 4f);   // 정상 — 바닥 위(y ≥ −3m)
            var guard = _room.AddComponent<CaveFallGuard>();
            InvokePrivate(guard, "Awake");

            InvokePrivate(guard, "Update");

            AssertPosition(_player.transform.position, new Vector3(3f, 2f, 4f),
                "y≥−3m → 무영향(실내 게이트·정상 이동과 무충돌).");
        }

        // ── 공용 헬퍼 ──

        private static void InvokePrivate(object instance, string method)
        {
            var m = instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(m, Is.Not.Null, $"CaveFallGuard.{method} 존재 — EditMode 리플렉션 진입점(선례: StatusEquipmentFigma84Tests).");
            m.Invoke(instance, null);
        }

        private static void ClearTaggedPlayers()
        {
            foreach (var go in GameObject.FindGameObjectsWithTag("Player"))
                UnityEngine.Object.DestroyImmediate(go);
        }

        private static void AssertPosition(Vector3 actual, Vector3 expected, string label)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(1e-4f), $"{label} — x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(1e-4f), $"{label} — y");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(1e-4f), $"{label} — z");
        }

        private static string ReadSource(string path)
        {
            Assert.That(System.IO.File.Exists(path), Is.True, $"소스 파일 존재: {path}");
            return System.IO.File.ReadAllText(path);
        }

        private static int CountOccurrences(string text, string token)
        {
            int count = 0, idx = 0;
            while ((idx = text.IndexOf(token, idx, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += token.Length;
            }
            return count;
        }
    }
}
