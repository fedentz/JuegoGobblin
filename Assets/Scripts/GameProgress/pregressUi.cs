using UnityEngine;
using TMPro;
using Project.Core;

namespace Project.UI
{
    /// <summary>
    /// Texto compartido (no es por-jugador, es UNO solo en pantalla, o uno igual
    /// repetido en cada Canvas si querés que lo vea cada jugador en su propio HUD).
    /// </summary>
    public class ProgresoUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI texto;
        [SerializeField] private string formato = "{0} / {1}";

        private void OnEnable()
        {
            if (GameProgress.Instance != null)
            {
                GameProgress.Instance.OnProgresoCambiado += ActualizarTexto;
                ActualizarTexto(GameProgress.Instance.TotalVendido, GameProgress.Instance.ObjetivoTotal);
            }
        }

        private void OnDisable()
        {
            if (GameProgress.Instance != null)
            {
                GameProgress.Instance.OnProgresoCambiado -= ActualizarTexto;
            }
        }

        private void ActualizarTexto(int totalVendido, int objetivo)
        {
            if (texto != null) texto.text = string.Format(formato, totalVendido, objetivo);
        }
    }
}