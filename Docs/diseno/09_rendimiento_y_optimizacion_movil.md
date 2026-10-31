# 09 · Rendimiento y optimización móvil

## 1. Objetivos

| Clase de dispositivo | Ejemplos de referencia | FPS objetivo | Resolución de render | Memoria máxima de la app |
|---|---|---|---|---|
| Gama baja | Android con 3–4 GB de RAM, Mali-G52 / Adreno 610 (p. ej. Galaxy A13/A14, Redmi 10); iPhone XR/11 | **30** | 0,7 × nativa (Render Scale) | 1,0–1,2 GB |
| Gama media | Adreno 642L / Mali-G68 (Galaxy A54, Pixel 6a); iPhone 12–13 | **60** (30 opcional para ahorrar batería) | 0,85 × | 1,5 GB |
| Gama alta | Snapdragon 8 Gen 2+, iPhone 15 Pro+ | 60 (120 en menús, opcional) | 1,0 × | 2 GB |

El presupuesto de frame es de **33,3 ms a 30 fps** y **16,6 ms a 60 fps**. Se reserva un **margen del 20–30 %** para la térmica: el móvil baja frecuencias tras 5–10 minutos.

## 2. Presupuestos por frame (gama media, 60 fps)

| Recurso | Presupuesto | Notas |
|---|---|---|
| CPU principal (scripts + render thread) | ≤ 10 ms | La resolución de turno puede tardar más (no es por frame) |
| GPU | ≤ 12 ms | |
| Draw calls / *SetPass calls* | ≤ 150 / ≤ 40 | En gama baja, ≤ 100 / ≤ 30 |
| Triángulos en pantalla | ≤ 300 000 | 150 000 en gama baja |
| Partículas vivas | ≤ 1 500 | 500 en gama baja |
| Luces adicionales por píxel | ≤ 2 | 0 en gama baja (por vértice) |
| Texturas en memoria | ≤ 350 MB | Gama baja: 200 MB |
| GC alloc por frame | **0 B** en juego | Las asignaciones solo ocurren al cambiar de capa o universo |

## 3. Batching y draw calls

| Técnica | Cuándo usarla | Cómo |
|---|---|---|
| **SRP Batcher** | Siempre (por defecto en URP) | Todos los shaders con las propiedades en `CBUFFER_START(UnityPerMaterial)`. Los 3 shaders de ejemplo lo cumplen. Se varía por objeto con propiedades del material, no con `MaterialPropertyBlock`, que rompe el SRP Batcher. |
| **GPU Instancing** | Muchas copias de la misma malla (unidades, asteroides, estrellas de fondo, iconos) | `Graphics.RenderMeshInstanced` o `RenderMeshIndirect` (gama media y alta). Los shaders tienen `#pragma multi_compile_instancing`. |
| **Static Batching** | Decorados estáticos de las escenas de historia | Marcar *Batching Static* |
| **Combinar mallas** | Personajes modulares (Quimeras, el Octavo) | `Mesh.CombineMeshes` en runtime al cambiar de piezas |
| **Atlas de texturas** | Por facción y por UI | 1 material por facción = batch compartido |
| **Polvo galáctico** | 15 000 puntos | Una sola malla estática de quads: 1 draw call |

Nota: el SRP Batcher y el GPU instancing no se combinan en el mismo draw. Para los objetos instanciados (`RenderMeshInstanced`) se usa instancing; para el resto, el SRP Batcher.

## 4. Texturas y memoria

| Uso | Tamaño máximo | Formato iOS / Android | Mipmaps |
|---|---|---|---|
| Atlas de personajes de facción | 1024² | ASTC 6×6 | Sí |
| Héroes / dioses | 1024² (+ emisivo 512²) | ASTC 5×5 o 6×6 | Sí |
| Normales (solo héroes) | 1024² | ASTC 5×5 | Sí |
| Gradientes de biomas | 256×1 | RGBA32 (minúscula) | No |
| Skybox (horneado) | Cubemap de 512–1024 | ASTC 6×6 | Sí |
| UI | Atlas de sprites de 2048² | ASTC 4×4 (nitidez) | **No** |
| Retratos Spine | 2048² (alta) / 1024² (baja) | ASTC 6×6 | No |

- *Texture Streaming* (*Mipmap Streaming*) activado con un presupuesto por perfil de calidad.
- *Read/Write* **desactivado** en todas las texturas y mallas que no se procesan en CPU (duplica la memoria).
- **Addressables:** al salir de un universo se hace `Addressables.Release` de su paquete. Se comprueba con el *Memory Profiler* que la memoria vuelve a la línea base.
- **Audio:** música en *Streaming* (Vorbis, calidad 60–70 %); SFX cortos en *Decompress On Load* (ADPCM); voces en *Compressed In Memory*.
- **Mallas procedurales:** se reutiliza la `Mesh` (parámetro `reuse`) y se libera con `Destroy(mesh)` al descartarla. Las mallas creadas por código **no** las recoge el GC.

## 5. CPU y scripts

- La simulación (`TurnManager`) **no se ejecuta por frame**, solo al resolver el ciclo. La IA se puede llevar a un hilo (`Task.Run`) porque es C# puro.
- Generación procedural con **Jobs + Burst** (planetas) y, para galaxias grandes, versiones en job de `GalaxyGenerator.GenerateSpiralPoints`.
- Nada de `Find`, `GetComponent` ni LINQ en `Update`. Las referencias se cachean y se usan *managers* que actualizan listas en lugar de cientos de `Update()`.
- *Object pooling* para VFX, textos flotantes e iconos.
- UI Toolkit: se evita reconstruir el árbol visual cada frame y se actualizan solo los `Label` que cambian.
- *Incremental GC* activado (opción por defecto en Unity 6) como red de seguridad, no como solución.

## 6. GPU y shaders

- Sombreado toon: más barato que el PBR Lit (sin BRDF compleja, sin muestras de reflexión).
- **Overdraw:** el peor enemigo en móvil (atmósferas, nubes, partículas, nebulosas). Se mide con el modo *Overdraw* de la Scene View o con las herramientas de GPU:
  - atmósfera: 1 capa aditiva;
  - nubes: 1 capa;
  - partículas: como máximo 3 capas superpuestas;
  - nebulosas: quads grandes con alfa baja **detrás** de todo.
- La precisión `half` en los fragment shaders reduce mucho el coste en GPU Mali y Adreno.
- *Shader variants:* se limitan los `multi_compile` y se hace *strip* de las variantes no usadas (*Graphics Settings → Shader Stripping*) y con un `IPreprocessShaders` propio. Hay que prewarmar los shaders (*ShaderVariantCollection*) en la pantalla de carga para evitar tirones.
- **Post-proceso:** ver la tabla en [05_escenografia_y_generacion_procedural.md](05_escenografia_y_generacion_procedural.md) §8. En gama baja, Bloom con 2 iteraciones a la mitad de resolución y sin DOF.
- **MSAA:** 2x en gama media (barato en GPU de *tiles*) y FXAA o nada en gama baja.

## 7. Térmica y batería

| Medida | Efecto |
|---|---|
| **Limitar FPS en menús, en el turno de planificación sin interacción y en diálogos a 30** (`Application.targetFrameRate`) | Hasta un 40 % menos de consumo |
| **On-Demand Rendering** (`OnDemandRendering.renderFrameInterval = 2–3`) cuando la cámara está quieta | La UI sigue respondiendo y la GPU descansa |
| Volver a 60 fps al tocar la pantalla o durante la animación de resolución | La fluidez se nota donde importa |
| **Adaptive Performance** (paquete; proveedores Samsung y Android genéricos) | Baja la *render scale* o el LOD al detectar *throttling* |
| Sin *busy loops*; `Update` mínimo | Menos CPU |
| Modo "Ahorro de batería" en Ajustes | 30 fps fijos, render scale 0,7, sin bloom |

**Prueba de térmica:** 15 minutos de juego continuo en el dispositivo de gama baja. El FPS medio debe mantenerse por encima del 90 % del objetivo y la temperatura no debe disparar un aviso del sistema.

## 8. Perfiles de calidad por dispositivo

| Ajuste | Bajo | Medio | Alto |
|---|---|---|---|
| URP Asset | `URP_Low` | `URP_Medium` | `URP_High` |
| Render Scale | 0,7 | 0,85 | 1,0 |
| HDR | Desactivado (bloom en LDR) | Activado | Activado |
| MSAA | No | 2x | 4x |
| Sombras | No | 1 cascada de 1024 | 2 cascadas de 2048 |
| Resolución del planeta enfocado | 64 | 96 | 128 |
| Nubes | Sin sombras | Con sombras | Con sombras |
| Puntos de polvo galáctico | 5 000 | 10 000 | 20 000 |
| Contorno toon | Solo dioses | Héroes | Héroes y avatares |
| FPS por defecto | 30 | 60 | 60 |

**Detección automática** al primer arranque:
1. Se lee `SystemInfo.systemMemorySize`, `SystemInfo.graphicsDeviceName`, `processorCount` y `graphicsMemorySize`.
2. Se compara con una **tabla de dispositivos** (Remote Config) que mapea GPUs conocidas a perfiles.
3. Si la GPU no aparece en la tabla, se hace un **micro-benchmark** de 3 s en la escena de nacimiento (renderiza el planeta y mide el tiempo de frame) y se elige el perfil.
4. El jugador puede cambiarlo en Ajustes. Se guarda en `PlayerPrefs`.

## 9. Profiling

| Herramienta | Para qué | Consejo |
|---|---|---|
| **Unity Profiler** (conectado al dispositivo) | CPU, GC alloc, render thread, memoria | Siempre en una **build de desarrollo en el dispositivo**, nunca solo en el editor. Usar *Deep Profile* solo de forma puntual. |
| **Profile Analyzer** | Comparar dos capturas (antes y después de optimizar) | Con capturas de 300 frames |
| **Memory Profiler** (paquete) | Fugas, texturas duplicadas, Addressables no liberados | Snapshot antes y después de visitar un universo |
| **Frame Debugger** | Draw calls, por qué no se agrupa un batch, orden de pases | Buscar "SRP Batch" y las razones de ruptura |
| **Rendering Debugger** (URP) | Overdraw, complejidad de vértices y modos de visualización | |
| **Xcode Instruments** (Metal System Trace, Time Profiler, Energy Log) | GPU en iOS, energía y *hitches* | Junto al *Xcode GPU Frame Capture* para ver el coste por shader |
| **Android GPU Inspector (AGI)** | Contadores de GPU en Adreno y Mali, captura de frames Vulkan | Requiere un dispositivo compatible |
| **Arm Performance Studio** (Streamline, Mali Offline Compiler) | Ciclos de shader en Mali | Pasar los 3 shaders por el compilador offline |
| **Snapdragon Profiler** | GPU Adreno | |
| **Android Vitals / Xcode Organizer** | ANR, crashes, consumo y arranque en producción | Revisión semanal después del lanzamiento |

**Rutina:** al final de cada sprint se ejecuta un **benchmark automatizado** (una escena que recorre planeta → sistema → galaxia → universo con la cámara durante 60 s y registra FPS, frame time p95 y memoria) en los 3 dispositivos de referencia. Se guardan los resultados en CI. Una regresión de más del 10 % bloquea el merge.

## 10. Tiempo de arranque y tamaño

- Objetivo: **primer frame interactivo en menos de 5 s** en gama media. *Splash* mínimo y carga de Addressables asíncrona.
- Tamaño base menor de 150 MB. *Build Report* (o el paquete *Build Report Inspector*) en CI para avisar si sube más de 5 MB entre versiones.
- *Managed stripping* en High con `link.xml` para los tipos que se usan por reflexión (`JsonUtility` serializa los tipos del modelo y hay que mantenerlos).
