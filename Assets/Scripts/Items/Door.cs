using UnityEngine;
using UnityEngine.Localization;

namespace Project.Interaction
{
    public class Door : MonoBehaviour, IInteractable
    {
        [SerializeField] private float openAngle = 90f;
        [SerializeField] private float openSpeed = 2f;

        [Header("UI")]
        [Tooltip("Asignar en el Inspector la entrada localizada 'Open' / 'Abrir'.")]
        [SerializeField] private LocalizedString actionVerb;

        public LocalizedString ActionVerb => actionVerb;

        private bool isOpen;
        private Quaternion closedRot;
        private Quaternion openRot;

        private void Awake()
        {
            closedRot = transform.rotation;
            openRot = closedRot * Quaternion.Euler(0f, openAngle, 0f);
        }

        public void Interact(GameObject interactor)
        {
            isOpen = !isOpen;
        }

        private void Update()
        {
            Quaternion target = isOpen ? openRot : closedRot;
            transform.rotation = Quaternion.Slerp(transform.rotation, target, openSpeed * Time.deltaTime);
        }
    }
}