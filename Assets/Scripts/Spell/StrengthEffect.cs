using UnityEngine;
using Project.Player;

namespace Project.Spells
{
    [CreateAssetMenu(menuName = "Hechizos/Efectos/Strength", fileName = "Efecto_Strength")]
    public class StrengthEfecto : HechizoEfectoBase
    {
        [SerializeField] private float multiplicador = 1.5f; // +50%

        public override TipoDuracion TipoDuracion => TipoDuracion.MientrasEquipado;

        public override void Ejecutar(PlayerSpellCaster caster) => caster.AumentarCapacidadCarga(multiplicador);
        public override void Revertir(PlayerSpellCaster caster) => caster.QuitarCapacidadCarga(multiplicador);
    }
}