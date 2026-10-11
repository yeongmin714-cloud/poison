using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [15차 A1] RTS 명령 확인음 — 이동/공격 우클릭 시 짧은 절차 클립(AttackSoundLayerManager 선례).
    /// AudioClip.Create + SetData 패턴. 실패는 전투 방해 금지 원칙대로 흡수(try/catch).
    /// </summary>
    public static class RTSCommandFeedback
    {
        private static AudioClip _moveAck;
        private static AudioClip _attackAck;

        /// <summary>이동 명령 확인음 — 부드러운 하강 블립.</summary>
        public static void PlayMoveAck()
        {
            try
            {
                if (_moveAck == null) _moveAck = BuildBlip("rts_ack_move", 760f, 560f, 0.09f);
                PlayAtCamera(_moveAck, 0.35f);
            }
            catch { /* 오디오 부재 환경 흡수 */ }
        }

        /// <summary>공격 명령 확인음 — 더 날카로운 상승 2톤.</summary>
        public static void PlayAttackAck()
        {
            try
            {
                if (_attackAck == null) _attackAck = BuildBlip("rts_ack_attack", 520f, 820f, 0.13f);
                PlayAtCamera(_attackAck, 0.45f);
            }
            catch { /* 오디오 부재 환경 흡수 */ }
        }

        private static AudioClip BuildBlip(string name, float freqFrom, float freqTo, float duration)
        {
            const int sampleRate = 22050;
            int n = Mathf.CeilToInt(sampleRate * duration);
            var samples = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float freq = Mathf.Lerp(freqFrom, freqTo, t);
                float envelope = Mathf.Pow(1f - t, 2f);   // 지수 감쇠 — 짧고 건조한 ack
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * (i / (float)sampleRate)) * envelope * 0.8f;
            }
            var clip = AudioClip.Create(name, n, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static void PlayAtCamera(AudioClip clip, float volume)
        {
            var cam = Camera.main;
            if (cam == null || clip == null) return;
            AudioSource.PlayClipAtPoint(clip, cam.transform.position, volume);
        }
    }
}
