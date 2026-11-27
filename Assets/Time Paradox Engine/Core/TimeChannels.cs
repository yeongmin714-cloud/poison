using System;

namespace TimeWarp.Core
{
    [Flags]
    public enum TimeChannels
    {
        None              = 0,
        Transform         = 1 << 0,
        Velocity          = 1 << 1, 
        ActiveState       = 1 << 2,
        Particles         = 1 << 3, 
        Shaders           = 1 << 4,
        Animation         = 1 << 5, 
        Custom            = 1 << 6, 
        Audio             = 1 << 7, 
        Velocity2D        = 1 << 8, 
        Navigation        = 1 << 9,
        Echoes            = 1 << 10, // NEW: Channel for Ghosts / Paradoxes

        StandardPhysics   = Transform | Velocity | ActiveState,
        StandardPhysics2D = Transform | Velocity2D | ActiveState,
        All               = ~0
    }
}