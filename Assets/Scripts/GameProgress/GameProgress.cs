using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Project.Player;

namespace Project.Core
{
    public class GameProgress : MonoBehaviour
    {
        public static GameProgress Instance { get; private set; }

        [Header("Config")]
        [SerializeField] private int objetivoTotal = 500;
        [Tooltip("Nombre EXACTO de la escena de Lobby (Build Settings) a la que volver al ganar.")]
        [SerializeField] private string nombreEscenaLobby = "Lobby";

        public int TotalVendido { get; private set; }
        public int ObjetivoTotal => objetivoTotal;

        public event Action<int, int> OnProgresoCambiado; // (totalVendido, objetivo)
        public event Action OnObjetivoCumplido;
        public event Action OnAllPlayersDead;

        private bool objetivoYaCumplido;
        private bool _allDeadFired;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void AgregarVenta(int valor)
        {
            if (valor <= 0 || objetivoYaCumplido) return;

            TotalVendido += valor;
            OnProgresoCambiado?.Invoke(TotalVendido, objetivoTotal);

            if (TotalVendido >= objetivoTotal)
            {
                objetivoYaCumplido = true;
                OnObjetivoCumplido?.Invoke();
                // La navegación al Lobby ya no es automática: GameOverUIController
                // escucha OnObjetivoCumplido y muestra la pantalla de victoria.
                // El botón de esa pantalla llama VolverAlMenu() que hace el LoadScene.
            }
        }

        public void NotifyPlayerDeath()
        {
            if (_allDeadFired) return;

            var players = PlayerRegistry.ActivePlayers;
            if (players.Count == 0) return;

            foreach (var player in players)
            {
                var ph = player.GetComponent<PlayerHealth>();
                if (ph == null || !ph.IsDead) return;
            }

            _allDeadFired = true;
            OnAllPlayersDead?.Invoke();
        }

        private void VolverAlLobby()
        {
            SceneManager.LoadScene(nombreEscenaLobby);
        }
    }
}
