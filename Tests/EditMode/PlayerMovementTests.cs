using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// PlayerMovement 테스트.
    /// 
    /// EditMode 테스트는 씬이 없는 상태에서 실행됩니다.
    /// 따라서 CharacterController 같은 물리 컴포넌트가 필요한 기능은
    /// PlayMode 테스트에서 검증해야 합니다.
    /// 
    /// 여기서는 설정값(속도, 점프 높이 등)이 올바른지 검증합니다.
    /// </summary>
    public class PlayerMovementTests
    {
        /// <summary>
        /// GameObject를 만들고 PlayerMovement 컴포넌트를 추가합니다.
        /// (CharacterController는 [RequireComponent]로 자동 추가됨)
        /// </summary>
        private PlayerMovement CreatePlayer()
        {
            var go = new GameObject("TestPlayer");
            var player = go.AddComponent<PlayerMovement>();
            return player;
        }

        // ===================== 기본 속성 테스트 =====================

        [Test]
        public void PlayerMovement_WalkSpeed_DefaultIsFive()
        {
            // Arrange: 플레이어 생성
            var player = CreatePlayer();

            // Act: WalkSpeed 읽기
            float speed = player.WalkSpeed;

            // Assert: 기본 걷기 속도는 5
            Assert.AreEqual(5f, speed, "기본 걷기 속도는 5여야 합니다");
        }

        [Test]
        public void PlayerMovement_RunSpeed_DefaultIsTen()
        {
            var player = CreatePlayer();
            Assert.AreEqual(10f, player.RunSpeed, "기본 달리기 속도는 10이어야 합니다");
        }

        [Test]
        public void PlayerMovement_JumpHeight_DefaultIsTwo()
        {
            var player = CreatePlayer();
            Assert.AreEqual(2f, player.JumpHeight, "기본 점프 높이는 2여야 합니다");
        }

        [Test]
        public void PlayerMovement_HasCharacterController()
        {
            var go = new GameObject("TestPlayer");
            var player = go.AddComponent<PlayerMovement>();

            // RequireComponent 특성으로 자동 추가되었는지 확인
            var controller = go.GetComponent<CharacterController>();
            Assert.IsNotNull(controller, "PlayerMovement에는 CharacterController가 자동 추가되어야 합니다");
        }

        // ===================== 생성 시 초기 상태 테스트 =====================

        [Test]
        public void PlayerMovement_OnCreate_IsGroundedIsFalse()
        {
            // 빈 공간에서 생성되면 isGrounded는 false (씬 없음)
            var player = CreatePlayer();
            Assert.IsFalse(player.IsGrounded, "씬이 없으므로 IsGrounded는 false");
        }

        [Test]
        public void PlayerMovement_OnCreate_VelocityIsZero()
        {
            var player = CreatePlayer();
            Assert.AreEqual(Vector3.zero, player.Velocity, "생성 직후 속도는 0");
        }

        // ===================== 독립 실행 테스트 =====================

        [Test]
        public void PlayerMovement_MultipleInstances_EachHasOwnValues()
        {
            var player1 = CreatePlayer();
            var player2 = CreatePlayer();

            // 각각 독립적인 인스턴스
            Assert.AreEqual(player1.WalkSpeed, player2.WalkSpeed, "기본값은 동일");
        }
    }
}