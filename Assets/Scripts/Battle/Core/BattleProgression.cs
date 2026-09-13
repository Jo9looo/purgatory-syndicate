using UnityEngine;

namespace PurgatorySyndicate.Battle
{
    /// <summary>
    /// Tracks which encounter stage the player is on across scene reloads
    /// (static = survives LoadScene). Reset when returning to the main menu.
    /// Encounters live at Resources/Battle/Encounter_Stage{N}.
    /// </summary>
    public static class BattleProgression
    {
        public const int StageCount = 3;

        public static int StageIndex { get; private set; }

        /// <summary>1-based stage number for UI ("Stage 2/3").</summary>
        public static int StageNumber => StageIndex + 1;

        public static bool HasNextStage => StageIndex < StageCount - 1;

        public static void Advance()
        {
            StageIndex = Mathf.Min(StageIndex + 1, StageCount - 1);
        }

        public static void Reset()
        {
            StageIndex = 0;
        }

        public static EncounterData LoadCurrent()
        {
            EncounterData staged = Resources.Load<EncounterData>($"Battle/Encounter_Stage{StageNumber}");
            return staged != null
                ? staged
                : Resources.Load<EncounterData>("Battle/Encounter_Default");
        }
    }
}
