using UnityEngine;
using UnityEngine.UI;
using ProjectName.UI.Core;

namespace ProjectName.UI.Functions
{
    /// <summary>
    /// 메인 메뉴 UI - UIWindow를 상속받아 Show()/Hide() 사용
    /// </summary>
    public class MainMenuUI : UIWindow
    {
        public GameObject mainMenuPanel;

        protected override void Awake()
        {
            base.Awake();
            Debug.Log("Main Menu UI initialized");
        }

        public override void Show()
        {
            if (mainMenuPanel != null)
                mainMenuPanel.SetActive(true);
            Debug.Log("[MainMenuUI] 메인 메뉴 표시");
        }

        public override void Hide()
        {
            if (mainMenuPanel != null)
                mainMenuPanel.SetActive(false);
            Debug.Log("[MainMenuUI] 메인 메뉴 숨김");
        }
    }
}