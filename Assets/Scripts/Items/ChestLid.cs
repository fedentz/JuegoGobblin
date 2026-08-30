using UnityEngine;

namespace Project.Interaction
{
    // Va en un GameObject vacío ubicado en el BORDE de la tapa (la bisagra), no en su centro,
    // con el mesh de la tapa como hijo. Mismo patrón que Door.cs: rota su propio pivote.
    public class ChestLid : MonoBehaviour
    {
        [SerializeField] private float openAngle = 100f;
        [SerializeField] private float openSpeed = 2f;

        private bool isOpen;
        private Quaternion closedRot;
        private Quaternion openRot;

        private void Awake()
        {
            closedRot = transform.localRotation;
            openRot = closedRot * Quaternion.Euler(openAngle, 0f, 0f);
        }

        public void Open()
        {
            isOpen = true;
        }

        public void Close()
        {
            isOpen = false;
        }

        private void Update()
        {
            Quaternion target = isOpen ? openRot : closedRot;
            transform.localRotation = Quaternion.Slerp(transform.localRotation, target, openSpeed * Time.deltaTime);
        }
    }
}
