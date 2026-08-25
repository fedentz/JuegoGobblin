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
        [Tooltip("Asignar en el Inspector la entrada localizada 'Learn Ritual' / 'Aprender Ritual'.")]
        [SerializeField] private LocalizedString actionVerb;

        public LocalizedString ActionVerb => actionVerb;
        public SpellData Spell => spell;

        public void Interact(GameObject interactor)
        {
            var caster = interactor.GetComponent<PlayerSpellCaster>();
            if (caster != null) caster.LearnSpell(spell);
        }
    }
}