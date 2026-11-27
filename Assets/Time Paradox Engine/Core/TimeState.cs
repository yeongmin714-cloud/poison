namespace TimeWarp.Core
{
    public enum TimeState
    {
        Normal,
        PreAction,
        Rewinding,
        TimeStop,
        SlowMotion,
        PostAction
    }

    public enum EngineMode
    {
        GlobalOnly,
        LocalZonesOnly,
        Hybrid
    }

    public enum GlobalAction
    {
        Rewind,
        SlowMotion,
        TimeStop
    }
}