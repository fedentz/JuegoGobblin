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

        public float CurrentWeight { get; private set; }
        public float MaxWeight => maxWeight;
        public bool IsOverweight => CurrentWeight >= overweightThreshold;

        public event Action<float> OnWeightChanged;
        public event Action<bool> OnOverweightChanged; // avisa solo cuando CRUZA el umbral

        public bool TryAddItem(float weight)
        {
            if (CurrentWeight + weight > maxWeight) return false;

            bool wasOverweight = IsOverweight;
            itemWeights.Add(weight);
            CurrentWeight += weight;

            OnWeightChanged?.Invoke(CurrentWeight);
            if (IsOverweight != wasOverweight) OnOverweightChanged?.Invoke(IsOverweight);

            return true;
        }

        public void RemoveItem(float weight)
        {
            bool wasOverweight = IsOverweight;
            itemWeights.Remove(weight);
            CurrentWeight = Mathf.Max(0f, CurrentWeight - weight);

            OnWeightChanged?.Invoke(CurrentWeight);
            if (IsOverweight != wasOverweight) OnOverweightChanged?.Invoke(IsOverweight);
        }
    }
}