using System;
using UnityEngine;

namespace Project.Player
{
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private int maxHearts = 5;

        public int CurrentHearts { get; private set; }
        public int MaxHearts => maxHearts;
        public bool IsDead => CurrentHearts <= 0;

        public event Action<int> OnHealthChanged; // avisa el nuevo valor
        public event Action OnDeath;

        private bool escudoActivo;

        // La duración ahora la maneja PlayerSpellCaster (RevertirTrasDuracion),
        // este método solo prende el flag.
        public void ActivarEscudo()
        {
            escudoActivo = true;
        }

        public void DesactivarEscudo()
        {
            escudoActivo = false;
        }

        private void Awake()
        {
            CurrentHearts = maxHearts;
        }

        public void TakeDamage(int amount = 1)
        {
            if (IsDead) return;

            // Bug corregido: antes este chequeo estaba DESPUÉS de aplicar el daño,
            // así que el escudo nunca bloqueaba nada. Ahora corta acá, antes de tocar CurrentHearts.
            if (escudoActivo)
            {
                escudoActivo = false; // se consume con el primer golpe
                return;
            }

            CurrentHearts = Mathf.Max(0, CurrentHearts - amount);
            OnHealthChanged?.Invoke(CurrentHearts);

            if (CurrentHearts == 0)
            {
                OnDeath?.Invoke();
            }
        }

        public void Heal(int amount = 1)
        {
            if (IsDead) return;

            CurrentHearts = Mathf.Min(maxHearts, CurrentHearts + amount);
            OnHealthChanged?.Invoke(CurrentHearts);
        }
    }
}