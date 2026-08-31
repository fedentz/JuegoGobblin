using UnityEngine;
using Project.Player;

namespace Project.Spells
{
    [CreateAssetMenu(menuName = "Hechizos/Efectos/Apertura", fileName = "Efecto_Apertura")]
    public class AperturaEfecto : HechizoEfectoBase
    {
        [SerializeField] private float radioDeteccion = 10f;

        public override void Ejecutar(PlayerSpellCaster caster)
        {
            Debug.Log("AperturaEfecto.Ejecutar SE LLAMÓ"); // <- línea de diagnóstico, temporal
            caster.DesbloquearPuertaCercana(radioDeteccion);
        }
    }
}