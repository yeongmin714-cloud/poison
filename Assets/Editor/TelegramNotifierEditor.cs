using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Editor
{
    /// <summary>
    /// Telegram 알림 테스트 및 빌드 후 자동 전송
    /// </summary>
    public static class TelegramNotifierEditor
    {
        private const string MenuPath = "Tools/Telegram/";

        [MenuItem(MenuPath + "테스트 메시지 전송", false, 100)]
        public static void SendTestMessage()
        {
            string message = $"🧪 *Telegram 테스트*\n" +
                            $"프로젝트: {Application.productName}\n" +
                            $"시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                            $"Unity: {Application.unityVersion}\n" +
                            $"플랫폼: {Application.platform}";

            TelegramNotifier.SendMessage(message, (success, error) =>
            {
                if (success)
                    Debug.Log("[Telegram] 테스트 메시지 전송 성공");
                else
                    Debug.LogError($"[Telegram] 테스트 메시지 전송 실패: {error}");
            });
        }

        [MenuItem(MenuPath + "빌드 완료 알림 전송", false, 101)]
        public static void SendBuildCompleteMessage()
        {
            string message = $"✅ *빌드 완료*\n" +
                            $"프로젝트: {Application.productName}\n" +
                            $"버전: {PlayerSettings.bundleVersion}\n" +
                            $"빌드 타겟: {EditorUserBuildSettings.activeBuildTarget}\n" +
                            $"시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

            TelegramNotifier.SendMessage(message, (success, error) =>
            {
                if (success)
                    Debug.Log("[Telegram] 빌드 완료 알림 전송 성공");
                else
                    Debug.LogError($"[Telegram] 빌드 완료 알림 실패: {error}");
            });
        }

        [MenuItem(MenuPath + "에러 알림 테스트", false, 102)]
        public static void SendErrorTestMessage()
        {
            string message = $"🚨 *에러 테스트 알림*\n" +
                            $"이것은 테스트용 에러 알림입니다.\n" +
                            $"시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

            TelegramNotifier.SendMessage(message, (success, error) =>
            {
                if (success)
                    Debug.Log("[Telegram] 에러 테스트 알림 전송 성공");
                else
                    Debug.LogError($"[Telegram] 에러 테스트 알림 실패: {error}");
            });
        }

        [MenuItem(MenuPath + "설정 파일 열기", false, 200)]
        public static void OpenConfigFile()
        {
            string path = "Assets/Resources/TelegramConfig.json";
            var obj = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (obj != null)
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = obj;
        }

        /// <summary>
        /// 빌드 후 자동 알림 (BuildPlayerOptions 콜백)
        /// </summary>
        public static void OnBuildComplete(BuildReport report)
        {
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                string message = $"✅ *빌드 성공*\n" +
                                $"프로젝트: {Application.productName}\n" +
                                $"버전: {PlayerSettings.bundleVersion}\n" +
                                $"타겟: {report.summary.platform}\n" +
                                $"크기: {report.summary.totalSize / 1024 / 1024:F1} MB\n" +
                                $"시간: {report.summary.totalTime}\n" +
                                $"시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

                TelegramNotifier.SendMessage(message);
            }
            else if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Failed)
            {
                string message = $"❌ *빌드 실패*\n" +
                                $"프로젝트: {Application.productName}\n" +
                                $"타겟: {report.summary.platform}\n" +
                                $"에러: {report.summary.totalErrors}개\n" +
                                $"시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

                TelegramNotifier.SendMessage(message);
            }
        }
    }
}