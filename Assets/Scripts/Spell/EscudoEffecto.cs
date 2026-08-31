using UnityEngine;
using Project.Player;

namespace Project.Spells
{
    [CreateAssetMenu(menuName = "Hechizos/Efectos/Escudo", fileName = "Efecto_Escudo")]
    public class EscudoEfecto : HechizoEfectoBase
    {
        [SerializeField] private float duracion = 5f;

        public override TipoDuracion TipoDuracion => TipoDuracion.Temporizado;
        public override float Duracion => duracion;

        public override void Ejecutar(PlayerSpellCaster caster) => caster.ActivarEscudo();
        public override void Revertir(PlayerSpellCaster caster) => caster.DesactivarEscudo();
    }
}