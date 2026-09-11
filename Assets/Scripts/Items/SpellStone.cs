using UnityEngine;
using UnityEngine.Localization;
using Project.Spells;
using Project.Player;

namespace Project.Interaction
{
    public class SpellStone : MonoBehaviour, IInteractable
    {
        [Header("Ritual")]
        [SerializeField] private SpellData spell;

        [Header("UI")]
        [Tooltip("Verbo al aprender (slots libres). Ej: 'Aprender Ritual'.")]
        [SerializeField] private LocalizedString actionVerb;
        [Tooltip("Verbo al reemplazar (slots llenos). Ej: 'Reemplazar Ritual'.")]
        [SerializeField] private LocalizedString replaceVerb;

        public LocalizedString ActionVerb => actionVerb;
        public LocalizedString ReplaceVerb => replaceVerb;
        public SpellData Spell => spell;

        public void Interact(GameObject interactor)
        {
            var caster = interactor.GetComponent<PlayerSpellCaster>();
            if (caster != null) caster.LearnSpell(spell);
        }
    }
}