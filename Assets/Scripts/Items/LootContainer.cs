using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace Project.Interaction
{
    public class LootContainer : MonoBehaviour, IInteractable
    {
        [SerializeField] private LootTable lootTable;
        [SerializeField] private int minItems = 1;
        [SerializeField] private int maxItems = 3;

        [Header("UI")]
        [Tooltip("Asignar en el Inspector la entrada localizada 'Search' / 'Buscar'.")]
        [SerializeField] private LocalizedString actionVerb;

        public LocalizedString ActionVerb => actionVerb;

        private Queue<ItemData> queue;

        public ItemData CurrentItem => queue != null && queue.Count > 0 ? queue.Peek() : null;
        public bool HasItems => queue != null && queue.Count > 0;

        private void Awake()
        {
            List<ItemData> rolled = lootTable != null
                ? lootTable.RollItems(Random.Range(minItems, maxItems + 1))
                : new List<ItemData>();
            queue = new Queue<ItemData>(rolled);
        }

        public void Interact(GameObject interactor) { }

        public ItemData TakeCurrent()
        {
            return queue.Count > 0 ? queue.Dequeue() : null;
        }

        public void ReturnItem(ItemData item)
        {
            if (item != null) queue.Enqueue(item);
        }
    }
}