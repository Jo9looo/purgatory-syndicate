namespace PurgatorySyndicate.Battle
{
    /// <summary>
    /// Defines the high-level phases of a single battle turn.
    /// </summary>
    public enum BattleState
    {
        /// <summary>Roll speed values for every combatant.</summary>
        RollSpeed,

        /// <summary>Players and AI choose targets and skills.</summary>
        Targeting,

        /// <summary>Build and sort the action queue by speed.</summary>
        ActionSorting,

        /// <summary>Resolve actions and clashes in speed order.</summary>
        Execution,

        /// <summary>Cleanup, status ticks, and prepare the next turn.</summary>
        TurnEnd,

        /// <summary>Battle has ended (win or lose). Waiting for restart.</summary>
        BattleOver
    }
}
