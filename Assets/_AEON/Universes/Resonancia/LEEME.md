# Módulo de universo: Resonancia

Resonancia es el universo de **cristal y sonido**: la luz es lenta, el sonido viaja por el vacío y toda la materia es cristalina y vibra. Las civilizaciones se comunican cantando.

## Contenido

| Archivo | Qué es |
|---|---|
| `GodGame.Universes.Resonancia.asmdef` | Ensamblado del módulo. Solo referencia `GodGame.Domain`, `GodGame.Simulation` y `GodGame.Rules`; C# puro (`noEngineReferences`). Nadie del núcleo lo referencia. |
| `ResonanciaModule.cs` | Implementa `IUniverseModule`: leyes físicas, generación, dios nativo y presentación por defecto. `UniverseModuleRegistry` lo descubre por reflexión. |
| `AfinacionRule.cs` | Regla especial "Afinación" (subclase de `UniverseRuleBase`). |
| `link.xml` | Evita que IL2CPP elimine los tipos del módulo (se usan por reflexión). |
| `Data/` | La crea el configurador (*AEON → Configurar proyecto*): `UR_Resonancia.asset` y sus materiales. No se escribe a mano. |

## Origen de los valores

| Valores | Fuente |
|---|---|
| `id`, nombre, ley dominante, paleta (lila, rosa cuarzo, plata), dios `Lyrael`, energía `Armonica` | doc 03 §1 (vista general) |
| `gravityMultiplier` 1,0 · `timeFlowRate` 1,0 · `timeModel` Ciclico · `spatialDimensions` 3 · `lightSpeedFactor` 0,2 · `matterStability` 1,0 · `mediumViscosity` 0,2 · `beliefToReality` 0,2 | doc 03 §2 |
| Modificadores: fe 1,2 · energía 1,0 · materia 1,1 · conocimiento 1,0 · milagros 0,9 · militar 0,8 · cultural 1,6 · tecnológica 1,0 · viaje 1,0 · estabilidad 0,9 | doc 03 §2 |
| `galaxyCount` 2 · `spiralArms` 5 | doc 03 §2 |
| Especies `cantor_cuarzo`, `coro_siliceo` | doc 03 §3.4 (Cantores de Cuarzo, Coro Silíceo) |
| Regla "Afinación": dos planetas propios con la misma frecuencia (1–5) en un sistema reciben +20 % de Fe | doc 03 §3.4 |

### Supuestos (el doc 03 no los fija)

- `systemsPerGalaxy` (6, 12), `planetsPerSystem` (2, 7) y `spectralWeights` (0,01 · 0,03 · 0,06 · 0,12 · 0,18 · 0,25 · 0,35): valores por defecto del código de ejemplo.
- Personalidad de Lyrael (agresividad 0,2 · piedad 0,9 · curiosidad 0,5 · diplomacia 0,8 · avaricia 0,3): tabla de rivales del generador de ejemplo (doc 02).
- Colores RGB concretos de la paleta y rampa de biomas: interpretación de la paleta del doc 03 §1. Órbitas sin vaivén (`orbitWobble` 0).
- Frecuencia de un planeta = `1 + (DeriveSeed(semilla, "freq:" + id) mod 5)`: estable por partida y sin guardarla en el archivo.
- El +20 % se calcula sobre la Fe que genera cada planeta afinado (`EconomyFormulas.PlanetFaithIncome`) y se suma tras la economía base. Solo cuentan planetas cuya civilización adora a su dueño.
- La luz lenta (informes con retraso) y el Gran Acorde (`TimeModel.Ciclico`) del doc 03 §3.4 quedan para fases posteriores.

Factor de viaje resultante (`ComputeTravelCostFactor`): 1 · (3/3) · 1,2 · (1/√0,2) ≈ **2,6833**.
