using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PurgatorySyndicate.Battle
{
    /// <summary>
    /// Mouse-driven targeting:
    /// - Hover any character → HoveredEntity (BattleHUD shows the stat tooltip).
    /// - During Targeting, press-and-drag from an ally: an arrow rises from the ally
    ///   and follows the cursor; release over an enemy to lock the target.
    /// - Locked arrows persist (through ActionSorting/Execution) and can be re-dragged
    ///   to another enemy while Targeting is active. They vanish at TurnEnd.
    /// </summary>
    public class BattleMouseController : MonoBehaviour
    {
        private static readonly Color DragColor = new Color(1f, 0.9f, 0.3f);
        private static readonly Color LockedColor = new Color(1f, 0.35f, 0.25f);

        private BattleManager manager;
        private TargetingController targeting;
        private Camera cam;

        private readonly Dictionary<BattleEntity, TargetArrow> arrows =
            new Dictionary<BattleEntity, TargetArrow>();

        private SelectionMarker selectionMarker;

        /// <summary>Character currently under the cursor (any team, alive or dead).</summary>
        public BattleEntity HoveredEntity { get; private set; }

        /// <summary>
        /// Ally currently being controlled: hovered/dragged ally, sticky to the last
        /// one touched, falling back to the first unit without an order. The skill bar
        /// and the triangle selection marker both follow this.
        /// </summary>
        public BattleEntity SelectedAlly { get; private set; }

        /// <summary>Ally currently being dragged from (null when not dragging).</summary>
        public BattleEntity DragSource { get; private set; }

        public bool IsDragging => DragSource != null;

        /// <summary>World point the drag arrow tip follows (enemy head or cursor plane).</summary>
        public Vector3 DragPoint { get; private set; }

        /// <summary>
        /// Ally that keyboard skill keys (1/2/3) apply to: the dragged ally,
        /// or the hovered ally when not dragging.
        /// </summary>
        public BattleEntity ContextAlly
        {
            get
            {
                if (IsDragging)
                {
                    return DragSource;
                }

                if (HoveredEntity != null && HoveredEntity.Team == EntityTeam.Ally)
                {
                    return HoveredEntity;
                }

                return null;
            }
        }

        private void Awake()
        {
            manager = GetComponent<BattleManager>();
            targeting = GetComponent<TargetingController>();
        }

        private void Update()
        {
            if (cam == null)
            {
                cam = Camera.main;
            }

            if (manager == null || targeting == null || cam == null)
            {
                return;
            }

            UpdateHover();
            UpdateDrag();
            UpdateSelection();
            UpdateArrowVisuals();
        }

        // ---------------------------------------------------------------- Selection

        private void UpdateSelection()
        {
            if (!targeting.IsActive)
            {
                SelectedAlly = null;
            }
            else
            {
                BattleEntity context = ContextAlly;
                if (context != null && targeting.CanCommand(context))
                {
                    SelectedAlly = context;
                }

                if (SelectedAlly == null || !targeting.CanCommand(SelectedAlly))
                {
                    SelectedAlly = FirstAllyNeedingOrder();
                }
            }

            // Triangle marker above the controlled character.
            if (selectionMarker == null)
            {
                GameObject markerObject = new GameObject("SelectionMarker");
                markerObject.transform.SetParent(transform, false);
                selectionMarker = markerObject.AddComponent<SelectionMarker>();
            }

            selectionMarker.Follow(SelectedAlly);
        }

        private BattleEntity FirstAllyNeedingOrder()
        {
            foreach (BattleEntity ally in targeting.ReadyAllies)
            {
                PendingAssignment a = targeting.GetAssignment(ally);
                if (a == null || a.target == null)
                {
                    return ally;
                }
            }

            return targeting.ReadyAllies.Count > 0 ? targeting.ReadyAllies[0] : null;
        }

        /// <summary>
        /// Called by BattleHUD when the player presses on a skill slot: starts a drag
        /// whose arrow springs from the character itself (drop on an enemy to target).
        /// </summary>
        public void BeginSkillDrag(BattleEntity ally)
        {
            if (targeting.IsActive && targeting.CanCommand(ally))
            {
                DragSource = ally;
                DragPoint = HeadPoint(ally);
            }
        }

        // ---------------------------------------------------------------- Hover

        private void UpdateHover()
        {
            HoveredEntity = null;

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            RaycastHit[] hits = Physics.RaycastAll(ray, 100f);

            float closest = float.MaxValue;
            foreach (RaycastHit hit in hits)
            {
                BattleEntity entity = hit.collider.GetComponentInParent<BattleEntity>();
                if (entity != null && hit.distance < closest)
                {
                    closest = hit.distance;
                    HoveredEntity = entity;
                }
            }
        }

        // ---------------------------------------------------------------- Drag & drop

        private void UpdateDrag()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            // Start drag: press on a commandable ally during Targeting.
            // Presses over the bottom skill bar belong to the UI, never the world.
            if (mouse.leftButton.wasPressedThisFrame &&
                targeting.IsActive &&
                HoveredEntity != null &&
                HoveredEntity.Team == EntityTeam.Ally &&
                targeting.CanCommand(HoveredEntity) &&
                !IsPointerOverSkillBar(mouse))
            {
                DragSource = HoveredEntity;
            }

            if (!IsDragging)
            {
                return;
            }

            // Drag no longer valid (state advanced mid-drag).
            if (!targeting.IsActive)
            {
                DragSource = null;
                return;
            }

            // Arrow tip: snap to a living enemy under the cursor, otherwise follow
            // the cursor on a horizontal plane at head height.
            if (HoveredEntity != null && HoveredEntity.Team == EntityTeam.Enemy && HoveredEntity.IsAlive)
            {
                DragPoint = HeadPoint(HoveredEntity);
            }
            else
            {
                Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
                Plane plane = new Plane(Vector3.up, new Vector3(0f, 1.4f, 0f));
                DragPoint = plane.Raycast(ray, out float enter)
                    ? ray.GetPoint(enter)
                    : HeadPoint(DragSource);
            }

            // Drop: lock target when released over a living enemy; otherwise the
            // previous assignment (if any) is kept untouched.
            if (mouse.leftButton.wasReleasedThisFrame)
            {
                if (HoveredEntity != null &&
                    HoveredEntity.Team == EntityTeam.Enemy &&
                    HoveredEntity.IsAlive)
                {
                    targeting.SetTarget(DragSource, HoveredEntity);
                }

                DragSource = null;
            }
        }

        // ---------------------------------------------------------------- Arrows

        private void UpdateArrowVisuals()
        {
            // Arrows live from Targeting through Execution, then vanish at TurnEnd.
            bool arrowsVisible =
                manager.CurrentState == BattleState.Targeting ||
                manager.CurrentState == BattleState.ActionSorting ||
                manager.CurrentState == BattleState.Execution;

            foreach (KeyValuePair<BattleEntity, TargetArrow> pair in arrows)
            {
                pair.Value.Hide();
            }

            if (!arrowsVisible)
            {
                return;
            }

            // Locked assignment arrows (defensive stances are self-targeted: no arrow).
            foreach (KeyValuePair<BattleEntity, PendingAssignment> pair in targeting.Assignments)
            {
                BattleEntity ally = pair.Key;
                PendingAssignment assignment = pair.Value;

                if (ally == null || assignment.target == null ||
                    assignment.isDefense || assignment.target == ally)
                {
                    continue;
                }

                // While re-dragging this ally, the live drag arrow takes over below.
                if (IsDragging && ally == DragSource)
                {
                    continue;
                }

                GetArrow(ally).Show(HeadPoint(ally), HeadPoint(assignment.target), LockedColor);
            }

            // Live drag arrow following the cursor.
            if (IsDragging)
            {
                GetArrow(DragSource).Show(HeadPoint(DragSource), DragPoint, DragColor);
            }
        }

        private TargetArrow GetArrow(BattleEntity ally)
        {
            if (!arrows.TryGetValue(ally, out TargetArrow arrow) || arrow == null)
            {
                GameObject arrowObject = new GameObject($"TargetArrow_{ally.DisplayName}");
                arrowObject.transform.SetParent(transform, false);
                arrow = arrowObject.AddComponent<TargetArrow>();
                arrows[ally] = arrow;
            }

            return arrow;
        }

        private static Vector3 HeadPoint(BattleEntity entity)
        {
            return entity.transform.position + Vector3.up * 1.35f;
        }

        private static bool IsPointerOverSkillBar(Mouse mouse)
        {
            Vector2 screenPos = mouse.position.ReadValue();
            Vector2 guiPos = new Vector2(screenPos.x, Screen.height - screenPos.y);
            return BattleHUD.GetSkillBarRect().Contains(guiPos);
        }
    }
}
