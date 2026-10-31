# ÆON: Guerra de los Siete Velos · proyecto base Unity

Proyecto base del juego para iOS y Android: Unity 6.3 LTS con URP. Incluye dos universos jugables, **Thalassa** y **Resonancia**, y está preparado para crecer hasta los 7 universos del diseño sin tocar el núcleo.

El slice jugable recorre el bucle completo: semilla → multiverso con 2 universos → el jugador recibe un planeta de su universo (sorteado con la semilla) → planeta procedural con atmósfera → HUD de recursos → milagros y conquista → fin de ciclo → turno de la IA rival → guardar/cargar en JSON.

La documentación de diseño está en `Docs/diseno/` (GDD, lore, universos y leyes físicas, arquitectura, plan de trabajo).

---

## 1. Requisitos

- **Unity Hub** y **Unity 6.3 LTS** (`6000.3.x`; el proyecto indica `6000.3.14f1` en `ProjectSettings/ProjectVersion.txt`, y Hub permite abrirlo con cualquier 6000.3 instalado).
- Módulos del Editor, que se añaden desde Unity Hub → *Installs* → ⚙ → *Add modules*:
  - **Android Build Support** con **OpenJDK** y **Android SDK & NDK Tools**.
  - **iOS Build Support**.
- Para compilar y publicar en iOS hace falta **macOS con Xcode**. Desde Windows se puede desarrollar y generar el build de Android.
- Opcional: **.NET 8 SDK** para ejecutar los tests del núcleo sin Unity (ver §5).
- Opcional: **git** y **git-lfs** (ver §8).

## 2. Abrir el proyecto

1. Unity Hub → *Projects* → *Add* → *Add project from disk* → selecciona esta carpeta (`AEON`).
2. Si Hub avisa de que la versión exacta no está instalada, elige cualquier Unity 6000.3.x instalada.
3. La primera apertura tarda unos minutos: Unity descarga los paquetes de `Packages/manifest.json`, importa los assets y genera `Library/` y los archivos `.meta`.

El repositorio no incluye escenas, materiales ni assets de Unity (`.unity`, `.mat`, `.asset`, `.meta`). Los crea el configurador de Editor la primera vez.

## 3. Primer arranque: el configurador

Al terminar la primera compilación se ejecuta solo, una vez, el configurador `Assets/_AEON/Scripts/Editor/AeonProjectConfigurator.cs`. Puedes relanzarlo cuando quieras desde el menú **AEON → Configurar proyecto** (`AEON/Configurar proyecto`).

Hace esto, en orden, y **solo crea lo que falta** (nunca sobrescribe un asset existente, así que ejecutarlo varias veces no duplica nada ni pisa tus ajustes):

| Paso | Resultado |
|---|---|
| Carpetas | `Assets/_AEON/Resources/AEON`, `Art/Materials`, `Scenes`, `Settings/URP` y la carpeta `Data` de cada universo |
| URP | Si el proyecto no tiene pipeline: `Settings/URP/AEON_URP_Renderer.asset` y `AEON_URP_Movil.asset` (render scale 1, sin MSAA ni HDR, sombras a 40 m, sin depth/opaque texture) asignado en *Project Settings → Graphics* |
| Universos | Por cada módulo de `Assets/_AEON/Universes/<Nombre>`: `Data/UR_<Nombre>.asset` con los valores del módulo, `Data/M_Planeta_<id>.mat` y `Data/M_Atmosfera_<id>.mat` |
| Materiales compartidos | `Art/Materials/M_Estrella.mat`, `M_Orbita.mat`, `M_PlanetaBase.mat`, `M_AtmosferaBase.mat` |
| Catálogo | `Assets/_AEON/Resources/AEON/UniverseCatalog.asset` con todos los `UR_*.asset` y los materiales compartidos |
| Escenas | `Assets/_AEON/Scenes/Boot.unity`, `MainMenu.unity` y `Game.unity`, cada una con un único objeto (`AEON_Boot`, `AEON_MainMenu`, `AEON_Game`); cámara, luz y UI se crean al darle a Play |
| Build Settings | Boot, MainMenu y Game al principio de la lista, sin duplicados |
| PlayerSettings | Solo si siguen con los valores por defecto de Unity: nombre *ÆON: Guerra de los Siete Velos*, compañía *AEON Studio*, identificador `com.aeonstudio.sietevelos` (Android e iOS), espacio de color Linear, IL2CPP y ARM64 en Android, orientación horizontal |

Detalles:

- El resumen aparece en la consola con el prefijo `[AEON]`. Cada paso que falla deja un aviso en español y los demás pasos siguen.
- Cuando termina sin avisos escribe la bandera `Library/AEON/configurado-v1.flag`. Mientras la bandera no exista, el configurador vuelve a intentarlo al abrir el proyecto. Para forzar un nuevo arranque automático, borra esa carpeta o usa el menú.
- En el arranque automático, si hay escenas abiertas con cambios sin guardar, no crea las escenas y lo avisa. Desde el menú te pregunta si quieres guardarlas.
- Si la configuración automática de URP falla: *Assets → Create → Rendering → URP Asset (with Universal Renderer)* y asígnalo en *Edit → Project Settings → Graphics → Default Render Pipeline*.
- Para CI (modo batch): `Unity -batchmode -quit -projectPath <ruta> -executeMethod GodGame.EditorTools.AeonProjectConfigurator.ConfigurarDesdeLineaDeComandos`. Sale con código 1 si algún paso deja avisos. En modo batch no hay arranque automático.
- Si falta el catálogo (configurador sin ejecutar), el juego arranca igualmente con los valores por defecto de los módulos y materiales creados con `Shader.Find`. La consola lo indica con un aviso.

## 4. Cómo jugar el slice

1. Abre `Assets/_AEON/Scenes/Boot.unity` y pulsa **Play**. Boot fija 60 fps y carga **MainMenu**.
2. En el menú principal aparecen los universos disponibles y un campo **Semilla** ya relleno. **Semilla aleatoria** genera otra. La semilla decide el multiverso y en qué universo empiezas: la misma semilla genera siempre la misma partida.
3. **Nueva partida** carga la escena **Game** con tu sistema solar: estrella, planetas procedurales con atmósfera y órbitas. **Continuar** carga la partida guardada (solo se activa si existe).
4. Cámara: rueda del ratón o pellizco para el zoom continuo planeta ↔ sistema; arrastrar para girar. El botón **Vista sistema/planeta** alterna entre ambos extremos.
5. Con **◄ ►** eliges planeta. El panel muestra su tipo, dueño, fe, defensa, población, si es alcanzable y la probabilidad de éxito de cada vía de conquista.
6. Empiezas como dios sin planetas. Tu planeta asignado adora a "los ancestros", así que primero hay que convertirlo: **Conquista cultural** (varios ciclos), **Conquista militar** o **Conquista tecnológica**. **Profeta** se lanza sobre planetas ajenos y **Bendición** sobre los tuyos. **Cancelar órdenes** retira las órdenes pendientes.
7. **Fin de ciclo** resuelve el turno: primero los milagros, luego las conquistas, después la economía (Fe, Energía Divina, Materia, Conocimiento) y las reglas especiales del universo. El dios rival juega su turno con IA de utilidad. La **Crónica** muestra los últimos 6 eventos.
8. Cada fin de ciclo guarda automáticamente. **Guardar** y **Cargar** usan la ranura 0 en `Application.persistentDataPath/saves`. **Menú** vuelve al menú principal.
9. Si un dios controla el 75 % de los planetas civilizados del multiverso aparece el panel de victoria (o de derrota).

Reglas especiales del slice:

- **Thalassa · Corrientes**: viajar a otro sistema "a favor de la corriente" cuesta la mitad. La dirección cambia cada 6 ciclos.
- **Resonancia · Afinación**: tus planetas de un mismo sistema que vibran en la misma frecuencia (de 1 a 5) dan un 20 % de Fe extra cada uno.

## 5. Tests

**En Unity (Test Runner):** *Window → General → Test Runner → EditMode → Run All*. El ensamblado `GodGame.Tests.EditMode` (`Assets/_AEON/Tests/EditMode`) contiene:

- `Pure/`: generador del multiverso, reglas y módulos de universo, turnos e IA, guardado. Son C# puro y también corren con dotnet.
- `Unity/UniverseRulesAssetTests.cs`: los ScriptableObjects `UniverseRules` y `UniverseCatalog`. Solo corren en Unity.

**Sin Unity (dotnet):** el proyecto `Tools/PureCore/AEON.PureCore.Tests.csproj` compila las capas puras con C# 9 (la versión de Unity) y ejecuta los tests de `Pure/`. Instrucciones completas en [`Tools/PureCore/README.md`](Tools/PureCore/README.md):

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" test "C:\Users\RR24104.SLV\Documents\AEON\Tools\PureCore\AEON.PureCore.Tests.csproj"
```

## 6. Añadir un universo

Copia la carpeta `Assets/_AEON/Universes/_Plantilla` y sigue la guía paso a paso de [`Assets/_AEON/Universes/_Plantilla/LEEME.md`](Assets/_AEON/Universes/_Plantilla/LEEME.md). En resumen: copiar la carpeta, quitar la extensión `.txt`, renombrar `Plantilla`, rellenar los valores del doc 03 §2, abrir Unity y ejecutar **AEON → Configurar proyecto**.

El universo nuevo se descubre por reflexión (`UniverseModuleRegistry`). No hay que tocar `Assets/_AEON/Scripts` ni el configurador. Máximo 7 universos por partida. Los módulos existentes sirven de ejemplo: `Universes/Thalassa/LEEME.md` y `Universes/Resonancia/LEEME.md` explican de dónde sale cada valor.

## 7. Mapa de carpetas y ensamblados

```
AEON/
  Packages/manifest.json          paquetes (URP, Input System, Burst, Collections, Addressables, Localization, Cinemachine...)
  ProjectSettings/ProjectVersion.txt
  Docs/diseno/                    documentación de diseño (fuente de verdad)
  Tools/PureCore/                 tests del núcleo con dotnet (fuera de Assets: Unity lo ignora)
  Assets/_AEON/
    Scripts/
      Domain/        GodGame.Domain         modelo del multiverso, leyes físicas (UniverseRulesData), azar determinista
      Generation/    GodGame.Generation     generadores de multiverso, galaxia y sistema solar
      Simulation/    GodGame.Simulation     GameState, TurnManager, comandos, economía, reglas especiales (UniverseRuleBase)
      AI/            GodGame.AI             IA de utilidad de los dioses rivales
      Save/          GodGame.Save           guardado JSON (Newtonsoft) con checksum, copia .bak y migración
      Rules/         GodGame.Rules          contratos de módulo (IUniverseModule) y registro por reflexión
      Rules/Assets/  GodGame.Rules.Assets   ScriptableObjects UniverseRules y UniverseCatalog
      Core/          GodGame.Core           GameSession, carga del catálogo, rutas de guardado, BootLoader
      Presentation/  GodGame.Presentation   planetas procedurales (Burst), sistema solar, cámara, HUD y menú (uGUI)
      Editor/        GodGame.Editor         configurador (menú AEON/Configurar proyecto)
    Shaders/                      PlanetSurface, PlanetAtmosphere, ToonCharacter (URP)
    Universes/
      Thalassa/      GodGame.Universes.Thalassa     módulo + regla Corrientes
      Resonancia/    GodGame.Universes.Resonancia   módulo + regla Afinación
      _Plantilla/                                   molde para nuevos universos (archivos .txt)
    Tests/EditMode/  GodGame.Tests.EditMode         Pure/ y Unity/
    Resources/AEON/, Art/Materials/, Scenes/, Settings/URP/   los genera el configurador
```

Dependencias entre ensamblados (sin ciclos):

```
Domain ← Generation
Domain ← Simulation ← AI
Domain, Simulation ← Save
Domain, Simulation ← Rules ← Rules.Assets
Domain, Simulation, Rules ← Universes.<Nombre>      (ningún ensamblado del núcleo los referencia)
Domain, Generation, Simulation, AI, Save, Rules, Rules.Assets ← Core ← Presentation ← Editor
```

Domain, Generation, Simulation, AI, Save, Rules y los módulos de universo son C# puro (`noEngineReferences: true`): no usan `UnityEngine`, son deterministas (`System.Random` con semillas derivadas) y se prueban sin Unity.

## 8. Notas

- **Paquetes core:** URP, uGUI y Test Framework son paquetes core de Unity. El Editor los ajusta a su propia versión al abrir el proyecto. Si otro paquete no resuelve en tu versión de Editor, el Package Manager lo marca y basta con pulsar *Update* en esa fila.
- **Input System:** el código funciona con el Input System nuevo y con el antiguo (`#if ENABLE_INPUT_SYSTEM`). Si Unity pregunta al abrir si quieres activar el nuevo backend y reiniciar, puedes aceptar. El ajuste está en *Project Settings → Player → Active Input Handling*.
- **TextMeshPro:** la UI usa `Text` de uGUI con la fuente integrada `LegacyRuntime.ttf`, así que no hace falta importar los recursos de TMP.
- **git-lfs:** `.gitattributes` envía imágenes, modelos, audio y fuentes a Git LFS. Instala git-lfs y ejecuta `git lfs install` una vez antes del primer commit con arte.
- **No versionar:** `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Builds/` y las salidas de `Tools/PureCore` ya están en `.gitignore`.
- **Errores al abrir:** la capa Unity (Rules.Assets, Core, Presentation, Editor) se escribió sin poder compilarla en Unity. Si la consola muestra un error de compilación, anota el archivo, la línea y el mensaje para corregirlo.

## 9. Próximos pasos (doc 07, sprints S5–S14)

Según `Docs/diseno/07_plan_de_trabajo_y_cronograma.md`:

- **S5:** migrar la cámara propia (`CameraZoomController`) a Cinemachine 3 con Input System (el paquete ya está instalado).
- **S6:** HUD de recursos en UI Toolkit; pulir órbitas, estrella y atmósfera.
- **S7:** regiones del planeta (Acto I) y conversión cultural por regiones.
- **S8:** IA de Nerith con más comportamiento, conquista militar con VFX de milagro.
- **S9:** arte final de Thalassa (skybox, gradientes, 2 especies) y música adaptativa v1.
- **S10:** tutorial de los ciclos 0–10, *playtest* externo y cierre del vertical slice.
- **S11–S12:** escala galáctica (vista de galaxia, coste por distancia) y diplomacia básica (tregua y opinión).
- **S13–S14:** árbol divino, conquista tecnológica completa y mutaciones de especies.
- Más adelante (S21–S23): Addressables por universo (`addressablesLabel` ya está en los datos), Localization con las claves de `TextosES`, Unity Gaming Services (Cloud Save, Analytics) y ajustes de calidad por dispositivo.
