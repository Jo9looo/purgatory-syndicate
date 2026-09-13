using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PurgatorySyndicate.Battle
{
    /// <summary>
    /// One floating combat text entry (damage numbers, power rolls, status shouts).
    /// Rendered by BattleHUD; stored here so battle logic stays UI-agnostic.
    /// </summary>
    public struct FloatingText
    {
        public Vector3 worldPosition;
        public string text;
        public Color color;
        public float spawnTime;
        public float size;
    }

    /// <summary>
    /// Central battle orchestrator. Owns the turn state machine, action queue,
    /// per-coin clash resolution, damage/stagger application, win/lose checks,
    /// and drives whitebox attack animations (lunge/shake/knockback) during execution.
    ///
    /// Turn flow:
    ///   RollSpeed -> Targeting (player + AI pick actions) -> ActionSorting (press Enter)
    ///   -> Execution (coroutine, resolves clashes) -> TurnEnd -> next turn / BattleOver
    /// </summary>
    [RequireComponent(typeof(TargetingController))]
    public class BattleManager : MonoBehaviour
    {
        /// <summary>Flat power bonus when a skill's sin matches the actor's affinity.</summary>
        public const int SinAffinityBonus = 2;

        private const float FloatingTextLifetime = 1.5f;

        [Header("Pacing")]
        [Tooltip("Delay between each resolved action so animations/logs are readable.")]
        [SerializeField] private float actionDelay = 0.65f;

        [Tooltip("Delay after TurnEnd before the next turn starts.")]
        [SerializeField] private float turnEndDelay = 1.2f;

        private BattleState currentState = BattleState.RollSpeed;
        private readonly List<BattleEntity> combatants = new List<BattleEntity>();
        private readonly List<BattleAction> actionQueue = new List<BattleAction>();
        private readonly List<string> battleLog = new List<string>();
        private readonly List<FloatingText> floatingTexts = new List<FloatingText>();
        private readonly Dictionary<(EntityTeam team, SinType sin), int> resonanceCounts =
            new Dictionary<(EntityTeam, SinType), int>();
        private TargetingController targeting;
        private int turnNumber;

        /// <summary>Index in ActionQueue of the action currently resolving (-1 = none).</summary>
        public int CurrentActionIndex { get; private set; } = -1;

        public BattleState CurrentState => currentState;
        public IReadOnlyList<BattleEntity> Combatants => combatants;
        public IReadOnlyList<BattleAction> ActionQueue => actionQueue;
        public IReadOnlyList<string> LogLines => battleLog;
        public IReadOnlyList<FloatingText> FloatingTexts => floatingTexts;
        public int TurnNumber => turnNumber;
        public string BattleResult { get; private set; } = "";
        public string EncounterName { get; private set; } = "";
        public bool CanAdvanceStage =>
            currentState == BattleState.BattleOver &&
            BattleResult == "MENANG!" &&
            BattleProgression.HasNextStage;

        /// <summary>Large center-screen announcement text (TURN X, CLASH!, MENANG!).</summary>
        public string BannerText { get; private set; } = "";
        public float BannerSpawnTime { get; private set; }

        private void Awake()
        {
            targeting = GetComponent<TargetingController>();
        }

        private void Start()
        {
            // Fallback for scene-placed setups; bootstrap normally registers combatants.
            if (combatants.Count == 0)
            {
                combatants.AddRange(FindObjectsByType<BattleEntity>(FindObjectsSortMode.None));
            }

            if (combatants.Count < 2)
            {
                AddLog("Butuh minimal 2 BattleEntity untuk memulai battle.");
                return;
            }

            BeginNewTurn();
        }

        private void Update()
        {
            floatingTexts.RemoveAll(f => Time.time - f.spawnTime > FloatingTextLifetime);
        }

        /// <summary>
        /// Called by BattleBootstrap so the manager knows its combatants before Start().
        /// </summary>
        public void RegisterCombatants(IEnumerable<BattleEntity> entities, string encounterName = "")
        {
            combatants.Clear();
            combatants.AddRange(entities);
            EncounterName = encounterName ?? "";
        }

        // ---------------------------------------------------------------- Turn flow

        public void BeginNewTurn()
        {
            turnNumber++;
            actionQueue.Clear();

            SetState(BattleState.RollSpeed);
            AddLog($"===== TURN {turnNumber} =====");
            ShowBanner($"TURN {turnNumber}");

            CurrentActionIndex = -1;

            foreach (BattleEntity entity in combatants.Where(c => c != null && c.IsAlive))
            {
                // Defensive stances only last one turn; cooldowns tick down.
                entity.ActiveDefense = null;
                entity.TickCooldowns();
                entity.GainSin(1);

                if (entity.IsStaggered)
                {
                    // Mark for recovery at THIS turn's end: the unit skips one full turn.
                    entity.PendingStaggerRecovery = true;
                    AddLog($"{entity.DisplayName} STAGGERED — melewati turn ini (pulih di akhir turn).");
                    continue;
                }

                entity.RollSpeed();
                AddLog($"{entity.DisplayName} roll Speed: {entity.CurrentSpeed}");
            }

            SetState(BattleState.Targeting);
            targeting.BeginTargeting(this);
        }

        /// <summary>
        /// Registers an entity's intended action for the current turn.
        /// </summary>
        public void RegisterAction(BattleEntity actor, BattleEntity target, SkillData skill)
        {
            if (actor == null || target == null || skill == null)
            {
                Debug.LogWarning("[BattleManager] RegisterAction called with null argument(s). Ignored.");
                return;
            }

            actionQueue.Add(new BattleAction(actor, target, skill));
            AddLog(actor == target
                ? $"{actor.DisplayName} siap bertahan: [{skill.skillName}]"
                : $"{actor.DisplayName} siap: [{skill.skillName}] ke {target.DisplayName}");
        }

        /// <summary>
        /// Called by TargetingController when every unit has an action assigned.
        /// Sorts the queue and waits for the player to confirm execution.
        /// </summary>
        public void OnTargetingComplete()
        {
            SetState(BattleState.ActionSorting);
            SortActionQueue();

            AddLog("--- TIMELINE AKSI ---");
            for (int i = 0; i < actionQueue.Count; i++)
            {
                BattleAction a = actionQueue[i];
                AddLog(a.Actor == a.Target
                    ? $"  {i + 1}. {a.Actor.DisplayName} (Spd {a.Actor.CurrentSpeed}) [{a.Skill.skillName}] (bertahan)"
                    : $"  {i + 1}. {a.Actor.DisplayName} (Spd {a.Actor.CurrentSpeed}) -> " +
                      $"{a.Target.DisplayName} [{a.Skill.skillName}]");
            }

            AddLog("Tekan ENTER / SPACE untuk eksekusi.");
        }

        /// <summary>
        /// Sorts by speed (descending). Ties: Ally acts before Enemy, then by name.
        /// </summary>
        public void SortActionQueue()
        {
            actionQueue.Sort((a, b) =>
            {
                int speedCompare = b.Actor.CurrentSpeed.CompareTo(a.Actor.CurrentSpeed);
                if (speedCompare != 0)
                {
                    return speedCompare;
                }

                int teamCompare = a.Actor.Team.CompareTo(b.Actor.Team); // Ally (0) first
                return teamCompare != 0
                    ? teamCompare
                    : string.Compare(a.Actor.DisplayName, b.Actor.DisplayName, System.StringComparison.Ordinal);
            });
        }

        /// <summary>
        /// Player pressed Enter during ActionSorting: run the execution coroutine.
        /// </summary>
        public void RequestExecution()
        {
            if (currentState != BattleState.ActionSorting)
            {
                return;
            }

            StartCoroutine(ExecuteTurnRoutine());
        }

        private IEnumerator ExecuteTurnRoutine()
        {
            SetState(BattleState.Execution);

            HashSet<BattleEntity> acted = new HashSet<BattleEntity>();
            bool battleEnded = false;

            ComputeResonance();

            // ---- Defensive stances activate FIRST (before any attack lands) ----
            foreach (BattleAction action in actionQueue.Where(a => a.Skill.kind != SkillKind.Attack))
            {
                BattleEntity defender = action.Actor;
                if (defender == null || !defender.IsAlive || defender.IsStaggered)
                {
                    continue;
                }

                CurrentActionIndex = actionQueue.IndexOf(action);
                defender.ActiveDefense = action.Skill;
                defender.SpendSin(action.Skill.sinCost);
                defender.PutOnCooldown(action.Skill);
                acted.Add(defender);

                string stanceName = action.Skill.kind.ToString().ToUpperInvariant();
                AddLog($"{defender.DisplayName} memasang {stanceName} [{action.Skill.skillName}]!");
                SpawnFloatingText(defender.transform.position + Vector3.up * 2.1f,
                    $"{stanceName}!", new Color(0.35f, 0.75f, 1f), 1.2f);
                PulseEntity(defender, new Color(0.35f, 0.75f, 1f));
                yield return new WaitForSeconds(0.35f);
            }

            foreach (BattleAction action in actionQueue)
            {
                // Defensive actions were already processed above.
                if (action.Skill.kind != SkillKind.Attack)
                {
                    continue;
                }

                BattleEntity actor = action.Actor;

                if (actor == null || !actor.IsAlive)
                {
                    AddLog($"Aksi dibatalkan — {(actor != null ? actor.DisplayName : "unit")} sudah gugur.");
                    continue;
                }

                if (actor.IsStaggered)
                {
                    AddLog($"Aksi {actor.DisplayName} batal — staggered di tengah turn.");
                    continue;
                }

                if (acted.Contains(actor))
                {
                    continue;
                }

                CurrentActionIndex = actionQueue.IndexOf(action);

                // BLEED: attacking while bleeding hurts, and can even break the unit.
                int bleed = actor.GetEffectPotency(StatusEffectType.Bleed);
                if (bleed > 0)
                {
                    DamageResult bleedResult = actor.TakeDamage(bleed);
                    AddLog($"{actor.DisplayName} berdarah saat beraksi — {bleedResult.damageApplied} damage (Bleed).");
                    SpawnFloatingText(actor.transform.position + Vector3.up * 2.3f,
                        $"BLEED -{bleedResult.damageApplied}", new Color(0.85f, 0.1f, 0.1f), 1.1f);

                    if (bleedResult.died)
                    {
                        AddLog($"  >>> {actor.DisplayName} GUGUR karena Bleed!");
                        if (CheckBattleEnd())
                        {
                            battleEnded = true;
                            break;
                        }

                        continue;
                    }

                    if (bleedResult.becameStaggered)
                    {
                        AddLog($"  >>> {actor.DisplayName} STAGGERED karena Bleed — aksi batal!");
                        SpawnFloatingText(actor.transform.position + Vector3.up * 2.7f,
                            "STAGGER BREAK!", new Color(1f, 0.6f, 0.1f), 1.1f);
                        continue;
                    }
                }

                // Retarget when the original target died earlier this turn.
                BattleEntity target = action.Target;
                bool redirected = false;
                if (target == null || !target.IsAlive)
                {
                    target = FindRedirectTarget(actor);
                    if (target == null)
                    {
                        continue; // no living opponents; end check below will fire
                    }

                    redirected = true;
                    AddLog($"{actor.DisplayName} mengalihkan serangan ke {target.DisplayName}.");
                }

                BattleAction? counter = redirected ? null : FindCounterAction(action, acted);

                if (counter.HasValue)
                {
                    actor.SpendSin(action.Skill.sinCost);
                    counter.Value.Actor.SpendSin(counter.Value.Skill.sinCost);
                    yield return ResolveClashRoutine(action, counter.Value);
                    acted.Add(actor);
                    acted.Add(counter.Value.Actor);
                    actor.PutOnCooldown(action.Skill);
                    counter.Value.Actor.PutOnCooldown(counter.Value.Skill);
                }
                else
                {
                    actor.SpendSin(action.Skill.sinCost);
                    yield return ResolveOneSidedRoutine(actor, target, action.Skill);
                    acted.Add(actor);
                    actor.PutOnCooldown(action.Skill);
                }

                yield return new WaitForSeconds(actionDelay);

                if (CheckBattleEnd())
                {
                    battleEnded = true;
                    break;
                }
            }

            if (battleEnded)
            {
                yield break;
            }

            // ---- Turn End ----
            CurrentActionIndex = -1;
            SetState(BattleState.TurnEnd);

            // Buff/debuff durations tick down at the end of every turn.
            foreach (BattleEntity entity in combatants.Where(c => c != null && c.IsAlive))
            {
                entity.TickStatusEffects();
            }

            // Only units staggered since the START of this turn recover here,
            // so a stagger break always costs one full turn.
            foreach (BattleEntity entity in combatants.Where(c =>
                         c != null && c.IsAlive && c.IsStaggered && c.PendingStaggerRecovery))
            {
                entity.RecoverFromStagger();
                AddLog($"{entity.DisplayName} pulih dari stagger (Stagger {entity.CurrentStagger}/{entity.MaxStagger}).");
                SpawnFloatingText(entity.transform.position + Vector3.up * 2.1f,
                    "PULIH", new Color(0.4f, 0.9f, 1f), 1f);
            }

            yield return new WaitForSeconds(turnEndDelay);
            BeginNewTurn();
        }

        // ---------------------------------------------------------------- Resolution

        /// <summary>
        /// Finds an opposing pending action where the target fights back (mutual targeting).
        /// </summary>
        private BattleAction? FindCounterAction(BattleAction incoming, HashSet<BattleEntity> acted)
        {
            foreach (BattleAction candidate in actionQueue)
            {
                if (candidate.Actor == incoming.Target &&
                    candidate.Target == incoming.Actor &&
                    candidate.Actor != null &&
                    candidate.Actor.IsAlive &&
                    !candidate.Actor.IsStaggered &&
                    !acted.Contains(candidate.Actor))
                {
                    return candidate;
                }
            }

            return null;
        }

        private BattleEntity FindRedirectTarget(BattleEntity actor)
        {
            List<BattleEntity> candidates = combatants
                .Where(c => c != null && c.IsAlive && c.Team != actor.Team)
                .ToList();

            return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
        }

        /// <summary>
        /// Multi-round clash (ala Limbus): both units lunge to the midpoint, then trade
        /// power rolls round after round. Each round both sides roll with their REMAINING
        /// coins; the round loser loses one coin. When one side runs out of coins, the
        /// winner lands a final attack rolled with their remaining coins.
        /// </summary>
        private IEnumerator ResolveClashRoutine(BattleAction attack, BattleAction counter)
        {
            BattleEntity unitA = attack.Actor;
            BattleEntity unitB = counter.Actor;

            AddLog($"CLASH! {unitA.DisplayName} [{attack.Skill.skillName}] vs " +
                   $"{unitB.DisplayName} [{counter.Skill.skillName}]");
            ShowBanner("CLASH!");

            BattleEntityMotion motionA = GetMotion(unitA);
            BattleEntityMotion motionB = GetMotion(unitB);
            Vector3 midpoint = (unitA.transform.position + unitB.transform.position) * 0.5f;

            // Both units dash toward each other.
            StartCoroutine(motionB.LungeToward(midpoint, 0.75f));
            yield return motionA.LungeToward(midpoint, 0.75f);

            Color gold = new Color(1f, 0.85f, 0.2f);
            Color gray = new Color(0.7f, 0.7f, 0.7f);

            int coinsA = Mathf.Max(1, attack.Skill.coinCount);
            int coinsB = Mathf.Max(1, counter.Skill.coinCount);
            const int maxRounds = 12; // safety cap against endless draw streaks
            int round = 0;

            while (coinsA > 0 && coinsB > 0 && round < maxRounds)
            {
                round++;
                AddLog($"  -- Ronde {round} (koin: {coinsA} vs {coinsB}) --");

                int powerA = RollPower(unitA, attack.Skill, coinsA);
                int powerB = RollPower(unitB, counter.Skill, coinsB);

                SpawnFloatingText(unitA.transform.position + Vector3.up * 2.1f,
                    powerA.ToString(), powerA >= powerB ? gold : gray, 1.2f);
                SpawnFloatingText(unitB.transform.position + Vector3.up * 2.1f,
                    powerB.ToString(), powerB >= powerA ? gold : gray, 1.2f);

                if (powerA > powerB)
                {
                    coinsB--;
                    AddLog($"  {unitA.DisplayName} menang ronde — " +
                           $"{unitB.DisplayName} kehilangan koin (sisa {coinsB}).");
                    PulseEntity(unitA, gold);
                    StartCoroutine(GetMotion(unitB).HitShake());
                }
                else if (powerB > powerA)
                {
                    coinsA--;
                    AddLog($"  {unitB.DisplayName} menang ronde — " +
                           $"{unitA.DisplayName} kehilangan koin (sisa {coinsA}).");
                    PulseEntity(unitB, gold);
                    StartCoroutine(GetMotion(unitA).HitShake());
                }
                else
                {
                    AddLog($"  Seri ({powerA} vs {powerB}) — ronde diulang.");
                }

                yield return new WaitForSeconds(0.5f);
            }

            // Cap reached with both still holding coins: the clash fizzles out.
            if (coinsA > 0 && coinsB > 0)
            {
                AddLog($"  Clash buntu setelah {round} ronde — kedua serangan saling meniadakan.");
                StartCoroutine(motionB.ReturnHome());
                yield return motionA.ReturnHome();
                yield break;
            }

            bool aWins = coinsA > 0;
            BattleEntity winner = aWins ? unitA : unitB;
            BattleEntity loser = aWins ? unitB : unitA;
            SkillData winnerSkill = aWins ? attack.Skill : counter.Skill;
            int remainingCoins = aWins ? coinsA : coinsB;

            AddLog($"  {winner.DisplayName} MENANG clash setelah {round} ronde " +
                   $"(sisa {remainingCoins} koin) — serangan penuh!");

            int finalPower = RollPower(winner, winnerSkill, remainingCoins);
            SpawnFloatingText(winner.transform.position + Vector3.up * 2.4f,
                finalPower.ToString(), gold, 1.4f);

            PulseEntity(winner, gold);
            bool landed = ApplyDamage(winner, loser, finalPower, winnerSkill.sinType);
            if (landed)
            {
                ApplySkillEffect(winner, loser, winnerSkill);
            }

            if (loser.IsAlive)
            {
                PulseEntity(loser, new Color(0.45f, 0.45f, 0.45f));
            }

            yield return GetMotion(loser).KnockbackFrom(winner.transform.position);

            StartCoroutine(motionB.ReturnHome());
            yield return motionA.ReturnHome();
        }

        /// <summary>
        /// Unopposed attack with animation: attacker lunges in, target takes the full
        /// rolled power and shakes, attacker returns home.
        /// </summary>
        private IEnumerator ResolveOneSidedRoutine(BattleEntity actor, BattleEntity target, SkillData skill)
        {
            AddLog($"{actor.DisplayName} menyerang {target.DisplayName} dengan [{skill.skillName}] (tanpa clash).");

            int power = RollFinalPower(actor, skill);

            BattleEntityMotion actorMotion = GetMotion(actor);
            yield return actorMotion.LungeToward(target.transform.position, 0.6f);

            SpawnFloatingText(actor.transform.position + Vector3.up * 2.1f,
                power.ToString(), new Color(1f, 0.85f, 0.2f), 1.1f);

            PulseEntity(actor, new Color(1f, 0.85f, 0.2f));
            bool landed = ApplyDamage(actor, target, power, skill.sinType);
            if (landed)
            {
                ApplySkillEffect(actor, target, skill);
            }

            if (target.IsAlive)
            {
                PulseEntity(target, new Color(0.9f, 0.3f, 0.3f));
            }

            StartCoroutine(GetMotion(target).HitShake());
            yield return actorMotion.ReturnHome();
        }

        /// <summary>
        /// Per-coin power roll with the skill's full coin count.
        /// </summary>
        private int RollFinalPower(BattleEntity actor, SkillData skill)
        {
            return RollPower(actor, skill, skill.coinCount);
        }

        /// <summary>
        /// Per-coin power roll: basePower + 1 per head + sin affinity bonus.
        /// Coin count can be overridden (multi-round clashes burn coins per round).
        /// </summary>
        private int RollPower(BattleEntity actor, SkillData skill, int coinCount)
        {
            int heads = 0;
            StringBuilder coins = new StringBuilder();

            for (int i = 0; i < coinCount; i++)
            {
                bool isHead = Random.Range(0, 2) == 1;
                if (isHead)
                {
                    heads++;
                }

                coins.Append(isHead ? "H" : "T").Append(' ');
            }

            int sinBonus = actor.HasAffinity(skill.sinType) ? SinAffinityBonus : 0;
            int powerUp = actor.GetEffectPotency(StatusEffectType.PowerUp);
            int powerDown = actor.GetEffectPotency(StatusEffectType.PowerDown);
            int resonance = GetResonanceBonus(actor.Team, skill.sinType);

            int finalPower = Mathf.Max(0,
                skill.basePower + heads + sinBonus + powerUp - powerDown + resonance);

            StringBuilder detail = new StringBuilder();
            detail.Append($"{skill.basePower} + {heads}");
            if (sinBonus > 0) detail.Append($" + {sinBonus} sin({skill.sinType})");
            if (resonance > 0) detail.Append($" + {resonance} resonance");
            if (powerUp > 0) detail.Append($" + {powerUp} PowerUp");
            if (powerDown > 0) detail.Append($" - {powerDown} PowerDown");

            AddLog($"  {actor.DisplayName} koin: {coins.ToString().Trim()} | {detail} = {finalPower}");
            return finalPower;
        }

        // ---------------------------------------------------------------- Resonance

        /// <summary>
        /// Counts attack actions per (team, sin) at execution start. Two or more
        /// units on the same team attacking with the same sin = resonance bonus.
        /// </summary>
        private void ComputeResonance()
        {
            resonanceCounts.Clear();

            foreach (BattleAction action in actionQueue)
            {
                if (action.Skill.kind != SkillKind.Attack || action.Actor == null)
                {
                    continue;
                }

                var key = (action.Actor.Team, action.Skill.sinType);
                resonanceCounts.TryGetValue(key, out int count);
                resonanceCounts[key] = count + 1;
            }

            foreach (KeyValuePair<(EntityTeam team, SinType sin), int> pair in resonanceCounts)
            {
                if (pair.Value >= 2)
                {
                    AddLog($"RESONANCE {pair.Key.sin} x{pair.Value} ({pair.Key.team}) — " +
                           $"+{pair.Value - 1} power untuk semua aksi {pair.Key.sin} tim itu!");
                }
            }
        }

        /// <summary>+1 power per extra same-sin attacker on the same team.</summary>
        private int GetResonanceBonus(EntityTeam team, SinType sin)
        {
            return resonanceCounts.TryGetValue((team, sin), out int count) && count >= 2
                ? count - 1
                : 0;
        }

        /// <summary>
        /// Applies damage with all modifiers factored in:
        /// sin weakness (efektif/tahan), Fragile debuff, then the target's defensive
        /// stance (Dodge = avoid entirely, Block = reduce, Counter = strike back).
        /// Returns true when the hit landed (false = dodged), so callers know
        /// whether to apply on-hit status effects.
        /// </summary>
        private bool ApplyDamage(
            BattleEntity attacker,
            BattleEntity target,
            int amount,
            SinType attackSin,
            bool allowCounter = true)
        {
            SkillData defense = target.ActiveDefense;
            Vector3 popupBase = target.transform.position + Vector3.up * 2.3f;

            // DODGE: roll to avoid the hit entirely.
            if (defense != null && defense.kind == SkillKind.Dodge &&
                Random.Range(0, 100) < defense.dodgeChancePercent)
            {
                AddLog($"  {target.DisplayName} DODGE! Serangan luput (peluang {defense.dodgeChancePercent}%).");
                SpawnFloatingText(popupBase, "DODGE!", new Color(0.4f, 1f, 0.6f), 1.3f);
                return false;
            }

            // SIN WEAKNESS: attack sin vs the target's affinities.
            IReadOnlyList<SinType> affinities = target.Definition != null
                ? target.Definition.sinAffinities
                : (IReadOnlyList<SinType>)new List<SinType>();
            int sinPercent = SinRelations.GetDamagePercent(attackSin, affinities);
            if (sinPercent != 100)
            {
                int modified = amount * sinPercent / 100;
                if (sinPercent > 100)
                {
                    AddLog($"  EFEKTIF! {attackSin} unggul atas affinity {target.DisplayName} " +
                           $"— damage {amount} -> {modified} (+50%).");
                    SpawnFloatingText(popupBase + Vector3.up * 0.8f, "EFEKTIF!",
                        new Color(1f, 0.4f, 0.1f), 1.2f);
                }
                else
                {
                    AddLog($"  TAHAN. Affinity {target.DisplayName} meredam {attackSin} " +
                           $"— damage {amount} -> {modified} (-25%).");
                    SpawnFloatingText(popupBase + Vector3.up * 0.8f, "TAHAN",
                        new Color(0.6f, 0.6f, 0.9f), 1f);
                }

                amount = modified;
            }

            // FRAGILE: incoming damage amplified while the debuff is active.
            int fragile = target.GetEffectPotency(StatusEffectType.Fragile);
            if (fragile > 0)
            {
                int amplified = amount * (100 + fragile) / 100;
                AddLog($"  {target.DisplayName} Fragile — damage {amount} -> {amplified} (+{fragile}%).");
                amount = amplified;
            }

            // BLOCK: shave off part of the incoming damage before it lands.
            if (defense != null && defense.kind == SkillKind.Block)
            {
                int reduced = amount * (100 - defense.blockReductionPercent) / 100;
                AddLog($"  {target.DisplayName} BLOCK! Damage {amount} -> {reduced} " +
                       $"(-{defense.blockReductionPercent}%).");
                SpawnFloatingText(popupBase + Vector3.up * 0.4f, "BLOCK!",
                    new Color(0.35f, 0.75f, 1f), 1.1f);
                amount = reduced;
            }

            bool wasStaggered = target.IsStaggered;
            DamageResult result = target.TakeDamage(amount);

            SpawnFloatingText(popupBase, $"-{result.damageApplied}",
                wasStaggered ? new Color(1f, 0.45f, 0.1f) : new Color(1f, 0.25f, 0.25f), 1.4f);

            string multiplierText = wasStaggered ? " (x2 karena staggered)" : "";
            AddLog($"  {target.DisplayName} terkena {result.damageApplied} damage{multiplierText} -> " +
                   $"HP {target.CurrentHealth}/{target.MaxHealth}, Stagger {target.CurrentStagger}/{target.MaxStagger}");

            if (result.becameStaggered)
            {
                AddLog($"  >>> {target.DisplayName} STAGGERED! (damage x2, skip 1 turn)");
                SpawnFloatingText(popupBase + Vector3.up * 0.5f, "STAGGER BREAK!",
                    new Color(1f, 0.6f, 0.1f), 1.1f);
            }

            if (result.died)
            {
                AddLog($"  >>> {target.DisplayName} GUGUR!");
                SpawnFloatingText(popupBase + Vector3.up * 0.5f, "GUGUR",
                    new Color(0.75f, 0.75f, 0.75f), 1.2f);
            }

            // COUNTER: survived the hit -> strike back (never chains recursively).
            if (allowCounter && defense != null && defense.kind == SkillKind.Counter &&
                target.IsAlive && !target.IsStaggered &&
                attacker != null && attacker.IsAlive)
            {
                Color counterColor = new Color(1f, 0.5f, 0.9f);
                AddLog($"  {target.DisplayName} COUNTER dengan [{defense.skillName}]!");
                SpawnFloatingText(popupBase + Vector3.up * 0.8f, "COUNTER!", counterColor, 1.2f);

                int counterPower = RollFinalPower(target, defense);
                PulseEntity(target, counterColor);
                ApplyDamage(target, attacker, counterPower, defense.sinType, allowCounter: false);

                StartCoroutine(GetMotion(attacker).HitShake());
            }

            return true;
        }

        /// <summary>
        /// Applies the skill's on-hit status effect (to the target, or to the actor
        /// for self-buff skills). Called only when the hit actually landed.
        /// </summary>
        private void ApplySkillEffect(BattleEntity actor, BattleEntity target, SkillData skill)
        {
            if (skill.inflictEffect == StatusEffectType.None || skill.inflictPotency <= 0)
            {
                return;
            }

            BattleEntity recipient = skill.inflictOnSelf ? actor : target;
            if (recipient == null || !recipient.IsAlive)
            {
                return;
            }

            recipient.AddStatusEffect(skill.inflictEffect, skill.inflictPotency, skill.inflictDuration);

            bool isBuff = skill.inflictEffect == StatusEffectType.PowerUp;
            Color color = isBuff ? new Color(0.4f, 1f, 0.5f) : new Color(0.8f, 0.4f, 1f);
            string potencyText = skill.inflictEffect == StatusEffectType.Fragile
                ? $"{skill.inflictPotency}%"
                : skill.inflictPotency.ToString();

            AddLog($"  {recipient.DisplayName} terkena {skill.inflictEffect} {potencyText} " +
                   $"({skill.inflictDuration} turn) dari [{skill.skillName}].");
            SpawnFloatingText(recipient.transform.position + Vector3.up * 2.6f,
                $"+{skill.inflictEffect}", color, 1.1f);
        }

        private void PulseEntity(BattleEntity entity, Color flashColor)
        {
            if (entity == null)
            {
                return;
            }

            BattleEntityPulse pulse = entity.GetComponent<BattleEntityPulse>();
            if (pulse == null)
            {
                pulse = entity.gameObject.AddComponent<BattleEntityPulse>();
            }

            pulse.Flash(flashColor);
        }

        private static BattleEntityMotion GetMotion(BattleEntity entity)
        {
            BattleEntityMotion motion = entity.GetComponent<BattleEntityMotion>();
            if (motion == null)
            {
                motion = entity.gameObject.AddComponent<BattleEntityMotion>();
                motion.CaptureHome();
            }

            return motion;
        }

        // ---------------------------------------------------------------- End / restart

        private bool CheckBattleEnd()
        {
            bool allyAlive = combatants.Any(c => c != null && c.IsAlive && c.Team == EntityTeam.Ally);
            bool enemyAlive = combatants.Any(c => c != null && c.IsAlive && c.Team == EntityTeam.Enemy);

            if (allyAlive && enemyAlive)
            {
                return false;
            }

            BattleResult = allyAlive ? "MENANG!" : "KALAH...";
            AddLog($"===== BATTLE SELESAI: {BattleResult} =====");
            if (allyAlive && BattleProgression.HasNextStage)
            {
                AddLog($"Tekan N untuk Stage {BattleProgression.StageNumber + 1}/{BattleProgression.StageCount}.");
            }

            AddLog("Tekan R untuk mengulang stage ini | M kembali ke menu.");
            ShowBanner(BattleResult);
            SetState(BattleState.BattleOver);
            return true;
        }

        /// <summary>Win only: advance the run and reload the battle scene with the next encounter.</summary>
        public void AdvanceToNextStage()
        {
            if (!CanAdvanceStage)
            {
                return;
            }

            BattleProgression.Advance();
            UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene");
        }

        public void RestartBattle()
        {
            StopAllCoroutines();

            foreach (BattleEntity entity in combatants.Where(c => c != null))
            {
                GetMotion(entity).ResetPosition();
                entity.ResetForBattle();
            }

            battleLog.Clear();
            floatingTexts.Clear();
            BannerText = "";
            BattleResult = "";
            turnNumber = 0;

            AddLog("Battle di-restart.");
            BeginNewTurn();
        }

        // ---------------------------------------------------------------- Helpers

        public void SpawnFloatingText(Vector3 worldPosition, string text, Color color, float size = 1f)
        {
            floatingTexts.Add(new FloatingText
            {
                worldPosition = worldPosition,
                text = text,
                color = color,
                spawnTime = Time.time,
                size = size
            });
        }

        private void ShowBanner(string text)
        {
            BannerText = text;
            BannerSpawnTime = Time.time;
        }

        private void SetState(BattleState newState)
        {
            currentState = newState;
            Debug.Log($"[BattleManager] State -> {newState}");
        }

        private void AddLog(string message)
        {
            battleLog.Add(message);
            Debug.Log($"[Battle] {message}");

            // Keep memory bounded during long sessions.
            if (battleLog.Count > 300)
            {
                battleLog.RemoveRange(0, battleLog.Count - 300);
            }
        }
    }
}
