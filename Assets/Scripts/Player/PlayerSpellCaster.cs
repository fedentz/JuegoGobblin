using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Project.Spells;

namespace Project.Player
{
    public class PlayerSpellCaster : MonoBehaviour
    {
        [Header("TEST - equipar hechizos al arrancar (sacar esto cuando el flujo real de piedras/cofres esté listo)")]
        [SerializeField] private bool equiparParaTestingAlIniciar = true;
        [SerializeField] private SpellData[] hechizosParaTestear;

        [Header("Refs")]
        [Tooltip("Luz usada por el efecto Lumos (prender/apagar).")]
        [SerializeField] private Light flashlight;
        [Tooltip("Usado por el efecto Run para activar el boost temporal de velocidad.")]
        [SerializeField] private GobblinController gobblinController;
        [Tooltip("Opcional. Si está asignado, reproduce spell.sonidoAlCastear al castear.")]
        [SerializeField] private AudioSource audioSource;

        // index 0-3, spell es null cuando el slot está vacío (para que la UI actualice el ícono).
        public event Action<int, SpellData> SlotChanged;
        // index 0-3, true al entrar en cooldown, false cuando termina (para oscurecer/restaurar el ícono).
        public event Action<int, bool> SlotCooldownChanged;
        // avisa cuál slot quedó seleccionado (para que la UI lo resalte).
        public event Action<int> SelectedSlotChanged;

        private readonly SpellData[] slots = new SpellData[4];
        private readonly float[] cooldownEndTime = new float[4];
        private bool flashlightOn;
        private int selectedSlot = 0;

        public SpellData GetSlot(int index) => index >= 0 && index < slots.Length ? slots[index] : null;
        public bool IsSlotOnCooldown(int index) => index >= 0 && index < slots.Length && Time.time < cooldownEndTime[index];
        public int SelectedSlot => selectedSlot;

        private void Start()
        {
            if (!equiparParaTestingAlIniciar || hechizosParaTestear == null) return;

            foreach (SpellData spell in hechizosParaTestear)
            {
                LearnSpell(spell);
            }
        }

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
        // atadas a las teclas 1/2/3/4. SELECCIONAN, no castean.
        public void OnCastSlot1(InputValue value) => TrySelect(0, value);
        public void OnCastSlot2(InputValue value) => TrySelect(1, value);
        public void OnCastSlot3(InputValue value) => TrySelect(2, value);
        public void OnCastSlot4(InputValue value) => TrySelect(3, value);

        private void TrySelect(int index, InputValue value)
        {
            if (!value.isPressed) return;
            selectedSlot = index;
            SelectedSlotChanged?.Invoke(selectedSlot);
            Debug.Log($"[PlayerSpellCaster] {gameObject.name} seleccionó slot {index}");
        }

        // Rueda del mouse: cicla la selección. Bindeada a <Mouse>/scroll/y como Value/Axis.
        public void OnCycleSpell(InputValue value)
        {
            float scrollY = value.Get<float>();
            if (Mathf.Approximately(scrollY, 0f)) return;

            int direction = scrollY > 0f ? 1 : -1;
            selectedSlot = ((selectedSlot + direction) % slots.Length + slots.Length) % slots.Length;

            SelectedSlotChanged?.Invoke(selectedSlot);
            Debug.Log($"[PlayerSpellCaster] {gameObject.name} seleccionó slot {selectedSlot} (rueda)");
        }

        // Castea lo que esté seleccionado. Bindeada a <Mouse>/leftButton como "UseSpell".
        public void OnUseSpell(InputValue value)
        {
            if (!value.isPressed) return;
            CastSlot(selectedSlot);
        }

        private void CastSlot(int index)
        {
            SpellData spell = GetSlot(index);
            if (spell == null) return;
            if (IsSlotOnCooldown(index)) return;

            if (spell.efecto != null)
            {
                spell.efecto.Ejecutar(this);
                Debug.Log($"[PlayerSpellCaster] {gameObject.name} casteó slot {index}: efecto '{spell.efecto.name}'");
            }
            else
            {
                Debug.LogWarning($"[PlayerSpellCaster] {gameObject.name} intentó castear slot {index}, pero el SpellData no tiene 'efecto' asignado todavía.");
            }

            PlaySpellPresentation(spell);

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

        // Sonido ya cableado. Animación y glow quedan como TODO hasta que sumemos
        // las referencias necesarias (Animator público en GobblinController, punto de spawn del VFX, etc).
        private void PlaySpellPresentation(SpellData spell)
        {
            if (spell.sonidoAlCastear != null && audioSource != null)
            {
                audioSource.PlayOneShot(spell.sonidoAlCastear);
            }

            // TODO: spell.animacionTrigger -> disparar en el Animator cuando lo cableemos.
            // TODO: spell.glowVfxPrefab -> instanciar VFX cuando lo cableemos.
        }

        private void StartCooldown(int index, float duration)
        {
            cooldownEndTime[index] = Time.time + duration;
            SlotCooldownChanged?.Invoke(index, true);
        }

        public void ToggleFlashlight()
        {
            if (flashlight == null) return;
            flashlightOn = !flashlightOn;
            flashlight.enabled = flashlightOn;
        }

        public void ActivarRunBoost(float duracion)
        {
            if (gobblinController != null) gobblinController.ActivateTemporaryRun(duracion);
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