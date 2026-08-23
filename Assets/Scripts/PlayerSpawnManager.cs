using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Core
{
    public class PlayerSpawnManager : MonoBehaviour
    {
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private Color[] playerColors = new Color[]
        {
            Color.green, Color.blue, Color.red, Color.yellow
        };

        private void Start()
        {
            var players = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
            Debug.Log($"SpawnManager encontró {players.Length} jugadores");

            foreach (var player in players)
            {
                int index = player.playerIndex;

                if (index < spawnPoints.Length)
                {
                    var rb = player.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }

                    player.transform.position = spawnPoints[index].position;
                    player.transform.rotation = spawnPoints[index].rotation;
                }

                if (index < playerColors.Length)
                {
                    var renderer = player.GetComponentInChildren<Renderer>();
                    if (renderer != null) renderer.material.color = playerColors[index];
                }
            }
        }
    }
}