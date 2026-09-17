// ============================================================================
// El Viaje del Héroe — port C# de la lógica pura (sin dependencias de Unity).
// Espejo de src/game/types.ts + enums del dominio.
// ============================================================================
namespace ElViaje.Game
{
    public enum Dir { Up, Down, Left, Right }

    public enum Region { Bosque, Planicies, Montanas, Volcan }

    public enum CardKind { Camino, Pueblo, Heroe, General, Castillo, Rey }

    public enum Phase { Draw, Play, Roll, Move, Combat, World, Final }

    public enum GameStatus { Playing, Won, Lost }

    public enum Difficulty { Facil, Medio, Dificil }

    public enum LogKind { Player, World, Combat, System }

    public enum CombatTier { Low, Mid, High }

    /// <summary>Orientación de un Héroe colocado (o None si no aplica).</summary>
    public enum Orientation { None, Horizontal, Vertical }

    public enum Starter { Heroe, Heroina }
}
