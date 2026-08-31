using UnityEngine;
using Project.Player;

namespace Project.Spells
{
    [CreateAssetMenu(menuName = "Hechizos/Efectos/Push", fileName = "Efecto_Push")]
    public class PushEfecto : HechizoEfectoBase
    {
        [SerializeField] private float radioDeteccion = 2f;
        [SerializeField] private float fuerza = 8f;

        public override void Ejecutar(PlayerSpellCaster caster)
        {
            caster.EmpujarEntidadCercana(radioDeteccion, fuerza);
        }
    }
}