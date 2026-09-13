namespace PurgatorySyndicate.Battle
{
    /// <summary>
    /// What a skill does when used. Every character carries 3 Attack skills
    /// (slots 1-3) plus exactly one defensive skill (slot 4).
    /// </summary>
    public enum SkillKind
    {
        /// <summary>Normal offensive skill: clashes / deals damage.</summary>
        Attack,

        /// <summary>Defensive stance: reduces incoming damage this turn.</summary>
        Block,

        /// <summary>Defensive stance: chance to fully avoid incoming attacks this turn.</summary>
        Dodge,

        /// <summary>Defensive stance: strikes back at attackers after being hit this turn.</summary>
        Counter
    }
}
