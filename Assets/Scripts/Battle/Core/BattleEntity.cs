using System.Collections.Generic;
using UnityEngine;

namespace PurgatorySyndicate.Battle
{
    /// <summary>
    /// Result of one damage application, so the manager can log what happened.
    /// </summary>
    public struct DamageResult
    {
        public int damageApplied;
        public bool becameStaggered;
        public bool died;
    }

    /// <summary>
    /// One active buff/debuff on a unit. Ticks down at the end of every turn.
    /// </summary>
    public class StatusEffect
    {
        public StatusEffectType type;
        public int potency;
        public int turnsRemaining;

        public override string ToString()
        {
            string potencyText = type == StatusEffectType.Fragile ? $"{potency}%" : potency.ToString();
            return $"{type} {potencyText} ({turnsRemaining}t)";
        }
    }

    /// <summary>
    /// Runtime combatant attached to scene capsules. Tracks mutable battle state
    /// (HP / Stagger / Speed / staggered flag) while referencing an immutable
    /// CharacterDefinition asset for stats, skills, and sin affinities.
    /// </summary>
    public class BattleEntity : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private CharacterDefinition definition;
        [SerializeField] private EntityTeam team = EntityTeam.Ally;

        [Header("Runtime State (Read-Only in Inspector)")]
        [SerializeField] private int currentHealth;
        [SerializeField] private int currentStagger;
        [SerializeField] private int currentSpeed;
        [SerializeField] private int currentSin;
        [SerializeField] private bool isStaggered;

        private static readonly List<SkillData> EmptySkills = new List<SkillData>();

        private Renderer bodyRenderer;
        private Color aliveColor = Color.white;
        private Quaternion aliveRotation = Quaternion.identity;
        private bool visualsCaptured;

        public CharacterDefinition Definition => definition;
        public EntityTeam Team => team;
        public IReadOnlyList<SkillData> Skills =>
            definition != null ? definition.skills : EmptySkills;

        /// <summary>Defensive skill in slot 4 (Block / Dodge / Counter), or null.</summary>
        public SkillData DefenseSkill => definition != null ? definition.defenseSkill : null;

        /// <summary>
        /// Defensive stance active for the current turn (set during Execution,
        /// cleared at the start of the next turn). Null = not defending.
        /// </summary>
        public SkillData ActiveDefense { get; set; }

        public int CurrentHealth => currentHealth;
        public int CurrentStagger => currentStagger;
        public int CurrentSpeed => currentSpeed;
        public int CurrentSin => currentSin;
        public bool IsStaggered => isStaggered;
        public bool IsAlive => currentHealth > 0;

        /// <summary>
        /// Set at turn start for units already staggered, so they only recover at the
        /// end of a FULL skipped turn (not the same turn they broke).
        /// </summary>
        public bool PendingStaggerRecovery { get; set; }

        private readonly List<StatusEffect> activeEffects = new List<StatusEffect>();
        private readonly Dictionary<SkillData, int> cooldowns = new Dictionary<SkillData, int>();

        /// <summary>Active buffs/debuffs (ticked down at every TurnEnd).</summary>
        public IReadOnlyList<StatusEffect> ActiveEffects => activeEffects;

        /// <summary>
        /// Adds/stacks a status effect: same type stacks potency, duration refreshes
        /// to the longest remaining value.
        /// </summary>
        public void AddStatusEffect(StatusEffectType type, int potency, int duration)
        {
            if (type == StatusEffectType.None || potency <= 0 || duration <= 0)
            {
                return;
            }

            foreach (StatusEffect effect in activeEffects)
            {
                if (effect.type == type)
                {
                    effect.potency += potency;
                    effect.turnsRemaining = Mathf.Max(effect.turnsRemaining, duration);
                    return;
                }
            }

            activeEffects.Add(new StatusEffect
            {
                type = type,
                potency = potency,
                turnsRemaining = duration
            });
        }

        /// <summary>Total potency of the given effect type (0 when not active).</summary>
        public int GetEffectPotency(StatusEffectType type)
        {
            int total = 0;
            foreach (StatusEffect effect in activeEffects)
            {
                if (effect.type == type)
                {
                    total += effect.potency;
                }
            }

            return total;
        }

        /// <summary>Called at TurnEnd: durations tick down, expired effects drop off.</summary>
        public void TickStatusEffects()
        {
            foreach (StatusEffect effect in activeEffects)
            {
                effect.turnsRemaining--;
            }

            activeEffects.RemoveAll(e => e.turnsRemaining <= 0);
        }

        // ---------------------------------------------------------------- Cooldowns

        /// <summary>Turns this skill is still unusable (0 = ready).</summary>
        public int GetCooldown(SkillData skill)
        {
            return skill != null && cooldowns.TryGetValue(skill, out int remaining) ? remaining : 0;
        }

        /// <summary>
        /// Called when the skill's attack actually resolves. Stored +1 because the
        /// tick at the next turn start eats one immediately.
        /// </summary>
        public void PutOnCooldown(SkillData skill)
        {
            if (skill != null && skill.cooldownTurns > 0)
            {
                cooldowns[skill] = skill.cooldownTurns + 1;
            }
        }

        /// <summary>Called at the start of every turn.</summary>
        public void TickCooldowns()
        {
            if (cooldowns.Count == 0)
            {
                return;
            }

            List<SkillData> keys = new List<SkillData>(cooldowns.Keys);
            foreach (SkillData skill in keys)
            {
                cooldowns[skill] = Mathf.Max(0, cooldowns[skill] - 1);
            }
        }

        public bool CanUseSkill(SkillData skill)
        {
            return skill != null && GetCooldown(skill) <= 0 && currentSin >= skill.sinCost;
        }

        public void SpendSin(int amount)
        {
            currentSin = Mathf.Max(0, currentSin - Mathf.Max(0, amount));
        }

        public void GainSin(int amount)
        {
            currentSin = Mathf.Min(MaxSin, currentSin + Mathf.Max(0, amount));
        }

        public int MaxHealth =>
            definition != null && definition.baseStats != null ? definition.baseStats.maxHealth : 1;

        public int MaxStagger =>
            definition != null && definition.baseStats != null ? definition.baseStats.maxStagger : 1;

        public int MaxSin =>
            definition != null && definition.baseStats != null ? definition.baseStats.maxSin : 6;

        public string DisplayName
        {
            get
            {
                if (definition != null && definition.baseStats != null)
                {
                    return definition.baseStats.characterName;
                }

                return gameObject.name;
            }
        }

        private void Awake()
        {
            // Scene-placed path: definition assigned in Inspector.
            // Bootstrap path: Configure() is called right after AddComponent instead.
            if (definition != null && !visualsCaptured)
            {
                CaptureVisualDefaults();
                InitializeFromBaseStats();
            }
        }

        /// <summary>
        /// Assigns data at runtime (used by BattleBootstrap after spawning the capsule).
        /// </summary>
        public void Configure(CharacterDefinition characterDefinition, EntityTeam entityTeam)
        {
            definition = characterDefinition;
            team = entityTeam;
            CaptureVisualDefaults();
            InitializeFromBaseStats();
        }

        private void CaptureVisualDefaults()
        {
            bodyRenderer = GetComponent<Renderer>();
            if (bodyRenderer != null)
            {
                aliveColor = bodyRenderer.material.color;
            }

            aliveRotation = transform.rotation;
            visualsCaptured = true;
        }

        /// <summary>
        /// Resets runtime values from the assigned definition.
        /// </summary>
        public void InitializeFromBaseStats()
        {
            if (definition == null || definition.baseStats == null)
            {
                Debug.LogWarning($"[BattleEntity] {gameObject.name} has no CharacterDefinition/stats assigned.");
                return;
            }

            currentHealth = definition.baseStats.maxHealth;
            currentStagger = definition.baseStats.maxStagger;
            currentSpeed = 0;
            currentSin = Mathf.Min(1, MaxSin);
            isStaggered = false;
            ActiveDefense = null;
            activeEffects.Clear();
            cooldowns.Clear();

            Debug.Log(
                $"[BattleEntity] Initialized {DisplayName} ({team}) | HP {currentHealth}/{MaxHealth} | " +
                $"Stagger {currentStagger}/{MaxStagger}");
        }

        /// <summary>
        /// True if this character has affinity with the given sin (grants clash power bonus).
        /// </summary>
        public bool HasAffinity(SinType sin)
        {
            return definition != null && definition.sinAffinities.Contains(sin);
        }

        /// <summary>
        /// Rolls a new speed value using the character's configured speed range.
        /// </summary>
        public void RollSpeed()
        {
            if (definition == null || definition.baseStats == null)
            {
                return;
            }

            int min = definition.baseStats.speedRange.x;
            int max = definition.baseStats.speedRange.y;

            if (min > max)
            {
                (min, max) = (max, min);
            }

            // Random.Range int overload: max is exclusive, so +1 for inclusive upper bound.
            currentSpeed = Random.Range(min, max + 1);
        }

        /// <summary>
        /// Applies damage to HP and Stagger.
        /// Rules: staggered units take x2 HP damage; stagger reaching 0 triggers Staggered state.
        /// </summary>
        public DamageResult TakeDamage(int amount)
        {
            DamageResult result = new DamageResult();

            if (amount <= 0 || !IsAlive)
            {
                return result;
            }

            int finalAmount = isStaggered ? amount * 2 : amount;
            currentHealth = Mathf.Max(0, currentHealth - finalAmount);
            result.damageApplied = finalAmount;

            // Stagger only depletes while not already staggered (1:1 with incoming raw damage).
            if (!isStaggered)
            {
                currentStagger = Mathf.Max(0, currentStagger - amount);
                if (currentStagger == 0 && currentHealth > 0)
                {
                    isStaggered = true;
                    result.becameStaggered = true;
                }
            }

            if (currentHealth == 0)
            {
                result.died = true;
                Die();
            }

            return result;
        }

        /// <summary>
        /// Called at TurnEnd: staggered unit recovers with a full stagger bar.
        /// </summary>
        public void RecoverFromStagger()
        {
            isStaggered = false;
            currentStagger = MaxStagger;
            PendingStaggerRecovery = false;
        }

        private void Die()
        {
            isStaggered = false;

            // Whitebox death visual: capsule tips over and turns gray.
            transform.rotation = aliveRotation * Quaternion.Euler(0f, 0f, 90f);
            if (bodyRenderer != null)
            {
                bodyRenderer.material.color = new Color(0.3f, 0.3f, 0.3f);
            }

            Debug.Log($"[BattleEntity] {DisplayName} has been defeated.");
        }

        /// <summary>
        /// Full reset for battle restart: restores visuals and re-initializes stats.
        /// </summary>
        public void ResetForBattle()
        {
            transform.rotation = aliveRotation;
            transform.localScale = Vector3.one;
            PendingStaggerRecovery = false;
            if (bodyRenderer != null)
            {
                bodyRenderer.material.color = aliveColor;
            }

            InitializeFromBaseStats();
        }
    }
}
