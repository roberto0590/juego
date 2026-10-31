# Informe de creación · proyecto base Unity de ÆON

Proyecto base del juego *ÆON: Guerra de los Siete Velos* para iOS y Android, con dos universos jugables (**Thalassa** y **Resonancia**) preparados como módulos independientes del núcleo para poder crecer hasta los 7 universos del diseño.

- **Ubicación:** `C:\Users\RR24104.SLV\Documents\AEON`
- **Motor:** Unity 6.3 LTS (`6000.3.14f1` en `ProjectSettings/ProjectVersion.txt`), URP, C# 9.
- **Control de versiones:** repositorio git local creado en esa carpeta, con un único commit inicial `ÆON: proyecto base Unity 6.3 con Thalassa y Resonancia` (consulta el hash con `git log --oneline`). No se ha hecho push ni se ha tocado la configuración de git.
- **Documentación de uso:** `README.md` en la raíz del proyecto. La documentación de diseño (GDD, lore, universos, arquitectura, plan de trabajo) está en `Docs/diseno/`.

---

## 1. Qué se creó

| Bloque | Contenido |
|---|---|
| Andamiaje | `.gitignore` (Unity + salidas de dotnet y Addressables), `.gitattributes` (LF, merge YAML de Unity, Git LFS para arte y audio), `Packages/manifest.json` (25 dependencias: URP 17.3, Input System 1.19, Burst, Collections, Mathematics, Addressables 2.10, Localization, Cinemachine 3, Newtonsoft JSON, Test Framework, uGUI y módulos), `ProjectSettings/ProjectVersion.txt` |
| Núcleo C# puro | Domain, Generation, Simulation, AI, Save y Rules (`noEngineReferences: true`): modelo del multiverso, leyes físicas, generadores deterministas, turnos, comandos, economía, IA de utilidad, guardado JSON con checksum y contratos de módulo |
| Capa Unity | Rules.Assets (ScriptableObjects `UniverseRules` y `UniverseCatalog`), Core (sesión de juego, carga del catálogo, rutas de guardado, Boot), Presentation (planetas procedurales con Burst, sistema solar, cámara con zoom, HUD y menú en uGUI) y los 3 shaders URP |
| Configurador de Editor | `AeonProjectConfigurator` (menú **AEON/Configurar proyecto**, autoejecución única, idempotente), `AeonUrpSetup`, `AeonSceneSetup`: crea carpetas, pipeline URP móvil, assets de universos, materiales, catálogo, escenas Boot/MainMenu/Game, Build Settings y PlayerSettings |
| Universos | Módulos `Thalassa` (regla *Corrientes*) y `Resonancia` (regla *Afinación*), cada uno con su asmdef, `link.xml` y `LEEME.md`; plantilla `_Plantilla` (archivos `.txt`) para añadir universos |
| Tests | `Tests/EditMode/Pure` (35 tests de C# puro que también corren con dotnet) y `Tests/EditMode/Unity` (test de ScriptableObjects, solo en Unity); `Tools/PureCore/AEON.PureCore.Tests.csproj` para ejecutar los tests puros sin Unity |
| Documentación | `README.md`, `Tools/PureCore/README.md`, `LEEME.md` de cada universo y de la plantilla, este informe |

No se escribió ningún `.meta`, `.unity`, `.asset` ni `.mat`: Unity y el configurador los generan al abrir el proyecto.

## 2. Mapa de carpetas

```
AEON/
  README.md  .gitignore  .gitattributes
  Packages/manifest.json
  ProjectSettings/ProjectVersion.txt
  Docs/
    diseno/                         documentación de diseño y código de ejemplo original
    INFORME_CREACION.md             este informe
  Tools/PureCore/                   csproj net8.0 (C# 9) + README para ejecutar los tests puros con dotnet
  Assets/_AEON/
    Scripts/
      Domain/        GodGame.Domain         UniverseRulesData, MultiverseModel, Vec3/ColorRgb/IntRange, MathUtil, OrbitMath, RandomExtensions
      Generation/    GodGame.Generation     MultiverseGenerator, GalaxyGenerator, StarSystemGenerator
      Simulation/    GodGame.Simulation     GameState, TurnManager, EconomyFormulas, UniverseRuleBase, Commands/
      AI/            GodGame.AI             GodUtilityAI
      Save/          GodGame.Save           SaveJson (Newtonsoft), SaveSystem
      Rules/         GodGame.Rules          IUniverseModule, IUniverseTexts, UniverseModuleRegistry, UniversePresentationDefaults, PreserveAttribute
      Rules/Assets/  GodGame.Rules.Assets   UniverseRules, UniverseCatalog (ScriptableObjects)
      Core/          GodGame.Core           GameSession, UniverseCatalogLoader, SessionRequest, SavePaths, GameConstants, BootLoader
      Presentation/  GodGame.Presentation   GameController, Planets/, Space/, Camera/, UI/
      Editor/        GodGame.Editor         AeonProjectConfigurator, AeonUrpSetup, AeonSceneSetup
    Shaders/                        PlanetSurface, PlanetAtmosphere, ToonCharacter
    Universes/
      Thalassa/      GodGame.Universes.Thalassa     ThalassaModule, CorrientesRule
      Resonancia/    GodGame.Universes.Resonancia   ResonanciaModule, AfinacionRule
      _Plantilla/                                   molde para nuevos universos
    Tests/EditMode/  GodGame.Tests.EditMode         Pure/, Unity/
    (Resources/AEON, Art/Materials, Scenes, Settings/URP y Universes/*/Data los genera el configurador)
```

Dependencias entre ensamblados (sin ciclos; ningún ensamblado del núcleo referencia un módulo de universo):

```
Domain ← Generation
Domain ← Simulation ← AI
Domain, Simulation ← Save
Domain, Simulation ← Rules ← Rules.Assets
Domain, Simulation, Rules ← Universes.Thalassa / Universes.Resonancia
Domain, Generation, Simulation, AI, Save, Rules, Rules.Assets ← Core ← Presentation ← Editor
```

## 3. Decisiones de diseño (D1–D17)

| # | Decisión | Motivo |
|---|---|---|
| D1 | Unity 6.3 LTS (`6000.3.14f1`) | Soporte hasta diciembre de 2027; Hub abre el proyecto con cualquier 6000.3.x |
| D2 | Domain, Generation, Simulation, AI, Save, Rules y los módulos son C# puro (`Vec3`, `ColorRgb`, `MathUtil` en lugar de tipos de Unity) | Todo lo que decide una partida es determinista y se prueba sin Unity |
| D3 | `UniverseRulesData` (datos puros) separado del ScriptableObject `UniverseRules` (envoltorio con materiales) | El asset sigue siendo la herramienta de diseño; la simulación trabaja con una copia pura |
| D4 | El dios nativo de cada universo lo aporta su módulo; el generador ya no tiene una tabla de rivales | El núcleo no nombra ningún universo |
| D5 | Módulos de universo como asmdef propios descubiertos por reflexión (`UniverseModuleRegistry`), con `link.xml` y `[Preserve]` para IL2CPP | Añadir un universo no exige tocar el núcleo ni el Editor |
| D6 | Reglas especiales como clase base `UniverseRuleBase` (economía y factor de distancia) | Estrategias por universo sin métodos de interfaz por defecto (C# 9/IL2CPP) |
| D7 | Guardado con Newtonsoft.Json, solo campos públicos; el directorio lo pone Core | Funciona igual en Unity y en dotnet |
| D8 | Malla y render de planetas (Burst) en Presentation | Generation queda puro |
| D9 | UI uGUI construida por código con la fuente `LegacyRuntime.ttf`; escenas con un único objeto raíz | No se pueden escribir prefabs ni escenas a mano; evita importar TMP |
| D10 | Cámara propia con zoom continuo planeta ↔ sistema; Cinemachine instalado sin usar | Menos API externa sin compilador; migración prevista en S5 |
| D11 | Entrada con `#if ENABLE_INPUT_SYSTEM` y alternativa con el Input antiguo | Funciona con cualquier backend de entrada |
| D12 | Rampa de biomas horneada en runtime (`BiomeRampCache`, textura 256×1) | Sin PNG ni importadores en el configurador |
| D13 | Catálogo en `Resources/AEON/UniverseCatalog.asset`; si falta, se construye desde los módulos | El juego arranca aunque el configurador no haya corrido |
| D14 | Ingreso base por dios no derrotado (Fe 8, Energía 4, Materia 3, Conocimiento 2), Fe inicial 30, planeta asignado adorando a "ancestros" con fe 0,6–0,8 | Evita el bloqueo del jugador sin planetas y hace que la conversión dure varios ciclos (valores de balance provisionales) |
| D15 | El generador ordena el catálogo por `id` antes de barajar | El orden de descubrimiento no cambia el multiverso de una semilla |
| D16 | Sin commits por funcionalidad: `git init` y un único commit inicial al final, sin tocar la configuración de git | Petición de commit inicial |
| D17 | C# 9 (sin namespaces de archivo, `global using`, `init`, records, métodos de interfaz por defecto…); el csproj de dotnet fija `LangVersion 9.0` | Compatibilidad real con el compilador de Unity |

## 4. Valores de los dos universos

Valores del doc 03 §2 (los no fijados por el doc usan los valores por defecto del código de ejemplo).

| Campo | Thalassa | Resonancia |
|---|---|---|
| Gravedad | 0,6 | 1,0 |
| Flujo del tiempo / modelo | 0,7 / Lineal | 1,0 / Cíclico |
| Dimensiones espaciales | 3 | 3 |
| Factor de velocidad de la luz | 0,5 | 0,2 |
| Tipo de energía | Fluida | Armónica |
| Estabilidad de la materia | 0,7 | 1,0 |
| Viscosidad del medio | 0,8 | 0,2 |
| Creencia → realidad | 0,1 | 0,2 |
| Fe / Energía divina / Materia / Conocimiento | 1,0 / 1,0 / 1,0 / 1,0 | 1,2 / 1,0 / 1,1 / 1,0 |
| Coste de milagros | 1,0 | 0,9 |
| Fuerza militar | 0,8 | 0,8 |
| Difusión cultural | 1,2 | 1,6 |
| Progreso tecnológico / coste de viaje | 1,0 / 1,0 | 1,0 / 1,0 |
| Estabilidad de civilizaciones | 0,8 | 0,9 |
| Galaxias / brazos espirales | 2 / 3 | 2 / 5 |
| Sistemas por galaxia / planetas por sistema | 6–12 / 2–7 | 6–12 / 2–7 |
| Especies nativas | leviatán estelar, colonia coralina, mareante | cantor de cuarzo, coro silíceo |
| Dios nativo | Nerith (agresividad 0,4, piedad 0,6, curiosidad 0,4, diplomacia 0,9, avaricia 0,5) | Lyrael (0,2, 0,9, 0,5, 0,8, 0,3) |
| Factor de viaje calculado | ≈ 2,5456 | ≈ 2,6833 |
| Paleta | azul abisal y verde bioluminiscente; órbitas con ondulación 0,1 | lila, rosa cuarzo y plata; órbitas sin ondulación |
| Regla especial | **Corrientes:** viajar a otro sistema a favor de la corriente cuesta la mitad; la dirección cambia cada 6 ciclos | **Afinación:** los planetas propios de un mismo sistema con la misma frecuencia (1–5), si son 2 o más, dan cada uno un 20 % extra de Fe |

## 5. Resultado de la verificación

**Tests con dotnet (verificación real de las capas puras).** Se instaló .NET 8 SDK (8.0.427) sin permisos de administrador en `%USERPROFILE%\.dotnet`. El comando

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" test "C:\Users\RR24104.SLV\Documents\AEON\Tools\PureCore\AEON.PureCore.Tests.csproj"
```

compila Domain, Generation, Simulation, AI, Save, Rules, los módulos de universo y los tests `Pure/` con `LangVersion 9.0`, sin errores ni avisos. Resultado final: **35/35 tests en verde** (generador del multiverso, valores de Thalassa y Resonancia, factores de viaje, registro de módulos, reglas Corrientes y Afinación, turnos, IA determinista en 50 ciclos, guardado y carga con checksum y `.bak`, planeta del jugador fuera del sistema del rival).

**Revisión estática.**
- Ningún archivo `.meta`, `.unity`, `.asset` ni `.mat` escrito a mano; `manifest.json` válido con 25 dependencias; los 13 `.asmdef` son JSON válido y el grafo no tiene ciclos.
- El núcleo (`Assets/_AEON/Scripts`) no contiene `thalassa`, `resonancia` ni referencias a `GodGame.Universes`; las capas puras no usan `UnityEngine`.
- Cada `using` de la capa Unity está cubierto por una referencia de su asmdef; las firmas entre capas coinciden; ningún miembro `internal` se usa desde otro ensamblado; no se usan APIs obsoletas de Unity 6 (`FindObjectOfType`, `BuildTargetGroup`, `Arial.ttf`…).
- La revisión de código final quedó **APROBADA** sin hallazgos, tras corregir 6 hallazgos de la primera revisión (estrella y atmósfera con malla propia sin collider, texto de la Afinación movido al módulo, tests no atados a exactamente dos módulos, planeta del jugador fuera del sistema del rival, csproj con `Universes/**`, `.gitignore` con la salida de Addressables).

## 6. Limitaciones

- **La capa Unity no se compiló:** en este equipo no hay Unity Editor. Rules.Assets, Core, Presentation, Editor y el test `UniverseRulesAssetTests` solo se revisaron de forma estática. La primera apertura puede mostrar algún error de compilación menor; anota archivo, línea y mensaje para corregirlo.
- El renderer URP creado por código no recibe `postProcessData`: el post-procesado no estará disponible hasta asignarlo en el renderer o crear el URP Asset a mano.
- Si los shaders aún no están importados cuando corre el configurador automático, no se crean los materiales; el configurador lo avisa y vuelve a intentarlo en la siguiente apertura (o desde el menú).
- El arranque automático no crea escenas si hay escenas abiertas con cambios sin guardar.
- Los valores de balance de D14 son provisionales.
- Los paquetes core (URP, uGUI, Test Framework) los ajusta el Editor a su versión; si otro paquete no resuelve, basta con *Update* en el Package Manager.

## 7. Primeros pasos

1. **Abrir:** Unity Hub → *Projects* → *Add* → *Add project from disk* → `C:\Users\RR24104.SLV\Documents\AEON`. Usa Unity **6.3 LTS** (cualquier 6000.3.x) con Android Build Support (OpenJDK, SDK y NDK) e iOS Build Support. La primera apertura tarda unos minutos.
2. **Configurar:** espera a que termine el configurador automático (mensajes `[AEON]` en la consola) o ejecútalo desde el menú **AEON → Configurar proyecto**. Puede relanzarse sin duplicar nada.
3. **Jugar:** abre `Assets/_AEON/Scenes/Boot.unity` y pulsa **Play**. En el menú elige o genera una semilla y pulsa **Nueva partida**; convierte tu planeta asignado, pulsa **Fin de ciclo** y prueba **Guardar** / **Cargar**.
4. **Ejecutar tests:** en Unity, *Window → General → Test Runner → EditMode → Run All*. Sin Unity, el comando dotnet de la sección 5.
5. **Añadir un universo:** copia `Assets/_AEON/Universes/_Plantilla`, borra los `.meta` de la copia, quita la extensión `.txt`, renombra `Plantilla`, rellena los valores del doc 03 §2 y ejecuta **AEON → Configurar proyecto**. La guía completa está en `Assets/_AEON/Universes/_Plantilla/LEEME.md`.
