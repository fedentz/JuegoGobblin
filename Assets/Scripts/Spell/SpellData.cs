using UnityEngine;
using UnityEngine.Localization;

namespace Project.Spells
{
    [CreateAssetMenu(menuName = "Hechizos/Hechizo", fileName = "Hechizo_Nuevo")]
    public class SpellData : ScriptableObject
    {
        public LocalizedString displayName;
        public Sprite icon;
        public SpellEffectType effectType; // sin uso por ahora, no lo tocamos todavía
        public HechizoEfectoBase efecto;   // quién ejecuta el comportamiento

        [Tooltip("true = se gasta después de un solo uso (scrolls). false = queda permanente (rituales).")]
        public bool consumable;

        [Tooltip("Segundos de cooldown después de usarse. 0 = sin cooldown.")]
        public float cooldownDuration;

        [Header("Presentación (opcional, se va completando de a poco)")]
        public AudioClip sonidoAlCastear;
        public string animacionTrigger;
        public GameObject glowVfxPrefab;
    }
}