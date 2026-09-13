using System.Collections;
using UnityEngine;

namespace PurgatorySyndicate.Battle
{
    /// <summary>
    /// Brief color flash on capsules when a clash resolves — minimal visual feedback.
    /// </summary>
    public class BattleEntityPulse : MonoBehaviour
    {
        private Renderer capsuleRenderer;
        private Color originalColor;
        private Coroutine flashRoutine;

        private void Awake()
        {
            capsuleRenderer = GetComponent<Renderer>();
            if (capsuleRenderer != null)
            {
                originalColor = capsuleRenderer.material.color;
            }
        }

        public void Flash(Color flashColor)
        {
            if (capsuleRenderer == null)
            {
                return;
            }

            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
            }

            flashRoutine = StartCoroutine(FlashRoutine(flashColor));
        }

        private IEnumerator FlashRoutine(Color flashColor)
        {
            capsuleRenderer.material.color = flashColor;
            transform.localScale = Vector3.one * 1.15f;

            yield return new WaitForSeconds(0.2f);

            // Dead units keep their gray "defeated" tint instead of the alive color.
            BattleEntity entity = GetComponent<BattleEntity>();
            capsuleRenderer.material.color = entity != null && !entity.IsAlive
                ? new Color(0.3f, 0.3f, 0.3f)
                : originalColor;

            transform.localScale = Vector3.one;
            flashRoutine = null;
        }
    }
}
