using UnityEngine;
using UnityEngine.UI;
using Project.Player;

namespace Project.UI
{
    // Vive en el Canvas del jugador. Escucha al PlayerAnxiety de ESE jugador
    // y actualiza el Slider de la barra de ansiedad.
    public class AnxietyUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private PlayerAnxiety anxiety;
        [SerializeField] private Slider anxietySlider;

        private void OnEnable()
        {
            if (anxiety == null || anxietySlider == null) return;

            anxiety.OnAnxietyChanged += HandleAnxietyChanged;

            anxietySlider.minValue = 0f;
            anxietySlider.maxValue = anxiety.MaxAnxiety;
            HandleAnxietyChanged(anxiety.CurrentAnxiety);
        }

        private void OnDisable()
        {
            if (anxiety == null) return;
            anxiety.OnAnxietyChanged -= HandleAnxietyChanged;
        }

        private void HandleAnxietyChanged(float value)
        {
            anxietySlider.value = value;
        }
    }
}