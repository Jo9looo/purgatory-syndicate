namespace PurgatorySyndicate.Battle
{
    /// <summary>
    /// Immutable record of a registered combat action waiting in the turn queue.
    /// </summary>
    public readonly struct BattleAction
    {
        public BattleEntity Actor { get; }
        public BattleEntity Target { get; }
        public SkillData Skill { get; }

        public BattleAction(BattleEntity actor, BattleEntity target, SkillData skill)
        {
            Actor = actor;
            Target = target;
            Skill = skill;
        }

        public override string ToString()
        {
            string actorName = Actor != null ? Actor.DisplayName : "null";
            string targetName = Target != null ? Target.DisplayName : "null";
            string skillName = Skill != null ? Skill.skillName : "null";

            return $"{actorName} -> {targetName} [{skillName}] (Speed: {Actor?.CurrentSpeed ?? 0})";
        }
    }
}
