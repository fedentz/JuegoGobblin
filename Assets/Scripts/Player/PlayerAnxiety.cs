using System;
using UnityEngine;

namespace Project.Player
{
    public class PlayerAnxiety : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private float maxAnxiety = 100f;

        [Header("Quietud -> Ansiedad")]
        [Tooltip("Segundos quieto antes de que la ansiedad empiece a subir.")]
        [SerializeField] private float tiempoQuietoParaEmpezar = 2f;
        [Tooltip("Ansiedad por segundo mientras estás quieto (en unidades de maxAnxiety).")]
        [SerializeField] private float velocidadDeSubida = 15f;
        [Tooltip("Ansiedad por segundo que baja apenas te movés.")]
        [SerializeField] private float velocidadDeBajada = 40f;
        [Tooltip("Distancia mínima recorrida en un frame para NO considerarse quieto.")]
        [SerializeField] private float distanciaMinimaMovimiento = 0.02f;

        public float CurrentAnxiety { get; private set; }
        public float MaxAnxiety => maxAnxiety;

        public event Action<float> OnAnxietyChanged; // avisa el nuevo valor actual

        private Vector3 _ultimaPosicion;
        private float _tiempoQuieto;

        private void Awake()
        {
            _ultimaPosicion = transform.position;
        }

        private void Update()
        {
            float distanciaRecorrida = Vector3.Distance(transform.position, _ultimaPosicion);
            _ultimaPosicion = transform.position;

            bool estaQuieto = distanciaRecorrida < distanciaMinimaMovimiento;

            if (estaQuieto)
            {
                _tiempoQuieto += Time.deltaTime;
                if (_tiempoQuieto >= tiempoQuietoParaEmpezar)
                {
                    AddAnxiety(velocidadDeSubida * Time.deltaTime);
                }
            }
            else
            {
                _tiempoQuieto = 0f;
                ReduceAnxiety(velocidadDeBajada * Time.deltaTime);
            }
        }

        public void AddAnxiety(float amount)
        {
            CurrentAnxiety = Mathf.Clamp(CurrentAnxiety + amount, 0f, maxAnxiety);
            OnAnxietyChanged?.Invoke(CurrentAnxiety);
        }

        public void ReduceAnxiety(float amount)
        {
            CurrentAnxiety = Mathf.Clamp(CurrentAnxiety - amount, 0f, maxAnxiety);
            OnAnxietyChanged?.Invoke(CurrentAnxiety);
        }
    }
}