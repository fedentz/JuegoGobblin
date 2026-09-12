using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.UI;
using Project.Interaction;
using Project.Player;
using Project.Spells;

namespace Project.UI
{
    // Vive en el Center_Group del Canvas del jugador. Escucha al PlayerInteractor
    // de ESE mismo jugador y decide qué panel mostrar: prompt genérico (Puerta/Cofre),
    // preview de ritual (Piedra de Hechizo), panel de pickup de ítem, o mensaje de error.
    public class InteractionPromptUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private PlayerInteractor interactor;

        [Header("Generic Prompt (Puerta / Cofre antes de agarrar)")]
        [SerializeField] private GameObject genericPromptRoot;
        [SerializeField] private TMP_Text genericPromptText;

        [Header("Ritual Preview (Piedra de Hechizo)")]
        [SerializeField] private GameObject ritualPreviewRoot;
        [Tooltip("Capa de fondo: marco de Ritual o de Scroll, según spell.consumable.")]
        [SerializeField] private Image ritualTypeIcon;
        [Tooltip("Capa de encima: el ícono puntual del hechizo (spell.icon).")]
        [SerializeField] private Image ritualSpellIcon;
        [SerializeField] private Sprite ritualFrameSprite; // fondo para hechizos NO consumibles (rituales)
        [SerializeField] private Sprite scrollFrameSprite; // fondo para hechizos consumibles (scrolls)
        [SerializeField] private TMP_Text ritualNameText;
        [SerializeField] private TMP_Text ritualPromptText;

        [Header("Item Pickup Panel")]
        [SerializeField] private GameObject itemPanelRoot;
        [SerializeField] private Image itemPanelBackground;
        [SerializeField] private Image itemIcon;
        [SerializeField] private TMP_Text itemNameText;
        [SerializeField] private TMP_Text itemValueText;
        [SerializeField] private TMP_Text itemWeightText;
        [SerializeField] private TMP_Text itemSaveLine;
        [SerializeField] private TMP_Text itemReturnLine;
        [Tooltip("4 sprites de fondo del panel, uno por color de jugador (índice 0-3).")]
        [SerializeField] private Sprite[] itemPanelSpritesByPlayerColor;

        [Header("Localized Labels (crear en la String Table UI_HUD)")]
        [Tooltip("Solo la palabra 'Save'/'Guardar' — el 'E: ' se arma en código.")]
        [SerializeField] private LocalizedString saveLabel;
        [Tooltip("Solo la palabra 'Return'/'Devolver' — el 'Q: ' se arma en código.")]
        [SerializeField] private LocalizedString returnLabel;
        [Tooltip("Prefijo de hold, ej: 'Mantener' / 'Hold'. Se pone ANTES del glyph: 'Mantener E: Reemplazar'.")]
        [SerializeField] private LocalizedString holdPrefix;
        [Tooltip("Formato con el nombre del hechizo nuevo, ej: '¡{0} equipado!'")]
        [SerializeField] private LocalizedString spellReplacedFormat;
        [Tooltip("Cuando el jugador ya tiene ese hechizo equipado. Ej: 'Ya tenés este hechizo'.")]
        [SerializeField] private LocalizedString spellAlreadyOwnedMessage;

        [Header("Error Message")]
        [SerializeField] private TMP_Text errorText;
        [SerializeField] private float errorDuration = 2.5f;
        [SerializeField] private LocalizedString notEnoughSpaceMessage;

        [Header("Success Message")]
        [SerializeField] private TMP_Text successText;
        [SerializeField] private float successDuration = 2f;
        [Tooltip("Formato con el nombre del ítem, ej: \"{0} guardado!\"")]
        [SerializeField] private LocalizedString itemSavedFormat;

        private PlayerSpellCaster spellCaster;
        private Coroutine errorCoroutine;
        private Coroutine successCoroutine;
        private Coroutine spellReplacedCoroutine;

        private void OnEnable()
        {
            if (interactor == null) return;

            spellCaster = interactor.GetComponent<PlayerSpellCaster>();

            interactor.TargetChanged += HandleTargetChanged;
            interactor.ItemPickedUp += HandleItemPickedUp;
            interactor.ItemResolved += HandleItemResolved;
            interactor.InsufficientSpace += HandleInsufficientSpace;
            interactor.ItemSaved += HandleItemSaved;

            if (spellCaster != null)
            {
                spellCaster.SlotChanged += HandleSlotChangedWhileLookingAtStone;
                spellCaster.OnSpellReplaced += HandleSpellReplaced;
            }

            HideAllPanels();
            HideErrorAndSuccess();
        }

        private void OnDisable()
        {
            if (interactor == null) return;

            interactor.TargetChanged -= HandleTargetChanged;
            interactor.ItemPickedUp -= HandleItemPickedUp;
            interactor.ItemResolved -= HandleItemResolved;
            interactor.InsufficientSpace -= HandleInsufficientSpace;
            interactor.ItemSaved -= HandleItemSaved;

            if (spellCaster != null)
            {
                spellCaster.SlotChanged -= HandleSlotChangedWhileLookingAtStone;
                spellCaster.OnSpellReplaced -= HandleSpellReplaced;
            }
        }

        private void HandleTargetChanged(IInteractable target)
        {
            // Si hay un ítem en mano, el panel de pickup manda; no lo pisamos con el prompt genérico.
            if (interactor.HeldItem != null) return;

            HideAllPanels();

            if (target == null) return;

            if (target is SpellStone spellStone)
            {
                ShowRitualPreview(spellStone);
            }
            else
            {
                ShowGenericPrompt(target);
            }
        }

        private void ShowGenericPrompt(IInteractable target)
        {
            genericPromptRoot.SetActive(true);
            string verb = target.ActionVerb.GetLocalizedString();
            genericPromptText.text = $"{interactor.InteractKeyGlyph}: {verb}";
        }

        private void ShowRitualPreview(SpellStone spellStone)
        {
            ritualPreviewRoot.SetActive(true);

            SpellData spell = spellStone.Spell;

            // Capa de fondo (Type): distingue Ritual (permanente) de Scroll (consumible).
            if (ritualTypeIcon != null)
            {
                Sprite frame = spell != null && spell.consumable ? scrollFrameSprite : ritualFrameSprite;
                ritualTypeIcon.sprite = frame;
                ritualTypeIcon.enabled = frame != null;
            }

            // Capa de encima (Spell_Icon): el ícono puntual de este hechizo.
            bool hasIcon = spell != null && spell.icon != null;
            ritualSpellIcon.enabled = hasIcon;
            if (hasIcon) ritualSpellIcon.sprite = spell.icon;

            ritualNameText.text = spell != null ? spell.displayName.GetLocalizedString() : "";

            // Prompt dinámico: ya equipado > hold (slots llenos) > tap (slot libre).
            if (interactor.CurrentTargetAlreadyOwned)
            {
                ritualPromptText.text = spellAlreadyOwnedMessage.IsEmpty
                    ? "Ya tenés este hechizo"
                    : spellAlreadyOwnedMessage.GetLocalizedString();
            }
            else if (interactor.CurrentTargetRequiresHold)
            {
                string hold = holdPrefix.IsEmpty ? "Hold" : holdPrefix.GetLocalizedString();
                string verb = spellStone.ReplaceVerb.IsEmpty ? spellStone.ActionVerb.GetLocalizedString() : spellStone.ReplaceVerb.GetLocalizedString();
                ritualPromptText.text = $"{hold} {interactor.InteractKeyGlyph}: {verb}";
            }
            else
            {
                string verb = spellStone.ActionVerb.GetLocalizedString();
                ritualPromptText.text = $"{interactor.InteractKeyGlyph}: {verb}";
            }
        }

        // Refresca el prompt de la SpellStone si el jugador llena (o vacía) un slot
        // mientras sigue mirando la misma piedra.
        private void HandleSlotChangedWhileLookingAtStone(int index, SpellData spell)
        {
            if (interactor.CurrentTarget is SpellStone stone)
                ShowRitualPreview(stone);
        }

        private void HandleSpellReplaced(int slotIndex, SpellData newSpell)
        {
            if (spellReplacedCoroutine != null) StopCoroutine(spellReplacedCoroutine);
            spellReplacedCoroutine = StartCoroutine(ShowSpellReplacedMessage(newSpell));
        }

        private IEnumerator ShowSpellReplacedMessage(SpellData spell)
        {
            successText.gameObject.SetActive(true);
            string spellName = spell != null && !spell.displayName.IsEmpty
                ? spell.displayName.GetLocalizedString()
                : "";
            successText.text = spellReplacedFormat.IsEmpty ? spellName : spellReplacedFormat.GetLocalizedString(spellName);
            yield return new WaitForSeconds(successDuration);
            successText.gameObject.SetActive(false);
        }

        private void HandleItemPickedUp(ItemData item)
        {
            HideAllPanels();
            if (item == null) return;

            itemPanelRoot.SetActive(true);

            int colorIndex = GetPlayerColorIndex();
            if (itemPanelBackground != null && itemPanelSpritesByPlayerColor != null
                && colorIndex >= 0 && colorIndex < itemPanelSpritesByPlayerColor.Length)
            {
                itemPanelBackground.sprite = itemPanelSpritesByPlayerColor[colorIndex];
            }

            bool hasIcon = item.icon != null;
            itemIcon.enabled = hasIcon;
            if (hasIcon) itemIcon.sprite = item.icon;

            itemNameText.text = item.itemName.GetLocalizedString();
            itemValueText.text = item.value.ToString();
            itemWeightText.text = $"{item.weight} kg";

            string interactKey = interactor.InteractKeyGlyph;
            string discardKey = interactor.DiscardKeyGlyph;
            string saveText = saveLabel.GetLocalizedString();
            string returnText = returnLabel.GetLocalizedString();

            itemSaveLine.text = $"{interactKey}: {saveText}";
            itemReturnLine.text = $"{discardKey}: {returnText}";
        }

        private void HandleItemResolved()
        {
            itemPanelRoot.SetActive(false);
            // Volver a mostrar el prompt correspondiente si seguimos mirando algo.
            HandleTargetChanged(interactor.CurrentTarget);
        }

        private void HandleInsufficientSpace(ItemData item)
        {
            if (errorCoroutine != null) StopCoroutine(errorCoroutine);
            errorCoroutine = StartCoroutine(ShowErrorMessage());
        }

        private IEnumerator ShowErrorMessage()
        {
            errorText.gameObject.SetActive(true);
            errorText.text = notEnoughSpaceMessage.GetLocalizedString();
            yield return new WaitForSeconds(errorDuration);
            errorText.gameObject.SetActive(false);
        }

        private void HandleItemSaved(ItemData item)
        {
            if (successCoroutine != null) StopCoroutine(successCoroutine);
            successCoroutine = StartCoroutine(ShowSuccessMessage(item));
        }

        private IEnumerator ShowSuccessMessage(ItemData item)
        {
            successText.gameObject.SetActive(true);
            string name = item.itemName.GetLocalizedString();
            successText.text = itemSavedFormat.GetLocalizedString(name);
            yield return new WaitForSeconds(successDuration);
            successText.gameObject.SetActive(false);
        }

        private void HideAllPanels()
        {
            genericPromptRoot.SetActive(false);
            ritualPreviewRoot.SetActive(false);
            itemPanelRoot.SetActive(false);
        }

        private void HideErrorAndSuccess()
        {
            if (errorText != null) errorText.gameObject.SetActive(false);
            if (successText != null) successText.gameObject.SetActive(false);
        }

        private int GetPlayerColorIndex()
        {
            PlayerInput playerInput = interactor.GetComponent<PlayerInput>();
            return playerInput != null ? playerInput.playerIndex : 0;
        }
    }
}