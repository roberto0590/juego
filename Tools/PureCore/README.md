# Tools/PureCore: compilar y probar el núcleo sin Unity

`AEON.PureCore.Tests.csproj` compila con el SDK de .NET las capas de C# puro del juego y ejecuta sus tests NUnit:

- `Assets/_AEON/Scripts/{Domain, Generation, Simulation, AI, Save}`
- `Assets/_AEON/Scripts/Rules/*.cs` (solo los contratos; `Rules/Assets` usa UnityEngine y queda fuera)
- `Assets/_AEON/Universes/**/*.cs` (todos los módulos; la `_Plantilla` queda fuera porque sus archivos terminan en `.txt`)
- `Assets/_AEON/Tests/EditMode/Pure` (los mismos tests que corren en el Test Runner de Unity)

El proyecto fija `LangVersion 9.0`, la versión de C# de Unity 6, así que detecta cualquier característica del lenguaje que Unity no admita. Esta carpeta está fuera de `Assets`, por lo que Unity la ignora.

## Instalar .NET 8 SDK sin permisos de administrador

En PowerShell:

```powershell
Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile $env:TEMP\dotnet-install.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File $env:TEMP\dotnet-install.ps1 -Channel 8.0 -InstallDir "$env:USERPROFILE\.dotnet"
```

`-ExecutionPolicy Bypass` solo afecta a ese proceso; hace falta si la política del equipo es `Restricted`. El SDK queda en `%USERPROFILE%\.dotnet` y no modifica el sistema.

## Ejecutar los tests

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" test "C:\Users\RR24104.SLV\Documents\AEON\Tools\PureCore\AEON.PureCore.Tests.csproj"
```

La primera vez descarga de NuGet NUnit 3.14.0, NUnit3TestAdapter 4.5.0, Microsoft.NET.Test.Sdk 17.8.0 y Newtonsoft.Json 13.0.3. Las salidas (`bin/`, `obj/`) están en `.gitignore`.

## Notas

- Todo se compila en un único ensamblado. En Unity, en cambio, cada carpeta tiene su asmdef: los tests no deben usar miembros `internal` (compilarían aquí y fallarían en Unity).
- El registro de módulos (`UniverseModuleRegistry`) descubre los módulos por reflexión también aquí, porque están en el ensamblado que define `IUniverseModule`.
- Usar solo aserciones clásicas de NUnit (`Assert.AreEqual`, `IsTrue`, `IsFalse`, `Greater`, `LessOrEqual`, `IsNotNull`), compatibles con el NUnit que trae Unity.
