using System.Collections.Generic;
using UnityEngine;

namespace PurgatorySyndicate.Battle
{
    /// <summary>
    /// Defines one battle encounter: which characters fight on each side.
    /// Create via: Assets > Create > Purgatory Syndicate > Encounter Data
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewEncounter",
        menuName = "Purgatory Syndicate/Encounter Data",
        order = 3)]
    public class EncounterData : ScriptableObject
    {
        public string encounterName = "Unnamed Encounter";

        [Header("Sisi pemain")]
        public List<CharacterDefinition> allies = new List<CharacterDefinition>();

        [Header("Sisi lawan")]
        public List<CharacterDefinition> enemies = new List<CharacterDefinition>();
    }
}
