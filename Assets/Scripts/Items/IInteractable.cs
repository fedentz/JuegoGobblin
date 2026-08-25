using UnityEngine;
using UnityEngine.Localization;

namespace Project.Interaction
{
    public interface IInteractable
    {
        void Interact(GameObject interactor);

        // Verbo localizado a mostrar ("Abrir", "Buscar", "Aprender Ritual").
        // La UI arma el texto final como "PRESS {tecla}: {ActionVerb}".
        LocalizedString ActionVerb { get; }
    }
}