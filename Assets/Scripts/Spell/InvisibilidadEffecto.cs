using UnityEngine;
using Project.Player;

namespace Project.Spells
{
    [CreateAssetMenu(menuName = "Hechizos/Efectos/Invisibilidad", fileName = "Efecto_Invisibilidad")]
    public class InvisibilidadEfecto : HechizoEfectoBase
    {
        [SerializeField] private float duracion = 5f;

        public override TipoDuracion TipoDuracion => TipoDuracion.Temporizado;
        public override float Duracion => duracion;

        public override void Ejecutar(PlayerSpellCaster caster) => caster.Ocultar();
        public override void Revertir(PlayerSpellCaster caster) => caster.Mostrar();
    }
}