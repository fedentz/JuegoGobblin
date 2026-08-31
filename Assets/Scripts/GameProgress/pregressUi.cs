using UnityEngine;
using TMPro;
using Project.Core;

namespace Project.UI
{
    /// <summary>
    /// Texto compartido (uno solo en pantalla, no es por-jugador).
    /// Reintenta conectarse a GameProgress.Instance cada frame hasta encontrarlo,
    /// para no depender de en qué orden Unity ejecuta los Awake() de cada script.
    /// </summary>
    public class ProgresoUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI texto;
        [SerializeField] private string formato = "{0} / {1}";

        private bool suscripto;

        private void OnEnable()
        {
            TrySuscribirse();
        }

        private void Update()
        {
            if (!suscripto) TrySuscribirse();
        }

        private void TrySuscribirse()
        {
            if (GameProgress.Instance == null) return;

            GameProgress.Instance.OnProgresoCambiado += ActualizarTexto;
            ActualizarTexto(GameProgress.Instance.TotalVendido, GameProgress.Instance.ObjetivoTotal);
            suscripto = true;
        }

        private void OnDisable()
        {
            if (suscripto && GameProgress.Instance != null)
            {
                GameProgress.Instance.OnProgresoCambiado -= ActualizarTexto;
            }
            suscripto = false;
        }

        private void ActualizarTexto(int totalVendido, int objetivo)
        {
            if (texto != null) texto.text = string.Format(formato, totalVendido, objetivo);
        }
    }
}