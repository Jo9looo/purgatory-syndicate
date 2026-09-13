using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PurgatorySyndicate.Battle
{
    /// <summary>
    /// One ally's pending order during the Targeting phase (drag & drop model).
    /// Created when the player drops an arrow on an enemy or picks a skill.
    /// </summary>
    public class PendingAssignment
    {
        public int skillIndex;
        public BattleEntity target;

        /// <summary>True = use the defensive skill (slot 4); target is the ally itself.</summary>
        public bool isDefense;
    }

    /// <summary>
    /// Drives the Targeting phase with the drag-targeting model:
    /// - Player drags from an ally to an enemy (BattleMouseController) → SetTarget.
    /// - Keys 1/2/3 change the skill of the hovered/dragged ally → SetSkill.
    /// - Assignments can be re-dragged to a new enemy any time before confirming.
    /// - When ALL ready allies have targets, Enter → ConfirmAll registers the actions,
    ///   simple AI assigns enemy actions, and control returns to BattleManager.
    /// Assignments stay readable after confirm so the arrows persist until TurnEnd.
    /// </summary>
    public class TargetingController : MonoBehaviour
    {
        private BattleManager manager;
        private readonly List<BattleEntity> readyAllies = new List<BattleEntity>();
        private readonly Dictionary<BattleEntity, PendingAssignment> assignments =
            new Dictionary<BattleEntity, PendingAssignment>();

        /// <summary>True while the player is still allowed to (re)assign actions.</summary>
        public bool IsActive { get; private set; }

        public IReadOnlyList<BattleEntity> ReadyAllies => readyAllies;
        public IReadOnlyDictionary<BattleEntity, PendingAssignment> Assignments => assignments;

        public void BeginTargeting(BattleManager battleManager)
        {
            manager = battleManager;

            readyAllies.Clear();
            readyAllies.AddRange(manager.Combatants.Where(c =>
                c != null && c.IsAlive && !c.IsStaggered && c.Team == EntityTeam.Ally));

            assignments.Clear();

            if (readyAllies.Count == 0)
            {
                // All allies dead or staggered: straight to AI + execution.
                FinishTargeting();
                return;
            }

            IsActive = true;
        }

        /// <summary>Can this ally receive orders right now (drag source validity)?</summary>
        public bool CanCommand(BattleEntity ally)
        {
            return IsActive && ally != null && readyAllies.Contains(ally);
        }

        public PendingAssignment GetAssignment(BattleEntity ally)
        {
            return ally != null && assignments.TryGetValue(ally, out PendingAssignment a) ? a : null;
        }

        /// <summary>Called by BattleMouseController when an arrow is dropped on an enemy.</summary>
        public void SetTarget(BattleEntity ally, BattleEntity target)
        {
            if (!CanCommand(ally) || target == null || !target.IsAlive || target.Team == ally.Team)
            {
                return;
            }

            PendingAssignment assignment = GetOrCreate(ally);
            assignment.target = target;
            assignment.isDefense = false;
            Debug.Log($"[Targeting] {ally.DisplayName} membidik {target.DisplayName}");
        }

        /// <summary>Keys 1/2/3 (or skill bar click) on the hovered or dragged ally.</summary>
        public void SetSkill(BattleEntity ally, int index)
        {
            if (!CanCommand(ally) || index < 0 || index >= ally.Skills.Count)
            {
                return;
            }

            SkillData skill = ally.Skills[index];
            if (!ally.CanUseSkill(skill))
            {
                Debug.Log($"[Targeting] {ally.DisplayName} tidak bisa memakai [{skill.skillName}] " +
                          $"(CD {ally.GetCooldown(skill)} / Sin {ally.CurrentSin}/{skill.sinCost}).");
                return;
            }

            PendingAssignment assignment = GetOrCreate(ally);
            assignment.skillIndex = index;

            // Switching back to an attack skill cancels a queued defensive stance.
            if (assignment.isDefense)
            {
                assignment.isDefense = false;
                assignment.target = null;
            }
        }

        /// <summary>
        /// Key 4 (or skill bar click): queue the ally's defensive skill (Block/Dodge/Counter).
        /// Self-targeted, so no arrow drag is needed.
        /// </summary>
        public void SetDefense(BattleEntity ally)
        {
            if (!CanCommand(ally) || ally.DefenseSkill == null || !ally.CanUseSkill(ally.DefenseSkill))
            {
                return;
            }

            PendingAssignment assignment = GetOrCreate(ally);
            assignment.isDefense = true;
            assignment.target = ally;
            Debug.Log($"[Targeting] {ally.DisplayName} bersiap bertahan [{ally.DefenseSkill.skillName}]");
        }

        private PendingAssignment GetOrCreate(BattleEntity ally)
        {
            if (!assignments.TryGetValue(ally, out PendingAssignment assignment))
            {
                assignment = new PendingAssignment();
                assignments[ally] = assignment;
            }

            return assignment;
        }

        /// <summary>Every ready ally has a target locked in with a usable skill.</summary>
        public bool AllAssigned =>
            readyAllies.Count > 0 && readyAllies.All(IsAssignmentReady);

        public int AssignedCount => readyAllies.Count(IsAssignmentReady);

        private bool IsAssignmentReady(BattleEntity ally)
        {
            if (!assignments.TryGetValue(ally, out PendingAssignment p) || p.target == null)
            {
                return false;
            }

            SkillData skill = p.isDefense
                ? ally.DefenseSkill
                : (p.skillIndex >= 0 && p.skillIndex < ally.Skills.Count ? ally.Skills[p.skillIndex] : null);
            return ally.CanUseSkill(skill);
        }

        /// <summary>Enter pressed: lock all assignments, let AI act, hand off to the manager.</summary>
        public void ConfirmAll()
        {
            if (!IsActive || !AllAssigned)
            {
                return;
            }

            foreach (BattleEntity ally in readyAllies)
            {
                PendingAssignment a = assignments[ally];
                if (a.isDefense)
                {
                    manager.RegisterAction(ally, ally, ally.DefenseSkill);
                }
                else
                {
                    int skillIndex = Mathf.Clamp(a.skillIndex, 0, ally.Skills.Count - 1);
                    manager.RegisterAction(ally, a.target, ally.Skills[skillIndex]);
                }
            }

            FinishTargeting();
        }

        private void FinishTargeting()
        {
            IsActive = false;
            AssignEnemyActions();
            manager.OnTargetingComplete();
        }

        /// <summary>
        /// Rule-based enemy AI:
        /// - Defend when own stagger is critical and a defense skill is usable.
        /// - Hunt the lowest-HP living ally.
        /// - Prefer affordable, off-cooldown skills that are EFEKTIF vs the target,
        ///   then affinity-matching skills, then the highest base power.
        /// </summary>
        private void AssignEnemyActions()
        {
            List<BattleEntity> livingAllies = manager.Combatants
                .Where(c => c != null && c.IsAlive && c.Team == EntityTeam.Ally)
                .ToList();

            IEnumerable<BattleEntity> readyEnemies = manager.Combatants.Where(c =>
                c != null && c.IsAlive && !c.IsStaggered && c.Team == EntityTeam.Enemy);

            foreach (BattleEntity enemy in readyEnemies)
            {
                if (livingAllies.Count == 0)
                {
                    continue;
                }

                bool staggerCritical = enemy.MaxStagger > 0 &&
                                       enemy.CurrentStagger <= enemy.MaxStagger * 0.3f;
                if (staggerCritical && enemy.DefenseSkill != null && enemy.CanUseSkill(enemy.DefenseSkill))
                {
                    manager.RegisterAction(enemy, enemy, enemy.DefenseSkill);
                    continue;
                }

                BattleEntity target = livingAllies
                    .OrderBy(a => a.CurrentHealth)
                    .ThenBy(a => a.CurrentStagger)
                    .First();

                SkillData skill = PickEnemySkill(enemy, target);
                if (skill != null)
                {
                    manager.RegisterAction(enemy, target, skill);
                }
                else if (enemy.DefenseSkill != null && enemy.CanUseSkill(enemy.DefenseSkill))
                {
                    manager.RegisterAction(enemy, enemy, enemy.DefenseSkill);
                }
            }
        }

        private static SkillData PickEnemySkill(BattleEntity enemy, BattleEntity target)
        {
            IReadOnlyList<SinType> affinities = target.Definition != null
                ? target.Definition.sinAffinities
                : (IReadOnlyList<SinType>)new List<SinType>();

            SkillData best = null;
            int bestScore = int.MinValue;

            foreach (SkillData skill in enemy.Skills)
            {
                if (!enemy.CanUseSkill(skill))
                {
                    continue;
                }

                int score = skill.basePower + skill.coinCount;
                if (enemy.HasAffinity(skill.sinType))
                {
                    score += 4;
                }

                int sinPercent = SinRelations.GetDamagePercent(skill.sinType, affinities);
                if (sinPercent > 100)
                {
                    score += 8;
                }
                else if (sinPercent < 100)
                {
                    score -= 4;
                }

                if (skill.inflictEffect != StatusEffectType.None)
                {
                    score += 2;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = skill;
                }
            }

            return best;
        }
    }
}
