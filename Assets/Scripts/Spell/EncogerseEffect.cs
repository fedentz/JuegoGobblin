using UnityEngine;
using Project.Player;

namespace Project.Spells
{
    [CreateAssetMenu(menuName = "Hechizos/Efectos/Encogerse", fileName = "Efecto_Encogerse")]
    public class EncogerseEfecto : HechizoEfectoBase
    {
        [SerializeField] private float escalaEncogido = 0.5f;
        [SerializeField] private float duracion = 8f;

        public override TipoDuracion TipoDuracion => TipoDuracion.Temporizado;
        public override float Duracion => duracion;

        public override void Ejecutar(PlayerSpellCaster caster) => caster.Encoger(escalaEncogido);
        public override void Revertir(PlayerSpellCaster caster) => caster.VolverATamanoNormal();
    }
}