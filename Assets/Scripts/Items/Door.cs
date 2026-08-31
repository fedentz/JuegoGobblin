using UnityEngine;
using UnityEngine.Localization;

namespace Project.Interaction
{
    public class Door : MonoBehaviour, IInteractable
    {
        [Header("Traba")]
        [Tooltip("Si está trabada, Interact() no hace nada hasta que se llame a Desbloquear() (hechizo Apertura/Desbloquear).")]
        [SerializeField] private bool trabada = false;

        [Header("UI")]
        [Tooltip("Asignar en el Inspector la entrada localizada 'Open' / 'Abrir'.")]
        [SerializeField] private LocalizedString actionVerb;

        public LocalizedString ActionVerb => actionVerb;

        private bool rota;

        public void Interact(GameObject interactor)
        {
            if (trabada) return; // trabada: no se puede romper hasta desbloquearla
            if (rota) return;
            Romper();
        }

        // Llamado por PlayerSpellCaster (hechizo Apertura/Desbloquear).
        public void Desbloquear()
        {
            trabada = false;
        }

        private void Romper()
        {
            rota = true;
            // TODO: VFX/sonido de romper cuando haya tiempo. Por ahora, sin animación: desaparece.
            gameObject.SetActive(false);
        }
    }
}