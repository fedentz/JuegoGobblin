using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Project.Player;
using Project.Spells;

namespace Project.UI
{
    // Vive en el mismo Canvas del jugador. Escucha al PlayerSpellCaster de ESE jugador
    // y pinta los 4 SpellSlot_X reales: el fondo de color (según jugador), el Type_Icon
    // (pergamino / roca clara / roca oscura) y el Spell_Icon.
    public class SpellSlotUI : MonoBehaviour
    {
        [System.Serializable]
        private struct SlotRefs
        {
            public Image typeIcon;   // SpelSlot_X_Type_Icon
            public Image spellIcon;  // SpelSlot_X_Spell_Icon
        }

        [Header("Refs")]
        [SerializeField] private PlayerSpellCaster caster;

        [Header("4 slots, en orden (arrastrar los Type_Icon y Spell_Icon de cada SpellSlot_X)")]
        [SerializeField] private SlotRefs[] slots = new SlotRefs[4];

        [Header("Sprites del Type_Icon (3 estados distintos, no un tinte)")]
        [Tooltip("Pergamino: hechizo consumible (scroll).")]
        [SerializeField] private Sprite scrollSprite;
        [Tooltip("Roca clara: ritual permanente, disponible para usar.")]
        [SerializeField] private Sprite ritualReadySprite;
        [Tooltip("Roca oscura: ritual permanente, en cooldown.")]
        [SerializeField] private Sprite ritualCooldownSprite;
        [Tooltip("4 sprites para el estado vacío, uno por color de jugador. Si se deja vacío, usa Empty Sprite (único) como fallback.")]
        [SerializeField] private Sprite[] emptySpritesByPlayer;
        [Tooltip("Fallback si emptySpritesByPlayer está vacío.")]
        [SerializeField] private Sprite emptySprite;

        [Header("Color de fondo por jugador (el SpellSlot_X base, no Type/Spell icon)")]
        [Tooltip("El Image raíz de cada SpellSlot_X (el fondo, no el Type_Icon ni el Spell_Icon).")]
        [SerializeField] private Image[] slotBackgrounds;
        [Tooltip("4 sprites de fondo, uno por color de jugador (índice 0-3). Se aplica una sola vez.")]
        [SerializeField] private Sprite[] backgroundSpritesByPlayer;

        private int playerColorIndex;

        private void OnEnable()
        {
            if (caster == null) return;
            caster.SlotChanged += HandleSlotChanged;
            caster.SlotCooldownChanged += HandleCooldownChanged;

            CachePlayerColorIndex();
            ApplyBackgroundColor();

            // Estado inicial: refleja lo que ya tenga cada slot al activarse la UI.
            for (int i = 0; i < slots.Length; i++)
            {
                ApplySlot(i, caster.GetSlot(i));
            }
        }

        private void OnDisable()
        {
            if (caster == null) return;
            caster.SlotChanged -= HandleSlotChanged;
            caster.SlotCooldownChanged -= HandleCooldownChanged;
        }

        private void HandleSlotChanged(int index, SpellData spell)
        {
            ApplySlot(index, spell);
        }

        private void ApplySlot(int index, SpellData spell)
        {
            if (index < 0 || index >= slots.Length) return;
            SlotRefs refs = slots[index];

            bool hasSpell = spell != null;
            bool onCooldown = hasSpell && caster.IsSlotOnCooldown(index);

            if (refs.typeIcon != null)
            {
                Sprite frame = GetTypeSprite(spell, onCooldown);
                refs.typeIcon.sprite = frame;
                refs.typeIcon.enabled = frame != null;
            }

            if (refs.spellIcon != null)
            {
                bool hasIcon = hasSpell && spell.icon != null;
                refs.spellIcon.enabled = hasIcon;
                if (hasIcon) refs.spellIcon.sprite = spell.icon;
            }
        }

        private Sprite GetTypeSprite(SpellData spell, bool onCooldown)
        {
            if (spell == null) return GetEmptySprite();
            if (spell.consumable) return scrollSprite; // los consumibles no entran en cooldown, siempre pergamino
            return onCooldown ? ritualCooldownSprite : ritualReadySprite;
        }

        private Sprite GetEmptySprite()
        {
            if (emptySpritesByPlayer != null && emptySpritesByPlayer.Length > 0
                && playerColorIndex >= 0 && playerColorIndex < emptySpritesByPlayer.Length
                && emptySpritesByPlayer[playerColorIndex] != null)
            {
                return emptySpritesByPlayer[playerColorIndex];
            }
            return emptySprite;
        }

        private void CachePlayerColorIndex()
        {
            PlayerInput playerInput = caster.GetComponent<PlayerInput>();
            playerColorIndex = playerInput != null ? playerInput.playerIndex : 0;
        }

        private void ApplyBackgroundColor()
        {
            if (backgroundSpritesByPlayer == null || backgroundSpritesByPlayer.Length == 0) return;
            if (slotBackgrounds == null) return;
            if (playerColorIndex < 0 || playerColorIndex >= backgroundSpritesByPlayer.Length) return;

            Sprite sprite = backgroundSpritesByPlayer[playerColorIndex];
            foreach (var bg in slotBackgrounds)
            {
                if (bg != null) bg.sprite = sprite;
            }
        }

        private void HandleCooldownChanged(int index, bool isOnCooldown)
        {
            if (index < 0 || index >= slots.Length) return;

            SpellData spell = caster.GetSlot(index);
            SlotRefs refs = slots[index];

            if (refs.typeIcon != null)
            {
                Sprite frame = GetTypeSprite(spell, isOnCooldown);
                refs.typeIcon.sprite = frame;
                refs.typeIcon.enabled = frame != null;
            }
        }
    }
}