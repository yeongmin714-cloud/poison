using System;
using System.Collections;
using ProjectName.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectName.Systems
{
    /// <summary>
    /// C12-01: 로딩 화면 싱글톤 매니저.
    /// 씬 전환 시 로딩 상태를 관리하고 AsyncOperation 진행률을 추적합니다.
    /// </summary>
    public class LoadingManager : MonoBehaviour
    {
        public static LoadingManager Instance { get; private set; }

        // ===== 상태 =====
        public bool IsLoading { get; private set; }
        public float Progress { get; private set; } // 0.0 ~ 1.0
        public string CurrentTip { get; private set; }

        // ===== 이벤트 =====
        public event Action<float> OnProgressChanged;
        public event Action OnLoadStart;
        public event Action OnLoadComplete;

        // ===== 페이드 설정 =====
        private float _fadeInDuration = 0.3f;
        private float _fadeOutDuration = 0.3f;

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

        /// <summary>로딩 시작</summary>
        public void StartLoading(float fadeInDuration = 0.3f)
        {
            if (IsLoading) return;

            IsLoading = true;
            Progress = 0f;
            _fadeInDuration = fadeInDuration;
            CurrentTip = TipDatabase.GetRandomTip();

            OnLoadStart?.Invoke();
            OnProgressChanged?.Invoke(0f);
        }

        /// <summary>진행률 업데이트 (0.0~1.0)</summary>
        public void SetProgress(float progress)
        {
            Progress = Mathf.Clamp01(progress);
            OnProgressChanged?.Invoke(Progress);
        }

        /// <summary>로딩 완료</summary>
        public void CompleteLoading(float fadeOutDuration = 0.3f)
        {
            if (!IsLoading) return;

            _fadeOutDuration = fadeOutDuration;
            Progress = 1f;
            OnProgressChanged?.Invoke(1f);

            StartCoroutine(CompleteAfterFade());
        }

        private IEnumerator CompleteAfterFade()
        {
            yield return new WaitForSeconds(_fadeOutDuration);

            IsLoading = false;
            OnLoadComplete?.Invoke();
        }

        /// <summary>
        /// 씬을 비동기로 로드하며 로딩 화면을 표시합니다.
        /// </summary>
        public void LoadSceneAsync(string sceneName, float fadeInDuration = 0.3f, float fadeOutDuration = 0.3f)
        {
            if (IsLoading) return;

            StartLoading(fadeInDuration);
            StartCoroutine(LoadSceneCoroutine(sceneName, fadeOutDuration));
        }

        private IEnumerator LoadSceneCoroutine(string sceneName, float fadeOutDuration)
        {
            // 씬이 존재하는지 확인
            bool sceneExists = false;
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (!string.IsNullOrEmpty(path) && path.Contains(sceneName))
                {
                    sceneExists = true;
                    break;
                }
            }

            if (!sceneExists)
            {
                // 씬이 없으면 에러 로그 후 로딩 종료
                Debug.LogError($"[LoadingManager] 씬을 찾을 수 없음: {sceneName}");
                CompleteLoading(fadeOutDuration);
                yield break;
            }

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
            operation.allowSceneActivation = false;

            // 0 ~ 0.9까지 진행률 반영
            while (operation.progress < 0.9f)
            {
                SetProgress(operation.progress / 0.9f);
                yield return null;
            }

            // 0.9 도달 → 1.0으로 완료 표시
            SetProgress(1.0f);

            // 잠시 대기 후 씬 활성화
            yield return new WaitForSeconds(0.5f);

            // 페이드 아웃 후 씬 활성화
            _fadeOutDuration = fadeOutDuration;
            CompleteLoading(fadeOutDuration);

            // 씬 활성화
            operation.allowSceneActivation = true;
        }
    }
}