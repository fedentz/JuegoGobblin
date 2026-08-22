using System.Collections.Generic;
using UnityEngine;

namespace Project.Player
{
    public static class SplitScreenManager
    {
        private static readonly List<Camera> cameras = new();

        public static void Register(Camera cam) { cameras.Add(cam); Recalculate(); }
        public static void Unregister(Camera cam) { cameras.Remove(cam); Recalculate(); }

        private static void Recalculate()
        {
            int count = cameras.Count;
            for (int i = 0; i < count; i++)
            {
                cameras[i].rect = GetRect(i, count);
                var listener = cameras[i].GetComponent<AudioListener>();
                if (listener != null) listener.enabled = (i == 0);
            }
        }

        private static Rect GetRect(int index, int count)
        {
            if (count <= 1) return new Rect(0, 0, 1, 1);
            if (count == 2) return index == 0 ? new Rect(0, 0, 0.5f, 1) : new Rect(0.5f, 0, 0.5f, 1);
            if (count == 3)
                return index switch
                {
                    0 => new Rect(0f, 0.5f, 0.5f, 0.5f),
                    1 => new Rect(0.5f, 0.5f, 0.5f, 0.5f),
                    _ => new Rect(0.25f, 0f, 0.5f, 0.5f),
                };
            return index switch
            {
                0 => new Rect(0f, 0.5f, 0.5f, 0.5f),
                1 => new Rect(0.5f, 0.5f, 0.5f, 0.5f),
                2 => new Rect(0f, 0f, 0.5f, 0.5f),
                _ => new Rect(0.5f, 0f, 0.5f, 0.5f),
            };
        }
    }
}