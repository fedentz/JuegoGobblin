using UnityEngine;
using Project.Player;

namespace Project.Environment
{
    // Apaga el ParticleSystem cuando ningún jugador está a "radius" o menos, y lo reactiva
    // cuando alguno vuelve a entrar en rango. Pensado para las antorchas y efectos ambientales
    // repartidos por el dungeon, que son las que más pesan en compus flojas.
    [RequireComponent(typeof(ParticleSystem))]
    public class ParticleProximityCuller : MonoBehaviour
    {
        [Tooltip("Si ningún jugador está a esta distancia o menos, se apaga el sistema de partículas.")]
        [SerializeField] private float radius = 15f;
        [Tooltip("Cada cuántos segundos se revisa la distancia. No hace falta chequear todos los frames.")]
        [SerializeField] private float checkInterval = 0.5f;

        private ParticleSystem ps;
        private bool isActive = true;
        private float timer;

        private void Awake()
        {
            ps = GetComponent<ParticleSystem>();
            // Arrancamos el timer en un valor random para que no todos los emisores
            // del dungeon chequeen la distancia en el mismo frame.
            timer = Random.Range(0f, checkInterval);
        }

        private void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = checkInterval;

            bool playerNearby = IsAnyPlayerWithinRadius();
            if (playerNearby && !isActive) Activate();
            else if (!playerNearby && isActive) Deactivate();
        }

        private bool IsAnyPlayerWithinRadius()
        {
            var players = PlayerRegistry.ActivePlayers;
            float sqrRadius = radius * radius;

            for (int i = 0; i < players.Count; i++)
            {
                GobblinController player = players[i];
                if (player == null) continue;

                if ((player.transform.position - transform.position).sqrMagnitude <= sqrRadius) return true;
            }

            return false;
        }

        private void Activate()
        {
            isActive = true;
            ps.Play(true);
        }

        private void Deactivate()
        {
            isActive = false;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
