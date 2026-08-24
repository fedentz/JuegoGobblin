using System.Collections.Generic;
using UnityEngine;

namespace Project.Interaction
{
    public class LootContainer : MonoBehaviour, IInteractable
    {
        [SerializeField] private List<ItemData> items = new();

        private Queue<ItemData> queue;

        public ItemData CurrentItem => queue != null && queue.Count > 0 ? queue.Peek() : null;
        public bool HasItems => queue != null && queue.Count > 0;

        private void Awake()
        {
            queue = new Queue<ItemData>(items);
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