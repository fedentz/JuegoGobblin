using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Player
{
    public class PlayerInventory : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private float maxWeight = 10f;
        [SerializeField] private float overweightThreshold = 7.5f;

        private readonly List<float> itemWeights = new();
        private readonly List<int> itemValues = new();

        public float CurrentWeight { get; private set; }
        public float MaxWeight => maxWeight;
        public bool IsOverweight => CurrentWeight >= overweightThreshold;
        public int CurrentValue { get; private set; }

        public event Action<float> OnWeightChanged;
        public event Action<bool> OnOverweightChanged; // avisa solo cuando CRUZA el umbral
        public event Action<int> OnValueChanged;

        private bool capacidadAumentada = false;

        public void AumentarCapacidad(float multiplicador)
        {
            if (capacidadAumentada) return;
            maxWeight *= multiplicador;
            overweightThreshold *= multiplicador;
            capacidadAumentada = true;
        }

        public void QuitarCapacidad(float multiplicador)
        {
            if (!capacidadAumentada) return;
            maxWeight /= multiplicador;
            overweightThreshold /= multiplicador;
            capacidadAumentada = false;
        }

        // value es opcional (default 0) a propósito: los call sites viejos que solo
        // pasan weight siguen compilando igual, sin romper nada de lo que ya andaba.
        public bool TryAddItem(float weight, int value = 0)
        {
            if (CurrentWeight + weight > maxWeight) return false;

            bool wasOverweight = IsOverweight;
            itemWeights.Add(weight);
            itemValues.Add(value);
            CurrentWeight += weight;
            CurrentValue += value;

            OnWeightChanged?.Invoke(CurrentWeight);
            OnValueChanged?.Invoke(CurrentValue);
            if (IsOverweight != wasOverweight) OnOverweightChanged?.Invoke(IsOverweight);

            return true;
        }

        public void RemoveItem(float weight)
        {
            bool wasOverweight = IsOverweight;
            int index = itemWeights.IndexOf(weight);
            itemWeights.Remove(weight);
            if (index >= 0 && index < itemValues.Count)
            {
                CurrentValue -= itemValues[index];
                itemValues.RemoveAt(index);
                OnValueChanged?.Invoke(CurrentValue);
            }
            CurrentWeight = Mathf.Max(0f, CurrentWeight - weight);

            OnWeightChanged?.Invoke(CurrentWeight);
            if (IsOverweight != wasOverweight) OnOverweightChanged?.Invoke(IsOverweight);
        }

        // La "venta": vacía todo el inventario y devuelve cuánto valía en total.
        // Llamado por SellPlatform.
        public int VenderTodo()
        {
            int total = CurrentValue;

            itemWeights.Clear();
            itemValues.Clear();
            CurrentWeight = 0f;
            CurrentValue = 0;

            OnWeightChanged?.Invoke(CurrentWeight);
            OnValueChanged?.Invoke(CurrentValue);

            return total;
        }
    }
}