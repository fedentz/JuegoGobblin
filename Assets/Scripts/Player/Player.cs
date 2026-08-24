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
        [SerializeField] private float sprintSpeedMultiplier = 1.6f;
        [SerializeField] private float sneakySpeedMultiplier = 0.4f;

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
        [Tooltip("Valores que le mandamos al Blend Tree del Animator (parámetro Speed)")]
        [SerializeField] private float animSpeedIdle = 0f;
        [SerializeField] private float animSpeedSneaky = 0.3f;
        [SerializeField] private float animSpeedWalk = 0.6f;
        [SerializeField] private float animSpeedRun = 1f;

        private Rigidbody rb;
        private PlayerInput playerInput;
        private Vector2 moveInput;
        private Vector2 lookInput;
        private float pitch;
        private bool jumpQueued;
        private bool isSprinting;
        private bool isSneaking;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            playerInput = GetComponent<PlayerInput>();
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

        private void Update()
        {
            bool isMouseLook = playerInput.currentControlScheme == "Keyboard&Mouse";
            float sensitivity = isMouseLook ? mouseSensitivity : gamepadSensitivity * Time.deltaTime;

            transform.Rotate(Vector3.up, lookInput.x * sensitivity);

            pitch = Mathf.Clamp(pitch - lookInput.y * sensitivity, minPitch, maxPitch);
            cameraRig.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            // Sprint: Shift sostenido. Sneaky: Left Ctrl sostenido.
            // TODO: si vas a Input Actions "de verdad" (no polling directo de Keyboard),
            // reemplazar por OnSprint(InputValue)/OnSneak(InputValue) igual que OnMove/OnJump.
            if (Keyboard.current != null)
            {
                isSprinting = Keyboard.current.leftShiftKey.isPressed;
                isSneaking = Keyboard.current.leftCtrlKey.isPressed;
            }
        }

        private void FixedUpdate()
        {
            EnsureAnimatorController();

            float inputMagnitude = moveInput.magnitude;

            // Determinar el multiplicador de velocidad física según el modo.
            // Prioridad: sneaky > sprint > normal (no se puede sprintear agachado).
            float effectiveSpeed = moveSpeed;
            if (isSneaking)
            {
                effectiveSpeed *= sneakySpeedMultiplier;
            }
            else if (isSprinting)
            {
                effectiveSpeed *= sprintSpeedMultiplier;
            }

            if (inventory != null && inventory.IsOverweight)
            {
                effectiveSpeed *= overweightSpeedMultiplier;
            }

            Vector3 moveDir = transform.forward * moveInput.y + transform.right * moveInput.x;
            Vector3 targetVelocity = moveDir.normalized * effectiveSpeed;
            rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);

            // Elegir el escalón de animación según el mismo criterio que la velocidad física.
            float animSpeed;
            if (inputMagnitude < 0.05f)
            {
                animSpeed = animSpeedIdle;
            }
            else if (isSneaking)
            {
                animSpeed = animSpeedSneaky;
            }
            else if (isSprinting)
            {
                animSpeed = animSpeedRun;
            }
            else
            {
                animSpeed = animSpeedWalk;
            }

            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetFloat("Speed", animSpeed);
            }

            if (jumpQueued)
            {
                jumpQueued = false;
                if (IsGrounded())
                {
                    rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
                }
            }
        }

        private bool IsGrounded()
        {
            return Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer);
        }
    }
}