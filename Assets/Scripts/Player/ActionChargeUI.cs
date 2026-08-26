using UnityEngine;
using UnityEngine.UI;
using Project.Player;

namespace Project.UI
{
    // Vive en el Canvas del jugador. Mientras se mantiene E sobre un cofre (u otra
    // acción con carga), muestra el redondel y lo va llenando según el progreso real.
    // Se oculta solo cuando no hay nada en curso.
    public class ActionChargeUI : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor interactor;
        [Tooltip("El GameObject completo del redondel (Action_Charge_Radial).")]
        [SerializeField] private GameObject root;
        [Tooltip("El Image con Image Type = Filled, Radial 360 (Circle).")]
        [SerializeField] private Image fillImage;

        private void Update()
        {
            if (interactor == null || root == null || fillImage == null) return;

            float progress = interactor.HoldProgress;
            bool shouldShow = progress > 0f;

            if (root.activeSelf != shouldShow)
            {
                root.SetActive(shouldShow);
            }

            if (shouldShow)
            {
                fillImage.fillAmount = progress;
            }
        }
    }
}