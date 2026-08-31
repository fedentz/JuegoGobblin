using System;
using System.Collections;
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
        [Tooltip("Usado por el efecto Relax para bajar la ansiedad. Vive en otro GameObject, hay que arrastrarlo a mano.")]
        [SerializeField] private PlayerAnxiety anxiety;
        [Tooltip("Usado por Strength (aumentar capacidad de carga). Vive en otro GameObject, hay que arrastrarlo a mano.")]
        [SerializeField] private PlayerInventory inventory;
        [Tooltip("Usado por Escudo (bloquear el próximo daño). Vive en otro GameObject, hay que arrastrarlo a mano.")]
        [SerializeField] private PlayerHealth health;
        [Tooltip("El modelo visual del gobblin (ej: GOBLIN_GREEN), NO el root del jugador. Usado por Encogerse e Invisibilidad.")]
        [SerializeField] private Transform gobblinVisual;
        [Tooltip("Punto desde donde se detecta a quién empujar con Push (ej: la cámara).")]
        [SerializeField] private Transform pushOrigin;

        // index 0-3, spell es null cuando el slot está vacío (para que la UI actualice el ícono).
        public event Action<int, SpellData> SlotChanged;
        // index 0-3, true al entrar en cooldown, false cuando termina (para oscurecer/restaurar el ícono).
        public event Action<int, bool> SlotCooldownChanged;
        // avisa cuál slot quedó seleccionado (para que la UI lo resalte).
        public event Action<int> SelectedSlotChanged;

        private readonly SpellData[] slots = new SpellData[4];
        private readonly float[] cooldownEndTime = new float[4];
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

        // Saca un hechizo del slot a mano (todavía no hay UI para esto, pero queda listo
        // para cuando se arme un flujo de "cambiar hechizo"). Si era MientrasEquipado
        // (ej: Strength), revierte su efecto antes de vaciar el slot.
        public void UnlearnSpell(int index)
        {
            if (index < 0 || index >= slots.Length) return;
            SpellData spell = slots[index];
            if (spell == null) return;

            if (spell.efecto != null && spell.efecto.TipoDuracion == TipoDuracion.MientrasEquipado)
            {
                spell.efecto.Revertir(this);
            }

            slots[index] = null;
            SlotChanged?.Invoke(index, null);
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

                if (spell.efecto.TipoDuracion == TipoDuracion.Temporizado && spell.efecto.Duracion > 0f)
                {
                    StartCoroutine(RevertirTrasDuracion(spell.efecto, spell.efecto.Duracion));
                }
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
            else if (spell.efecto != null && spell.efecto.TipoDuracion == TipoDuracion.MientrasEquipado)
            {
                // Sin cooldown: el efecto queda activo hasta que lo saquen del slot con UnlearnSpell.
            }
            else
            {
                // Instantaneo: solo el cooldown propio. Temporizado: cooldown + duración
                // (no podés volver a castear hasta que termine el efecto Y el cooldown).
                float bloqueoTotal = spell.cooldownDuration;
                if (spell.efecto != null && spell.efecto.TipoDuracion == TipoDuracion.Temporizado)
                {
                    bloqueoTotal += spell.efecto.Duracion;
                }

                if (bloqueoTotal > 0f) StartCooldown(index, bloqueoTotal);
            }
        }

        private IEnumerator RevertirTrasDuracion(HechizoEfectoBase efecto, float duracion)
        {
            yield return new WaitForSeconds(duracion);
            efecto.Revertir(this);
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

        // ---- Lumos ----
        public void EncenderLuz()
        {
            if (flashlight != null) flashlight.enabled = true;
        }

        public void ApagarLuz()
        {
            if (flashlight != null) flashlight.enabled = false;
        }

        // ---- Run / Aceleración ----
        public void ActivarRunBoost()
        {
            if (gobblinController != null) gobblinController.SetRunBoost(true);
        }

        public void DesactivarRunBoost()
        {
            if (gobblinController != null) gobblinController.SetRunBoost(false);
        }

        // ---- Relax ----
        public void ReducirAnsiedad(float cantidad)
        {
            if (anxiety != null) anxiety.ReduceAnxiety(cantidad);
        }

        // ---- Strength ----
        public void AumentarCapacidadCarga(float multiplicador)
        {
            if (inventory != null) inventory.AumentarCapacidad(multiplicador);
        }

        public void QuitarCapacidadCarga(float multiplicador)
        {
            if (inventory != null) inventory.QuitarCapacidad(multiplicador);
        }

        // ---- Escudo ----
        public void ActivarEscudo()
        {
            if (health != null) health.ActivarEscudo();
        }

        public void DesactivarEscudo()
        {
            if (health != null) health.DesactivarEscudo();
        }

        // ---- Encogerse ----
        // Asume que la escala original del modelo es Vector3.one (ajustar si no es así).
        // También baja la cámara para que el cambio de tamaño se note en primera persona.
        public void Encoger(float escala)
        {
            if (gobblinVisual != null) gobblinVisual.localScale = Vector3.one * escala;
            if (gobblinController != null) gobblinController.AjustarAlturaCamara(escala);
        }

        public void VolverATamanoNormal()
        {
            if (gobblinVisual != null) gobblinVisual.localScale = Vector3.one;
            if (gobblinController != null) gobblinController.AjustarAlturaCamara(1f);
        }

        // ---- Invisibilidad ----
        public void Ocultar()
        {
            if (gobblinVisual != null) gobblinVisual.gameObject.SetActive(false);
        }

        public void Mostrar()
        {
            if (gobblinVisual != null) gobblinVisual.gameObject.SetActive(true);
        }

        // ---- Push ----
        // Empuja la primera entidad con Rigidbody que encuentre cerca de pushOrigin,
        // en la dirección hacia adelante. Sin enemigos todavía, probalo con cualquier
        // objeto suelto con Rigidbody (un barril, un jarrón, otro jugador).
        public void EmpujarEntidadCercana(float radioDeteccion, float fuerza)
        {
            if (pushOrigin == null) return;

            Vector3 centro = pushOrigin.position + pushOrigin.forward * radioDeteccion;
            Collider[] hits = Physics.OverlapSphere(centro, radioDeteccion);

            foreach (var hit in hits)
            {
                if (hit.transform.root == transform.root) continue; // no empujarse a sí mismo
                Rigidbody rb = hit.attachedRigidbody;
                if (rb == null) continue;

                Vector3 direccion = (hit.transform.position - pushOrigin.position).normalized;
                rb.AddForce(direccion * fuerza, ForceMode.Impulse);
                break;
            }
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