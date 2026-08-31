using UnityEngine;
using Project.Player;
using Project.UI;

namespace Project.Core
{
    /// <summary>
    /// Trigger en el piso. Al pararse un jugador ahí, se vende TODO su inventario
    /// automáticamente: el valor se suma al contador global (GameProgress), la bolsa
    /// queda vacía, y se muestra un cartel de feedback ("¡Vendiste X de oro!").
    /// Requiere: un Collider con Is Trigger tildado en este mismo GameObject.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SellPlatform : MonoBehaviour
    {
        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerInventory inventory = other.GetComponentInParent<PlayerInventory>();
            if (inventory == null) return;

            int valorVendido = inventory.VenderTodo();
            if (valorVendido <= 0) return;

            if (GameProgress.Instance != null)
            {
                GameProgress.Instance.AgregarVenta(valorVendido);
            }
            else
            {
                Debug.LogWarning("[SellPlatform] No hay GameProgress en la escena. Agregá el GameObject con GameProgress.");
            }

            VentaFeedbackUI.MostrarMensaje(valorVendido);
        }
    }
}