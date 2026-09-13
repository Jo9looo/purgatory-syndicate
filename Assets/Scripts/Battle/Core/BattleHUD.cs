using System.Text;
using UnityEngine;

namespace PurgatorySyndicate.Battle
{
    /// <summary>
    /// Whitebox on-screen UI (OnGUI):
    /// - Top-left: contextual instructions per phase (skill picker, timeline, result).
    /// - Above each capsule: name, HP bar, Stagger bar, status, actor/target markers.
    /// - Right side: scrolling battle log.
    /// </summary>
    public class BattleHUD : MonoBehaviour
    {
        private const int LogLinesShown = 18;

        private BattleManager manager;
        private TargetingController targeting;
        private BattleMouseController mouse;
        private Camera cam;

        private GUIStyle boxStyle;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;
        private GUIStyle markerStyle;
        private GUIStyle barTextStyle;
        private GUIStyle tinyStyle;
        private GUIStyle popupStyle;
        private GUIStyle bannerStyle;
        private bool stylesReady;

        private void EnsureRefs()
        {
            if (manager == null)
            {
                manager = FindAnyObjectByType<BattleManager>();
            }

            if (targeting == null && manager != null)
            {
                targeting = manager.GetComponent<TargetingController>();
            }

            if (mouse == null && manager != null)
            {
                mouse = manager.GetComponent<BattleMouseController>();
            }

            if (cam == null)
            {
                cam = Camera.main;
            }
        }

        private void InitStyles()
        {
            if (stylesReady)
            {
                return;
            }

            boxStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(12, 12, 10, 10)
            };

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                richText = true,
                normal = { textColor = new Color(0.92f, 0.92f, 0.92f) }
            };

            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.UpperCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            markerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.UpperCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.9f, 0.3f) }
            };

            barTextStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            tinyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = new Color(0.75f, 0.75f, 0.8f) }
            };

            popupStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };

            bannerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 44,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };

            stylesReady = true;
        }

        private void OnGUI()
        {
            EnsureRefs();
            InitStyles();

            if (manager == null)
            {
                return;
            }

            DrawTopPanel();
            DrawTimeline();
            DrawControlsPanel();
            DrawEntityOverlays();
            DrawArrowLabels();
            DrawSkillBar();
            DrawFloatingTexts();
            DrawBanner();
            DrawLogPanel();
            DrawHoverTooltip(); // last, so it renders on top of everything
        }

        // ---------------------------------------------------------------- Skill bar

        private const float SkillBarWidth = 780f;
        private const float SkillBarHeight = 132f;

        /// <summary>
        /// Screen rect of the bottom-center skill bar, shared with
        /// BattleMouseController so bar clicks never start a world drag.
        /// </summary>
        public static Rect GetSkillBarRect()
        {
            return new Rect(
                (Screen.width - SkillBarWidth) * 0.5f,
                Screen.height - SkillBarHeight - 14f,
                SkillBarWidth,
                SkillBarHeight);
        }

        /// <summary>
        /// Bottom-center skill bar for the currently controlled ally (the unit under
        /// the triangle marker). Slots 1-3 = active attack skills, slot 4 = the unit's
        /// defensive skill. Press a slot to select it AND start a drag: the arrow
        /// springs from the character, drop it on an enemy to lock the target.
        /// </summary>
        private void DrawSkillBar()
        {
            if (targeting == null || !targeting.IsActive || mouse == null)
            {
                return;
            }

            BattleEntity barAlly = mouse.SelectedAlly;
            if (barAlly == null)
            {
                return;
            }

            Rect bar = GetSkillBarRect();
            GUI.Box(bar, GUIContent.none, boxStyle);
            GUI.Label(new Rect(bar.x + 12f, bar.y + 6f, bar.width - 24f, 20f),
                $"<b>{barAlly.DisplayName}</b>  Sin {barAlly.CurrentSin}/{barAlly.MaxSin} — " +
                "klik/drag skill ke musuh, atau tekan 1-4 (slot 4 = bertahan)", bodyStyle);

            PendingAssignment assignment = targeting.GetAssignment(barAlly);

            const float pad = 10f;
            float slotWidth = (bar.width - pad * 5f) / 4f;
            float slotHeight = bar.height - 40f;
            bool pressed = WasBarClickThisFrame(out Vector2 pressPos);

            for (int i = 0; i < 4; i++)
            {
                Rect slot = new Rect(
                    bar.x + pad + i * (slotWidth + pad),
                    bar.y + 30f,
                    slotWidth,
                    slotHeight);

                bool isDefenseSlot = i == 3;
                SkillData skill = isDefenseSlot
                    ? barAlly.DefenseSkill
                    : (i < barAlly.Skills.Count ? barAlly.Skills[i] : null);

                bool selected = assignment != null
                    ? (isDefenseSlot ? assignment.isDefense : !assignment.isDefense && assignment.skillIndex == i)
                    : i == 0; // default skill before any input
                bool usable = skill != null && barAlly.CanUseSkill(skill);

                GUI.color = skill == null
                    ? new Color(1f, 1f, 1f, 0.25f)
                    : !usable ? new Color(0.45f, 0.45f, 0.48f)
                    : selected ? new Color(1f, 0.85f, 0.2f) : Color.white;
                GUI.Box(slot, GUIContent.none, boxStyle);

                if (skill != null)
                {
                    GUI.Label(new Rect(slot.x + 8f, slot.y + 6f, slot.width - 16f, slot.height - 12f),
                        BuildSlotText(barAlly, i, skill, selected), smallStyle);

                    if (pressed && slot.Contains(pressPos) && usable)
                    {
                        if (isDefenseSlot)
                        {
                            targeting.SetDefense(barAlly);
                        }
                        else
                        {
                            targeting.SetSkill(barAlly, i);
                            mouse.BeginSkillDrag(barAlly);
                        }
                    }
                }

                GUI.color = Color.white;
            }
        }

        private string BuildSlotText(BattleEntity ally, int index, SkillData skill, bool selected)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(selected ? "<b>" : "");
            sb.AppendLine($"[{index + 1}] {skill.skillName}");

            switch (skill.kind)
            {
                case SkillKind.Block:
                    sb.AppendLine($"BLOCK — damage diterima -{skill.blockReductionPercent}%");
                    break;
                case SkillKind.Dodge:
                    sb.AppendLine($"DODGE — {skill.dodgeChancePercent}% menghindar");
                    break;
                case SkillKind.Counter:
                    sb.AppendLine($"COUNTER — balas Pow {skill.basePower} + {skill.coinCount} koin");
                    break;
                default:
                    sb.AppendLine($"Pow {skill.basePower} + {skill.coinCount} koin");
                    break;
            }

            int cd = ally.GetCooldown(skill);
            string cost = skill.sinCost > 0 ? $"Sin {skill.sinCost}" : "gratis";
            string lockText = cd > 0 ? $" | CD {cd}" : ally.CurrentSin < skill.sinCost ? " | SIN KURANG" : "";
            string sinMatch = ally.HasAffinity(skill.sinType) ? " MATCH+2" : "";
            sb.Append($"{skill.sinType} | {cost}{lockText}{sinMatch}");
            if (skill.inflictEffect != StatusEffectType.None)
            {
                sb.Append($"\n+{skill.inflictEffect}");
            }

            sb.Append(selected ? "</b>" : "");
            return sb.ToString();
        }

        /// <summary>
        /// Click detection via the new Input System (IMGUI mouse events are not
        /// reliable when only the Input System package is active).
        /// </summary>
        private static bool WasBarClickThisFrame(out Vector2 guiPosition)
        {
            guiPosition = default;
            UnityEngine.InputSystem.Mouse mouseDevice = UnityEngine.InputSystem.Mouse.current;
            if (mouseDevice == null || !mouseDevice.leftButton.wasPressedThisFrame)
            {
                return false;
            }

            Vector2 screenPos = mouseDevice.position.ReadValue();
            guiPosition = new Vector2(screenPos.x, Screen.height - screenPos.y);
            return true;
        }

        /// <summary>
        /// Floating combat numbers (damage, power rolls, status shouts) that rise and fade.
        /// </summary>
        private void DrawFloatingTexts()
        {
            if (cam == null)
            {
                return;
            }

            const float lifetime = 1.4f;

            foreach (FloatingText ft in manager.FloatingTexts)
            {
                float age = Time.time - ft.spawnTime;
                float progress = age / lifetime;
                if (progress >= 1f)
                {
                    continue;
                }

                Vector3 screenPos = cam.WorldToScreenPoint(ft.worldPosition);
                if (screenPos.z < 0f)
                {
                    continue;
                }

                float x = screenPos.x;
                float y = Screen.height - screenPos.y - 55f * progress;
                float alpha = 1f - progress * progress;

                popupStyle.fontSize = Mathf.RoundToInt(20f * ft.size);
                popupStyle.normal.textColor = new Color(ft.color.r, ft.color.g, ft.color.b, alpha);

                // Cheap shadow for readability.
                Color shadow = new Color(0f, 0f, 0f, alpha * 0.7f);
                Color keep = popupStyle.normal.textColor;
                popupStyle.normal.textColor = shadow;
                GUI.Label(new Rect(x - 99f, y - 14f, 200f, 30f), ft.text, popupStyle);
                popupStyle.normal.textColor = keep;
                GUI.Label(new Rect(x - 100f, y - 15f, 200f, 30f), ft.text, popupStyle);
            }
        }

        /// <summary>
        /// Large center-screen announcement (TURN X, CLASH!, MENANG!/KALAH...).
        /// </summary>
        private void DrawBanner()
        {
            if (string.IsNullOrEmpty(manager.BannerText))
            {
                return;
            }

            const float lifetime = 1.6f;
            float age = Time.time - manager.BannerSpawnTime;
            if (age > lifetime)
            {
                return;
            }

            float alpha = age < 1f ? 1f : 1f - (age - 1f) / (lifetime - 1f);
            Rect rect = new Rect(0f, Screen.height * 0.16f, Screen.width, 60f);

            bannerStyle.normal.textColor = new Color(0f, 0f, 0f, alpha * 0.7f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), manager.BannerText, bannerStyle);
            bannerStyle.normal.textColor = new Color(1f, 0.92f, 0.5f, alpha);
            GUI.Label(rect, manager.BannerText, bannerStyle);
        }

        /// <summary>
        /// Vertical control legend on the left edge of the screen.
        /// Controls relevant to the current phase are highlighted.
        /// </summary>
        private void DrawControlsPanel()
        {
            bool targetingPhase = manager.CurrentState == BattleState.Targeting;
            bool sortingPhase = manager.CurrentState == BattleState.ActionSorting;
            bool overPhase = manager.CurrentState == BattleState.BattleOver;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Line("DRAG MOUSE", "Ally -> musuh = bidik", targetingPhase));
            sb.AppendLine(Line("DRAG SKILL", "Slot skill -> musuh", targetingPhase));
            sb.AppendLine(Line("DRAG ULANG", "Pindah target", targetingPhase));
            sb.AppendLine(Line("HOVER UNIT", "Lihat statistik", true));
            sb.AppendLine(Line("1 / 2 / 3", "Pilih skill aktif", targetingPhase));
            sb.AppendLine(Line("4", "Bertahan (blok/hindar/balas)", targetingPhase));
            sb.AppendLine(Line("ENTER", "Kunci semua aksi", targetingPhase));
            sb.AppendLine(Line("ENTER / SPACE", "Eksekusi turn", sortingPhase));
            sb.AppendLine(Line("R", "Restart stage ini", overPhase));
            sb.AppendLine(Line("N", "Stage berikutnya", manager.CanAdvanceStage));
            sb.Append(Line("M", "Kembali ke menu", overPhase));

            string text = sb.ToString();
            float width = 190f;
            float height = 44f + bodyStyle.CalcHeight(new GUIContent(text), width - 24f);
            float y = Screen.height * 0.5f - height * 0.5f;

            GUI.Box(new Rect(12f, y, width, height), GUIContent.none, boxStyle);
            GUI.Label(new Rect(24f, y + 8f, width - 24f, 20f), "KONTROL", titleStyle);
            GUI.Label(new Rect(24f, y + 32f, width - 24f, height - 36f), text, bodyStyle);
        }

        private static string Line(string key, string action, bool active)
        {
            return active
                ? $"<b><color=#FFE64C>{key}</color></b>\n  {action}\n"
                : $"<color=#AAAAAA>{key}</color>\n  {action}\n";
        }

        // ---------------------------------------------------------------- Panels

        private void DrawTopPanel()
        {
            string text = BuildTopPanelText();
            float height = 30f + bodyStyle.CalcHeight(new GUIContent(text), 440f);

            GUI.Box(new Rect(12f, 12f, 470f, height), GUIContent.none, boxStyle);
            GUI.Label(new Rect(24f, 18f, 446f, 22f),
                $"STAGE {BattleProgression.StageNumber}/{BattleProgression.StageCount} — Turn {manager.TurnNumber} | {manager.CurrentState}", titleStyle);
            GUI.Label(new Rect(24f, 42f, 446f, height - 46f), text, bodyStyle);
        }

        private string BuildTopPanelText()
        {
            StringBuilder sb = new StringBuilder();

            switch (manager.CurrentState)
            {
                case BattleState.Targeting:
                    BuildTargetingText(sb);
                    break;

                case BattleState.ActionSorting:
                    sb.AppendLine("Timeline aksi di atas tengah (urut speed).");
                    sb.AppendLine();
                    sb.AppendLine("<b>ENTER / SPACE</b> = eksekusi turn");
                    break;

                case BattleState.Execution:
                    sb.AppendLine("Mengeksekusi aksi... perhatikan capsule & log.");
                    break;

                case BattleState.TurnEnd:
                    sb.AppendLine("Turn selesai. Turn berikutnya segera dimulai...");
                    break;

                case BattleState.BattleOver:
                    sb.AppendLine($"<b>=== {manager.BattleResult} ===</b>");
                    if (!string.IsNullOrEmpty(manager.EncounterName))
                    {
                        sb.AppendLine(manager.EncounterName);
                    }

                    sb.AppendLine(manager.CanAdvanceStage
                        ? $"<b>N</b> = Stage {BattleProgression.StageNumber + 1}/{BattleProgression.StageCount}"
                        : "Run selesai — tidak ada stage berikutnya.");
                    sb.AppendLine("R = ulang stage ini | M = kembali ke menu");
                    break;

                default:
                    sb.AppendLine("Menyiapkan turn...");
                    break;
            }

            sb.AppendLine();
            sb.Append("R = restart battle");
            return sb.ToString();
        }

        private void BuildTargetingText(StringBuilder sb)
        {
            if (targeting == null || !targeting.IsActive)
            {
                sb.AppendLine("Menunggu aksi musuh...");
                return;
            }

            sb.AppendLine("<b>PILIH AKSI</b> — drag dari unit-mu, drop panah ke musuh.");
            sb.AppendLine("Skill dipilih lewat bar bawah (klik / tombol 1-4). Slot 4 = bertahan.");
            sb.AppendLine();

            foreach (BattleEntity ally in targeting.ReadyAllies)
            {
                PendingAssignment assignment = targeting.GetAssignment(ally);
                if (assignment != null && assignment.isDefense)
                {
                    sb.AppendLine($"  [OK] {ally.DisplayName}: [{ally.DefenseSkill.skillName}] (bertahan)");
                }
                else if (assignment != null && assignment.target != null)
                {
                    int skillIndex = Mathf.Clamp(assignment.skillIndex, 0, ally.Skills.Count - 1);
                    SkillData skill = ally.Skills[skillIndex];
                    sb.AppendLine($"  [OK] {ally.DisplayName}: [{skill.skillName}] -> {assignment.target.DisplayName}");
                }
                else
                {
                    sb.AppendLine($"  [ .. ] {ally.DisplayName}: <i>belum ada aksi — drag ke musuh / slot 4</i>");
                }
            }

            sb.AppendLine();
            sb.AppendLine(targeting.AllAssigned
                ? $"<b>ENTER</b> = kunci aksi ({targeting.AssignedCount}/{targeting.ReadyAllies.Count})"
                : $"Bidik semua unit dulu ({targeting.AssignedCount}/{targeting.ReadyAllies.Count})...");
        }

        // ---------------------------------------------------------------- Timeline

        /// <summary>
        /// Horizontal speed-order strip across the top-center. Highlights the
        /// action currently resolving during Execution.
        /// </summary>
        private void DrawTimeline()
        {
            if (manager.CurrentState != BattleState.ActionSorting &&
                manager.CurrentState != BattleState.Execution)
            {
                return;
            }

            int count = manager.ActionQueue.Count;
            if (count == 0)
            {
                return;
            }

            const float slotW = 118f;
            const float slotH = 46f;
            const float gap = 6f;
            float total = count * slotW + (count - 1) * gap;
            float x = (Screen.width - total) * 0.5f;
            float y = 12f;

            GUI.Box(new Rect(x - 10f, y - 4f, total + 20f, slotH + 22f), GUIContent.none, boxStyle);
            GUI.Label(new Rect(x, y - 2f, total, 16f), "TIMELINE (cepat -> lambat)", tinyStyle);

            for (int i = 0; i < count; i++)
            {
                BattleAction a = manager.ActionQueue[i];
                Rect slot = new Rect(x + i * (slotW + gap), y + 16f, slotW, slotH);
                bool current = i == manager.CurrentActionIndex;
                bool ally = a.Actor != null && a.Actor.Team == EntityTeam.Ally;

                GUI.color = current
                    ? new Color(1f, 0.85f, 0.2f)
                    : ally ? new Color(0.35f, 0.55f, 1f) : new Color(0.9f, 0.35f, 0.3f);
                GUI.Box(slot, GUIContent.none, boxStyle);
                GUI.color = Color.white;

                string name = a.Actor != null ? a.Actor.DisplayName : "?";
                string skill = a.Skill != null ? a.Skill.skillName : "?";
                int spd = a.Actor != null ? a.Actor.CurrentSpeed : 0;
                string prefix = current ? "> " : "";
                GUI.Label(new Rect(slot.x + 4f, slot.y + 4f, slot.width - 8f, slot.height - 8f),
                    $"{prefix}{name}\nSpd {spd}\n{skill}", tinyStyle);
            }
        }

        // ---------------------------------------------------------------- World overlays

        private void DrawEntityOverlays()
        {
            if (cam == null)
            {
                return;
            }

            foreach (BattleEntity entity in manager.Combatants)
            {
                if (entity == null)
                {
                    continue;
                }

                Vector3 worldPos = entity.transform.position + Vector3.up * 1.7f;
                Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
                if (screenPos.z < 0f)
                {
                    continue;
                }

                float x = screenPos.x;
                float y = Screen.height - screenPos.y;

                // Markers during Targeting: allies missing an order + live drop target.
                if (targeting != null && targeting.IsActive)
                {
                    bool needsOrder = entity.Team == EntityTeam.Ally &&
                                      targeting.CanCommand(entity) &&
                                      (targeting.GetAssignment(entity) == null ||
                                       targeting.GetAssignment(entity).target == null);

                    bool isDropTarget = mouse != null && mouse.IsDragging &&
                                        entity == mouse.HoveredEntity &&
                                        entity.Team == EntityTeam.Enemy && entity.IsAlive;

                    if (isDropTarget)
                    {
                        GUI.Label(new Rect(x - 70f, y - 40f, 140f, 20f), "vvv DROP DI SINI vvv", markerStyle);
                    }
                    else if (needsOrder)
                    {
                        GUI.Label(new Rect(x - 70f, y - 40f, 140f, 20f), "vvv DRAG AKU vvv", markerStyle);
                    }
                }

                // Name + speed + status.
                string status = !entity.IsAlive ? " [GUGUR]" : entity.IsStaggered ? " [STAGGERED]" : "";
                GUI.Label(new Rect(x - 80f, y - 22f, 160f, 18f),
                    $"{entity.DisplayName} (Spd {entity.CurrentSpeed}){status}", smallStyle);

                // HP + Stagger bars with numeric values.
                float hpRatio = Mathf.Clamp01((float)entity.CurrentHealth / entity.MaxHealth);
                float stRatio = Mathf.Clamp01((float)entity.CurrentStagger / entity.MaxStagger);

                DrawBarWithText(x - 55f, y - 4f, 110f, 11f, hpRatio,
                    new Color(0.2f, 0.85f, 0.3f), $"{entity.CurrentHealth}/{entity.MaxHealth}");
                DrawBarWithText(x - 55f, y + 9f, 110f, 9f, stRatio,
                    new Color(1f, 0.8f, 0.15f), $"{entity.CurrentStagger}/{entity.MaxStagger}");

                float sinRatio = entity.MaxSin > 0
                    ? Mathf.Clamp01((float)entity.CurrentSin / entity.MaxSin)
                    : 0f;
                DrawBarWithText(x - 55f, y + 20f, 110f, 8f, sinRatio,
                    new Color(0.55f, 0.4f, 0.95f), $"Sin {entity.CurrentSin}/{entity.MaxSin}");

                float extraY = y + 30f;
                if (entity.Definition != null && entity.Definition.sinAffinities.Count > 0)
                {
                    string sins = string.Join("+", entity.Definition.sinAffinities);
                    GUI.Label(new Rect(x - 80f, extraY, 160f, 14f), sins, tinyStyle);
                    extraY += 12f;
                }

                if (entity.ActiveEffects.Count > 0)
                {
                    string fx = string.Join(" | ", entity.ActiveEffects);
                    GUI.Label(new Rect(x - 90f, extraY, 180f, 14f), fx, tinyStyle);
                }
            }
        }

        private void DrawBarWithText(float x, float y, float width, float height, float ratio, Color fill, string text)
        {
            Color previous = GUI.color;

            GUI.color = new Color(0.08f, 0.08f, 0.08f, 0.9f);
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);

            GUI.color = fill;
            GUI.DrawTexture(new Rect(x + 1f, y + 1f, (width - 2f) * ratio, height - 2f), Texture2D.whiteTexture);

            GUI.color = previous;
            GUI.Label(new Rect(x, y - 1f, width, height + 2f), text, barTextStyle);
        }

        // ---------------------------------------------------------------- Arrow labels

        /// <summary>
        /// Skill name floating at the apex of each locked targeting arrow.
        /// </summary>
        private void DrawArrowLabels()
        {
            if (cam == null || targeting == null)
            {
                return;
            }

            bool visible = manager.CurrentState == BattleState.Targeting ||
                           manager.CurrentState == BattleState.ActionSorting ||
                           manager.CurrentState == BattleState.Execution;
            if (!visible)
            {
                return;
            }

            foreach (System.Collections.Generic.KeyValuePair<BattleEntity, PendingAssignment> pair
                     in targeting.Assignments)
            {
                BattleEntity ally = pair.Key;
                PendingAssignment assignment = pair.Value;

                // Defensive stances are self-targeted: no arrow, no label.
                if (ally == null || assignment.target == null || assignment.isDefense ||
                    assignment.target == ally)
                {
                    continue;
                }

                int skillIndex = Mathf.Clamp(assignment.skillIndex, 0, ally.Skills.Count - 1);
                SkillData skill = ally.Skills[skillIndex];
                if (skill == null)
                {
                    continue;
                }

                // Approximate arc apex (matches TargetArrow's curve).
                Vector3 from = ally.transform.position + Vector3.up * 1.35f;
                Vector3 to = assignment.target.transform.position + Vector3.up * 1.35f;
                Vector3 apex = (from + to) * 0.5f +
                               Vector3.up * (0.9f + Vector3.Distance(from, to) * 0.18f);

                Vector3 screenPos = cam.WorldToScreenPoint(apex);
                if (screenPos.z < 0f)
                {
                    continue;
                }

                GUI.Label(new Rect(screenPos.x - 80f, Screen.height - screenPos.y - 24f, 160f, 20f),
                    skill.skillName, markerStyle);
            }
        }

        // ---------------------------------------------------------------- Hover tooltip

        /// <summary>
        /// Full stat breakdown near the cursor when hovering any character
        /// (base stats, current condition, skills, buffs/debuffs).
        /// </summary>
        private void DrawHoverTooltip()
        {
            if (mouse == null || mouse.HoveredEntity == null)
            {
                return;
            }

            BattleEntity entity = mouse.HoveredEntity;
            string text = BuildTooltipText(entity);

            const float width = 320f;
            float height = 24f + bodyStyle.CalcHeight(new GUIContent(text), width - 24f);

            // Event.current.mousePosition is already in GUI coordinates.
            Vector2 mousePos = Event.current.mousePosition;
            float x = Mathf.Min(mousePos.x + 20f, Screen.width - width - 8f);
            float y = Mathf.Min(mousePos.y + 20f, Screen.height - height - 8f);

            GUI.Box(new Rect(x, y, width, height), GUIContent.none, boxStyle);
            GUI.Label(new Rect(x + 12f, y + 10f, width - 24f, height - 20f), text, bodyStyle);
        }

        private string BuildTooltipText(BattleEntity entity)
        {
            StringBuilder sb = new StringBuilder();

            string teamLabel = entity.Team == EntityTeam.Ally ? "Ally" : "Enemy";
            string status = !entity.IsAlive ? "GUGUR" : entity.IsStaggered ? "STAGGERED" : "Normal";
            sb.AppendLine($"<b>{entity.DisplayName}</b> ({teamLabel}) — {status}");

            if (entity.Definition != null && entity.Definition.baseStats != null)
            {
                CharacterBaseStats baseStats = entity.Definition.baseStats;
                sb.AppendLine();
                sb.AppendLine("<b>BASE STAT</b>");
                sb.AppendLine($"  Max HP {baseStats.maxHealth} | Max Stagger {baseStats.maxStagger} | " +
                              $"Speed {baseStats.speedRange.x}-{baseStats.speedRange.y} | Max Sin {baseStats.maxSin}");
                sb.AppendLine($"  Sin Affinity: {string.Join(", ", entity.Definition.sinAffinities)}");
                sb.AppendLine("  Weakness: Wrath>Sloth>Gluttony>Gloom>Pride>Envy>Lust>Wrath");
            }

            sb.AppendLine();
            sb.AppendLine("<b>KONDISI SEKARANG</b>");
            sb.AppendLine($"  HP {entity.CurrentHealth}/{entity.MaxHealth} | " +
                          $"Stagger {entity.CurrentStagger}/{entity.MaxStagger} | " +
                          $"Sin {entity.CurrentSin}/{entity.MaxSin}");
            sb.AppendLine($"  Speed turn ini: {entity.CurrentSpeed}" +
                          (entity.IsStaggered ? " | damage diterima x2" : ""));
            if (entity.ActiveDefense != null)
            {
                sb.AppendLine($"  Bertahan: {entity.ActiveDefense.kind.ToString().ToUpperInvariant()} " +
                              $"[{entity.ActiveDefense.skillName}]");
            }

            if (entity.Skills.Count > 0 || entity.DefenseSkill != null)
            {
                sb.AppendLine();
                sb.AppendLine("<b>SKILL</b>");
                foreach (SkillData skill in entity.Skills)
                {
                    if (skill == null)
                    {
                        continue;
                    }

                    string sinMatch = entity.HasAffinity(skill.sinType) ? " (MATCH +2)" : "";
                    int cd = entity.GetCooldown(skill);
                    string lockText = cd > 0 ? $" | CD {cd}" : "";
                    string cost = skill.sinCost > 0 ? $" | Sin {skill.sinCost}" : "";
                    string fx = skill.inflictEffect != StatusEffectType.None
                        ? $" | +{skill.inflictEffect}"
                        : "";
                    sb.AppendLine($"  {skill.skillName} | Pow {skill.basePower} + {skill.coinCount} koin | " +
                                  $"{skill.sinType}{sinMatch}{cost}{lockText}{fx}");
                }

                if (entity.DefenseSkill != null)
                {
                    SkillData def = entity.DefenseSkill;
                    sb.AppendLine($"  {def.skillName} | {def.kind.ToString().ToUpperInvariant()} (DEF) | " +
                                  $"{def.sinType}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("<b>BUFF / DEBUFF</b>");
            if (entity.ActiveEffects.Count == 0)
            {
                sb.Append("  (tidak ada)");
            }
            else
            {
                foreach (StatusEffect effect in entity.ActiveEffects)
                {
                    sb.AppendLine($"  {effect}");
                }
            }

            return sb.ToString();
        }

        // ---------------------------------------------------------------- Log

        private void DrawLogPanel()
        {
            float width = 400f;
            float x = Screen.width - width - 12f;

            StringBuilder sb = new StringBuilder();
            int start = Mathf.Max(0, manager.LogLines.Count - LogLinesShown);
            for (int i = start; i < manager.LogLines.Count; i++)
            {
                sb.AppendLine(manager.LogLines[i]);
            }

            string text = sb.ToString();
            float height = 40f + bodyStyle.CalcHeight(new GUIContent(text), width - 30f);

            GUI.Box(new Rect(x, 12f, width, height), GUIContent.none, boxStyle);
            GUI.Label(new Rect(x + 12f, 18f, width - 24f, 20f), "BATTLE LOG", titleStyle);
            GUI.Label(new Rect(x + 12f, 40f, width - 24f, height - 46f), text, bodyStyle);
        }
    }
}
