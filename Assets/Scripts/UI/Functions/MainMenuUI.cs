using UnityEngine;
using UnityEngine.UI;
using ProjectName.Core;

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
        }

        public override void Show()
        {
            if (mainMenuPanel != null)
                mainMenuPanel.SetActive(true);
        }

        public override void Hide()
        {
            if (mainMenuPanel != null)
                mainMenuPanel.SetActive(false);
        }
    }
}