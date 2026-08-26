using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Project.Spells;

namespace Project.Player
{
    public class PlayerSpellCaster : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("Luz usada por el efecto Lumos (prender/apagar).")]
        [SerializeField] private Light flashlight;
        [Tooltip("Usado por el efecto Run para activar el boost temporal de velocidad.")]
        [SerializeField] private GobblinController gobblinController;

        // index 0-3, spell es null cuando el slot está vacío (para que la UI actualice el ícono).
        public event Action<int, SpellData> SlotChanged;
        // index 0-3, true al entrar en cooldown, false cuando termina (para oscurecer/restaurar el ícono).
        public event Action<int, bool> SlotCooldownChanged;

        private readonly SpellData[] slots = new SpellData[4];
        private readonly float[] cooldownEndTime = new float[4];
        private bool flashlightOn;

        public SpellData GetSlot(int index) => index >= 0 && index < slots.Length ? slots[index] : null;
        public bool IsSlotOnCooldown(int index) => index >= 0 && index < slots.Length && Time.time < cooldownEndTime[index];

        public void LearnSpell(SpellData spell)
        {
            if (spell == null) return;

            // Un ritual permanente solo se aprende una vez. Los consumibles (scrolls)
            // sí pueden volver a agregarse, porque el anterior ya se gastó y se sacó del slot.
            if (!spell.consumable)
            {
                foreach (SpellData learned in slots)
                {
                    if (learned == spell)
                    {
                        Debug.Log("Ese ritual ya está aprendido");
                        return;
                    }
                }
            }

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                {
                    slots[i] = spell;
                    SlotChanged?.Invoke(i, spell);
                    return;
                }
            }

            Debug.Log("No hay slots de hechizo libres");
        }

        // Estas 4 acciones tienen que existir en el Input Actions asset (CastSlot1..4),
        // atadas a las teclas 1/2/3/4, igual que Sprint/Sneak/Emote* de GobblinController.
        public void OnCastSlot1(InputValue value) => TryCast(0, value);
        public void OnCastSlot2(InputValue value) => TryCast(1, value);
        public void OnCastSlot3(InputValue value) => TryCast(2, value);
        public void OnCastSlot4(InputValue value) => TryCast(3, value);

        private void TryCast(int index, InputValue value)
        {
            if (!value.isPressed) return;
            CastSlot(index);
        }

        private void CastSlot(int index)
        {
            SpellData spell = GetSlot(index);
            if (spell == null) return;
            if (IsSlotOnCooldown(index)) return;

            switch (spell.effectType)
            {
                case SpellEffectType.Lumos:
                    ToggleFlashlight();
                    break;
                case SpellEffectType.Run:
                    if (gobblinController != null) gobblinController.ActivateTemporaryRun(5f);
                    break;
            }

            if (spell.consumable)
            {
                slots[index] = null;
                SlotChanged?.Invoke(index, null);
            }
            else if (spell.cooldownDuration > 0f)
            {
                StartCooldown(index, spell.cooldownDuration);
            }
        }

        private void StartCooldown(int index, float duration)
        {
            cooldownEndTime[index] = Time.time + duration;
            SlotCooldownChanged?.Invoke(index, true);
        }

        private void ToggleFlashlight()
        {
            if (flashlight == null) return;
            flashlightOn = !flashlightOn;
            flashlight.enabled = flashlightOn;
        }

        private void Update()
        {
            // Revisamos cooldowns vencidos cada frame (4 slots, costo despreciable) y
            // avisamos a la UI apenas terminan, sin depender de coroutines por slot.
            for (int i = 0; i < slots.Length; i++)
            {
                if (cooldownEndTime[i] > 0f && Time.time >= cooldownEndTime[i])
                {
                    cooldownEndTime[i] = 0f;
                    SlotCooldownChanged?.Invoke(i, false);
                }
            }
        }
    }
}