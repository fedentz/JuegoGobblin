using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Core
{
    /// <summary>
    /// Contador global compartido entre los 4 jugadores (juego local, mismo dispositivo,
    /// no hace falta networking). Vive en la escena de Gameplay, se resetea cada partida.
    /// Cuando se alcanza el objetivo, vuelve al Lobby (por ahora no hay Win Scene separada).
    /// </summary>
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

        private bool objetivoYaCumplido;

        private void Awake()
        {
            // Singleton simple. No usamos DontDestroyOnLoad porque esto vive SOLO
            // durante la partida (se recrea/resetea cada vez que se carga Gameplay).
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
                VolverAlLobby();
            }
        }

        private void VolverAlLobby()
        {
            SceneManager.LoadScene(nombreEscenaLobby);
        }
    }
}