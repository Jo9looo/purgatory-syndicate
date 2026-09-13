using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace PurgatorySyndicate.Battle
{
    /// <summary>
    /// Routes keyboard input (new Input System) to the battle systems per phase.
    /// Targeting itself is mouse-driven (BattleMouseController); the keyboard only:
    ///   Targeting     : 1/2/3 change skill of hovered/dragged ally, Enter lock all
    ///   ActionSorting : Enter/Space execute the turn
    ///   BattleOver    : M back to main menu
    ///   Any phase     : R restart battle
    /// </summary>
    public class BattleInputController : MonoBehaviour
    {
        private BattleManager manager;
        private TargetingController targeting;
        private BattleMouseController mouse;

        private void Awake()
        {
            manager = GetComponent<BattleManager>();
            targeting = GetComponent<TargetingController>();
            mouse = GetComponent<BattleMouseController>();
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || manager == null || targeting == null)
            {
                return;
            }

            if (kb.rKey.wasPressedThisFrame)
            {
                manager.RestartBattle();
                return;
            }

            switch (manager.CurrentState)
            {
                case BattleState.Targeting:
                    HandleTargetingInput(kb);
                    break;

                case BattleState.ActionSorting:
                    if (WasConfirmPressed(kb) || kb.spaceKey.wasPressedThisFrame)
                    {
                        manager.RequestExecution();
                    }
                    break;

                case BattleState.BattleOver:
                    if (kb.nKey.wasPressedThisFrame)
                    {
                        manager.AdvanceToNextStage();
                    }
                    else if (kb.mKey.wasPressedThisFrame)
                    {
                        BattleProgression.Reset();
                        SceneManager.LoadScene("MainMenu");
                    }
                    break;
            }
        }

        private void HandleTargetingInput(Keyboard kb)
        {
            if (!targeting.IsActive)
            {
                return;
            }

            // Skill keys apply to the ally under the cursor (or being dragged).
            BattleEntity contextAlly = mouse != null ? mouse.ContextAlly : null;
            if (contextAlly != null)
            {
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)
                {
                    targeting.SetSkill(contextAlly, 0);
                }
                else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
                {
                    targeting.SetSkill(contextAlly, 1);
                }
                else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame)
                {
                    targeting.SetSkill(contextAlly, 2);
                }
                else if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame)
                {
                    targeting.SetDefense(contextAlly);
                }
            }

            // Lock in the turn once every ally has a target.
            if (WasConfirmPressed(kb))
            {
                targeting.ConfirmAll();
            }
        }

        private static bool WasConfirmPressed(Keyboard kb)
        {
            return kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
        }
    }
}
