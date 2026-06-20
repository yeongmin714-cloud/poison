using ProjectName.Core;
using ProjectName.Core;
using UnityEngine;
using ProjectName.Core.Data;

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
        /// 깃발/색상 교체 + 알림 표시.
        /// </summary>
        public void ChangeOwnership(string territoryId, string territoryName, NationType newOwner, bool isPlayerOwned = false)
        {
            Debug.Log($"[TerritoryBannerSystem] 🎉 {territoryName}을(를) 점령했습니다!");

            bool isPlayer = isPlayerOwned;

            // FadeManager를 통한 페이드 효과
            FadeManager fade = FindObjectOfType<FadeManager>();
            if (fade != null)
            {
                // 페이드 효과 (선택 사항 — FadeManager API에 따라 조정)
            }

            // 병사 색상 변경 (TerritoryManager 연동)
            TerritoryManager territoryManager = FindObjectOfType<TerritoryManager>();
            if (territoryManager != null)
            {
                territoryManager.SetTerritoryOwner(territoryId, newOwner);
            }

            // 플레이어 소유 시 문장에 맞게 색상 교체
            if (isPlayer && EmblemManager.Instance != null)
            {
                EmblemManager.Instance.CreateFlagMaterial();
                Debug.Log($"[TerritoryBannerSystem] {EmblemManager.Instance.CurrentEmblem.emblemName}의 깃발이 게양됩니다!");
            }

            // 병사들 색상 변경
            UpdateGuardColors(territoryId, isPlayer);
        }

        /// <summary>해당 영지 병사들의 색상을 변경</summary>
        private void UpdateGuardColors(string territoryId, bool isPlayer)
        {
            var guards = FindObjectsOfType<GuardPlaceholder>();
            Color targetColor = isPlayer && EmblemManager.Instance != null
                ? EmblemManager.GetEmblemColor(EmblemManager.Instance.CurrentEmblem.primaryColor)
                : Color.gray;

            foreach (var guard in guards)
            {
                var renderer = guard.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.material.color = Color.Lerp(renderer.material.color, targetColor, 0.5f);
                }
            }
        }

        /// <summary>
        /// 점령 완료 알림 문자열 반환 (UI 표시용).
        /// </summary>
        public static string GetOccupationMessage(string territoryName)
        {
            string emblemName = EmblemManager.Instance != null
                ? EmblemManager.Instance.CurrentEmblem.emblemName
                : "정복자";

            return $"🎉 {territoryName}을(를) 점령했습니다!\n{emblemName}의 깃발이 게양됩니다!";
        }
    }
}