using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Project.Interaction;

namespace Project.Player
{
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Transform interactOrigin;
        [SerializeField] private PlayerInventory inventory;

        [Header("Config")]
        [SerializeField] private float interactRange = 3f;
        [SerializeField] private float interactRadius = 0.4f;
        [SerializeField] private LayerMask interactableLayer;
        [SerializeField] private float holdThreshold = 0.5f;

        // Eventos para que la UI reaccione sin que este script sepa nada de paneles/textos.
        public event Action<IInteractable> TargetChanged;
        public event Action<ItemData> ItemPickedUp;
        public event Action ItemResolved;
        public event Action<ItemData> InsufficientSpace;
        public event Action<ItemData> ItemSaved;

        public IInteractable CurrentTarget => currentTarget;
        public ItemData HeldItem => heldItem;

        // Nombre del botón real, según el dispositivo activo de ESTE jugador (teclado, gamepad, etc).
        // "E" en teclado, o el botón correspondiente ("X", "A"...) si juega con control.
        // Buscamos explícitamente el binding del dispositivo activo en vez de dejar que
        // GetBindingDisplayString() adivine entre varios bindings (Interact tiene 3: Keyboard/PS/Xbox).
        public string InteractKeyGlyph => GetGlyph(interactAction, "E");
        public string DiscardKeyGlyph => GetGlyph(discardAction, "Q");

        // 0 a 1: progreso de "mantener E" sobre un cofre para sacar un ítem.
        // 0 cuando no se está manteniendo nada (el redondel de carga debería ocultarse).
        public float HoldProgress => holdThreshold > 0f ? Mathf.Clamp01(holdTimer / holdThreshold) : 0f;

        private string GetGlyph(InputAction action, string fallback)
        {
            if (action == null) return fallback;

            bool usingGamepad = playerInput != null && playerInput.currentControlScheme != null
                && playerInput.currentControlScheme.Contains("Gamepad");
            string devicePathHint = usingGamepad ? "<Gamepad>" : "<Keyboard>";

            // 1) Buscar el binding que coincida con el dispositivo activo de ESTE jugador.
            for (int i = 0; i < action.bindings.Count; i++)
            {
                string path = action.bindings[i].effectivePath;
                if (path != null && path.Contains(devicePathHint))
                {
                    string display = action.GetBindingDisplayString(i);
                    if (!string.IsNullOrEmpty(display)) return display;
                }
            }

            // 2) Si no encontramos uno para ese dispositivo, devolver el primer binding con texto.
            for (int i = 0; i < action.bindings.Count; i++)
            {
                string display = action.GetBindingDisplayString(i);
                if (!string.IsNullOrEmpty(display)) return display;
            }

            return fallback;
        }

        private PlayerInput playerInput;
        private InputAction interactAction;
        private InputAction discardAction;

        private IInteractable currentTarget;
        private LootContainer currentLoot;
        private LootContainer lootWithPendingItem;
        private float holdTimer;
        private ItemData heldItem;
        private bool waitingForRelease;

        private void Awake()
        {
            playerInput = GetComponent<PlayerInput>();
            interactAction = playerInput.actions["Interact"];
            discardAction = playerInput.actions["Discard"];
        }

        private void Update()
        {
            DetectTarget();

            if (currentTarget == null)
            {
                ResetHoldState();
                return;
            }

            currentLoot = currentTarget as LootContainer;

            if (currentLoot != null) HandleLootInteraction();
            else HandleSimpleInteraction();
        }

        private void DetectTarget()
        {
            IInteractable previousTarget = currentTarget;
            currentTarget = null;

            if (Physics.SphereCast(interactOrigin.position, interactRadius, interactOrigin.forward, out RaycastHit hit, interactRange, interactableLayer))
            {
                currentTarget = hit.collider.GetComponent<IInteractable>();
            }

            if (previousTarget != currentTarget)
            {
                if (heldItem != null && lootWithPendingItem != null)
                {
                    lootWithPendingItem.ReturnItem(heldItem);
                    Debug.Log("Te alejaste, el ítem vuelve al final de la cola");
                    ItemResolved?.Invoke();
                }

                if (previousTarget is LootContainer previousLoot)
                {
                    previousLoot.CloseLid();
                }

                ResetHoldState();
                TargetChanged?.Invoke(currentTarget);
            }
        }

        private void ResetHoldState()
        {
            holdTimer = 0f;
            heldItem = null;
            lootWithPendingItem = null;
            currentLoot = null;
            waitingForRelease = false;
        }

        private void HandleSimpleInteraction()
        {
            if (interactAction.WasPressedThisFrame())
            {
                currentTarget.Interact(gameObject);
            }
        }

        private void HandleLootInteraction()
        {
            // Ya hay un item en la mano, esperando decisión (guardar o descartar)
            if (heldItem != null)
            {
                if (interactAction.WasPressedThisFrame())
                {
                    bool added = inventory != null && inventory.TryAddItem(heldItem.weight);
                    if (added)
                    {
                        Debug.Log("Ítem agregado a la bolsa");
                        ItemSaved?.Invoke(heldItem);

                        if (heldItem.grantedSpell != null)
                        {
                            var caster = GetComponent<PlayerSpellCaster>();
                            if (caster != null) caster.LearnSpell(heldItem.grantedSpell);
                        }
                    }
                    else
                    {
                        Debug.Log("No entra, bolsa llena, vuelve al cofre");
                        currentLoot.ReturnItem(heldItem);
                        InsufficientSpace?.Invoke(heldItem);
                    }
                    heldItem = null;
                    lootWithPendingItem = null;
                    holdTimer = 0f;
                    waitingForRelease = true; // exigir soltar E antes de aceptar el próximo hold
                    ItemResolved?.Invoke();
                }
                else if (discardAction.WasPressedThisFrame())
                {
                    Debug.Log("Ítem descartado, vuelve al final de la cola");
                    currentLoot.ReturnItem(heldItem);
                    heldItem = null;
                    lootWithPendingItem = null;
                    holdTimer = 0f;
                    ItemResolved?.Invoke();
                }
                return;
            }

            if (!currentLoot.HasItems)
            {
                holdTimer = 0f;
                return;
            }

            // Si venimos de confirmar un guardado, esperamos a que suelten E antes de contar hold de nuevo
            if (waitingForRelease)
            {
                if (!interactAction.IsPressed()) waitingForRelease = false;
                return;
            }

            if (interactAction.IsPressed())
            {
                holdTimer += Time.deltaTime;
                if (holdTimer >= holdThreshold)
                {
                    heldItem = currentLoot.TakeCurrent();
                    lootWithPendingItem = currentLoot;
                    holdTimer = 0f;
                    Debug.Log("Sacaste un ítem. E para guardar, Q para descartar");
                    ItemPickedUp?.Invoke(heldItem);
                }
            }
            else
            {
                holdTimer = 0f;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (interactOrigin == null) return;
            Gizmos.color = currentTarget != null ? Color.green : Color.red;
            Gizmos.DrawRay(interactOrigin.position, interactOrigin.forward * interactRange);
            Gizmos.DrawWireSphere(interactOrigin.position + interactOrigin.forward * interactRange, interactRadius);
        }
    }
}