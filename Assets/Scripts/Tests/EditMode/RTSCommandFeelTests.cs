using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [15차] RTS 게임느낌 확장 계약 — 지휘 핫키(H홀드/X스톱)/확인음/컨텍스트 커서/
    /// 경로 점/공격 연결선/실루엣 하이라이트/미니맵 블립.
    /// </summary>
    public class RTSCommandFeelTests
    {
        [Test]
        public void GuardPlaceholder_HasHoldAndStopCommands()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/GuardPlaceholder.cs");
            Assert.That(src, Does.Contain("public void SetHold(bool hold)"), "홀드 명령.");
            Assert.That(src, Does.Contain("public void StopNow()"), "스톱 명령.");
            Assert.That(src, Does.Contain("public bool IsHolding"), "홀드 상태 노출.");
            Assert.That(src, Does.Contain("private void TryHoldEngage()"), "홀드 자동 반격(사거리 내만).");
            Assert.That(src.IndexOf("_isHolding = false;   // [15차] 새 명령이 홀드를 해제한다", System.StringComparison.Ordinal),
                Is.GreaterThan(-1), "새 명령이 홀드를 해제하는 계약.");
        }

        [Test]
        public void SelectionManager_WiresHAsHold_AndXAsStopOnlyWhenSelected()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/GuardSelectionManager.cs");
            Assert.That(src, Does.Contain("hKey.wasPressedThisFrame"), "H키 배선.");
            Assert.That(src, Does.Contain("HoldSelectedGuards();"), "H키 = 홀드 명령.");
            Assert.That(src, Does.Contain("xKey.wasPressedThisFrame"), "X키 배선.");
            Assert.That(src, Does.Contain("StopAllSelectedGuards();"), "X키 = 스톱 명령.");
            Assert.That(src, Does.Contain("SelectedCount > 0"), "지정된 병사가 있을 때만 소비(사용자 확정).");
        }

        [Test]
        public void CommandSystem_HasAckSound_Cursor_PathDots_AttackLink()
        {
            string feedback = System.IO.File.ReadAllText("Assets/Scripts/Systems/RTSCommandFeedback.cs");
            Assert.That(feedback, Does.Contain("AudioClip.Create"), "절차 확인음(에셋 의존 없음).");
            Assert.That(feedback, Does.Contain("PlayMoveAck"), "이동 ack.");
            Assert.That(feedback, Does.Contain("PlayAttackAck"), "공격 ack.");

            // [16차 사용자 요구] 컨텍스트 커서 제거 — 기본 커서 유지(파일 부재 + 배선 부재 단정).
            Assert.That(System.IO.File.Exists("Assets/Scripts/Systems/RTSCursorFeedback.cs"), Is.False,
                "RTSCursorFeedback 잔존 금지(기본 커서 복귀).");
            string rts = System.IO.File.ReadAllText("Assets/Scripts/Systems/RTSCommandSystem.cs");
            Assert.That(rts, Does.Not.Contain("UpdateCursorFeedback"), "커서 갱신 배선 잔존 금지.");
            Assert.That(rts, Does.Contain("RTSCommandFeedback.PlayMoveAck();"), "이동 ack 배선.");
            Assert.That(rts, Does.Contain("RTSCommandFeedback.PlayAttackAck();"), "공격 ack 배선.");
            // [16차 사용자 요구] 경로 점 제거 — 최종 지점 링만 표시(배선 부재 단정).
            Assert.That(rts, Does.Not.Contain("SpawnPathDots"), "경로 점 잔존 금지.");
            Assert.That(rts, Does.Contain("AttackLinkFX.Spawn(guard.transform.position, attackPos);"), "공격 연결선 배선.");
            Assert.That(rts, Does.Contain("public void IssueMoveCommandAtWorld(Vector3 position)"), "미니맵 월드 직행 API.");

            string link = System.IO.File.ReadAllText("Assets/Scripts/Systems/AttackLinkFX.cs");
            Assert.That(link, Does.Contain("ShadowCastingMode.Off"), "연결선 그림자 미방출.");
        }

        [Test]
        public void Highlight_UsesPropertyBlockWithoutSharedColorMutation()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/GuardHighlightUtility.cs");
            Assert.That(src, Does.Contain("SetPropertyBlock"), "MPB 경유(공유 머티리얼 색 변이 금지 계약).");
            Assert.That(src, Does.Contain("EnableKeyword(\"_EMISSION\")"), "이미시션 키워드 1회 활성.");
            Assert.That(src, Does.Contain("Color.black"), "해제 시 검정 복귀(미선택 무변화).");

            string gsm = System.IO.File.ReadAllText("Assets/Scripts/Systems/GuardSelectionManager.cs");
            Assert.That(gsm, Does.Contain("GuardHighlightUtility.SetHighlight(g, true);"), "오라 추가 시 하이라이트.");
            Assert.That(gsm, Does.Contain("GuardHighlightUtility.SetHighlight(g, false);"), "오라 제거 시 해제.");
        }

        [Test]
        public void Minimap_HasUnitBlipsAndRightClickCommand()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/MinimapUTK.cs");
            Assert.That(src, Does.Contain("public void RefreshUnitBlips()"), "유닛 블립 갱신.");
            Assert.That(src, Does.Contain("AllyBlipColor"), "아군 파랑 블립.");
            Assert.That(src, Does.Contain("EnemyBlipColor"), "적 빨강 블립.");
            Assert.That(src, Does.Contain("OnMapPointerDown"), "우클릭 핸들러.");
            Assert.That(src, Does.Contain("IssueMoveCommandAtWorld(world);"), "월드 직행 명령 배선.");
            Assert.That(src, Does.Contain("_blipTick = 0.25f"), "블립 고속 틱.");
        }
    }
}
