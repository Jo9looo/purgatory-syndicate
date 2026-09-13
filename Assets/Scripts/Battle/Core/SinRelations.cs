using System.Collections.Generic;

namespace PurgatorySyndicate.Battle
{
    /// <summary>
    /// Weakness cycle between the seven sins (each sin beats the next one):
    ///   Wrath > Sloth > Gluttony > Gloom > Pride > Envy > Lust > Wrath
    /// Attacking a unit whose affinity is beaten by the skill's sin = EFEKTIF (+50%).
    /// Attacking a unit whose affinity beats the skill's sin       = TAHAN  (-25%).
    /// </summary>
    public static class SinRelations
    {
        private const int SinCount = 7;

        /// <summary>The sin that the given sin is strong against.</summary>
        public static SinType GetPrey(SinType sin)
        {
            return (SinType)(((int)sin + 1) % SinCount);
        }

        /// <summary>
        /// Damage percent for an attack of [attackSin] against a defender with the
        /// given affinities: 150 (efektif), 75 (tahan), or 100 (netral).
        /// Efektif wins when both relations apply.
        /// </summary>
        public static int GetDamagePercent(SinType attackSin, IReadOnlyList<SinType> defenderAffinities)
        {
            bool effective = false;
            bool resisted = false;

            for (int i = 0; i < defenderAffinities.Count; i++)
            {
                SinType affinity = defenderAffinities[i];
                if (GetPrey(attackSin) == affinity)
                {
                    effective = true;
                }

                if (GetPrey(affinity) == attackSin)
                {
                    resisted = true;
                }
            }

            if (effective)
            {
                return 150;
            }

            return resisted ? 75 : 100;
        }
    }
}
