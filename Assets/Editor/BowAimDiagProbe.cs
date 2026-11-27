using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [168-P2] 활 조준 정합 진단 프로브 — UTKCursorOverlay(수동 스케일 수식)와 BowAimReticleUTK
    /// (RuntimePanelUtils.ScreenToPanel + WorldToLocal)의 패널 변환 수식을 더미 입력(screen (500,400))
    /// 으로 각각 계산해 패널좌표 차이를 로그한다.
    ///
    /// 배경(168차 사용자 보고): "활 조준선 ≠ 커서" — 드로 중 리티클 중심과 커서 오버레이(44px 아이콘)의
    ///   픽셀이 어긋난다. 수리([168-P2]): 리티클 경로를 단일 소스로 — PositionAtScreenPoint가 산출한
    ///   루트로컬 조준점을 UTKCursorOverlay.AlignCenterToScreenPoint 정적 훅에 위임(홀드 0.5s — 드로 틱
    ///   갱신 + 릴리즈 페이드 400ms 커버).
    ///
    /// 본 프로브가 계산하는 경로 차이(전수):
    ///   ① 수식 경로 — 커서: scale = root.worldBound.width/Screen.width + 수동 Y플립(루트 원점=화면 원점 가정).
    ///                리티클: RuntimePanelUtils.ScreenToPanel + root.WorldToLocal(루트 오프셋/스케일 정확 반영).
    ///                → 루트가 오프셋/비균등 스케일이면 커서 수식이 드리프트(프로브가 Δ를 수치 로그).
    ///   ② 16ms 스케줄 러그 — 두 요소가 각자 IVisualElementScheduler 틱에서 Mouse.position을 읽는다.
    ///                동일 프레임 패널 업데이트 안에서는 동일 샷 기반이라 무시 가능(해제 순간 릴리즈 핀 vs
    ///                라이브 샘플은 별도 — ③).
    ///   ③ 릴리즈 핀 vs 라이브 — 리티클은 해제 프레임 불변 픽셀에 고정(0.4s 페이드), 커서는 즉시 라이브
    ///                추적 재개 → 해제 직후 두 중심이 벌어진다(훅 홀드가 이 구간을 커버).
    ///   ④ 패널 스케일 — PanelSettings(ScaleWithScreenSize, match=0)에서 scaleFactor = Screen.width/1440.
    ///                풀필 루트에서는 두 수식이 수학적으로 등가(Δ=0), 오프셋 루트에서만 Δ≠0.
    ///   ⑤ hotspot — 커서 아이콘(44px 박스, ScaleToFit)의 시각적 화살촉이 박스 중심과 다를 수 있음(지각적
    ///                차이 — 좌표 버그 아님, 본 프로브는 좌표 정합만 판정).
    ///
    /// 실측 구성 3종(더미 screen (500,400)):
    ///   A 풀필 루트(현행 UIRoot — doc.rootVisualElement 직속, 무변형) → 기대 Δ=0
    ///   B 오프셋 루트(안전영역/레터박스 시뮬레이션 — 루트 (40,30) 인셋)  → 커서 수식 드리프트 Δ
    ///   C 비균등 스케일 루트(x1.0/y1.25)                              → 커서 수식 드리프트 Δ
    ///   + 릴리즈 핀 시뮬레이션(해제 픽셀 (500,400) 고정 vs 커서 라이브 (520,430)) → 훅 홀드의 필요성 Δ
    ///
    /// 실행(부모 몫 — 이 스크립트는 작성까지만):
    ///   bash /mnt/c/Unity/code/run_bowaim168_probe.sh
    ///   (Unity.exe -batchmode -projectPath C:/Unity/code -executeMethod
    ///    ProjectName.EditorTools.BowAimDiagProbe.RunAll -quit -logFile .../bowaim168_probe_run.log
    ///    — run_farm154_probe.sh 선례 1:1, 일회성 에디트모드 프로브라 -quit 허용)
    /// 산출: TestOutput/bowaim168_probe.log — CHECK=/Δ= 지표 + 배선 검증 + DONE/FAIL.
    /// </summary>
    public static class BowAimDiagProbe
    {
        private const string LogPath = "TestOutput/bowaim168_probe.log";
        private const string ReticleSource = "Assets/Scripts/UI/Toolkit/BowAimReticleUTK.cs";
        private const string CursorSource = "Assets/Scripts/UI/Toolkit/UTKCursorOverlay.cs";

        private static readonly StringBuilder _sb = new StringBuilder();

        public static void RunAll()
        {
            try
            {
                _sb.AppendLine("=== [168-P2] BowAimDiagProbe — 활 조준 정합 패널 변환 수식 차이 진단 ===");
                _sb.AppendLine("실행 시각: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                _sb.AppendLine("더미 입력 screen = (500, 400)");

                ProbePanelSettings();
                ProbeFormulaPaths();
                ProbeHookWiring();
            }
            catch (Exception ex)
            {
                _sb.AppendLine("FAIL: 예외 — " + ex.GetType().Name + ": " + ex.Message);
                _sb.AppendLine(ex.StackTrace);
            }

            string dir = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(LogPath, _sb.ToString());
            Debug.Log("[168-P2] BowAimDiagProbe 완료 — 로그: " + LogPath + " / " +
                (_sb.ToString().Contains("FAIL") ? "FAIL" : "DONE"));
            EditorApplication.Exit(_sb.ToString().Contains("FAIL") ? 1 : 0);
        }

        /// <summary>프로젝트 실제 PanelSettings(Resources/UI/PanelSettings)에서 패널 스케일 파라미터를 읽어
        /// ScreenToPanel 등가 수식의 scaleFactor를 산출한다(ScaleWithScreenSize + match=0 → width 기준).</summary>
        private static void ProbePanelSettings()
        {
            var settings = Resources.Load<PanelSettings>("UI/PanelSettings");
            if (settings == null)
            {
                _sb.AppendLine("FAIL: PanelSettings(Resources/UI/PanelSettings) 로드 실패 — 패널 스케일 파라미터 없음.");
                return;
            }
            _sb.AppendLine("PanelSettings: referenceResolution=(" +
                settings.referenceResolution.x.ToString(CultureInfo.InvariantCulture) + "," +
                settings.referenceResolution.y.ToString(CultureInfo.InvariantCulture) + ")" +
                " screenMatchMode=" + settings.screenMatchMode +
                " match=" + settings.match.ToString(CultureInfo.InvariantCulture) +
                " scaleMode=" + settings.scaleMode +
                " targetTexture=" + (settings.targetTexture != null ? "SET" : "null"));
            _sb.AppendLine("CHECK=PanelSettings 로드 성공(scaleMode=" + settings.scaleMode + ")");
        }

        /// <summary>세 루트 구성에 대해 커서 수식과 리티클 수식의 패널좌표를 각각 계산해 Δ를 로그한다.
        ///   리티클 등가 수식(ScreenToPanel, match=0): panelX = screen.x/scaleFactor,
        ///   panelY = panelHeight - screen.y/scaleFactor. 커서 수식: px = screen.x*(worldBound.width/Screen.width),
        ///   py = worldBound.height - screen.y*(worldBound.width/Screen.width) — 루트 로컬 좌표계.</summary>
        private static void ProbeFormulaPaths()
        {
            const float screenX = 500f, screenY = 400f;
            const float refW = 1440f;                       // PanelSettings.referenceResolution.x(실측값)
            int sw = Screen.width, sh = Screen.height;
            if (sw <= 0 || sh <= 0) { sw = 1920; sh = 1080; _sb.AppendLine("NOTE: Screen.width/height 비유효(배치모드) — 캐노니컬 1920x1080 사용."); }
            float scaleFactor = sw / refW;                  // match=0 → width 스케일(비균등 아님 — match=0이라 균등)
            float panelWidth = sw / scaleFactor;            // = refW
            float panelHeight = sh / scaleFactor;
            _sb.AppendLine("Screen=" + sw + "x" + sh + " scaleFactor=" + scaleFactor.ToString("F4", CultureInfo.InvariantCulture) +
                " panel=" + panelWidth.ToString("F1", CultureInfo.InvariantCulture) + "x" + panelHeight.ToString("F1", CultureInfo.InvariantCulture));

            // 리티클 경로(패널 좌표) — ScreenToPanel 등가 수식.
            float reticlePanelX = screenX / scaleFactor;
            float reticlePanelY = panelHeight - screenY / scaleFactor;
            _sb.AppendLine("RETICLE panelPoint=(" + reticlePanelX.ToString("F2", CultureInfo.InvariantCulture) +
                "," + reticlePanelY.ToString("F2", CultureInfo.InvariantCulture) + ")");

            // [A] 풀필 루트(현행) — worldBound = 패널 전체. 커서 수식이 리티클과 수학적으로 등가.
            float cursorScaleA = panelWidth / sw;           // = 1/scaleFactor
            float cursorPxA = screenX * cursorScaleA;
            float cursorPyA = panelHeight - screenY * cursorScaleA;
            LogDelta("A 풀필 루트(현행 UIRoot)", reticlePanelX, reticlePanelY, cursorPxA, cursorPyA);

            // [B] 오프셋 루트(안전영역/레터박스 시뮬레이션) — 루트가 (40,30) 인셋, 크기 축소.
            //     커서 수식은 루트 원점=화면 원점 가정이라 오프셋 무시 → 드리프트. 리티클 WorldToLocal은 반영.
            const float insetX = 40f, insetY = 30f;
            float rootWA = panelWidth - 2f * insetX, rootHA = panelHeight - 2f * insetY;
            float cursorScaleB = rootWA / sw;
            float cursorPxB = screenX * cursorScaleB;                       // 오프셋 미반영(수식 한계)
            float cursorPyB = rootHA - screenY * cursorScaleB;
            float reticleLocalBx = (reticlePanelX - insetX);                // 루트 로컬(스케일 1 가정)
            float reticleLocalBy = (reticlePanelY - insetY);
            LogDelta("B 오프셋 루트(안전영역 시뮬레이션 inset 40,30)", reticleLocalBx, reticleLocalBy, cursorPxB, cursorPyB);

            // [C] 비균등 스케일 루트(x1.0/y1.25) — worldBound.width는 x스케일만 반영, Y플립이 y스케일 무시.
            float scaleXc = 1f, scaleYc = 1.25f;
            float rootWC = panelWidth * scaleXc, rootHC = panelHeight * scaleYc;
            float cursorScaleC = rootWC / sw;
            float cursorPxC = screenX * cursorScaleC;
            float cursorPyC = rootHC - screenY * cursorScaleC;              // y스케일 반영 안 됨(수식 한계)
            float reticleLocalCx = reticlePanelX / scaleXc;                 // WorldToLocal = /scale
            float reticleLocalCy = reticlePanelY / scaleYc;
            LogDelta("C 비균등 스케일 루트(1.0/1.25)", reticleLocalCx, reticleLocalCy, cursorPxC, cursorPyC);

            // [D] 릴리즈 핀 시뮬레이션 — 해제 프레임 픽셀 (500,400)에 리티클 고정(불변), 커서는 라이브 (520,430).
            //     → 해제 직후 두 중심의 패널좌표 차이 = 훅 홀드(0.5s)가 제거하는 어긋남.
            const float liveX = 520f, liveY = 430f;
            float cursorPxDr = liveX * cursorScaleA;
            float cursorPyDr = panelHeight - liveY * cursorScaleA;
            LogDelta("D 릴리즈 핀 vs 커서 라이브(훅 홀드가 제거하는 Δ)", reticlePanelX, reticlePanelY, cursorPxDr, cursorPyDr);
        }

        private static void LogDelta(string label, float reticleX, float reticleY, float cursorX, float cursorY)
        {
            float dx = cursorX - reticleX;
            float dy = cursorY - reticleY;
            _sb.AppendLine(label + ": 리티클=(" + reticleX.ToString("F2", CultureInfo.InvariantCulture) +
                "," + reticleY.ToString("F2", CultureInfo.InvariantCulture) + ") 커서=(" +
                cursorX.ToString("F2", CultureInfo.InvariantCulture) + "," + cursorY.ToString("F2", CultureInfo.InvariantCulture) +
                ") Δ=(" + dx.ToString("F2", CultureInfo.InvariantCulture) + "," + dy.ToString("F2", CultureInfo.InvariantCulture) + ")");
            _sb.AppendLine("CHECK=" + label + " Δ=(" + dx.ToString("F2", CultureInfo.InvariantCulture) +
                "," + dy.ToString("F2", CultureInfo.InvariantCulture) + ")");
        }

        /// <summary>[168-P2] 수리 배선 검증 — 리티클 PositionAtScreenPoint의 훅 호출 + 커서 훅/홀드 존재.</summary>
        private static void ProbeHookWiring()
        {
            string reticle = File.Exists(ReticleSource) ? File.ReadAllText(ReticleSource) : "";
            string cursor = File.Exists(CursorSource) ? File.ReadAllText(CursorSource) : "";

            bool hookCall = reticle.Contains("UTKCursorOverlay.AlignCenterToScreenPoint(rootPoint);");
            bool hookDef = cursor.Contains("public static void AlignCenterToScreenPoint(Vector2 rootPoint)");
            bool holdGate = cursor.Contains("if (_alignHoldActive && Time.unscaledTime < _alignHoldUntil) return;");
            bool reticlePath = reticle.Contains("RuntimePanelUtils.ScreenToPanel(root.panel, screen)");

            _sb.AppendLine("CHECK=훅 호출(BowAimReticleUTK.PositionAtScreenPoint→UTKCursorOverlay)=" + (hookCall ? "OK" : "MISSING"));
            _sb.AppendLine("CHECK=훅 정의(UTKCursorOverlay.AlignCenterToScreenPoint)=" + (hookDef ? "OK" : "MISSING"));
            _sb.AppendLine("CHECK=홀드 게이트(커서 자체 추적 억제)=" + (holdGate ? "OK" : "MISSING"));
            _sb.AppendLine("CHECK=리티클 단일 소스 경로(ScreenToPanel+WorldToLocal) 유지=" + (reticlePath ? "OK" : "MISSING"));

            if (!hookCall || !hookDef || !holdGate || !reticlePath)
                _sb.AppendLine("FAIL: [168-P2] 배선 미완료 — 위 CHECK=MISSING 항목 참조.");
        }
    }
}
