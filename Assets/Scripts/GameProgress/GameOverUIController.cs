// ============================================================
// GameOverUIController — Setup en el Editor
// ============================================================
// Jerarquía esperada en la escena de Gameplay:
//
//   Canvas (Screen Space - Overlay, renderMode = ScreenSpaceOverlay)
//   └── GameOverUI          ← este GameObject lleva GameOverUIController
//       ├── PanelDerrota    ← GameObject con Image de fondo oscuro
//       │   └── TextoDerrota    ← TMP_Text ("¡Han caído todos!")
//       └── PanelVictoria   ← GameObject con Image de fondo claro
//           ├── TextoVictoria   ← TMP_Text ("¡Objetivo cumplido!")
//           └── BotonVolverMenu ← Button con Text hijo; onClick → (nada,
//                                 GameOverUIController.VolverAlMenu lo maneja
//                                 por evento — arrastralo al onClick del Inspector)
//
// Todos los paneles deben estar DESACTIVADOS por defecto en el Prefab/Scene.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Project.Core;
using Project.Player;

namespace Project.UI
{
    public class GameOverUIController : MonoBehaviour
    {
        [Header("Paneles raíz (activar/desactivar el GameObject entero)")]
        [SerializeField] private GameObject panelDerrota;
        [SerializeField] private GameObject panelVictoria;

        [Header("Textos opcionales — dejar vacío si no se usan")]
        [SerializeField] private TMP_Text textoDerrota;
        [SerializeField] private TMP_Text textoVictoria;

        // Un solo botón compartido basta: ambos paneles llevan al Lobby.
        // Si preferís botones separados, asigná el mismo método VolverAlMenu()
        // al onClick de cada uno desde el Inspector.
        [Header("Botón volver (puede ser uno por panel o compartido)")]
        [SerializeField] private Button botonVolverMenu;

        [Header("Nombre de escena Lobby (debe coincidir con GameProgress)")]
        [SerializeField] private string nombreEscenaLobby = "Lobby";

        private void Start()
        {
            if (panelDerrota != null) panelDerrota.SetActive(false);
            if (panelVictoria != null) panelVictoria.SetActive(false);

            if (botonVolverMenu != null)
                botonVolverMenu.onClick.AddListener(VolverAlMenu);
        }

        private void OnEnable()
        {
            if (GameProgress.Instance != null)
            {
                GameProgress.Instance.OnAllPlayersDead -= MostrarDerrota;
                GameProgress.Instance.OnAllPlayersDead += MostrarDerrota;
                GameProgress.Instance.OnObjetivoCumplido -= MostrarVictoria;
                GameProgress.Instance.OnObjetivoCumplido += MostrarVictoria;
            }
        }

        private void OnDisable()
        {
            if (GameProgress.Instance != null)
            {
                GameProgress.Instance.OnAllPlayersDead -= MostrarDerrota;
                GameProgress.Instance.OnObjetivoCumplido -= MostrarVictoria;
            }
        }

        private void OnDestroy()
        {
            if (GameProgress.Instance != null)
            {
                GameProgress.Instance.OnAllPlayersDead -= MostrarDerrota;
                GameProgress.Instance.OnObjetivoCumplido -= MostrarVictoria;
            }
            if (botonVolverMenu != null)
                botonVolverMenu.onClick.RemoveListener(VolverAlMenu);
        }

        public void MostrarDerrota()
        {
            if (panelVictoria != null) panelVictoria.SetActive(false);
            if (panelDerrota != null) panelDerrota.SetActive(true);
        }

        public void MostrarVictoria()
        {
            if (panelDerrota != null) panelDerrota.SetActive(false);
            if (panelVictoria != null) panelVictoria.SetActive(true);
        }

        public void VolverAlMenu()
        {
            ReiniciarJugadores();
            SceneManager.LoadScene(nombreEscenaLobby);
        }

        // Los jugadores quedan DontDestroyOnLoad desde que se unen en el Lobby (LobbyController),
        // así que sobreviven a cualquier cambio de escena. Hay que destruirlos acá para que la
        // próxima partida arranque con vida/inventario/hechizos limpios, no con lo que quedó de esta.
        private void ReiniciarJugadores()
        {
            var jugadores = new List<GobblinController>(PlayerRegistry.ActivePlayers);
            foreach (var jugador in jugadores)
            {
                if (jugador != null) Destroy(jugador.gameObject);
            }
        }
    }
}
