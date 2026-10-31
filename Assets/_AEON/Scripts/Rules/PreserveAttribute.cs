using System;

namespace GodGame.Rules
{
    /// <summary>
    /// Marca un tipo para que el linker de IL2CPP no lo elimine aunque solo se use por reflexión.
    /// El atributo Preserve de Unity (espacio Scripting del motor) no está disponible en un ensamblado sin motor; IL2CPP
    /// respeta cualquier atributo llamado "PreserveAttribute", así que basta con definir este.
    /// Cada módulo de universo, además, trae su link.xml con preserve="all".
    /// </summary>
    [AttributeUsage(AttributeTargets.All, Inherited = false)]
    public sealed class PreserveAttribute : Attribute
    {
    }
}
