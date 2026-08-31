using UnityEngine;
using Project.Player;

namespace Project.Spells
{
    [CreateAssetMenu(menuName = "Hechizos/Efectos/Lumos", fileName = "Efecto_Lumos")]
    public class LumosEfecto : HechizoEfectoBase
    {
        [SerializeField] private float duracion = 10f;

        public override TipoDuracion TipoDuracion => TipoDuracion.Temporizado;
        public override float Duracion => duracion;

        public override void Ejecutar(PlayerSpellCaster caster) => caster.EncenderLuz();
        public override void Revertir(PlayerSpellCaster caster) => caster.ApagarLuz();
    }
}