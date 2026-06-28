using UnityEngine;
using ProjectName.Core.Utils;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 31: 플레이어 문장/상징 데이터.
    /// </summary>
    [System.Serializable]
    public class PlayerEmblemData
    {
        public string emblemName = "내 문장";
        public EmblemShape shape = EmblemShape.Shield;
        public EmblemColor primaryColor = EmblemColor.Gold;
        public EmblemColor secondaryColor = EmblemColor.Red;

        public PlayerEmblemData Clone()
        {
            return new PlayerEmblemData
            {
                emblemName = this.emblemName,
                shape = this.shape,
                primaryColor = this.primaryColor,
                secondaryColor = this.secondaryColor
            };
        }
    }

    /// <summary>문양 10종</summary>
    public enum EmblemShape
    {
        Shield,     // 방패
        Sword,      // 검
        Dragon,     // 용
        Eagle,      // 독수리
        Skull,      // 해골
        Rose,       // 장미
        Flame,      // 불꽃
        Star,       // 별
        Crown,      // 왕관
        Moon        // 달
    }

    /// <summary>문장 색상 8종</summary>
    public enum EmblemColor
    {
        Red, Blue, Green, Purple, Gold, Silver, White, Black
    }

    /// <summary>
    /// Phase 31: 문장 매니저 싱글톤.
    /// 플레이어 문장 저장/로드/변경 관리.
    /// </summary>
    public class EmblemManager : MonoBehaviour
    {
        public static EmblemManager Instance { get; private set; }

        [Header("설정")]
        [SerializeField] private int _changeCost = 100; // 문장 변경 비용

        public PlayerEmblemData CurrentEmblem { get; private set; } = new PlayerEmblemData();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>문장 변경 (비용 소모)</summary>
        public bool ChangeEmblem(PlayerEmblemData newEmblem, int playerGold)
        {
            if (playerGold < _changeCost) return false;
            if (newEmblem == null) return false;
            CurrentEmblem = newEmblem.Clone();
            return true;
        }

        /// <summary>문장 변경 비용</summary>
        public int ChangeCost => _changeCost;

        /// <summary>주 색상의 실제 Color 값 반환</summary>
        public static Color GetEmblemColor(EmblemColor color)
        {
            return color switch
            {
                EmblemColor.Red => Color.red,
                EmblemColor.Blue => Color.blue,
                EmblemColor.Green => Color.green,
                EmblemColor.Purple => new Color(0.6f, 0.2f, 1f),
                EmblemColor.Gold => new Color(1f, 0.85f, 0.2f),
                EmblemColor.Silver => new Color(0.75f, 0.75f, 0.8f),
                EmblemColor.White => Color.white,
                EmblemColor.Black => new Color(0.2f, 0.2f, 0.2f),
                _ => Color.white
            };
        }

        /// <summary>문양 유니코드 문자 반환 (화면 표시용)</summary>
        public static string GetEmblemSymbol(EmblemShape shape)
        {
            return shape switch
            {
                EmblemShape.Shield => "🛡️",
                EmblemShape.Sword => "⚔️",
                EmblemShape.Dragon => "🐉",
                EmblemShape.Eagle => "🦅",
                EmblemShape.Skull => "💀",
                EmblemShape.Rose => "🌹",
                EmblemShape.Flame => "🔥",
                EmblemShape.Star => "⭐",
                EmblemShape.Crown => "👑",
                EmblemShape.Moon => "🌙",
                _ => "❓"
            };
        }

        /// <summary>문장 Material 생성 (깃발/상징용)</summary>
        public Material CreateFlagMaterial()
        {
            Color color = GetEmblemColor(CurrentEmblem.primaryColor);
            return MaterialHelper.CreateLitMaterial(color, $"EmblemFlag_{CurrentEmblem.emblemName}");
        }

        /// <summary>보조 색상 Material 생성</summary>
        public Material CreateSecondaryMaterial()
        {
            Color color = GetEmblemColor(CurrentEmblem.secondaryColor);
            return MaterialHelper.CreateLitMaterial(color, $"EmblemSec_{CurrentEmblem.emblemName}");
        }
    }
}