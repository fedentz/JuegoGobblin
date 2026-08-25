using System;
using UnityEngine;

namespace Project.Player
{
    public class PlayerAnxiety : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private float maxAnxiety = 100f;

        public float CurrentAnxiety { get; private set; }
        public float MaxAnxiety => maxAnxiety;

        public event Action<float> OnAnxietyChanged; // avisa el nuevo valor actual

        public void AddAnxiety(float amount)
        {
            CurrentAnxiety = Mathf.Clamp(CurrentAnxiety + amount, 0f, maxAnxiety);
            OnAnxietyChanged?.Invoke(CurrentAnxiety);
        }

        public void ReduceAnxiety(float amount)
        {
            CurrentAnxiety = Mathf.Clamp(CurrentAnxiety - amount, 0f, maxAnxiety);
            OnAnxietyChanged?.Invoke(CurrentAnxiety);
        }
    }
}