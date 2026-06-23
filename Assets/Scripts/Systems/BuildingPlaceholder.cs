using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 건물Placeholder - 사장님이 GLB를 제공하기 전까지 사용할 임시 건물 모델.
    /// 건물 종류에 따라 다른 색상과 크기로 표현됩니다.
    /// 상호작용(E키 → IndoorSceneTransition.EnterBuilding)은 BuildingTrigger 컴포넌트가 처리합니다.
    /// </summary>
    public class BuildingPlaceholder : MonoBehaviour
    {
        public enum BuildingType
        {
            Shop,
            CraftHouse,
            Church,
            NPCHouse,
            Tavern,
            Other
        }

        [Header("설정")]
        [SerializeField] public BuildingType buildingType = BuildingType.Other;
        [SerializeField] public string buildingName = "알 수 없는 건물";

        [Header("상호작용")]
        [SerializeField] private float _interactRange = 3f;

        private Transform _player;

        public void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (_player == null)
                Debug.LogWarning("[BuildingPlaceholder] Player 태그 오브젝트 없음");
            Debug.Log($"[BuildingPlaceholder] {buildingName} ({buildingType}) 생성됨");
        }

        // 상호작용은 BuildingTrigger 컴포넌트가 처리하므로,
        // BuildingPlaceholder는 시각적 Placeholder 역할만 수행합니다.

        // 건물별 기본 색상
        private Color GetDefaultColor()
        {
            switch (buildingType)
            {
                case BuildingType.Shop: return new Color(0.8f, 0.6f, 0.2f); // 노란빛
                case BuildingType.CraftHouse: return new Color(0.6f, 0.8f, 0.2f); // 연두색
                case BuildingType.Church: return new Color(0.2f, 0.6f, 0.8f); // 파란색
                case BuildingType.NPCHouse: return new Color(0.8f, 0.2f, 0.6f); // 분홍색
                case BuildingType.Tavern: return new Color(0.6f, 0.3f, 0.1f); // 갈색 (선술집)
                default: return Color.grey;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = GetDefaultColor();
            Gizmos.DrawCube(transform.position, new Vector3(3, 2, 3)); // 간단한 박스 표시
        }

        // 건물 이름 표시 (선택 사항)
        private void OnGUI()
        {
            // 플레이어와 가까운 경우에만 이름 표시
            if (_player == null) return;

            float dist = Vector3.Distance(transform.position, _player.position);
            if (dist <= _interactRange)
            {
                Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2);
                if (screenPos.z > 0) // 앞쪽에 있을 때만
                {
                    GUIStyle style = new GUIStyle(GUI.skin.label);
                    style.alignment = TextAnchor.UpperCenter;
                    style.fontSize = 14;
                    style.normal.textColor = Color.yellow;

                    float labelWidth = 100;
                    float labelHeight = 25;
                    GUI.Label(new Rect(screenPos.x - labelWidth/2, Screen.height - screenPos.y - labelHeight/2, labelWidth, labelHeight), buildingName, style);
                }
            }
        }
    }
}