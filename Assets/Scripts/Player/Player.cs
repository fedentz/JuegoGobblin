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

        [Header("Encogerse (offset de cámara)")]
        [Tooltip("Cuánto baja la cámara respecto a su posición normal cuando la escala llega a 0. Ajustable a mano, sin importar si Camera Rig tiene Y=0.")]
        [SerializeField] private float alturaOjosParaEncogerse = 0.8f;

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
        private Vector3 cameraRigPosicionOriginal;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            playerInput = GetComponent<PlayerInput>();
            if (cameraRig != null) cameraRigPosicionOriginal = cameraRig.localPosition;
        }

        // Llamado por PlayerSpellCaster (Encogerse): baja/sube la altura de la cámara.
        // Usa un offset propio (alturaOjosParaEncogerse) en vez de escalar la posición
        // actual, porque Camera Rig puede tener Y=0 (altura viene de otro lado de la
        // jerarquía) y escalar un 0 sigue dando 0.
        public void AjustarAlturaCamara(float escala)
        {
            if (cameraRig == null) return;
            float offset = alturaOjosParaEncogerse * (1f - escala);
            cameraRig.localPosition = new Vector3(
                cameraRigPosicionOriginal.x,
                cameraRigPosicionOriginal.y - offset,
                cameraRigPosicionOriginal.z);
        }

        // Llamado por PlayerSpellCaster: prende/apaga el boost. La duración
        // ahora la maneja PlayerSpellCaster (RevertirTrasDuracion), no este script.
        public void SetRunBoost(bool activo)
        {
            isRunBoostActive = activo;
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

        private float pendingYaw;

        private void Update()
        {
            bool isMouseLook = playerInput.currentControlScheme == "Keyboard&Mouse";
            float sensitivity = isMouseLook ? mouseSensitivity : gamepadSensitivity * Time.deltaTime;

            // Acumulamos el yaw acá (Update puede correr más seguido que FixedUpdate)
            // pero lo aplicamos recién en FixedUpdate vía rb.MoveRotation, para no pelear
            // contra el Rigidbody rotando el Transform directo (eso causaba el shake/tembleque).
            pendingYaw += lookInput.x * sensitivity;

            pitch = Mathf.Clamp(pitch - lookInput.y * sensitivity, minPitch, maxPitch);
            cameraRig.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void FixedUpdate()
        {
            EnsureAnimatorController();

            if (pendingYaw != 0f)
            {
                rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, pendingYaw, 0f));
                pendingYaw = 0f;
            }

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