using UnityEngine;
using System.Collections;

namespace ProjectName.Core
{
    /// <summary>
    /// 카메라 흔들림 시스템
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        [Header("Shake Settings")]
        [SerializeField] private float _shakeDuration = 0.5f;
        [SerializeField] private float _shakeMagnitude = 0.3f;
        [SerializeField] private float _shakeFrequency = 20f;

        private Vector3 _originalPosition;
        private bool _isShaking = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            _originalPosition = transform.localPosition;
        }

        /// <summary>
        /// 카메라 흔들기
        /// </summary>
        /// <param name="duration">흔들기 지속 시간</param>
        /// <param name="magnitude">흔들기 세기</param>
        public void Shake(float duration, float magnitude)
        {
            if (_isShaking) return;
            
            StartCoroutine(ShakeCoroutine(duration, magnitude));
        }

        /// <summary>
        /// 단순 카메라 흔들기 (기본 설정 사용)
        /// </summary>
        public void Shake()
        {
            Shake(_shakeDuration, _shakeMagnitude);
        }

        private IEnumerator ShakeCoroutine(float duration, float magnitude)
        {
            _isShaking = true;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                float x = Random.Range(-1f, 1f) * magnitude;
                float y = Random.Range(-1f, 1f) * magnitude;
                
                transform.localPosition = _originalPosition + new Vector3(x, y, 0);
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            transform.localPosition = _originalPosition;
            _isShaking = false;
        }
    }
}