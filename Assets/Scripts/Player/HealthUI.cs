using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Project.Player;

namespace Project.UI
{
    // Vive en el Canvas del jugador. Escucha al PlayerHealth de ESE jugador y
    // muestra/oculta cada Heart_X_Fill según cuántos corazones le quedan.
    public class HealthUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private PlayerHealth health;

        [Header("Corazones, en orden (índice 0 = primer corazón)")]
        [Tooltip("Arrastrar acá el Image de cada Heart_X_Fill, en orden.")]
        [SerializeField] private Image[] heartFills;

        [Header("Color por jugador")]
        [Tooltip("4 sprites de corazón lleno, uno por color de jugador (índice 0-3). Se aplica una sola vez.")]
        [SerializeField] private Sprite[] fillSpritesByPlayer;

        private void OnEnable()
        {
            if (health == null) return;

            health.OnHealthChanged += HandleHealthChanged;

            ApplyPlayerColor();
            HandleHealthChanged(health.CurrentHearts);
        }

        private void OnDisable()
        {
            if (health == null) return;
            health.OnHealthChanged -= HandleHealthChanged;
        }

        private void HandleHealthChanged(int currentHearts)
        {
            for (int i = 0; i < heartFills.Length; i++)
            {
                if (heartFills[i] == null) continue;
                heartFills[i].enabled = i < currentHearts;
            }
        }

        private void ApplyPlayerColor()
        {
            if (fillSpritesByPlayer == null || fillSpritesByPlayer.Length == 0) return;

            PlayerInput playerInput = health.GetComponent<PlayerInput>();
            int index = playerInput != null ? playerInput.playerIndex : 0;
            if (index < 0 || index >= fillSpritesByPlayer.Length) return;

            Sprite sprite = fillSpritesByPlayer[index];
            foreach (var fill in heartFills)
            {
                if (fill != null) fill.sprite = sprite;
            }
        }
    }
}