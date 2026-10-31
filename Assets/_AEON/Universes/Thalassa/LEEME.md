# Módulo de universo: Thalassa

Thalassa es el universo del **espacio fluido**: un éter líquido llena el vacío, las estrellas son faros submarinos y las **corrientes** conectan los sistemas.

## Contenido

| Archivo | Qué es |
|---|---|
| `GodGame.Universes.Thalassa.asmdef` | Ensamblado del módulo. Solo referencia `GodGame.Domain`, `GodGame.Simulation` y `GodGame.Rules`; C# puro (`noEngineReferences`). Nadie del núcleo lo referencia. |
| `ThalassaModule.cs` | Implementa `IUniverseModule`: leyes físicas, generación, dios nativo y presentación por defecto. `UniverseModuleRegistry` lo descubre por reflexión. |
| `CorrientesRule.cs` | Regla especial "Corrientes" (subclase de `UniverseRuleBase`). |
| `link.xml` | Evita que IL2CPP elimine los tipos del módulo (se usan por reflexión). |
| `Data/` | La crea el configurador (*AEON → Configurar proyecto*): `UR_Thalassa.asset` y sus materiales. No se escribe a mano. |

## Origen de los valores

| Valores | Fuente |
|---|---|
| `id`, nombre, ley dominante, paleta (azul abisal, verde bioluminiscente), dios `Nerith`, energía `Fluida` | doc 03 §1 (vista general) |
| `gravityMultiplier` 0,6 · `timeFlowRate` 0,7 · `timeModel` Lineal · `spatialDimensions` 3 · `lightSpeedFactor` 0,5 · `matterStability` 0,7 · `mediumViscosity` 0,8 · `beliefToReality` 0,1 | doc 03 §2 |
| Modificadores: fe 1,0 · energía 1,0 · materia 1,0 · conocimiento 1,0 · milagros 1,0 · militar 0,8 · cultural 1,2 · tecnológica 1,0 · viaje 1,0 · estabilidad 0,8 | doc 03 §2 |
| `galaxyCount` 2 · `spiralArms` 3 | doc 03 §2 |
| Especies `leviatan_estelar`, `colonia_coralina`, `mareante` | doc 03 §3.6 (Leviatanes estelares, Colonias coralinas, Mareantes) |
| Regla "Corrientes": a favor de la corriente el viaje cuesta ×0,5; cambia cada 6 ciclos | doc 03 §3.6 |
| `orbitWobble` 0,1 (órbitas onduladas) | doc 05 (y += sin(t)·0,1) |

### Supuestos (el doc 03 no los fija)

- `systemsPerGalaxy` (6, 12), `planetsPerSystem` (2, 7) y `spectralWeights` (0,01 · 0,03 · 0,06 · 0,12 · 0,18 · 0,25 · 0,35): valores por defecto del código de ejemplo.
- Personalidad de Nerith (agresividad 0,4 · piedad 0,6 · curiosidad 0,4 · diplomacia 0,9 · avaricia 0,5): tabla de rivales del generador de ejemplo (doc 02).
- Colores RGB concretos de la paleta y rampa de biomas: interpretación de la paleta del doc 03 §1.
- Dirección de la corriente: en los ciclos 0–5 fluye hacia los sistemas de ordinal mayor (orden de generación), en 6–11 hacia los de menor, y así sucesivamente. Solo afecta a viajes entre sistemas distintos.

Factor de viaje resultante (`ComputeTravelCostFactor`): 1 · (3/3) · 1,8 · (1/√0,5) ≈ **2,5456**.
