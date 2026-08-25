using UnityEngine;
using UnityEngine.Localization;

namespace Project.Spells
{
    [System.Serializable]
    public class SpellData
    {
        public LocalizedString displayName;
        public Sprite icon;
        public SpellEffectType effectType;

        [Tooltip("true = se gasta después de un solo uso (scrolls). false = queda permanente (rituales).")]
        public bool consumable;

        [Tooltip("Segundos de cooldown después de usarse. 0 = sin cooldown.")]
        public float cooldownDuration;
    }
}