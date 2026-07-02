using System.Collections;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 볼륨 페이드 전용 정적 헬퍼 클래스.
    /// AudioSource.volume을 target 값까지 duration 동안 Lerp로 페이드합니다.
    /// </summary>
    public static class Transitions
    {
        /// <summary>
        /// AudioSource의 볼륨을 target까지 duration(초) 동안 Lerp로 페이드합니다.
        /// </summary>
        /// <param name="source">대상 AudioSource (null이면 아무 동작 안 함)</param>
        /// <param name="target">목표 볼륨 (0~1)</param>
        /// <param name="duration">페이드 지속 시간 (초)</param>
        /// <returns>IEnumerator (코루틴에서 yield return으로 사용)</returns>
        public static IEnumerator FadeVolume(AudioSource source, float target, float duration)
        {
            // Null 체크
            if (source == null)
            {
                Debug.LogWarning("[Transitions] FadeVolume: AudioSource가 null입니다.");
                yield break;
            }

            // 유효성 검사
            if (duration <= 0f)
            {
                source.volume = Mathf.Clamp01(target);
                yield break;
            }

            float startVolume = source.volume;
            float clampedTarget = Mathf.Clamp01(target);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                source.volume = Mathf.Lerp(startVolume, clampedTarget, t);
                yield return null;
            }

            // 최종 값 보정
            source.volume = clampedTarget;
        }
    }
}