using UnityEngine;

namespace Project.Interaction
{
    public class SpellStone : MonoBehaviour, IInteractable
    {
        [SerializeField] private string spellName = "Lumos";

        public void Interact(GameObject interactor)
        {
            Debug.Log($"{interactor.name} aprendió el ritual: {spellName}");
            // TODO: conectar con el sistema de hechizos (rituales permanentes) cuando lo armemos
        }
    }
}