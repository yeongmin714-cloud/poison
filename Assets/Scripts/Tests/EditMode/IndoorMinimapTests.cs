// [146차 P2] 실내 미니맵 계약 테스트 — P2 A안(RT 캡처) 소스계약 + MapToUv 수치 어서션.
// 목적: IndoorMinimapBaker 신설 + MinimapUTK 실내 브랜치 + 훅 3곳 배선이 계획 계약을
//       소스 레벨로 유지하는지 단언한다(런타임 렌더/Play 판정은 QA 서브 몫 — Unity 실행 금지 계약).
// 패턴 소스: WorldMapFigma124Tests(ReadSource/소스계약) + CaveInteriorSceneTests(선언 계약)
//       + Village5DesignCatalogTests 145차 P3(Random 0건 — 주석 언급 허용 계약 진화).
using System.IO;
using NUnit.Framework;
using UnityEngine;
using ProjectName.UI.Toolkit;

namespace ProjectName.Tests.EditMode
{
    [TestFixture]
    public class IndoorMinimapTests
    {
        private const string BakerPath = "Assets/Scripts/UI/Toolkit/IndoorMinimapBaker.cs";
        private const string MinimapPath = "Assets/Scripts/UI/Toolkit/MinimapUTK.cs";
        private const string TransitionPath = "Assets/Scripts/UI/IndoorSceneTransition.cs";
        private const string CaveDirectorPath = "Assets/Scripts/Systems/CaveLevelDirector.cs";
        private const string CaveTestSetupPath = "Assets/Scripts/Systems/CaveInteriorTestSetup.cs";

        private static string ReadSource(string path)
        {
            var full = Path.Combine(Application.dataPath, "..", path);
            Assert.IsTrue(File.Exists(full), $"소스 파일 없음: {path}");
            return File.ReadAllText(full);
        }

        // ── 1. MapToUv 순수 함수 수치 어서션 (엔진 상태 비의존 — EditMode 직접 실행) ──
        // 기준 Bounds: min=(5,−2,10), max=(15,2,30) — size=(10,4,20)

        private static Bounds TestBounds()
        {
            return new Bounds(new Vector3(10f, 0f, 20f), new Vector3(10f, 4f, 20f));
        }

        [Test]
        public void MapToUv_SouthWestCorner_IsZeroZero()
        {
            Vector2 uv = IndoorMinimapBaker.MapToUv(new Vector3(5f, 0f, 10f), TestBounds());
            Assert.That(uv.x, Is.EqualTo(0f).Within(1e-4), "u=(x−min.x)/size.x — 서단(min.x) → 0");
            Assert.That(uv.y, Is.EqualTo(0f).Within(1e-4), "v=(z−min.z)/size.z — 남단(min.z) → 0");
        }

        [Test]
        public void MapToUv_NorthEastCorner_IsOneOne()
        {
            Vector2 uv = IndoorMinimapBaker.MapToUv(new Vector3(15f, 0f, 30f), TestBounds());
            Assert.That(uv.x, Is.EqualTo(1f).Within(1e-4), "동단(max.x) → u=1");
            Assert.That(uv.y, Is.EqualTo(1f).Within(1e-4), "북단(max.z) → v=1 (캡처 카메라 Euler(90,0,0) — 화면 위=+z)");
        }

        [Test]
        public void MapToUv_Center_IsHalfHalf()
        {
            Vector2 uv = IndoorMinimapBaker.MapToUv(new Vector3(10f, 1f, 20f), TestBounds());
            Assert.That(uv.x, Is.EqualTo(0.5f).Within(1e-4), "중심 x → u=0.5");
            Assert.That(uv.y, Is.EqualTo(0.5f).Within(1e-4), "중심 z → v=0.5");
        }

        [Test]
        public void MapToUv_AxisSemantics_XGrowsU_ZGrowsV()
        {
            Vector2 east = IndoorMinimapBaker.MapToUv(new Vector3(7.5f, 0f, 20f), TestBounds());
            Assert.That(east.x, Is.EqualTo(0.25f).Within(1e-4), "+x(동) 방향 u 증가 계약");
            Vector2 north = IndoorMinimapBaker.MapToUv(new Vector3(10f, 0f, 15f), TestBounds());
            Assert.That(north.y, Is.EqualTo(0.25f).Within(1e-4), "+z(북) 방향 v 증가 계약 — MinimapRouteOverlay py=half−dz·k 규약 정합");
        }

        [Test]
        public void MapToUv_OutOfRange_ClampedTo01()
        {
            Vector2 westOut = IndoorMinimapBaker.MapToUv(new Vector3(0f, 0f, 20f), TestBounds());
            Assert.That(westOut.x, Is.EqualTo(0f).Within(1e-4), "min.x 밖 → Clamp01(u)=0");
            Vector2 eastOut = IndoorMinimapBaker.MapToUv(new Vector3(20f, 0f, 20f), TestBounds());
            Assert.That(eastOut.x, Is.EqualTo(1f).Within(1e-4), "max.x 밖 → Clamp01(u)=1");
            Vector2 southOut = IndoorMinimapBaker.MapToUv(new Vector3(10f, 0f, 5f), TestBounds());
            Assert.That(southOut.y, Is.EqualTo(0f).Within(1e-4), "min.z 밖 → Clamp01(v)=0");
            Vector2 northOut = IndoorMinimapBaker.MapToUv(new Vector3(10f, 0f, 45f), TestBounds());
            Assert.That(northOut.y, Is.EqualTo(1f).Within(1e-4), "max.z 밖 → Clamp01(v)=1");
        }

        [Test]
        public void MapToUv_DegenerateBounds_NoNaN()
        {
            var degenerate = default(Bounds);   // size 0 — 0 나눔 가드 계약
            Vector2 uv = IndoorMinimapBaker.MapToUv(new Vector3(3f, 0f, 7f), degenerate);
            Assert.That(float.IsNaN(uv.x) || float.IsNaN(uv.y), Is.False, "퇴화 Bounds에서 NaN 금지(0 나눔 가드)");
            Assert.That(uv.x, Is.InRange(0f, 1f), "u는 항상 [0,1]");
            Assert.That(uv.y, Is.InRange(0f, 1f), "v는 항상 [0,1]");
        }

        // ── 2. IndoorMinimapBaker 소스계약 — RT 캡처 사양/수명/무음 ──

        [Test]
        public void Baker_PublicApiSurface_Contract()
        {
            string src = ReadSource(BakerPath);
            StringAssert.Contains("public static Texture2D LatestTexture => _latest;", src);
            StringAssert.Contains("public static Bounds LatestBounds => _latestBounds;", src);
            StringAssert.Contains("public static bool HasCapture => _latest != null;", src);
            StringAssert.Contains("public static void CaptureFor(GameObject room)", src);
            StringAssert.Contains("public static Vector2 MapToUv(Vector3 worldPos, Bounds b)", src);
        }

        [Test]
        public void Baker_CaptureSpec_OrthoSquareRt512()
        {
            string src = ReadSource(BakerPath);
            // 512² / 24bit depth / ARGB32 / 진회색 0.05 배경 — P2 A안 사양 진실치
            StringAssert.Contains("RenderTexture.GetTemporary(CaptureSize, CaptureSize, 24, RenderTextureFormat.ARGB32)", src);
            StringAssert.Contains("const int CaptureSize = 512;", src);
            StringAssert.Contains("new Color(0.05f, 0.05f, 0.05f, 1f)", src);
            // 임시 직교 카메라 — 태그 없음+중앙 상공 Euler(90,0,0)+여유 프레이밍
            StringAssert.Contains("Quaternion.Euler(90f, 0f, 0f)", src);
            StringAssert.Contains("Mathf.Max(bounds.extents.x, bounds.extents.z) * FramePadding", src);
            StringAssert.Contains("bounds.max.y + CaptureHeight", src);
            StringAssert.DoesNotContain("\"MainCamera\"", src, "임시 카메라에 MainCamera 태그 부여 금지(태그 계약 오염 방지)");
        }

        [Test]
        public void Baker_Lifecycle_LeakGuardAndSilentFailure()
        {
            string src = ReadSource(BakerPath);
            // 이전 캡처 Object.Destroy(누수 방지) + 성공 시에만 커밋(실패 시 기존 상태 보존)
            StringAssert.Contains("Object.Destroy(_latest)", src);
            StringAssert.Contains("if (_latest != null) Object.Destroy(_latest);", src);
            // 임시 프로브 즉시 파괴(프로브 소유권 원칙) + RT 즉시 반환
            StringAssert.Contains("Object.DestroyImmediate(probeGO)", src);
            StringAssert.Contains("RenderTexture.ReleaseTemporary(rt)", src);
            // 앰비언트 저장→캡처→정확 복원(finally) + Flat 강제
            StringAssert.Contains("RenderSettings.ambientLight = prevAmbientLight;", src);
            StringAssert.Contains("RenderSettings.ambientMode = prevAmbientMode;", src);
            StringAssert.Contains("AmbientMode.Flat", src);
            // 전체 try-catch — 실패 시 로그 1행+무음(크래시 금지 계약), 로그 태그 계약
            StringAssert.Contains("catch (System.Exception e)", src);
            StringAssert.Contains("[146차 P2]", src);
        }

        [Test]
        public void Baker_NoRandomApi_DeterministicCapture()
        {
            string src = ReadSource(BakerPath);
            Assert.That(src.Contains("UnityEngine.Random.Range"), Is.False,
                "Random API 사용 0건 — 캡처 결정론 계약(주석 언급은 허용, 145차 P3 계약 진화).");
            Assert.That(src.Contains("new System.Random"), Is.False, "System.Random 인스턴스화 0건 — 결정론 계약.");
        }

        // ── 3. MinimapUTK 실내 브랜치 계약 — 분기 추가만, 월드 경로 본체 무변경 ──

        [Test]
        public void MinimapUTK_IndoorBranch_Wired()
        {
            string src = ReadSource(MinimapPath);
            // 모드 판정 단일 소스 — IsIndoor 직접 참조 + 테스트씬 경유(IndoorActive+캡처 존재)
            StringAssert.Contains("private static bool IsIndoorMapActive()", src);
            StringAssert.Contains("ProjectName.UI.IndoorSceneTransition.IsIndoor", src);
            StringAssert.Contains("(UITransitionState.IndoorActive && IndoorMinimapBaker.HasCapture)", src);
            // 실내 갱신 경로 + 월드 원복 경로 + 실내 블립 분기
            StringAssert.Contains("private void UpdateIndoorMap()", src);
            StringAssert.Contains("private void RestoreWorldMapIfIndoorApplied()", src);
            StringAssert.Contains("private void RefreshUnitBlipsIndoor()", src);
            StringAssert.Contains("IndoorMinimapBaker.MapToUv(_playerTransform.position, bounds)", src);
        }

        [Test]
        public void MinimapUTK_UpdateMapCrop_BodyUnchanged_SingleSource()
        {
            string src = ReadSource(MinimapPath);
            // 단일소스 계약 — UpdateMapCrop 본체(WORLD_SIZE UV 산식) 무변경 진실치 2행
            StringAssert.Contains("float span = Mathf.Clamp01((LocalRadius * 2f) / TerrainSplatBaker.WORLD_SIZE);", src);
            StringAssert.Contains("_mapCanvas.style.top = -(1f - (v0 + span)) * imageSide;", src);
        }

        [Test]
        public void MinimapUTK_RouteHook_And_FigmaDiameter_Preserved()
        {
            string src = ReadSource(MinimapPath);
            // 기존 계약 파손 금지 — Figma125_38 훅 배선(2분기 모두 동일 시그니처 유지)
            StringAssert.Contains("MinimapRouteOverlay.OnMinimapRefresh(_mapFrame,", src);
            StringAssert.Contains("LocalRadius, Diameter - 4f);", src);
            // Figma 264×264 상수 불변 계약
            StringAssert.Contains("const float Diameter", src);
            StringAssert.Contains("264f", src);
        }

        [Test]
        public void MinimapUTK_NoRandomApi()
        {
            string src = ReadSource(MinimapPath);
            Assert.That(src.Contains("UnityEngine.Random.Range"), Is.False,
                "Random API 사용 0건 — 결정론 계약(주석 언급은 허용, 145차 P3 계약 진화).");
            Assert.That(src.Contains("new System.Random"), Is.False, "System.Random 인스턴스화 0건 — 결정론 계약.");
        }

        // ── 4. 훅 3곳 계약 — 호출부 존재 + Systems→UI 직접 참조 금지(리플렉션 경유) ──

        [Test]
        public void Hook1_IndoorSceneTransition_CapturesAfterRoomBuild()
        {
            string src = ReadSource(TransitionPath);
            // 동일 UI 어셈블리 직접 호출 — 룸 빌드 완료 지점(빌더 switch 직후)
            StringAssert.Contains("ProjectName.UI.Toolkit.IndoorMinimapBaker.CaptureFor(hqRoom);", src);
            // 훅이 try 블록 내(빌더 예외 시 자연 스킵) — _activeRoom 추적 직후 배치 계약
            int hook = src.IndexOf("CaptureFor(hqRoom);");
            int activeRoom = src.IndexOf("_activeRoom = hqRoom;");
            Assert.Greater(hook, activeRoom, "훅은 룸 추적 할당 이후에 위치한다(룸 빌드 완료 지점 계약).");
            int catchAfter = src.IndexOf("catch (System.Exception e)", activeRoom);
            Assert.Less(hook, catchAfter, "훅은 같은 try 블록 내에 위치한다(빌더 예외 시 무음 스킵 계약).");
        }

        [Test]
        public void Hook2_CaveLevelDirector_CapturesBeforeReturn_ViaReflection()
        {
            string src = ReadSource(CaveDirectorPath);
            // Systems→UI 직접 참조 금지 계약 — FindCrossAssemblyType 리플렉션 경유(선례 준용)
            StringAssert.Contains("CaptureMinimapForRoom(room);", src);
            StringAssert.Contains("\"ProjectName.UI.Toolkit.IndoorMinimapBaker\"", src);
            StringAssert.Contains("FindCrossAssemblyType", src);
            Assert.That(src.Contains("using ProjectName.UI"), Is.False,
                "Systems→UI 직접 참조 금지 — using 지시문 없이 리플렉션만 허용(어셈블리 경계 계약).");
        }

        [Test]
        public void Hook3_CaveInteriorTestSetup_CapturesAfterRoomBuild_ViaReflection()
        {
            string src = ReadSource(CaveTestSetupPath);
            // 룸 빌드 완료 지점(BuildCaveInterior 직후, 플레이어 스폰 전) 훅 + 리플렉션 경유
            StringAssert.Contains("CaptureMinimapForRoom(_caveRoom);", src);
            StringAssert.Contains("\"ProjectName.UI.Toolkit.IndoorMinimapBaker\"", src);
            Assert.That(src.Contains("using ProjectName.UI"), Is.False,
                "Systems→UI 직접 참조 금지 — using 지시문 없이 리플렉션만 허용(어셈블리 경계 계약).");
            int hook = src.IndexOf("CaptureMinimapForRoom(_caveRoom);");
            int spawn = src.IndexOf("EnsurePlayer();");
            Assert.Greater(hook, 0, "훅 호출이 존재한다.");
            Assert.Less(hook, spawn, "훅은 플레이어 스폰 이전에 위치한다(캡처 오브젝트 오염 방지 계약).");
        }
    }
}
