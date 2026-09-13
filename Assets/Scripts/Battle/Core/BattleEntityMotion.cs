using System.Collections;
using UnityEngine;

namespace PurgatorySyndicate.Battle
{
    /// <summary>
    /// Whitebox attack animations for capsules: lunge toward a target, return home,
    /// hit shake, and knockback. All coroutine-based so BattleManager can sequence them.
    /// </summary>
    public class BattleEntityMotion : MonoBehaviour
    {
        private Vector3 homePosition;
        private bool homeCaptured;

        /// <summary>Records the current position as this unit's battle station.</summary>
        public void CaptureHome()
        {
            homePosition = transform.position;
            homeCaptured = true;
        }

        /// <summary>Instantly snaps back to the battle station (used on restart).</summary>
        public void ResetPosition()
        {
            if (homeCaptured)
            {
                transform.position = homePosition;
            }
        }

        /// <summary>Dash from home toward a point (portion 0..1 of the distance).</summary>
        public IEnumerator LungeToward(Vector3 targetPosition, float portion = 0.55f, float duration = 0.15f)
        {
            if (!homeCaptured)
            {
                CaptureHome();
            }

            Vector3 end = Vector3.Lerp(homePosition, targetPosition, portion);
            end.y = homePosition.y;
            yield return MoveBetween(transform.position, end, duration);
        }

        /// <summary>Smoothly returns to the battle station.</summary>
        public IEnumerator ReturnHome(float duration = 0.18f)
        {
            if (!homeCaptured)
            {
                yield break;
            }

            yield return MoveBetween(transform.position, homePosition, duration);
        }

        /// <summary>Quick positional jitter when taking a hit.</summary>
        public IEnumerator HitShake(float duration = 0.25f, float magnitude = 0.12f)
        {
            Vector3 basePosition = transform.position;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                Vector3 offset = new Vector3(
                    Random.Range(-1f, 1f),
                    0f,
                    Random.Range(-1f, 1f)) * magnitude;
                transform.position = basePosition + offset;
                yield return null;
            }

            transform.position = basePosition;
        }

        /// <summary>Pushed away from the attacker, then springs back.</summary>
        public IEnumerator KnockbackFrom(Vector3 attackerPosition, float distance = 0.55f, float duration = 0.12f)
        {
            Vector3 direction = transform.position - attackerPosition;
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.back;

            Vector3 basePosition = transform.position;
            yield return MoveBetween(basePosition, basePosition + direction * distance, duration);
            yield return MoveBetween(transform.position, basePosition, duration * 1.5f);
        }

        private IEnumerator MoveBetween(Vector3 from, Vector3 to, float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                transform.position = Vector3.Lerp(from, to, t);
                yield return null;
            }

            transform.position = to;
        }
    }
}
