using UnityEngine;

namespace Project.Enemy
{
    public class EnemyPatrol : MonoBehaviour
    {
        private enum PatrolMode { Loop, PingPong }

        [Header("Waypoints")]
        [SerializeField] private Transform[] waypoints;
        [SerializeField] private PatrolMode mode = PatrolMode.Loop;
        [SerializeField] private float arrivalDistance = 0.4f;
        [SerializeField] private float waitTimeAtWaypoint = 1.5f;

        private int currentIndex;
        private int direction = 1;
        private float waitTimer;
        private bool isWaiting;

        public bool HasWaypoints => waypoints != null && waypoints.Length > 0;
        public Transform CurrentWaypoint => HasWaypoints ? waypoints[currentIndex] : null;
        public bool IsWaiting => isWaiting;

        // Usado al volver de Chase/Search: retoma la patrulla desde el waypoint más cercano en vez de saltar al primero.
        public void ResetToClosest(Vector3 fromPosition)
        {
            if (!HasWaypoints) return;

            int closestIndex = 0;
            float closestDistance = float.MaxValue;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;
                float distance = Vector3.Distance(fromPosition, waypoints[i].position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestIndex = i;
                }
            }

            currentIndex = closestIndex;
            direction = 1;
            isWaiting = false;
        }

        // Llamar cada frame en estado Patrol. Devuelve true cuando cambió el waypoint objetivo recién este frame.
        public bool Tick(Vector3 currentPosition, float deltaTime)
        {
            if (!HasWaypoints) return false;

            if (isWaiting)
            {
                waitTimer -= deltaTime;
                if (waitTimer > 0f) return false;

                isWaiting = false;
                AdvanceIndex();
                return true;
            }

            if (Vector3.Distance(currentPosition, CurrentWaypoint.position) <= arrivalDistance)
            {
                isWaiting = true;
                waitTimer = waitTimeAtWaypoint;
            }

            return false;
        }

        private void AdvanceIndex()
        {
            if (waypoints.Length <= 1) return;

            if (mode == PatrolMode.Loop)
            {
                currentIndex = (currentIndex + 1) % waypoints.Length;
                return;
            }

            if (currentIndex + direction < 0 || currentIndex + direction >= waypoints.Length)
            {
                direction *= -1;
            }
            currentIndex += direction;
        }

        private void OnDrawGizmosSelected()
        {
            if (!HasWaypoints) return;

            Gizmos.color = Color.cyan;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;
                Gizmos.DrawWireSphere(waypoints[i].position, arrivalDistance);

                Transform next = mode == PatrolMode.Loop
                    ? waypoints[(i + 1) % waypoints.Length]
                    : (i + 1 < waypoints.Length ? waypoints[i + 1] : null);

                if (next != null) Gizmos.DrawLine(waypoints[i].position, next.position);
            }
        }
    }
}
