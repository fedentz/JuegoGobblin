using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Core
{
    public class PlayerSpawnManager : MonoBehaviour
    {
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private Material[] playerMaterials;

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
                        spawnPos.y = terrainHeight + 1f;
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

                if (index < playerMaterials.Length)
                {
                    var renderer = player.GetComponentInChildren<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material = playerMaterials[index];
                    }
                }
            }
        }
    }
}