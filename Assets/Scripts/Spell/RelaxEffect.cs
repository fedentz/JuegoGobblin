using UnityEngine;
using Project.Player;

namespace Project.Spells
{
    [CreateAssetMenu(menuName = "Hechizos/Efectos/Relax", fileName = "Efecto_Relax")]
    public class RelaxEfecto : HechizoEfectoBase
    {
        [Tooltip("Cuánto baja la ansiedad de golpe (misma escala que MaxAnxiety, default 100).")]
        [SerializeField] private float cantidad = 30f;

        public override void Ejecutar(PlayerSpellCaster caster)
        {
            caster.ReducirAnsiedad(cantidad);
        }
    }
}