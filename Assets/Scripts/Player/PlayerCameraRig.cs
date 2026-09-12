using UnityEngine;

namespace Project.Player
{
    public class PlayerCameraRig : MonoBehaviour
    {
        [SerializeField] private Camera cam;

        private PlayerHealth _health;

        private void Awake()
        {
            _health = GetComponentInParent<PlayerHealth>();
        }

        private void OnEnable()
        {
            SplitScreenManager.Register(cam);
            if (_health != null)
            {
                _health.OnDeath -= OnPlayerDeath;
                _health.OnDeath += OnPlayerDeath;
            }
        }

        private void OnDisable()
        {
            SplitScreenManager.Unregister(cam);
            if (_health != null)
                _health.OnDeath -= OnPlayerDeath;
        }

        private void OnPlayerDeath()
        {
            if (cam == null) return;
            cam.cullingMask = 0;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;

            // Ocultar el modelo visual para el resto de jugadores.
            // GetComponentsInChildren cubre el mesh multi-parte (cuerpo, ojos, etc.).
            // No se usa SetActive para no romper colliders, Rigidbody ni PlayerHealth.
            foreach (var r in transform.parent.GetComponentsInChildren<Renderer>())
                r.enabled = false;

            Core.GameProgress.Instance?.NotifyPlayerDeath();
        }
    }
}
