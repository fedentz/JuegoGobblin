using System.Collections;
using UnityEngine;
using TMPro;

namespace Project.UI
{
    /// <summary>
    /// Cartel compartido tipo "¡Vendiste 38 de oro!" que aparece un par de segundos
    /// y se esconde solo. Acceso simple vía método estático (un solo cartel en pantalla).
    /// </summary>
    public class VentaFeedbackUI : MonoBehaviour
    {
        private static VentaFeedbackUI instance;

        [SerializeField] private TextMeshProUGUI texto;
        [SerializeField] private float duracionVisible = 2f;
        [SerializeField] private string formato = "¡Vendiste {0} de oro!";

        private Coroutine ocultarCoroutine;

        private void Awake()
        {
            instance = this;
            if (texto != null) texto.gameObject.SetActive(false);
        }

        public static void MostrarMensaje(int valor)
        {
            if (instance == null) return;
            instance.Mostrar(valor);
        }

        private void Mostrar(int valor)
        {
            if (texto == null) return;
            texto.text = string.Format(formato, valor);
            texto.gameObject.SetActive(true);

            if (ocultarCoroutine != null) StopCoroutine(ocultarCoroutine);
            ocultarCoroutine = StartCoroutine(OcultarTrasEspera());
        }

        private IEnumerator OcultarTrasEspera()
        {
            yield return new WaitForSeconds(duracionVisible);
            if (texto != null) texto.gameObject.SetActive(false);
        }
    }
}