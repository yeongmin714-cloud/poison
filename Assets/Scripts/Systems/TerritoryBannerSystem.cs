using ProjectName.Core;
using UnityEngine;
using ProjectName.Core.Data;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 31: 영지 점령 시 상징 교체 시스템.
    /// 깃발/병사 색상 교체 + 점령 알림.
    /// </summary>
    public class TerritoryBannerSystem : MonoBehaviour
    {
        public static TerritoryBannerSystem Instance { get; private set; }

        [Header("설정")]
        [SerializeField] private float _colorLerpDuration = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// 영지 소유주 변경 시 호출.
        /// </summary>
        /// <param name="territoryId">대상 영지 ID</param>
        /// <param name="territoryName">대상 영지 이름</param>
        /// <param name="newOwner">새 소유주 국가 (NationType)</param>
        /// <param name="isPlayerOwned">플레이어 소유 여부</param>
        public void ChangeOwnership(string territoryId, string territoryName, NationType newOwner, bool isPlayerOwned = false)
        {
            Debug.Log($"[TerritoryBannerSystem] 🎉 {territoryName}을(를) 점령했습니다!");

            // 페이드 효과 (FadeManager 싱글톤)
            if (FadeManager.Instance != null)
            {
                // TODO: 실제 페이드 아웃/인 로직 구현
                // ex) FadeManager.Instance.FadeOut(0.5f);
            }

            // 영지 소유자 갱신 (TerritoryManager 싱글톤)
            if (TerritoryManager.Instance != null)
            {
                TerritoryManager.Instance.SetTerritoryOwner(territoryId, newOwner);
            }

            // 플레이어 소유 시 문장에 맞게 색상 교체
            if (isPlayerOwned && EmblemManager.Instance != null)
            {
                EmblemManager.Instance.CreateFlagMaterial();
                Debug.Log($"[TerritoryBannerSystem] {EmblemManager.Instance.CurrentEmblem.emblemName}의 깃발이 게양됩니다!");
            }

            // 병사들 색상 변경
            UpdateGuardColors(territoryId, isPlayerOwned);
        }

        /// <summary>해당 영지 병사들의 색상을 변경</summary>
        /// <param name="territoryId">대상 영지 ID (향후 특정 영지 필터링용)</param>
        /// <param name="isPlayer">플레이어 소유 여부</param>
        private void UpdateGuardColors(string territoryId, bool isPlayer)
        {
            Color targetColor = isPlayer && EmblemManager.Instance != null
                ? EmblemManager.GetEmblemColor(EmblemManager.Instance.CurrentEmblem.primaryColor)
                : Color.gray;

            var guards = FindObjectsOfType<GuardPlaceholder>();
            foreach (var guard in guards)
            {
                var renderer = guard.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    // MaterialPropertyBlock 사용으로 인스턴스 생성 방지
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    block.SetColor(Shader.PropertyToID("_BaseColor"), targetColor);
                    renderer.SetPropertyBlock(block);
                }
            }
        }

        /// <summary>
        /// 점령 완료 알림 문자열 반환 (UI 표시용).
        /// </summary>
        /// <param name="territoryName">대상 영지 이름</param>
        /// <returns>UI 표시용 점령 완료 메시지</returns>
        public static string GetOccupationMessage(string territoryName)
        {
            string emblemName = EmblemManager.Instance != null
                ? EmblemManager.Instance.CurrentEmblem.emblemName
                : "정복자";

            return $"🎉 {territoryName}을(를) 점령했습니다!\n{emblemName}의 깃발이 게양됩니다!";
        }
    }
}