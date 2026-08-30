using UnityEngine;
using Project.Player;

namespace Project.Spells
{
    [CreateAssetMenu(menuName = "Hechizos/Efectos/Run", fileName = "Efecto_Run")]
    public class RunEfecto : HechizoEfectoBase
    {
        [SerializeField] private float duracion = 5f;

        public override void Ejecutar(PlayerSpellCaster caster)
        {
            caster.ActivarRunBoost(duracion);
        }
    }
}