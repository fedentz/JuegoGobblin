using System;
using UnityEngine;
using UnityEngine.AI;
using Project.Player;

namespace Project.Enemy
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(EnemyVision))]
    [RequireComponent(typeof(EnemyPatrol))]
    public class EnemyController : MonoBehaviour
    {
        private enum State { Patrol, LookingBack, Chasing, Searching }

        [Header("Refs")]
        [SerializeField] private Animator animator;

        [Header("Movimiento")]
        [SerializeField] private float patrolSpeed = 2.5f;
        [SerializeField] private float chaseSpeed = 4.5f;

        [Header("Detección")]
        [Tooltip("Cada cuántos segundos se revisa el cono de visión (perf).")]
        [SerializeField] private float detectionCheckInterval = 0.15f;
        [Tooltip("Cuánto tiempo sigue persiguiendo tras perder de vista al jugador antes de pasar a Search.")]
        [SerializeField] private float loseSightGraceTime = 2.5f;

        [Header("Búsqueda")]
        [Tooltip("Cuánto espera en el último punto visto antes de volver a patrullar.")]
        [SerializeField] private float searchWaitTime = 3f;

        [Header("Mirar atrás (durante patrulla)")]
        [SerializeField, Range(0f, 1f)] private float lookBackChance = 0.15f;
        [SerializeField] private float lookBackCheckInterval = 4f;
        [SerializeField] private float lookBackHoldTime = 1.5f;
        [SerializeField] private float lookBackTurnSpeed = 180f;

        [Header("Daño de contacto")]
        [Tooltip("Vidas que le quita al jugador al tocarlo (PlayerHealth.TakeDamage).")]
        [SerializeField] private int contactDamage = 1;
        [Tooltip("Tiempo mínimo entre golpes al mismo jugador mientras siguen en contacto.")]
        [SerializeField] private float attackCooldown = 1f;

        public event Action<GobblinController> OnPlayerSpotted;
        public event Action OnPlayerLost;

        private NavMeshAgent agent;
        private EnemyVision vision;
        private EnemyPatrol patrol;

        private State state;
        private float detectionTimer;
        private float lookBackTimer;
        private float stateTimer;
        private float attackCooldownTimer;
        private GobblinController chaseTarget;
        private Vector3 lastKnownPosition;
        private Quaternion facingBeforeLookBack;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            vision = GetComponent<EnemyVision>();
            patrol = GetComponent<EnemyPatrol>();
        }

        private void Start()
        {
            state = State.Patrol;
            agent.speed = patrolSpeed;
            lookBackTimer = lookBackCheckInterval;
        }

        private void Update()
        {
            if (attackCooldownTimer > 0f) attackCooldownTimer -= Time.deltaTime;

            detectionTimer -= Time.deltaTime;
            bool checkedVision = false;
            GobblinController visiblePlayer = null;
            if (detectionTimer <= 0f)
            {
                detectionTimer = detectionCheckInterval;
                visiblePlayer = vision.FindVisiblePlayer();
                checkedVision = true;
            }

            switch (state)
            {
                case State.Patrol: TickPatrol(visiblePlayer, checkedVision); break;
                case State.LookingBack: TickLookingBack(visiblePlayer, checkedVision); break;
                case State.Chasing: TickChasing(visiblePlayer, checkedVision); break;
                case State.Searching: TickSearching(visiblePlayer, checkedVision); break;
            }

            UpdateAnimator();
        }

        private void TickPatrol(GobblinController visiblePlayer, bool checkedVision)
        {
            if (checkedVision && visiblePlayer != null)
            {
                EnterChasing(visiblePlayer);
                return;
            }

            if (!patrol.HasWaypoints) return;

            agent.isStopped = patrol.IsWaiting;
            if (!patrol.IsWaiting) agent.SetDestination(patrol.CurrentWaypoint.position);

            if (patrol.Tick(transform.position, Time.deltaTime))
            {
                agent.SetDestination(patrol.CurrentWaypoint.position);
            }

            lookBackTimer -= Time.deltaTime;
            if (lookBackTimer <= 0f)
            {
                lookBackTimer = lookBackCheckInterval;
                if (UnityEngine.Random.value < lookBackChance) EnterLookingBack();
            }
        }

        private void EnterLookingBack()
        {
            state = State.LookingBack;
            stateTimer = lookBackHoldTime;
            facingBeforeLookBack = transform.rotation;
            agent.isStopped = true;
            agent.updateRotation = false;
        }

        private void TickLookingBack(GobblinController visiblePlayer, bool checkedVision)
        {
            if (checkedVision && visiblePlayer != null)
            {
                EnterChasing(visiblePlayer);
                return;
            }

            Quaternion targetRotation = facingBeforeLookBack * Quaternion.Euler(0f, 180f, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, lookBackTurnSpeed * Time.deltaTime);

            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                agent.updateRotation = true;
                agent.isStopped = false;
                state = State.Patrol;
            }
        }

        private void EnterChasing(GobblinController target)
        {
            state = State.Chasing;
            chaseTarget = target;
            lastKnownPosition = target.transform.position;
            stateTimer = loseSightGraceTime;
            agent.updateRotation = true;
            agent.isStopped = false;
            agent.speed = chaseSpeed;
            Debug.Log($"[Enemy] {name} detectó a {target.name} -> Chasing", this);
            OnPlayerSpotted?.Invoke(target);
        }

        private void TickChasing(GobblinController visiblePlayer, bool checkedVision)
        {
            if (chaseTarget == null)
            {
                EnterPatrol();
                return;
            }

            agent.SetDestination(chaseTarget.transform.position);

            if (!checkedVision) return;

            if (visiblePlayer == chaseTarget)
            {
                lastKnownPosition = chaseTarget.transform.position;
                stateTimer = loseSightGraceTime;
                return;
            }

            stateTimer -= detectionCheckInterval;
            if (stateTimer <= 0f) EnterSearching();
        }

        private void EnterSearching()
        {
            state = State.Searching;
            chaseTarget = null;
            agent.speed = patrolSpeed;
            agent.SetDestination(lastKnownPosition);
            stateTimer = searchWaitTime;
            Debug.Log($"[Enemy] {name} perdió de vista al jugador -> Searching", this);
            OnPlayerLost?.Invoke();
        }

        private void TickSearching(GobblinController visiblePlayer, bool checkedVision)
        {
            if (checkedVision && visiblePlayer != null)
            {
                EnterChasing(visiblePlayer);
                return;
            }

            if (agent.pathPending || agent.remainingDistance > agent.stoppingDistance) return;

            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f) EnterPatrol();
        }

        private void EnterPatrol()
        {
            if (state == State.Chasing || state == State.Searching)
            {
                Debug.Log($"[Enemy] {name} retoma la patrulla", this);
            }
            state = State.Patrol;
            chaseTarget = null;
            agent.speed = patrolSpeed;
            lookBackTimer = lookBackCheckInterval;
            if (patrol.HasWaypoints) patrol.ResetToClosest(transform.position);
        }

        private void UpdateAnimator()
        {
            if (animator == null) return;

            float speedParam;
            if (agent.velocity.magnitude < 0.1f) speedParam = 0f;
            else speedParam = state == State.Chasing ? 1f : 0.6f;

            animator.SetFloat("Speed", speedParam);
        }

        private void OnTriggerEnter(Collider other) => TryDealDamage(other);

        private void OnTriggerStay(Collider other) => TryDealDamage(other);

        private void TryDealDamage(Collider other)
        {
            if (attackCooldownTimer > 0f) return;

            PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
            if (health == null) return;

            health.TakeDamage(contactDamage);
            attackCooldownTimer = attackCooldown;
        }
    }
}
