using UnityEngine;
using Project.Player;

namespace Project.Spells
{
    /// <summary>
    /// Base para cada efecto de hechizo. Cada hechizo nuevo es un asset
    /// (ScriptableObject) que hereda de esta clase y sobreescribe Ejecutar().
    /// Se arrastra ese asset al campo "efecto" de SpellData.
    /// </summary>
    public abstract class HechizoEfectoBase : ScriptableObject
    {
        public abstract void Ejecutar(PlayerSpellCaster caster);
    }
}