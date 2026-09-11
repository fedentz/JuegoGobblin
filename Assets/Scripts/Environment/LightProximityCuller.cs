using UnityEngine;
using Project.Player;

namespace Project.Environment
{
    // Apaga la Light cuando ningún jugador está a "radius" o menos, y la reactiva cuando
    // alguno vuelve a entrar en rango. Pensado como red de seguridad para escenas con muchas
    // luces (antorchas, etc.) en compus flojas. El radio de activación debería ser bastante
    // mayor al Range real de la luz, para que se prenda antes de que el jugador pueda verla.
    [RequireComponent(typeof(Light))]
    public class LightProximityCuller : MonoBehaviour
    {
        [Tooltip("Si ningún jugador está a esta distancia o menos, se apaga la luz. Dejar bastante más grande que el Range de la luz, para que prenda antes de que se la vea.")]
        [SerializeField] private float radius = 24f;
        [Tooltip("Cada cuántos segundos se revisa la distancia. No hace falta chequear todos los frames.")]
        [SerializeField] private float checkInterval = 0.5f;

        private Light lightComponent;
        private bool isActive = true;
        private float timer;

        private void Awake()
        {
            lightComponent = GetComponent<Light>();
            // Arrancamos el timer en un valor random para que no todas las luces del dungeon
            // chequeen la distancia en el mismo frame.
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
            lightComponent.enabled = true;
        }

        private void Deactivate()
        {
            isActive = false;
            lightComponent.enabled = false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
