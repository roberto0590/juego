# ÆON: Guerra de los Siete Velos · Documentación de diseño e implementación

> Juego móvil (iOS y Android) de estrategia 4X / *god game* en el que el jugador es un dios. Empieza en un universo asignado al azar con un único planeta que conquistar, y termina disputando hasta **7 universos** con leyes físicas distintas a otros siete dioses.

## Resumen ejecutivo

| Aspecto | Decisión |
|---|---|
| Plataformas | App Store (iOS 15+) y Google Play (Android 8+), teléfono y tableta, horizontal |
| Motor y lenguaje | **Unity 6 LTS + URP**, **C#**; shaders en **Shader Graph + HLSL**; generación procedural con **Jobs + Burst** |
| Género | Estrategia 4X / god game con **turnos simultáneos ligeros** (sesiones de 5–15 min) |
| Progresión | Planeta → Sistema solar → Galaxia → Universo → **Multiverso (7 universos)** |
| Azar | El universo y el planeta iniciales salen de una **semilla reproducible** que se puede compartir |
| Recursos | Fe / Devoción, Energía Divina, Materia, Conocimiento |
| Conquista | Militar, cultural/religiosa y tecnológica, cada una con consecuencias en la historia |
| IA | **Utility AI** con 7 personalidades de dioses rivales |
| Estilo visual | **Low-poly premium** estilizado: toon + rim + bloom + emisivos, paleta propia por universo |
| Rendimiento | 60 fps en gama media y 30 fps en gama baja; paquete base < 150 MB; universos por **Addressables** |
| Arte | Krita/Procreate → Blender → Substance 3D Painter (o ArmorPaint) → Unity; Mixamo solo para humanoides; Spine/Live2D para retratos |
| Backend | Unity Gaming Services (Auth, Cloud Save, Remote Config, Leaderboards, Analytics) + Firebase Crashlytics; juego **offline-first** |
| Monetización | Ética: Acto I gratis + desbloqueo único de la campaña + cosméticos; **sin pay-to-win** |
| MVP | **2 universos** (Thalassa y Resonancia), Actos I–II; el resto en actualizaciones cada 8–10 semanas |
| Plazo | ~15 meses hasta el lanzamiento global con un equipo de ~5–6 personas (~81 personas-mes) |

## Historia en 30 segundos

El multiverso fue una sola mente, **Aion**, que se partió en siete universos durante **el Quiebre**. De cada fragmento nació un dios. Desde los bordes avanza **Nihil, el Vacío que Recuerda**. El jugador es **el Octavo**, una chispa divina sin hogar que el **Telar** deposita al azar en uno de los universos. Quien reúna las siete **Coronas de Ley** decidirá el destino de todo: **Tiranía, Armonía, Trascendencia o Silencio**. El giro final es que el Octavo *fue* Aion.

## Índice

| # | Documento | Contenido |
|---|---|---|
| 00 | [Visión y estrategia](00_vision_y_estrategia.md) | Pilares, público, propuesta de valor, comparativa de motores, monetización |
| 01 | [GDD: diseño de juego](01_GDD_diseno_de_juego.md) | Bucles, escalas, recursos, milagros, árbol divino, conquista, combate, diplomacia, IA, victoria, UX móvil, tutorial |
| 02 | [Trama y lore](02_trama_y_lore.md) | Premisa, actos, los 7 dioses, giros, finales múltiples, misiones |
| 03 | [Universos y leyes físicas](03_universos_y_leyes_fisicas.md) | Los 7 universos, especies, leyes → mecánicas, `UniverseRules`, asignación aleatoria |
| 04 | [Diseño de personajes](04_diseno_de_personajes.md) | Pipeline de 15 pasos, herramientas, guía de estilo, fichas, presupuestos, checklist |
| 05 | [Escenografía y generación procedural](05_escenografia_y_generacion_procedural.md) | Planetas, atmósfera, sistemas, galaxias, skyboxes, iluminación, post-proceso, LOD |
| 06 | [Arquitectura técnica](06_arquitectura_tecnica.md) | Capas, carpetas, asmdef, patrones, modelo de datos, guardado, determinismo, CI/CD |
| 07 | [Plan de trabajo y cronograma](07_plan_de_trabajo_y_cronograma.md) | MVP, fases, sprints, Gantt, equipo, esfuerzo, costes, riesgos |
| 08 | [Publicación en App Store y Play Store](08_publicacion_app_store_play_store.md) | Cuentas, firmas, AAB/PAD, TestFlight, políticas, IAP, ASO, checklist |
| 09 | [Rendimiento y optimización móvil](09_rendimiento_y_optimizacion_movil.md) | Presupuestos, batching, texturas, térmica, perfiles de calidad, profiling |
| 10 | [QA, testing y balance](10_QA_testing_y_balance.md) | Unity Test Framework, simulaciones de balance, playtesting, KPI, dispositivos |
| — | [Código de ejemplo](codigo/README.md) | Scripts C# y shaders listos para copiar a un proyecto Unity 6 |

## Cómo leer esta documentación

- **Dirección o producción:** README → [00](00_vision_y_estrategia.md) → [07](07_plan_de_trabajo_y_cronograma.md) → [08](08_publicacion_app_store_play_store.md).
- **Diseño y narrativa:** [01](01_GDD_diseno_de_juego.md) → [02](02_trama_y_lore.md) → [03](03_universos_y_leyes_fisicas.md) → [10](10_QA_testing_y_balance.md) §3 (balance).
- **Arte:** [04](04_diseno_de_personajes.md) → [05](05_escenografia_y_generacion_procedural.md) → [09](09_rendimiento_y_optimizacion_movil.md) §4 (texturas).
- **Programación:** [06](06_arquitectura_tecnica.md) → [codigo/README.md](codigo/README.md) → [03](03_universos_y_leyes_fisicas.md) §4 → [05](05_escenografia_y_generacion_procedural.md) → [09](09_rendimiento_y_optimizacion_movil.md) → [10](10_QA_testing_y_balance.md).

**Convenciones:**
- Los valores numéricos de diseño son **iniciales** y se ajustan con simulaciones y Remote Config.
- Los nombres de clases y campos en `monoespaciado` existen en [`codigo/`](codigo/).
- Los precios y las políticas de tienda deben **verificarse** en el momento de usarlos, porque cambian con frecuencia.
- Los diagramas usan **Mermaid**, que GitHub, GitLab, VS Code (con extensión) y Obsidian muestran directamente.

## Estructura de la carpeta

```text
juego/
├── README.md
├── 00_vision_y_estrategia.md
├── 01_GDD_diseno_de_juego.md
├── 02_trama_y_lore.md
├── 03_universos_y_leyes_fisicas.md
├── 04_diseno_de_personajes.md
├── 05_escenografia_y_generacion_procedural.md
├── 06_arquitectura_tecnica.md
├── 07_plan_de_trabajo_y_cronograma.md
├── 08_publicacion_app_store_play_store.md
├── 09_rendimiento_y_optimizacion_movil.md
├── 10_QA_testing_y_balance.md
└── codigo/
    ├── README.md
    ├── Domain/       UniverseRules.cs, MultiverseModel.cs, RandomExtensions.cs, GodGame.Domain.asmdef
    ├── Generation/   MultiverseGenerator.cs, GalaxyGenerator.cs, StarSystemGenerator.cs,
    │                 PlanetMeshGenerator.cs, ProceduralPlanet.cs, GodGame.Generation.asmdef
    ├── Simulation/   GameState.cs, TurnManager.cs, GodGame.Simulation.asmdef
    │   └── Commands/ ICommand.cs, CommandCosts.cs, ConquerPlanetCommand.cs, MiracleCommand.cs
    ├── AI/           GodUtilityAI.cs, GodGame.AI.asmdef
    ├── Save/         SaveSystem.cs, GodGame.Save.asmdef
    └── Shaders/      PlanetSurface.shader, PlanetAtmosphere.shader, ToonCharacter.shader
```

## Próximos pasos inmediatos (semana 1)

1. Crear el proyecto Unity 6 LTS (plantilla **Universal 3D**) y copiar `codigo/` según [codigo/README.md](codigo/README.md).
2. Crear los 2 assets `UniverseRules` del MVP con los valores de [03 §2](03_universos_y_leyes_fisicas.md).
3. Configurar el repositorio con Git LFS y la CI con GameCI ([06 §10](06_arquitectura_tecnica.md)).
4. Prototipar el core loop en papel ([10 §4](10_QA_testing_y_balance.md)) mientras arte trabaja en los moodboards.
5. Registrar las cuentas de Apple Developer y Google Play Console (la verificación tarda días o semanas).
