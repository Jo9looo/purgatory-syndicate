namespace PurgatorySyndicate.Battle
{
    /// <summary>
    /// Buff/debuff types. Potency and duration live on the StatusEffect instance.
    /// </summary>
    public enum StatusEffectType
    {
        /// <summary>No effect (skill does not inflict anything).</summary>
        None,

        /// <summary>Debuff: takes [potency] damage every time the unit attacks.</summary>
        Bleed,

        /// <summary>Debuff: incoming damage increased by [potency] percent.</summary>
        Fragile,

        /// <summary>Buff: +[potency] power on every clash/attack roll.</summary>
        PowerUp,

        /// <summary>Debuff: -[potency] power on every clash/attack roll.</summary>
        PowerDown
    }
}
