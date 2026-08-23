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

            foreach (var player in players)
            {
                int index = player.playerIndex;

                if (index < spawnPoints.Length)
                {
                    Vector3 spawnPos = spawnPoints[index].position;

                    if (Terrain.activeTerrain != null)
                    {
                        float terrainHeight = Terrain.activeTerrain.SampleHeight(spawnPos);
                        spawnPos.y = terrainHeight + 1f; // +1 para no aparecer justo pegado a la superficie
                    }

                    var rb = player.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                        rb.position = spawnPos;
                        rb.rotation = spawnPoints[index].rotation;
                    }
                    else
                    {
                        player.transform.position = spawnPos;
                        player.transform.rotation = spawnPoints[index].rotation;
                    }
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