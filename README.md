# El Viaje del Héroe — Unity

Recreación en Unity del juego web solitario (Vite + React + TypeScript). El
motor de juego se portó 1:1 a C# puro (determinista, sin dependencias de Unity)
desde el `src/game/*` del proyecto web.

- **Editor:** Unity 6 LTS (6000.x)
- **Plantilla:** 2D (URP)
- **Tests:** NUnit vía el Test Framework de Unity (ya incluido)

## Estado del puerto

| Fase | Contenido | Estado |
|------|-----------|--------|
| **1. Núcleo C#** | Lógica pura + tests NUnit | ✅ Hecho |
| 2. Bridge + tablero | `GameController` (MonoBehaviour) + render del mapa en UI Toolkit | ⏳ Pendiente |
| 3. UI de juego | Combate, libro, HUD, mano, dado en UXML/USS | ⏳ Pendiente |
| 4. Pulido | Animaciones, música, guardado, pantalla inicial | ⏳ Pendiente |

## Correr los tests

1. Abre el proyecto en Unity.
2. **Window → General → Test Runner**.
3. Pestaña **EditMode → Run All**. Deben pasar ~30 casos (equivalen a los 40 de
   Vitest de la web; varios tests web comprueban dos cosas a la vez).

## Estructura

```
Assets/
  Scripts/
    Game/                 ← ensamblado ElViaje.Game (lógica pura, sin UnityEngine)
      Enums.cs            tipos del dominio (Dir, Region, CardKind, Phase, …)
      Rng.cs              mulberry32 determinista (uint + unchecked)
      Cards.cs            catálogo: 52 cartas del Mazo + finales/iniciales
      Types.cs            GameState, PlacedCard, GameAction, Clone() profundo
      Geometry.cs         conexiones, adyacencia, BFS de rutas, colocaciones
      Engine.cs           CreateInitialState / GetLegalMoves / ApplyMove / …
      ElViaje.Game.asmdef
  Tests/                  ← ensamblado ElViaje.Game.Tests (EditMode)
    CardsTests.cs
    GeometryTests.cs
    EngineTests.cs
    ElViaje.Game.Tests.asmdef
```

## Decisiones del puerto

- **Determinismo:** la semilla del RNG vive en `GameState.Rng` (`uint`) y cada
  uso devuelve `(valor, nuevaSemilla)`. La aritmética va en `unchecked` para
  imitar el desbordamiento de 32 bits de JavaScript (`Math.imul` / `>>>`).
- **Orden de iteración:** JS recorre `Object.keys(grid)` en orden de inserción;
  `Dictionary<K,V>` de C# no lo garantiza. Donde el orden es observable
  (`FrontierCells`, `OpenEnds`) las claves se ordenan (`y`, luego `x`) para que
  el resultado sea reproducible.
- **`structuredClone` → `GameState.Clone()`** (copia profunda manual).
- **Uniones de TS → clases/enum:** `GameAction` es una clase con `ActionType` y
  fábricas estáticas (`GameAction.PlayCard(...)`). Opcionales → `int?` / enum.
- **Default de dificultad:** `NewGameOptions` es `struct`, así que su valor por
  defecto es `Difficulty.Facil`. La capa de UI debe fijar la dificultad
  explícitamente (los tests siempre la pasan).

## Siguiente paso (Fase 2)

`GameController : MonoBehaviour` que mantenga el `GameState`, exponga
`Dispatch(GameAction)` (→ `Engine.ApplyMove`) y notifique cambios; render del
tablero en UI Toolkit reutilizando los PNG y los rects de sprites de la web.
