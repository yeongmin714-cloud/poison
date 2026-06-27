namespace ProjectName.Core
{
    /// <summary>
    /// 사운드 카테고리 타입 — BGM, SFX, UI.<br/>
    /// 볼륨 제어, AudioSource 분리, 믹서 그룹 라우팅에 사용.
    /// </summary>
    public enum SoundType
    {
        /// <summary>배경 음악 (loop 재생, 단일 스트림)</summary>
        BGM,
        /// <summary>일반 효과음 (2D, PlayOneShot)</summary>
        SFX,
        /// <summary>UI 효과음 (버튼 클릭, 팝업 등)</summary>
        UI
    }
}