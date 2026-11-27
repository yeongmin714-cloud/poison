using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using ProjectName.Systems;
using UnityEditor;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [154차 P0] 굉이질(Tilled) 오목 "검정 렌더" 격리 프로브 — 원인 판정 증거 수집(H1~H4).
    ///
    /// 배경(검증된 사실): FarmPlot Tilled에서 플롯이 평면 흑색 사각형으로 렌더됨(인게임 영상 24s 실측 —
    ///   흙둑 융기/경준선 텍스처 안 보임). Planted(흙타일)는 갈색 정상 → 검정은 TillHole 활성 시에만 발생.
    ///
    /// [154차 소스 실측 신규 확인 — 프로브 설계에 반영]
    ///   1) FarmPlotVisuals.RefreshVisuals 656행: Tilled에서 흙타일을 SetActive(false) — 프로덕션 Tilled = "구멍 단독 표시".
    ///      → 태스크 문구 "흙타일+구멍"의 동시 활성 상태는 인게임에 존재하지 않음(콤보6에서만 리터럴 재현).
    ///   2) TestTerritoryCombatSetup.SetupFarm 2407행: 플롯 원점 = SurfaceY(x,z)+0.1 — 구멍 메시 전체가 지형면 위
    ///      (최저 +0.02m)에 노출 → 지면과 동일 평면 z-파이팅/매몰(H1)은 인게임 배치에서 성립하지 않음.
    ///      프로브도 동일 계약으로 배치(지면 상단 y=0, 플롯 원점 y=+0.1).
    ///   3) TestTerritoryCombatSetup.SetupLight 1683행: Directional intensity=1.2, Euler(50,-30,0), shadows 미설정(=None)
    ///      → 인게임 태양은 그림자가 없음. 프로브도 동일하게 shadows=None(콤보4의 receiveShadows 변인은
    ///      인게임 동일 조건에서의 H2 재확인용 — 예상 결과는 콤보2와 동일).
    ///   4) BuildTillHoleMesh: RecalculateTangents 호출 부재 — 구멍 메시에 탄젠트 없음. _NORMALMAP 활성 시 TBN 퇴화
    ///      → 법선 파괴 → 직사광 기여 소실 → 주변광만 반영 = 검정. 흙타일은 빌트인 Cube(탄젠트 보유)라 정상 갈색
    ///      → "흙타일 정상/구멍 검정" 증상과 정확히 부합하는 H4 변형(최유력 후보). 콤보1 진단 로그로 tangents 부재를 명시 기록.
    ///
    /// 콤보(6컷 — 각 컷 독립 플롯을 별도 위치에 배치, 프로덕션 코드 경로만 사용 — 복제 구현 금지, private는 리플렉션):
    ///   1 현행            — 프로덕션 Tilled 상태 그대로(BuildSoil+Hole+Crop → Tilled → RefreshVisuals) — 재현 슬롯
    ///                       (실제 결과: 흙타일 자동 비활성 + 구멍 활성 — 656행 계약)
    ///   2 구멍만          — 흙타일/작물 GO 미생성, 구멍 단독 — 콤보1 대비 계층 요인 배제+결정론 재확인
    ///   3 흙타일만        — 구멍 비활성 + 흙타일 활성 — 베이스라인(갈색 기준)
    ///   4 receiveShadows=false — 콤보1 상태 + 구멍 렌더러 그림자 수신 차단 — H2 판정
    ///   5 NORMALMAP해제   — 콤보1 상태 + 구멍/흙타일 머티리얼 _NORMALMAP 해제 — H4 판정(캡처 후 키워드 복원)
    ///   6 holeRoot+0.002  — 흙타일+구멍 리터럴 동시 활성 + 구멍 루트 +0.002 상승(계획 수리 미리보기 —
    ///                       흙타일 상면과 동일 평면이던 가장자리 링의 z-파이팅 해소 모습)
    ///
    /// 판정 지표: 콤보별 캡처의 "구멍 중심 영역(프레임 중앙 20% 박스) 평균 밝기"(0~255) —
    ///   avgRGB ≲ 80 = 검정 계열(원인 재현), ≳ 120 = 갈색 정상. 콤보 1/2 ↔ 4/5 단일 변인 비교로 원인 확정.
    ///   (콤보6는 흙타일이 구멍 내부를 가리므로 중심 지표는 흙타일 상면을 읽음 — 이미지로 육안 판독 슬롯)
    ///
    /// [154차 P2] 판독 보강 — 콤보당 앰비언트 2종 캡처. P0 실측(Flat 0.35): 전 콤보 avgRGB 15~17로 판별력 부족
    ///   (콤보3 흙타일 참조조차 15.3 — 조명 기여가 전반 억제되는 헤드리스 환경) → 판독용 Flat 1.0 흰색 컷(_Nb) 추가.
    ///   b컷 판정(콤보3b = 흙타일 알베도 참조 — 색공간/톤매핑 스케일 무관 자기보정):
    ///   ① 콤보1b > 콤보1 P0값 17.0의 등가 앰비언트 스케일(≈49) + 콤보3b의 약 35~55%(AO 프리멀티 0.40~0.62 × 재질 1.12)
    ///     = 갈색 회복 + AO 음영 정상. ② 콤보1b가 콤보2b와 동일 최저값 머물름 = 여전히 소실(조명 환경 추가 조사).
    ///   ③ 콤보1b ↔ 콤보5b(_NORMALMAP 해제) 격차 소실 = 탄젠트 수리로 TBN 문제 해소 확인.
    ///   0.35 컷은 P0 원인 판정 연속성 유지용(수리 전 로그와 직접 비교 가능).
    ///
    /// 실행(부모 몫 — 이 스크립트는 작성까지만):
    ///   Unity.exe -batchmode -projectPath C:/Unity/code
    ///     -executeMethod ProjectName.EditorTools.Farm154HoleIsolationProbe.RunAll -quit
    ///     -logFile C:/Unity/code/TestOutput/farm154_probe_run.log   (run_farm154_probe.sh 참조 — 153차 qa_153_play 선례와 동일 방식)
    /// 산출: TestOutput/farm154_isolation_1.png ~ _6.png(Flat 0.35 — 기존 규약)
    ///   + farm154_isolation_1b.png ~ _6b.png(판독용 Flat 1.0 — [154차 P2])
    ///   + "[154차 P0] combo=N name=... avgRGB=... avgRGBb=..." 로그(2종 평균 밝기).
    /// 패턴 준용: Farm153PlayProbe(RT+cam.Render+ReadPixels 캡처 — ScreenCapture 헤드리스 불안정 회피, 34차 선례).
    /// 소유권: 프로브 생성 환경(지면/라이트/카메라/RT)+플롯 계층+런타임 생성 메시·머티리얼 인스턴스는 프로브가 파괴.
    ///   파괴 금지: FarmPlotVisuals._holeMaterial 전역 캐시(키워드만 캡처 후 복원), 빌트인/임포트 에셋(Cube·Plane 메시, GLB 메시).
    ///   씬 상태 복원 불필요(일회성 배치 런 — RenderSettings ambient 변경 포함). UnityEngine.Object 명시 한정(CS0104 — using System 공존).
    /// </summary>
    public static class Farm154HoleIsolationProbe
    {
        private const int RTW = 1024;
        private const int RTH = 768;
        private const string Tag = "[154차 P0]";
        private const string HoleMeshName = "FarmSoil_TillHole";   // BuildTillHoleMesh가 명명 — 런타임 소유 메시 식별용
        private const string HoleNodeName = "TillHole";            // BuildHoleVisual 루트 이름(직계 자식)
        private const string SoilNodeName = "Soil";                // BuildSoilVisual 타일 이름(직계 자식)
        private const float PlotOriginY = 0.1f;                    // 인게임 계약 SurfaceY(x,z)+0.1 — 지면(상단 y=0) 위 0.1m

        public static void RunAll()
        {
            Debug.Log($"{Tag} === 굉이질 오목 격리 프로브 시작 ===");

            // ── 공통 환경 — 판독용 최소 구성(씬 복원 불필요: 일회성 배치 런) ──
            // ambient: 인게임 Test_15는 Skybox 모드(m_AmbientMode 0) — 헤드리스 판독용 Flat 근사(값은 로그로 명시).
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.35f, 0.35f, 1f);
            Debug.Log($"{Tag} env: ambient=Flat 0.35(인게임 Test_15는 Skybox 모드 — 판독용 근사)");

            var groundGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            groundGo.name = "Farm154ProbeGround";
            groundGo.transform.position = Vector3.zero;                 // 지형면 상단 = y 0(플롯 원점은 +0.1 — 인게임 계약)
            groundGo.transform.localScale = new Vector3(6f, 1f, 6f);    // Plane 프리미티브 10×10 → 60×60m
            var groundCol = groundGo.GetComponent<Collider>();
            if (groundCol != null) UnityEngine.Object.DestroyImmediate(groundCol);   // 시각 전용(프로브 소유)
            var groundMr = groundGo.GetComponent<MeshRenderer>();
            var groundMat = new Material(UrpLitShader());               // FarmPlotVisuals.UrpLitShader와 동일 폴백(2줄 유틸)
            groundMat.color = new Color(0.36f, 0.55f, 0.24f, 1f);       // 잔디(FarmPlotVisuals.GetSoilColor(Grass) 동일 톤)
            groundMr.sharedMaterial = groundMat;

            // 라이트 — Test_15 실측(TestTerritoryCombatSetup.SetupLight): 1.2 / Euler(50,-30,0) / shadows 미설정=None
            var lightGo = new GameObject("Farm154ProbeLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.None;                          // 인게임 동일(AddComponent 기본값 명시화 — H2 재확인 전제)
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Debug.Log($"{Tag} env: light intensity=1.2 rot=(50,-30,0) shadows=None — Test_15 실측(SetupLight) 동일");

            var camGo = new GameObject("Farm154ProbeCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.12f, 0.16f, 1f);
            cam.cullingMask = ~0;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            cam.fieldOfView = 40f;                                      // 153차 선례 — 게임 유사 탑다운 고도/FOV
            var rt = new RenderTexture(RTW, RTH, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            cam.targetTexture = rt;

            // ── 6콤보 — 각기 다른 x 위치(4m 간격), z=0, 플롯 원점 y=+0.1(인게임 계약) ──
            RunCombo(1, "현행_프로덕션Tilled", cam, -10f, fullBase: true, apply: null);
            RunCombo(2, "구멍만", cam, -6f, fullBase: false, apply: null);
            RunCombo(3, "흙타일만", cam, -2f, fullBase: true, apply: ShowSoilOnly);
            RunCombo(4, "receiveShadows=false", cam, 2f, fullBase: true, apply: p => SetHoleReceiveShadows(p, false));
            RunCombo(5, "NORMALMAP해제", cam, 6f, fullBase: true,
                apply: p => SetSoilNormalMapKeyword(p, false),
                postCapture: p => SetSoilNormalMapKeyword(p, true));    // 전역 캐시 머티리얼 오염 방지 — 캡처 직후 복원
            RunCombo(6, "holeRoot+0.002_수리미리보기", cam, 10f, fullBase: true, apply: PreviewLiftedHole);

            // ── 정리 — 프로브 소유 오브젝트 파괴(씬 복원 불필요) ──
            rt.Release();
            UnityEngine.Object.DestroyImmediate(camGo);
            UnityEngine.Object.DestroyImmediate(lightGo);
            UnityEngine.Object.DestroyImmediate(groundGo);
            UnityEngine.Object.DestroyImmediate(groundMat);             // 지면 머티리얼은 런타임 인스턴스 — 프로브 소유
            Debug.Log($"{Tag} DONE — 산출: TestOutput/farm154_isolation_1.png ~ _6.png + _1b ~ _6b(판독용 Flat1.0)");
        }

        /// <summary>콤보 1회 실행 — 플롯 구성(프로덕션 경로) → 변인 적용 → 탑다운 캡처 → 중심 평균 밝기 산출 → 소유 파괴.</summary>
        private static void RunCombo(int combo, string name, Camera cam, float x,
            bool fullBase, Action<FarmPlot> apply, Action<FarmPlot> postCapture = null)
        {
            GameObject plotGo = null;
            FarmPlot plot = null;   // finally 복원용 — 파괴 전 참조 확보(파괴된 컴포넌트 접근 방지)
            try
            {
                plotGo = new GameObject($"Farm154_Combo{combo}_Plot");
                plotGo.transform.position = new Vector3(x, PlotOriginY, 0f);   // 인게임 SurfaceY+0.1 계약 동일 배치
                plot = plotGo.AddComponent<FarmPlot>();

                if (fullBase)
                {
                    // 프로덕션 FarmPlot.Awake 재현(Grass 상태에서 빌드) + Tilled 전환 + RefreshVisuals(656행: 흙타일 숨김)
                    FarmPlotVisuals.BuildSoilVisual(plot);
                    FarmPlotVisuals.BuildHoleVisual(plot);
                    FarmPlotVisuals.BuildCropVisual(plot);
                    SetPhase(plot, FarmPlot.CropPhase.Tilled);
                    FarmPlotVisuals.RefreshVisuals(plot);
                }
                else
                {
                    // 구멍 단독 — 흙타일/작물 GO 자체를 만들지 않음(계층 요인 완전 배제)
                    SetPhase(plot, FarmPlot.CropPhase.Tilled);
                    FarmPlotVisuals.BuildHoleVisual(plot);
                    var hole = plot.transform.Find(HoleNodeName);
                    if (hole == null) throw new InvalidOperationException("TillHole 노드 부재");
                    hole.gameObject.SetActive(true);
                }

                LogComboStates(combo, "before", plot);
                if (apply != null) apply(plot);
                LogComboStates(combo, "after", plot);

                // 1회성 메시/머티리얼 진단 — 탄젠트 부재(H4 후보)와 정점색(H3) 상태를 로그로 고정
                if (combo == 1) LogHoleDiagnostics(plot);

                Snap(cam, new Vector3(x, 3.2f, 0.35f), new Vector3(x, PlotOriginY, 0f));   // 153차 선례 탑다운

                // [154차 P2] 판독 보강 — 콤보당 앰비언트 2종 캡처:
                //   ① Flat 0.35(기존 — 인게임 Skybox 근사, 원인 판정 연속성) → farm154_isolation_N.png
                //   ② Flat 1.0 흰색(판독용 — 앰비언트만으로도 갈색 알베도가 판독 가능) → farm154_isolation_Nb.png
                float avg = CaptureAndMeasure(cam, $"farm154_isolation_{combo}.png");
                RenderSettings.ambientLight = new Color(1f, 1f, 1f, 1f);                  // 판독용 흰 앰비언트
                float avgB = CaptureAndMeasure(cam, $"farm154_isolation_{combo}b.png");
                RenderSettings.ambientLight = new Color(0.35f, 0.35f, 0.35f, 1f);         // 다음 콤보 기준값 복원
                Debug.Log($"{Tag} combo={combo} name={name} avgRGB={avg.ToString("F1", CultureInfo.InvariantCulture)} " +
                    $"avgRGBb={avgB.ToString("F1", CultureInfo.InvariantCulture)}(Flat1.0)");
            }
            catch (Exception e)
            {
                Debug.LogError($"{Tag} combo={combo} 실패 — {e}");
            }
            finally
            {
                // 복원 우선 — 전역 캐시 머티리얼 키워드는 플롯 파괴 전에 되돌린다(이후 콤보 오염 방지)
                if (postCapture != null && plot != null)
                {
                    try { postCapture(plot); }
                    catch (Exception e) { Debug.LogError($"{Tag} combo={combo} 복원 실패 — {e}"); }
                }
                DestroyPlotOwned(plotGo);
            }
        }

        /// <summary>흙타일만 — RefreshVisuals의 Tilled 스왑(656행)을 수동 반전: 구멍 비활성 + 흙타일 활성.</summary>
        private static void ShowSoilOnly(FarmPlot plot)
        {
            var hole = plot.transform.Find(HoleNodeName);
            var soil = plot.transform.Find(SoilNodeName);
            if (hole != null) hole.gameObject.SetActive(false);
            if (soil == null) throw new InvalidOperationException("Soil 노드 부재");
            soil.gameObject.SetActive(true);
        }

        /// <summary>H2 변인 — 구멍 렌더러 그림자 수신 차단(흙타일은 프로덕션 계약상 비활성 상태).</summary>
        private static void SetHoleReceiveShadows(FarmPlot plot, bool value)
        {
            var hole = plot.transform.Find(HoleNodeName);
            var mr = hole != null ? hole.GetComponent<MeshRenderer>() : null;
            if (mr == null) throw new InvalidOperationException("TillHole MeshRenderer 부재");
            mr.receiveShadows = value;
        }

        /// <summary>H4 변인 — 구멍(+흙타일) 머티리얼 _NORMALMAP 키워드 토글. 구멍 머티리얼은 전역 캐시 — postCapture에서 복원 필수.</summary>
        private static void SetSoilNormalMapKeyword(FarmPlot plot, bool enable)
        {
            string[] nodes = { HoleNodeName, SoilNodeName };
            for (int i = 0; i < nodes.Length; i++)
            {
                var t = plot.transform.Find(nodes[i]);
                var mr = t != null ? t.GetComponent<MeshRenderer>() : null;
                var m = mr != null ? mr.sharedMaterial : null;
                if (m == null || !m.HasProperty("_BumpMap")) continue;
                if (enable) m.EnableKeyword("_NORMALMAP");
                else m.DisableKeyword("_NORMALMAP");
                Debug.Log($"{Tag} 변인: {nodes[i]} _NORMALMAP={(m.IsKeywordEnabled("_NORMALMAP") ? "ON" : "OFF")}");
            }
        }

        /// <summary>계획 수리 미리보기 — 흙타일+구멍 리터럴 동시 활성 + 구멍 루트 +0.002 상승(가장자리 링 y0 → 상면 위 2mm).</summary>
        private static void PreviewLiftedHole(FarmPlot plot)
        {
            var soil = plot.transform.Find(SoilNodeName);
            var hole = plot.transform.Find(HoleNodeName);
            if (soil == null || hole == null) throw new InvalidOperationException("Soil/TillHole 노드 부재");
            soil.gameObject.SetActive(true);                            // 리터럴 "흙타일+구멍" 동시 활성
            hole.gameObject.SetActive(true);
            hole.localPosition = new Vector3(0f, 0.002f, 0f);           // 계획 수리 — 동일 평면 해소
        }

        /// <summary>콤보 상태 로그 — soil/hole 활성, 구멍 로컬 y, receiveShadows, _NORMALMAP(ASCII 값 — 로그 파싱 안전).</summary>
        private static void LogComboStates(int combo, string when, FarmPlot plot)
        {
            var soil = plot.transform.Find(SoilNodeName);
            var hole = plot.transform.Find(HoleNodeName);
            bool soilOn = soil != null && soil.gameObject.activeSelf;
            bool holeOn = hole != null && hole.gameObject.activeSelf;
            string holeY = hole != null
                ? hole.localPosition.y.ToString("F3", CultureInfo.InvariantCulture)
                : "n/a";
            bool recv = true;
            bool nmap = false;
            if (hole != null)
            {
                var mr = hole.GetComponent<MeshRenderer>();
                if (mr != null) recv = mr.receiveShadows;
                var m = mr != null ? mr.sharedMaterial : null;
                if (m != null && m.HasProperty("_BumpMap")) nmap = m.IsKeywordEnabled("_NORMALMAP");
            }
            Debug.Log($"{Tag} states combo={combo} {when}: soil={(soilOn ? "active" : "inactive")} " +
                $"hole={(holeOn ? "active" : "inactive")} holeLocalY={holeY} receiveShadows={recv} _NORMALMAP={(nmap ? "ON" : "OFF")}");
        }

        /// <summary>1회성 진단 — 구멍 메시 탄젠트/정점색 보유 여부 + 머티리얼 배선(원인 판정 보조 증거).</summary>
        private static void LogHoleDiagnostics(FarmPlot plot)
        {
            var hole = plot.transform.Find(HoleNodeName);
            if (hole == null) return;
            var mf = hole.GetComponent<MeshFilter>();
            var mesh = mf != null ? mf.sharedMesh : null;
            if (mesh != null)
            {
                bool hasTangent = mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Tangent);
                bool hasColor = mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color);
                Debug.Log($"{Tag} 진단: TillHole 메시 verts={mesh.vertexCount} normals={mesh.normals.Length} " +
                    $"tangents={(hasTangent ? mesh.tangents.Length : 0)}(HasTangent={hasTangent}) " +
                    $"vertexColors={(hasColor ? "있음" : "없음")} — " +
                    "tangents=0 + _NORMALMAP 활성이면 TBN 퇴화(H4 최유력 — RecalculateTangents 부재)");
            }
            var mr = hole.GetComponent<MeshRenderer>();
            var m = mr != null ? mr.sharedMaterial : null;
            if (m != null)
            {
                bool bump = m.HasProperty("_BumpMap") && m.GetTexture("_BumpMap") != null;
                Debug.Log($"{Tag} 진단: 구멍 머티리얼 shader={m.shader.name} color={m.color} " +
                    $"mainTex={(m.mainTexture != null)} bumpMap={bump} NORMALMAP={(m.IsKeywordEnabled("_NORMALMAP") ? "ON" : "OFF")} " +
                    "— URP/Lit은 정점색 무시(H3 — AO 계약 무효 확인용)");
            }
        }

        /// <summary>카메라 배치+주시(153차 선례).</summary>
        private static void Snap(Camera cam, Vector3 pos, Vector3 lookAt)
        {
            cam.transform.position = pos;
            cam.transform.LookAt(lookAt);
        }

        /// <summary>프로젝트 검증 캡처 패턴(153차/34차 선례) — RT 렌더+ReadPixels+PNG 저장. 반환: 구멍 중심 영역 평균 밝기(0~255).</summary>
        private static float CaptureAndMeasure(Camera cam, string fileName)
        {
            cam.Render();
            var rt = cam.targetTexture;
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            float avg = ComputeCenterAvgRGB(tex);
            var png = tex.EncodeToPNG();
            string dir = Path.Combine(Application.dataPath, "..", "TestOutput");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, fileName);
            File.WriteAllBytes(path, png);
            Debug.Log($"{Tag} 캡처 저장 — {fileName} ({png.Length / 1024}KB)");
            UnityEngine.Object.DestroyImmediate(tex);
            return avg;
        }

        /// <summary>구멍 중심 영역 평균 밝기 — 프레임 중앙 20% 박스 픽셀 RGB 산술 평균(0~255). 카메라 주시점이 플롯 중심이므로 박스는 오목 중심부에 위치.</summary>
        private static float ComputeCenterAvgRGB(Texture2D tex)
        {
            int w = tex.width;
            int h = tex.height;
            int rw = Mathf.Max(1, w / 5);
            int rh = Mathf.Max(1, h / 5);
            int x0 = (w - rw) / 2;
            int y0 = (h - rh) / 2;
            var px = tex.GetPixels(x0, y0, rw, rh);
            double sum = 0.0;
            for (int i = 0; i < px.Length; i++)
                sum += (px[i].r + px[i].g + px[i].b) / 3.0;
            return (float)(sum / Mathf.Max(1, px.Length) * 255.0);
        }

        /// <summary>FarmPlot._phase private 필드 세팅(153차 선례 동일).</summary>
        private static void SetPhase(FarmPlot plot, FarmPlot.CropPhase phase)
        {
            var f = typeof(FarmPlot).GetField("_phase", BindingFlags.NonPublic | BindingFlags.Instance);
            if (f == null) throw new MissingFieldException(nameof(FarmPlot), "_phase");
            f.SetValue(plot, phase);
        }

        /// <summary>URP/Lit 셰이더 확보 — FarmPlotVisuals.UrpLitShader와 동일 폴백(2줄 유틸 — 시각 로직 복제 아님).</summary>
        private static Shader UrpLitShader()
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            return sh != null ? sh : Shader.Find("Standard");
        }

        /// <summary>프로브 소유 파괴 — 플롯 계층+런타임 생성 메시/머티리얼 인스턴스. 전역 캐시 머티리얼과 에셋(빌트인/GLB)은 보존.</summary>
        private static void DestroyPlotOwned(GameObject plotGo)
        {
            if (plotGo == null) return;

            var holeNode = plotGo.transform.Find(HoleNodeName);
            var holeMr = holeNode != null ? holeNode.GetComponent<MeshRenderer>() : null;
            Material holeCachedMat = holeMr != null ? holeMr.sharedMaterial : null;   // 전역 캐시 — 파괴 금지

            // 런타임 프로시저럴 메시만 파괴(빌트인 Cube/Plane·GLB 에셋 메시 보존)
            var filters = plotGo.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                var m = filters[i].sharedMesh;
                if (m != null && m.name == HoleMeshName)
                    UnityEngine.Object.DestroyImmediate(m);
            }

            // 런타임 생성 머티리얼 인스턴스만 파괴(에셋 머티리얼·전역 캐시 보존 — 에셋 판정: 에셋 경로 빈 문자열)
            var renderers = plotGo.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var m = renderers[i].sharedMaterial;
                if (m != null && m != holeCachedMat && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(m)))
                    UnityEngine.Object.DestroyImmediate(m);
            }

            UnityEngine.Object.DestroyImmediate(plotGo);
        }
    }
}
