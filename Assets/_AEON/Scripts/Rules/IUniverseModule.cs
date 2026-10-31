using System.Collections.Generic;
using GodGame.Domain;
using GodGame.Simulation;

namespace GodGame.Rules
{
    /// <summary>
    /// Contrato de un módulo de universo (carpeta Assets/_AEON/Universes/&lt;Nombre&gt; con su propio asmdef).
    /// El núcleo no conoce ningún módulo concreto: UniverseModuleRegistry los descubre por reflexión.
    /// Requisitos de la clase que lo implementa: no abstracta, con constructor público sin parámetros
    /// y marcada con [Preserve] (y link.xml en la carpeta del módulo) para sobrevivir al linker de IL2CPP.
    /// </summary>
    public interface IUniverseModule
    {
        /// <summary>Id estable de las reglas (UniverseRulesData.id). Único entre módulos.</summary>
        string RulesId { get; }

        /// <summary>Nombre visible del universo; también da nombre a sus assets (UR_&lt;DisplayName&gt;.asset).</summary>
        string DisplayName { get; }

        /// <summary>Leyes físicas y datos de generación por defecto (doc 03). Cada llamada devuelve una instancia nueva.</summary>
        UniverseRulesData CreateDefaultRules();

        /// <summary>Paleta, rampa de biomas y parámetros visuales por defecto. Cada llamada devuelve una instancia nueva.</summary>
        UniversePresentationDefaults CreatePresentationDefaults();

        /// <summary>Reglas especiales de simulación del universo (puede ser vacío). Instancias nuevas en cada llamada.</summary>
        IEnumerable<UniverseRuleBase> CreateSpecialRules();
    }
}
