using UnityEngine;
using Project.Player;

namespace Project.Spells
{
    public enum TipoDuracion
    {
        Instantaneo,       // pasa una vez, no hay nada que revertir (Relax, Push)
        Temporizado,       // se activa y se revierte solo después de Duracion segundos (Lumos, Run, Encogerse, Invisibilidad, Escudo)
        MientrasEquipado   // se activa al castear, se revierte cuando se saca del slot con UnlearnSpell (Strength)
    }

    /// <summary>
    /// Base para cada efecto de hechizo. Cada hechizo nuevo es un asset
    /// (ScriptableObject) que hereda de esta clase y sobreescribe Ejecutar()
    /// (y Revertir(), TipoDuracion, Duracion si corresponde).
    /// </summary>
    public abstract class HechizoEfectoBase : ScriptableObject
    {
        public virtual TipoDuracion TipoDuracion => TipoDuracion.Instantaneo;
        public virtual float Duracion => 0f; // solo se usa si TipoDuracion == Temporizado

        public abstract void Ejecutar(PlayerSpellCaster caster);

        // Temporizado: PlayerSpellCaster la llama sola al vencer Duracion.
        // MientrasEquipado: se llama cuando se saca el hechizo del slot (UnlearnSpell).
        // Instantaneo: nunca se llama, no hace falta sobreescribirla.
        public virtual void Revertir(PlayerSpellCaster caster) { }
    }
}