using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.UI
{
    // Componente genérico y reutilizable: elige 1 de 4 sprites según el color del
    // jugador, una sola vez al activarse. Para elementos de UI simples que solo
    // necesitan "pintarse del color del jugador" sin lógica reactiva adicional
    // (ej. el redondel de carga de acción). Para casos con más lógica (corazones,
    // slots de hechizo) seguimos usando sus scripts dedicados.
    public class PlayerColoredSprite : MonoBehaviour
    {
        [Tooltip("Cualquier componente que esté en el GameObject raíz del jugador (el que tiene PlayerInput).")]
        [SerializeField] private Component playerReference;
        [SerializeField] private Image target;
        [Tooltip("4 sprites, uno por color de jugador (índice 0-3).")]
        [SerializeField] private Sprite[] spritesByPlayer;

        private void OnEnable()
        {
            if (target == null || spritesByPlayer == null || spritesByPlayer.Length == 0) return;

            PlayerInput playerInput = playerReference != null ? playerReference.GetComponent<PlayerInput>() : null;
            int index = playerInput != null ? playerInput.playerIndex : 0;
            if (index < 0 || index >= spritesByPlayer.Length) return;

            target.sprite = spritesByPlayer[index];
        }
    }
}