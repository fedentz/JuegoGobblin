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
        [SerializeField] private Animator animator; // NUEVO: arrastrar acá el Animator de GOBLIN_GREEN

        [Header("Move")]
        [SerializeField] private float moveSpeed = 5f;

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

        private Rigidbody rb;
        private PlayerInput playerInput;
        private Vector2 moveInput;
        private Vector2 lookInput;
        private float pitch;
        private bool jumpQueued;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            playerInput = GetComponent<PlayerInput>();
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
        }

        private void FixedUpdate()
        {
            float effectiveSpeed = moveSpeed;
            if (inventory != null && inventory.IsOverweight)
            {
                effectiveSpeed *= overweightSpeedMultiplier;
            }

            Vector3 moveDir = transform.forward * moveInput.y + transform.right * moveInput.x;
            Vector3 targetVelocity = moveDir.normalized * effectiveSpeed;
            rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);

            // NUEVO: le pasamos al Animator qué tan rápido nos estamos moviendo
            if (animator != null)
            {
                animator.SetFloat("Speed", moveDir.magnitude);
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