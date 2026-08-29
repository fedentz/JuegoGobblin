using System.Collections.Generic;
using UnityEngine;
using Project.Player;

namespace Project.Enemy
{
    public class EnemyHearing : MonoBehaviour
    {
        [Header("Oído")]
        [Tooltip("Qué tan lejos puede escuchar este enemigo en particular.")]
        [SerializeField] private float radioOido = 3f;

        [Header("Performance")]
        [Tooltip("Cada cuántos segundos se refresca la lista de jugadores en la escena.")]
        [SerializeField] private float playerRefreshInterval = 1f;

        private readonly List<PlayerNoise> knownPlayers = new List<PlayerNoise>();
        private float playerRefreshTimer;

        // Intersección de esferas a mano, sin física: distancia entre enemigo y jugador
        // contra la suma de "cuánto ruido hace" (radioActual del jugador) y "cuánto oye" el enemigo.
        public PlayerNoise FindHeardPlayer()
        {
            RefreshKnownPlayersIfNeeded();

            PlayerNoise closest = null;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < knownPlayers.Count; i++)
            {
                PlayerNoise candidate = knownPlayers[i];
                if (candidate == null) continue;

                float distance = Vector3.Distance(transform.position, candidate.transform.position);
                if (distance >= closestDistance) continue;

                if (distance < candidate.RadioActual + radioOido)
                {
                    closest = candidate;
                    closestDistance = distance;
                }
            }

            return closest;
        }

        private void RefreshKnownPlayersIfNeeded()
        {
            playerRefreshTimer -= Time.deltaTime;
            if (playerRefreshTimer > 0f && knownPlayers.Count > 0) return;

            playerRefreshTimer = playerRefreshInterval;
            knownPlayers.Clear();
            knownPlayers.AddRange(FindObjectsByType<PlayerNoise>(FindObjectsSortMode.None));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, radioOido);
        }
    }
}
