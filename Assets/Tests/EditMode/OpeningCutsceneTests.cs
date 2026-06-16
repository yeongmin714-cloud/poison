using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C10-17: OpeningCutscene EditMode 테스트.
    ///
    /// 테스트 대상:
    /// - PlayerPrefs 기반 Seen 상태 관리
    /// - 컷씬 재생/스킵 상태 머신
    /// - 내레이션 텍스트 순차 표시
    /// - FadeManager 연동
    /// - ResetAll 초기화
    /// </summary>
    public class OpeningCutsceneTests
    {
        private const string HasSeenKey = "OpeningCutscene_Seen";

        [SetUp]
        public void Setup()
        {
            // PlayerPrefs 초기화
            PlayerPrefs.DeleteKey(HasSeenKey);
            PlayerPrefs.Save();

            // OpeningCutscene 상태 초기화
            OpeningCutscene.ResetAll();
        }

        [TearDown]
        public void Teardown()
        {
            OpeningCutscene.ResetAll();
            PlayerPrefs.DeleteKey(HasSeenKey);
            PlayerPrefs.Save();
        }

        // ================================================================
        // Seen 상태 테스트
        // ================================================================

        [Test]
        public void HasBeenSeen_Default_False()
        {
            Assert.IsFalse(OpeningCutscene.HasBeenSeen(), "기본 상태는 false");
        }

        [Test]
        public void MarkAsSeen_SetsHasBeenSeen_True()
        {
            OpeningCutscene.MarkAsSeen();
            Assert.IsTrue(OpeningCutscene.HasBeenSeen(), "MarkAsSeen 후 true");
        }

        [Test]
        public void ResetSeenState_ClearsHasBeenSeen()
        {
            OpeningCutscene.MarkAsSeen();
            Assert.IsTrue(OpeningCutscene.HasBeenSeen(), "MarkAsSeen 직후 true");

            OpeningCutscene.ResetSeenState();
            Assert.IsFalse(OpeningCutscene.HasBeenSeen(), "ResetSeenState 후 false");
        }

        [Test]
        public void PlayIfNeeded_AfterSeen_DoesNotPlay()
        {
            OpeningCutscene.MarkAsSeen();

            // HasBeenSeen=true면 PlayIfNeeded는 재생하지 않음
            OpeningCutscene.PlayIfNeeded();
            Assert.IsFalse(OpeningCutscene.IsPlaying, "이미 본 상태에서는 재생 안 함");
        }

        [Test]
        public void PlayIfNeeded_ForcePlay_PlaysAfterSeen()
        {
            OpeningCutscene.MarkAsSeen();

            OpeningCutscene.PlayIfNeeded(true);
            Assert.IsTrue(OpeningCutscene.IsPlaying, "forcePlay=true면 이미 본 상태에서도 재생");
        }

        [Test]
        public void PlayIfNeeded_NotSeen_StartsPlaying()
        {
            // MarkAsSeen 하지 않음
            OpeningCutscene.PlayIfNeeded();
            Assert.IsTrue(OpeningCutscene.IsPlaying, "처음 보는 상태에서는 재생");
        }

        [Test]
        public void PlayCutscene_SetsIsPlaying_True()
        {
            OpeningCutscene.PlayCutscene();
            Assert.IsTrue(OpeningCutscene.IsPlaying, "PlayCutscene 후 IsPlaying=true");
        }

        // ================================================================
        // 상태 머신 테스트
        // ================================================================

        [Test]
        public void Initial_IsPlaying_False()
        {
            Assert.IsFalse(OpeningCutscene.IsPlaying, "초기 IsPlaying=false");
        }

        [Test]
        public void Initial_CurrentNarrationIndex_Zero()
        {
            Assert.AreEqual(-1, OpeningCutscene.CurrentNarrationIndex, "초기 CurrentNarrationIndex=-1 (아직 아무것도 표시 안 함)");
        }

        [Test]
        public void StopCutscene_SetsIsPlaying_False()
        {
            OpeningCutscene.PlayCutscene();
            Assert.IsTrue(OpeningCutscene.IsPlaying, "PlayCutscene 후 IsPlaying=true");

            OpeningCutscene.StopCutscene();
            Assert.IsFalse(OpeningCutscene.IsPlaying, "StopCutscene 후 IsPlaying=false");
        }

        [Test]
        public void PlayCutscene_AlreadyPlaying_DoesNotStartAgain()
        {
            OpeningCutscene.PlayCutscene();
            Assert.IsTrue(OpeningCutscene.IsPlaying);

            // 두 번째 PlayCutscene 호출 — 이미 재생 중이면 무시
            OpeningCutscene.PlayCutscene();
            Assert.IsTrue(OpeningCutscene.IsPlaying, "이미 재생 중이면 두 번째 호출 무시");
        }

        [Test]
        public void ResetAll_ClearsState()
        {
            OpeningCutscene.PlayCutscene();
            Assert.IsTrue(OpeningCutscene.IsPlaying);

            OpeningCutscene.ResetAll();
            Assert.IsFalse(OpeningCutscene.IsPlaying, "ResetAll 후 IsPlaying=false");
            Assert.AreEqual(-1, OpeningCutscene.CurrentNarrationIndex, "ResetAll 후 CurrentNarrationIndex 초기화");
        }

        // ================================================================
        // 이벤트 테스트
        // ================================================================

        [Test]
        public void PlayCutscene_FiresOnCutsceneStarted()
        {
            bool started = false;
            OpeningCutscene.OnCutsceneStarted += () => started = true;

            OpeningCutscene.PlayCutscene();

            Assert.IsTrue(started, "PlayCutscene 시 OnCutsceneStarted 발생");
        }

        [Test]
        public void StopCutscene_FiresOnCutsceneSkipped()
        {
            bool skipped = false;
            OpeningCutscene.OnCutsceneSkipped += () => skipped = true;

            OpeningCutscene.PlayCutscene();
            OpeningCutscene.StopCutscene();

            Assert.IsTrue(skipped, "StopCutscene 시 OnCutsceneSkipped 발생");
        }

        [Test]
        public void StopCutscene_FiresOnCutsceneCompleted()
        {
            bool completed = false;
            OpeningCutscene.OnCutsceneCompleted += () => completed = true;

            OpeningCutscene.PlayCutscene();
            OpeningCutscene.StopCutscene();

            Assert.IsTrue(completed, "StopCutscene 시 OnCutsceneCompleted 발생");
        }

        // ================================================================
        // PlayerPrefs 저장 확인
        // ================================================================

        [Test]
        public void StopCutscene_SavesToPlayerPrefs()
        {
            OpeningCutscene.PlayCutscene();
            OpeningCutscene.StopCutscene();

            Assert.IsTrue(OpeningCutscene.HasBeenSeen(), "StopCutscene 후 PlayerPrefs 저장 확인");
        }

        [Test]
        public void PlayCutscene_Complete_SavesToPlayerPrefs()
        {
            // 직접 CompleteOpening 경로 테스트를 위해 StopCutscene 사용
            OpeningCutscene.PlayCutscene();
            OpeningCutscene.StopCutscene();

            int savedValue = PlayerPrefs.GetInt(HasSeenKey, 0);
            Assert.AreEqual(1, savedValue, "PlayerPrefs에 1 저장");
        }

        // ================================================================
        // 다중 호출 안전성
        // ================================================================

        [Test]
        public void StopCutscene_NotPlaying_DoesNothing()
        {
            // IsPlaying=false 상태에서 StopCutscene 호출 — 예외 없이 통과
            Assert.DoesNotThrow(() => OpeningCutscene.StopCutscene());
        }

        [Test]
        public void MultipleResetAll_Safe()
        {
            // ResetAll 여러 번 호출 — 예외 없이 통과
            Assert.DoesNotThrow(() =>
            {
                OpeningCutscene.ResetAll();
                OpeningCutscene.ResetAll();
                OpeningCutscene.ResetAll();
            });
        }

        [Test]
        public void PlayCutscene_AfterStop_CanRestart()
        {
            OpeningCutscene.PlayCutscene();
            OpeningCutscene.StopCutscene();

            Assert.IsFalse(OpeningCutscene.IsPlaying, "StopCutscene 후 IsPlaying=false");

            // 다시 재생
            OpeningCutscene.PlayCutscene();
            Assert.IsTrue(OpeningCutscene.IsPlaying, "Stop 후 다시 재생 가능");
        }
    }
}
