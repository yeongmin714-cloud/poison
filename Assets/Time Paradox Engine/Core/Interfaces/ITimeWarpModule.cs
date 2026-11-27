namespace TimeWarp.Core
{
    public interface ITimeWarpModule
    {
        void OnWarpFixedUpdate();
        void OnWarpUpdate();
    }
}