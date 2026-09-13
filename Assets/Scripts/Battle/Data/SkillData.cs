using UnityEngine;

namespace PurgatorySyndicate.Battle
{
    /// <summary>
    /// ScriptableObject definition for a combat skill used during clashes.
    /// Create assets via: Assets > Create > Purgatory Syndicate > Skill Data
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewSkill",
        menuName = "Purgatory Syndicate/Skill Data",
        order = 1)]
    public class SkillData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Display name shown in logs and future UI.")]
        public string skillName = "Unnamed Skill";

        [Tooltip("Attack = skill aktif (slot 1-3). Block/Dodge/Counter = skill bertahan (slot 4).")]
        public SkillKind kind = SkillKind.Attack;

        [Header("Combat")]
        [Tooltip("Flat power value before coin rolls are applied.")]
        public int basePower = 5;

        [Tooltip("Number of coin rolls that can increase final clash power.")]
        [Min(0)]
        public int coinCount = 3;

        [Header("Affinity")]
        [Tooltip("Sin affinity used for future resonance and typing rules.")]
        public SinType sinType = SinType.Wrath;

        [Header("Status Effect (opsional, diterapkan saat serangan kena)")]
        [Tooltip("Efek yang diberikan saat damage berhasil masuk. None = tidak ada.")]
        public StatusEffectType inflictEffect = StatusEffectType.None;

        [Tooltip("Kekuatan efek: Bleed = damage per aksi, Fragile = % damage tambahan, Power = ± power roll.")]
        [Min(0)]
        public int inflictPotency;

        [Tooltip("Durasi efek dalam turn.")]
        [Min(1)]
        public int inflictDuration = 2;

        [Tooltip("True = efek diterapkan ke DIRI SENDIRI (buff), bukan ke target.")]
        public bool inflictOnSelf;

        [Header("Cost")]
        [Tooltip("Jumlah Sin yang dikonsumsi saat skill dipakai. 0 = gratis.")]
        [Min(0)]
        public int sinCost;

        [Header("Cooldown")]
        [Tooltip("Jumlah turn skill tidak bisa dipakai setelah digunakan. 0 = tanpa cooldown.")]
        [Min(0)]
        public int cooldownTurns;

        [Header("Defense Tuning (hanya dipakai sesuai kind)")]
        [Tooltip("Block: persen damage yang dikurangi dari serangan masuk.")]
        [Range(0, 100)]
        public int blockReductionPercent = 50;

        [Tooltip("Dodge: peluang (persen) menghindari serangan sepenuhnya.")]
        [Range(0, 100)]
        public int dodgeChancePercent = 50;
    }
}
