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
                    Debug.Log($"Te alejaste, {heldItem.itemName} vuelve al final de la cola");
                }
                ResetHoldState();
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
                        Debug.Log($"Agregado {heldItem.itemName} a la bolsa");
                    }
                    else
                    {
                        Debug.Log($"No entra {heldItem.itemName}, bolsa llena, vuelve al cofre");
                        currentLoot.ReturnItem(heldItem);
                    }
                    heldItem = null;
                    lootWithPendingItem = null;
                    holdTimer = 0f;
                    waitingForRelease = true; // exigir soltar E antes de aceptar el próximo hold
                }
                else if (discardAction.WasPressedThisFrame())
                {
                    Debug.Log($"Descartado {heldItem.itemName}, vuelve al final de la cola");
                    currentLoot.ReturnItem(heldItem);
                    heldItem = null;
                    lootWithPendingItem = null;
                    holdTimer = 0f;
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
                    Debug.Log($"Sacaste: {heldItem.itemName}. E para guardar, Q para descartar");
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