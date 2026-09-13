using UnityEngine;

namespace PurgatorySyndicate.Battle
{
    /// <summary>
    /// ScriptableObject holding immutable baseline stats for a combatant.
    /// Create assets via: Assets > Create > Purgatory Syndicate > Character Base Stats
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewCharacterStats",
        menuName = "Purgatory Syndicate/Character Base Stats",
        order = 0)]
    public class CharacterBaseStats : ScriptableObject
    {
        [Header("Identity")]
        public string characterName = "Unnamed Character";

        [Header("Vitality")]
        [Min(1)]
        public int maxHealth = 100;

        [Min(1)]
        public int maxStagger = 50;

        [Header("Speed")]
        [Tooltip("Inclusive min/max values used when rolling turn speed (x = min, y = max).")]
        public Vector2Int speedRange = new Vector2Int(1, 6);

        [Header("Sin Resource")]
        [Tooltip("Kapasitas Sin. Skill mengonsumsi Sin; +1 tiap awal turn.")]
        [Min(1)]
        public int maxSin = 6;
    }
}
