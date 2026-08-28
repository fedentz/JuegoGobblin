using System.Collections.Generic;
using UnityEngine;
using Project.Player;

namespace Project.Enemy
{
    public class EnemyVision : MonoBehaviour
    {
        [Header("Cono de visión")]
        [SerializeField] private float viewRadius = 8f;
        [SerializeField, Range(0f, 360f)] private float viewAngle = 90f;
        [SerializeField] private float eyeHeight = 1.5f;

        [Header("Obstrucción (paredes)")]
        [Tooltip("Capas que bloquean la línea de visión. Por defecto todo menos 'Enemy', para que el enemigo no se detecte a sí mismo.")]
        [SerializeField] private LayerMask obstructionMask = ~0;

        [Header("Performance")]
        [Tooltip("Cada cuántos segundos se refresca la lista de jugadores en la escena.")]
        [SerializeField] private float playerRefreshInterval = 1f;

        private readonly List<GobblinController> knownPlayers = new List<GobblinController>();
        private float playerRefreshTimer;

        private void Reset()
        {
            obstructionMask = ~LayerMask.GetMask("Enemy");
        }

        private Vector3 EyePosition => transform.position + Vector3.up * eyeHeight;

        // Devuelve el jugador visible más cercano (dentro del radio, del cono y con línea de visión libre), o null.
        public GobblinController FindVisiblePlayer()
        {
            RefreshKnownPlayersIfNeeded();

            GobblinController closest = null;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < knownPlayers.Count; i++)
            {
                GobblinController candidate = knownPlayers[i];
                if (candidate == null) continue;

                float distance = Vector3.Distance(transform.position, candidate.transform.position);
                if (distance > viewRadius) continue;
                if (distance >= closestDistance) continue;

                if (IsWithinConeAndVisible(candidate))
                {
                    closest = candidate;
                    closestDistance = distance;
                }
            }

            return closest;
        }

        private bool IsWithinConeAndVisible(GobblinController candidate)
        {
            Vector3 toTarget = candidate.transform.position - transform.position;
            Vector3 flatDirection = new Vector3(toTarget.x, 0f, toTarget.z).normalized;
            if (flatDirection != Vector3.zero)
            {
                float angle = Vector3.Angle(transform.forward, flatDirection);
                if (angle > viewAngle * 0.5f)
                {
                    Debug.Log($"[EnemyVision] {name}: {candidate.name} fuera del cono (angulo={angle:F1}, limite={viewAngle * 0.5f:F1})", this);
                    return false;
                }
            }

            Vector3 eyePosition = EyePosition;
            Vector3 targetPoint = candidate.transform.position + Vector3.up * eyeHeight * 0.5f;
            Vector3 rayDir = targetPoint - eyePosition;
            float rayDistance = rayDir.magnitude;
            if (rayDistance <= 0.01f) return true;

            // Si algo (pared) golpea antes de llegar al jugador, la visión está bloqueada.
            // Si lo primero que golpea es el propio jugador buscado (su collider), cuenta como visión libre.
            bool hitSomething = Physics.Raycast(eyePosition, rayDir / rayDistance, out RaycastHit hit, rayDistance - 0.1f, obstructionMask);
            if (!hitSomething) return true;

            if (hit.collider.GetComponentInParent<GobblinController>() == candidate) return true;

            Debug.Log($"[EnemyVision] {name}: visión a {candidate.name} bloqueada por '{hit.collider.name}' (layer {LayerMask.LayerToName(hit.collider.gameObject.layer)}, dist={hit.distance:F2} de {rayDistance:F2})", this);
            return false;
        }

        private void RefreshKnownPlayersIfNeeded()
        {
            playerRefreshTimer -= Time.deltaTime;
            if (playerRefreshTimer > 0f && knownPlayers.Count > 0) return;

            playerRefreshTimer = playerRefreshInterval;
            knownPlayers.Clear();
            knownPlayers.AddRange(FindObjectsByType<GobblinController>(FindObjectsSortMode.None));
        }

        private Vector3 DirectionFromAngle(float angleDegrees)
        {
            float radians = (transform.eulerAngles.y + angleDegrees) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 eyePosition = EyePosition;
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.6f);

            Vector3 leftBoundary = DirectionFromAngle(-viewAngle * 0.5f);
            Vector3 rightBoundary = DirectionFromAngle(viewAngle * 0.5f);
            Gizmos.DrawLine(eyePosition, eyePosition + leftBoundary * viewRadius);
            Gizmos.DrawLine(eyePosition, eyePosition + rightBoundary * viewRadius);

            const int arcSegments = 20;
            Vector3 previousPoint = eyePosition + leftBoundary * viewRadius;
            for (int i = 1; i <= arcSegments; i++)
            {
                float t = (float)i / arcSegments;
                Vector3 point = eyePosition + DirectionFromAngle(Mathf.Lerp(-viewAngle * 0.5f, viewAngle * 0.5f, t)) * viewRadius;
                Gizmos.DrawLine(previousPoint, point);
                previousPoint = point;
            }
        }
    }
}
