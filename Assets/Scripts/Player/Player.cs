using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Player
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PlayerInput))]
    public class GobblinController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Transform cameraRig;
        [SerializeField] private Transform groundCheck;
        [SerializeField] private Animator animator; // arrastrar acá el Animator de GOBLIN_GREEN
        [SerializeField] private RuntimeAnimatorController animatorController; // arrastrar acá el asset GobblinAnimator (mismo que Controller del Animator)

        [Header("Move")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float sneakySpeedMultiplier = 0.4f;

        [Header("Run Boost (disparado por hechizo/scroll, no por Shift)")]
        [SerializeField] private float runSpeedMultiplier = 1.6f;

        [Header("Weight Penalty")]
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private float overweightSpeedMultiplier = 0.6f;

        [Header("Jump")]
        [SerializeField] private float jumpForce = 6f;
        [SerializeField] private float groundCheckRadius = 0.2f;
        [SerializeField] private LayerMask groundLayer;

        [Header("Look")]
        [SerializeField] private float mouseSensitivity = 0.1f;
        [SerializeField] private float gamepadSensitivity = 120f;
        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;

        [Header("Animator Speed Steps")]
        [Tooltip("Valores que le mandamos al parámetro Speed (decide Idle/Walking/Running)")]
        [SerializeField] private float animSpeedIdle = 0f;
        [SerializeField] private float animSpeedWalk = 0.6f;
        [SerializeField] private float animSpeedRun = 1f;

        private Rigidbody rb;
        private PlayerInput playerInput;
        private Vector2 moveInput;
        private Vector2 lookInput;
        private float pitch;
        private bool jumpQueued;
        private bool isSneaking;
        private bool isRunBoostActive;
        private Coroutine runBoostCoroutine;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            playerInput = GetComponent<PlayerInput>();
        }

        // Llamado por PlayerSpellCaster cuando se usa el hechizo/scroll de correr.
        public void ActivateTemporaryRun(float duration)
        {
            if (runBoostCoroutine != null) StopCoroutine(runBoostCoroutine);
            runBoostCoroutine = StartCoroutine(RunBoostRoutine(duration));
        }

        private IEnumerator RunBoostRoutine(float duration)
        {
            isRunBoostActive = true;
            yield return new WaitForSeconds(duration);
            isRunBoostActive = false;
        }

        private void EnsureAnimatorController()
        {
            // Fix: si GOBLIN_GREEN estaba inactivo cuando el Animator se inicializó
            // (pasa en nuestro flujo de Lobby -> DontDestroyOnLoad -> spawn),
            // el runtimeAnimatorController queda en null aunque el Inspector
            // muestre el Controller asignado. Lo reforzamos acá a mano, en
            // FixedUpdate, porque el hijo GOBLIN_GREEN puede activarse
            // DESPUÉS del OnEnable del padre (este script vive en el padre).
            if (animator == null) return;

            if (animatorController != null
                && animator.runtimeAnimatorController == null
                && animator.gameObject.activeInHierarchy)
            {
                animator.runtimeAnimatorController = animatorController;
                animator.Rebind();   // fuerza a reconstruir el grafo interno ya mismo
                animator.Update(0f); // lo deja listo para recibir SetFloat en este mismo frame
            }
        }

        public void OnMove(InputValue value) => moveInput = value.Get<Vector2>();

        public void OnLook(InputValue value) => lookInput = value.Get<Vector2>();

        public void OnJump(InputValue value)
        {
            if (value.isPressed) jumpQueued = true;
        }

        public void OnSneak(InputValue value) => isSneaking = value.isPressed;

        public void OnEmoteCheer(InputValue value) => TryPlayEmote(value, "TriggerCheer");

        public void OnEmoteBaile1(InputValue value) => TryPlayEmote(value, "TriggerBaile1");

        public void OnEmoteBaile2(InputValue value) => TryPlayEmote(value, "TriggerBaile2");

        public void OnEmoteAura(InputValue value) => TryPlayEmote(value, "TriggerAura");

        private void TryPlayEmote(InputValue value, string triggerName)
        {
            if (!value.isPressed) return;
            if (animator == null) return;

            if (animator.GetCurrentAnimatorStateInfo(0).IsTag("Emote")) return;

            animator.SetTrigger(triggerName);
        }

        private void Update()
        {
            bool isMouseLook = playerInput.currentControlScheme == "Keyboard&Mouse";
            float sensitivity = isMouseLook ? mouseSensitivity : gamepadSensitivity * Time.deltaTime;

            transform.Rotate(Vector3.up, lookInput.x * sensitivity);

            pitch = Mathf.Clamp(pitch - lookInput.y * sensitivity, minPitch, maxPitch);
            cameraRig.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void FixedUpdate()
        {
            EnsureAnimatorController();

            float inputMagnitude = moveInput.magnitude;

            float effectiveSpeed = moveSpeed;
            if (isSneaking)
            {
                effectiveSpeed *= sneakySpeedMultiplier;
            }
            else if (isRunBoostActive)
            {
                effectiveSpeed *= runSpeedMultiplier;
            }

            if (inventory != null && inventory.IsOverweight)
            {
                effectiveSpeed *= overweightSpeedMultiplier;
            }

            Vector3 moveDir = transform.forward * moveInput.y + transform.right * moveInput.x;
            Vector3 targetVelocity = moveDir.normalized * effectiveSpeed;
            rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);

            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetFloat("MoveX", moveInput.x);
                animator.SetFloat("MoveZ", moveInput.y);

                animator.SetBool("IsSneaking", isSneaking && inputMagnitude > 0.05f);

                float animSpeed;
                if (inputMagnitude < 0.05f)
                {
                    animSpeed = animSpeedIdle;
                }
                else if (isRunBoostActive)
                {
                    animSpeed = animSpeedRun;
                }
                else
                {
                    animSpeed = animSpeedWalk;
                }

                animator.SetFloat("Speed", animSpeed);
            }

            if (jumpQueued)
            {
                jumpQueued = false;
                if (IsGrounded())
                {
                    rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);

                    if (animator != null && animator.runtimeAnimatorController != null)
                    {
                        animator.SetTrigger("TriggerJump");
                    }
                }
            }
        }

        private bool IsGrounded()
        {
            return Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer);
        }
    }
}