using System.Collections.Generic;
using UnityEngine;

namespace PurgatorySyndicate.Battle
{
    /// <summary>
    /// Complete definition of a playable/enemy character:
    /// base stats + skill loadout + sin affinities, in one designer-facing asset.
    /// Create via: Assets > Create > Purgatory Syndicate > Character Definition
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewCharacter",
        menuName = "Purgatory Syndicate/Character Definition",
        order = 2)]
    public class CharacterDefinition : ScriptableObject
    {
        [Header("Stats")]
        public CharacterBaseStats baseStats;

        [Header("Skill Loadout (index 0-2 dipetakan ke tombol 1-3)")]
        public List<SkillData> skills = new List<SkillData>();

        [Header("Skill Bertahan (slot 4: Block / Dodge / Counter)")]
        public SkillData defenseSkill;

        [Header("Sin Affinity (skill dengan sin yang cocok dapat bonus power)")]
        public List<SinType> sinAffinities = new List<SinType>();
    }
}
