using UnityEngine;

namespace Project.Player
{
    [RequireComponent(typeof(PlayerAnxiety))]
    public class PlayerNoise : MonoBehaviour
    {
        [Header("Radio de ruido")]
        [Tooltip("Radio de ruido cuando la ansiedad está en 0.")]
        [SerializeField] private float radioRuidoBase = 2f;
        [Tooltip("Radio de ruido cuando la ansiedad está al máximo.")]
        [SerializeField] private float radioRuidoMaximo = 8f;

        [Header("Debug (solo para monitorear en el Inspector)")]
        [SerializeField] private float radioActual;
        public float RadioActual => radioActual;

        private PlayerAnxiety _ansiedad;

        private void Awake()
        {
            _ansiedad = GetComponent<PlayerAnxiety>();
        }

        private void Update()
        {
            float t = _ansiedad.MaxAnxiety > 0f
                ? _ansiedad.CurrentAnxiety / _ansiedad.MaxAnxiety
                : 0f;

            radioActual = Mathf.Lerp(radioRuidoBase, radioRuidoMaximo, t);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, radioActual);
        }
    }
}