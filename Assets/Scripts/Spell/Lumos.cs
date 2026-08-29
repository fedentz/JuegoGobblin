using UnityEngine;
using Project.Player;

namespace Project.Spells
{
    [CreateAssetMenu(menuName = "Hechizos/Efectos/Lumos", fileName = "Efecto_Lumos")]
    public class LumosEfecto : HechizoEfectoBase
    {
        public override void Ejecutar(PlayerSpellCaster caster)
        {
            caster.ToggleFlashlight();
        }
    }
}