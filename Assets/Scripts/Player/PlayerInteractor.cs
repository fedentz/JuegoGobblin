using System;
using System.Collections.Generic;
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

        // Mapeo de nombres genéricos de XInputController a nombres reales de Xbox.
        // Solo se aplica cuando specificLayout.Contains("XInputController") (cubre "XInputControllerWindows" y variantes).
        // Extendible: agregar entradas si se suman más botones al Input Actions asset.
        private static readonly Dictionary<string, string> XInputDisplayNames = new()
        {
            { "Button West",   "X" },
            { "Button North",  "Y" },
            { "Button East",   "B" },
            { "Button South",  "A" },
            { "D-Pad Down",    "↓" },
            { "D-Pad Left",    "←" },
            { "D-Pad Right",   "→" },
            { "D-Pad Up",      "↑" },
            { "Right Trigger", "RT" },
            { "Left Shoulder", "LB" },
            { "Right Shoulder","RB" },
        };

        private string GetGlyph(InputAction action, string fallback)
        {
            if (action == null) return fallback;

            bool usingGamepad = playerInput != null && playerInput.currentControlScheme != null
                && playerInput.currentControlScheme.Contains("Gamepad");

            // Identificar el layout del gamepad asignado a ESTE jugador (no el global Gamepad.current).
            // playerInput.devices contiene solo los dispositivos pareados con este PlayerInput específico.
            string specificLayout = null;
            if (usingGamepad && playerInput != null)
            {
                foreach (var device in playerInput.devices)
                {
                    if (device is Gamepad)
                    {
                        specificLayout = device.layout; // ej. "XInputControllerWindows", "DualShockGamepad"
                        break;
                    }
                }
            }

            // 1) Binding que coincida con el tipo base del gamepad de este jugador.
            // El path usa el tipo base (ej. "<XInputController>") mientras que specificLayout
            // puede ser una variante de plataforma (ej. "XInputControllerWindows").
            // Solución: extraer el tipo entre < > del path y ver si specificLayout empieza con él.
            if (usingGamepad && specificLayout != null)
            {
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    string path = action.bindings[i].effectivePath;
                    if (path == null) continue;
                    int lt = path.IndexOf('<'), gt = path.IndexOf('>');
                    string pathDeviceType = (lt >= 0 && gt > lt) ? path.Substring(lt + 1, gt - lt - 1) : null;
                    if (pathDeviceType == null || !specificLayout.StartsWith(pathDeviceType)) continue;
                    if (path.Contains("<Keyboard>") || path.Contains("<Mouse>") || path.Contains("<Gamepad>")) continue;

                    string display = action.GetBindingDisplayString(i);
                    if (string.IsNullOrEmpty(display)) continue;

                    if (specificLayout.Contains("XInputController")
                        && XInputDisplayNames.TryGetValue(display, out string friendly))
                        return friendly;

                    return display;
                }
            }

            // 2) Fallback gamepad: cualquier binding que no sea teclado/mouse (cubre gamepads no contemplados).
            if (usingGamepad)
            {
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    string path = action.bindings[i].effectivePath;
                    if (path != null && !path.Contains("<Keyboard>") && !path.Contains("<Mouse>"))
                    {
                        string display = action.GetBindingDisplayString(i);
                        if (!string.IsNullOrEmpty(display)) return display;
                    }
                }
            }

            // 3) Teclado/mouse.
            if (!usingGamepad)
            {
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    string path = action.bindings[i].effectivePath;
                    if (path != null && (path.Contains("<Keyboard>") || path.Contains("<Mouse>")))
                    {
                        string display = action.GetBindingDisplayString(i);
                        if (!string.IsNullOrEmpty(display)) return display;
                    }
                }
            }

            // 4) Red de seguridad: primer binding con texto disponible.
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
        private PlayerSpellCaster spellCaster;

        private IInteractable currentTarget;
        private LootContainer currentLoot;
        private LootContainer lootWithPendingItem;
        private SpellStone currentSpellStone;
        private float holdTimer;
        private ItemData heldItem;
        private bool waitingForRelease;

        // Verdadero cuando el jugador ya tiene el hechizo de la SpellStone actual equipado.
        public bool CurrentTargetAlreadyOwned => currentSpellStone != null && spellCaster != null && spellCaster.HasSpell(currentSpellStone.Spell);

        // Verdadero cuando el target actual es una SpellStone, todos los slots están llenos,
        // y el jugador NO tiene ya ese hechizo equipado.
        public bool CurrentTargetRequiresHold => currentSpellStone != null && spellCaster != null
            && spellCaster.AllSlotsFull && !spellCaster.HasSpell(currentSpellStone.Spell);

        private void Awake()
        {
            playerInput = GetComponent<PlayerInput>();
            interactAction = playerInput.actions["Interact"];
            discardAction = playerInput.actions["Discard"];
            spellCaster = GetComponent<PlayerSpellCaster>();
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
            currentSpellStone = currentTarget as SpellStone;

            if (currentSpellStone != null) HandleSpellStoneInteraction();
            else if (currentLoot != null) HandleLootInteraction();
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
            currentSpellStone = null;
            waitingForRelease = false;
        }

        private void HandleSpellStoneInteraction()
        {
            if (spellCaster == null || currentSpellStone == null) return;

            // Prioridad máxima: el jugador ya tiene este hechizo — no hacer nada.
            if (spellCaster.HasSpell(currentSpellStone.Spell)) return;

            if (!spellCaster.AllSlotsFull)
            {
                // Slot libre: tap normal, igual que cualquier IInteractable simple.
                if (interactAction.WasPressedThisFrame())
                    currentSpellStone.Interact(gameObject);
                return;
            }

            // Todos los slots llenos: hold para reemplazar el slot activo.
            // Reutiliza holdThreshold, holdTimer y waitingForRelease del flujo de cofres.
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
                    holdTimer = 0f;
                    waitingForRelease = true;
                    spellCaster.ReplaceSpell(spellCaster.SelectedSlot, currentSpellStone.Spell);
                }
            }
            else
            {
                holdTimer = 0f;
            }
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
                    bool added = inventory != null && inventory.TryAddItem(heldItem.weight, heldItem.value);
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