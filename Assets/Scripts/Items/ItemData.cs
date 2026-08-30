using UnityEngine;
using UnityEngine.Localization;
using Project.Spells;

namespace Project.Interaction
{
    [CreateAssetMenu(fileName = "ItemData", menuName = "Loot/Item")]
    public class ItemData : ScriptableObject
    {
        public LocalizedString itemName;
        public Sprite icon;
        public float weight = 1f;
        public int value = 10;

        [Header("Scroll (opcional)")]
        [Tooltip("Si se asigna, guardar este ítem en la bolsa TAMBIÉN enseña/activa este hechizo en un slot.")]
        public SpellData grantedSpell;
    }
}