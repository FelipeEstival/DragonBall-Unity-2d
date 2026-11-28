namespace DragonBall.Core
{
    /// <summary>
    /// High-level locomotion and combat states shared by playable fighters and enemies.
    /// </summary>
    public enum FighterState
    {
        Idle,
        Running,
        Jumping,
        Falling,
        // Reserved states for upcoming combat and Dragon Ball mechanics
        Attacking,
        Blocking,
        Flying,
        HitStun,
        Dead
    }

    /// <summary>
    /// Decoupled input contract so both Player keyboard/gamepad and AI controllers
    /// can drive the same fighter movement and combat systems.
    /// </summary>
    public interface IFighterInput
    {
        float MoveX { get; }
        float MoveY { get; }
        bool JumpPressed { get; }
        bool JumpHeld { get; }

        // Extension hooks for future combat systems
        bool AttackPressed { get; }
        bool KiBlastPressed { get; }
        bool BlockHeld { get; }
        bool FlightTogglePressed { get; }
    }
}
