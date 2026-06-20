using UnityEngine;

using UnityEngine.UI;

using ProjectName.Core;



namespace ProjectName.UI

{

    /// <summary>

    /// 플레이어 스테이터스를 표시하는 UI 윈도우.

    /// C 키로 열림.

    /// </summary>

    public class PlayerStatusWindow : UIWindow

    {

        [Header("UI References")]

        [SerializeField] private Text levelText;

        [SerializeField] private Text expText;

        [SerializeField] private Text hpText;

        [SerializeField] private Text alchemyText;

        [SerializeField] private Text cookingText;

        [SerializeField] private Text speechText;

        [SerializeField] private Text combatText;

        [SerializeField] private Slider addictionSlider;

        [SerializeField] private Text addictionText;



        private void OnShow()

        {

            UpdateDisplay();

        }



        public void UpdateDisplay()

        {

            if (PlayerStats.Instance == null) return;



            if (levelText != null)

                levelText.text = $"Lv. {PlayerStats.Instance.Level}";

            if (expText != null)

            {

                int curExp = PlayerStats.Instance.CurrentEXP;

                int nextExp = PlayerStats.Instance.GetExpForLevel(PlayerStats.Instance.Level + 1);

                int needExp = nextExp - curExp;

                expText.text = $"EXP: {curExp}/{nextExp} (+{needExp}까지)";

            }

            if (hpText != null)

            {

                float maxHP = PlayerStats.Instance.HPBase;

                float curHP = PlayerHealth.Instance != null ? PlayerHealth.Instance.CurrentHP : maxHP;

                hpText.text = $"HP: {curHP:F0}/{maxHP:F0}";

            }

            if (alchemyText != null)

                alchemyText.text = $"연금술 보너스: {PlayerStats.Instance.AlchemySuccessBonus * 100:F0}%";

            if (cookingText != null)

                cookingText.text = $"요리 보너스: {PlayerStats.Instance.CookingSuccessBonus * 100:F0}%";

            if (speechText != null)

                speechText.text = $"화술 보너스: +{PlayerStats.Instance.SpeechAffinityBonus}";

            if (combatText != null)

                combatText.text = $"전투 보너스: {PlayerStats.Instance.CombatDamageBonus * 100:F0}%";

            if (addictionSlider != null)

                addictionSlider.value = DrugEffectSystem.DrugAddictionLevel;

            if (addictionText != null)

                addictionText.text = $"중독: {DrugEffectSystem.DrugAddictionLevel:F0}% ({DrugEffectSystem.GetAddictionLabel()})";

        }}

}
