using System.Collections.Generic;
using UnityEngine;

namespace Project.Interaction
{
    [CreateAssetMenu(fileName = "LootTable", menuName = "Loot/Loot Table")]
    public class LootTable : ScriptableObject
    {
        [System.Serializable]
        private class Entry
        {
            public ItemData item;
            [Tooltip("Peso relativo de probabilidad de que salga este ítem. No tiene relación con el peso de inventario del ítem.")]
            public float dropWeight = 1f;
        }

        [SerializeField] private List<Entry> entries = new();

        public bool HasEntries => entries != null && entries.Count > 0;

        // Elige un ítem al azar: a mayor dropWeight, más probable.
        public ItemData RollItem()
        {
            if (!HasEntries) return null;

            float totalWeight = 0f;
            for (int i = 0; i < entries.Count; i++) totalWeight += Mathf.Max(0f, entries[i].dropWeight);
            if (totalWeight <= 0f) return null;

            float roll = Random.value * totalWeight;
            float accumulated = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                accumulated += Mathf.Max(0f, entries[i].dropWeight);
                if (roll <= accumulated) return entries[i].item;
            }

            return entries[^1].item;
        }

        // Tira "count" ítems (con reposición: puede repetir el mismo ítem).
        public List<ItemData> RollItems(int count)
        {
            var result = new List<ItemData>(Mathf.Max(0, count));
            for (int i = 0; i < count; i++)
            {
                ItemData item = RollItem();
                if (item != null) result.Add(item);
            }
            return result;
        }
    }
}
