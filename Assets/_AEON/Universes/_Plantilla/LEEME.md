# Plantilla para añadir un universo

Esta carpeta es un molde. Sus archivos terminan en `.txt` para que Unity **no** los compile mientras sigan aquí. Añadir un universo no exige tocar el núcleo (`Assets/_AEON/Scripts`) ni el Editor: el universo nuevo se descubre por reflexión (`UniverseModuleRegistry`).

## Paso a paso

1. **Copiar la carpeta.** Duplica `Assets/_AEON/Universes/_Plantilla` como `Assets/_AEON/Universes/<Nombre>` (por ejemplo `Fulgor`) desde el explorador de archivos (Unity no deja cambiar extensiones). Después borra los archivos `.meta` de la copia: llevan los GUID de la plantilla y Unity los regenera al volver a la ventana del Editor.
2. **Quitar la extensión `.txt`** de los archivos copiados:
   - `GodGame.Universes.Plantilla.asmdef.txt` → `GodGame.Universes.<Nombre>.asmdef`
   - `PlantillaModule.cs.txt` → `<Nombre>Module.cs`
   - `PlantillaRule.cs.txt` → `<Nombre>Rule.cs` (o el nombre de la regla, p. ej. `CombustionRule.cs`)
   - `link.xml.txt` → `link.xml`
   - Borra este `LEEME.md` de la copia o reescríbelo con el origen de los valores del nuevo universo.
3. **Renombrar `Plantilla` / `plantilla`** dentro de los archivos:
   - En el `.asmdef`: `"name"` y `"rootNamespace"` = `GodGame.Universes.<Nombre>`.
   - En `link.xml`: `fullname="GodGame.Universes.<Nombre>"` (debe coincidir con el `"name"` del asmdef).
   - En los `.cs`: namespace `GodGame.Universes.<Nombre>`, clases `<Nombre>Module` y `<Nombre>Rule`, `Id = "<id en minúsculas>"`, `DisplayName`, `displayName`, `displayNameKey = "universe.<id>.name"` y `addressablesLabel = "universe_<id>"`.
4. **Rellenar los valores** con la columna del universo en el doc 03 §2 (`Docs/diseno/03_universos_y_leyes_fisicas.md`): leyes físicas, modificadores, `galaxyCount` y `spiralArms`. Paleta, dios nativo y energía en el doc 03 §1; especies y regla especial en el doc 03 §3. Los valores que el doc no fije (sistemas por galaxia, planetas por sistema, pesos espectrales) pueden quedarse con los de la plantilla; anótalo en el `LEEME.md` del módulo.
5. **Regla especial (opcional).** Programa la mecánica en `<Nombre>Rule` (`OnEconomyPhase` y/o `ModifyDistanceFactor`) y devuélvela en `CreateSpecialRules()`. Si emite eventos para la Crónica, da el texto de su clave en `TryGetText` del módulo (interfaz `IUniverseTexts`); sin texto, la Crónica muestra la clave tal cual. Si el universo no tiene regla, borra el archivo y deja `CreateSpecialRules()` devolviendo un array vacío. Ejemplos reales: `Universes/Thalassa/CorrientesRule.cs` y `Universes/Resonancia/AfinacionRule.cs` (con su texto en `ResonanciaModule.TryGetText`).
6. **Abrir Unity y ejecutar *AEON → Configurar proyecto*.** El configurador crea `Data/UR_<Nombre>.asset` y sus materiales en la carpeta del módulo y lo añade al catálogo `Assets/_AEON/Resources/AEON/UniverseCatalog.asset`.
7. **Comprobar en el menú principal** (Play desde la escena `Boot`) que el universo nuevo aparece en la lista de universos disponibles.

## Reglas que no hay que romper

- El asmdef del módulo solo referencia `GodGame.Domain`, `GodGame.Simulation` y `GodGame.Rules`, con `noEngineReferences: true` y `autoReferenced: false`. Ningún ensamblado del núcleo debe referenciar el módulo.
- La clase del módulo debe ser pública, no abstracta, con constructor vacío y con `[Preserve]` (de `GodGame.Rules`). El `link.xml` protege el ensamblado en IL2CPP.
- El `Id` es único entre módulos; si se repite, el registro lanza una excepción al arrancar.
- Máximo 7 universos por partida (`Multiverse.MaxUniverses`).
- Nada de `UnityEngine`, `UnityEngine.Random` ni `string.GetHashCode` en el módulo: la simulación debe ser determinista.
