// ============================================================================
// Chronicle — genera la crónica final de forma determinista a partir del
// historial estructurado (GameState.Chronicle). Sin IA: plantillas + variación
// controlada por índice. Ver puntos 4–9 de la especificación.
// ============================================================================
using System.Collections.Generic;
using System.Linq;
using ElViaje.Game;

namespace ElViaje.App
{
    public static class Chronicle
    {
        // Conectores que encajan con "{conector} {camino}, {verbo}."
        static readonly string[] Conn = { "Luego de atravesar", "Después de recorrer", "Tras cruzar" };

        public static string Title(GameState s) => s.Status == GameStatus.Won ? "Victoria" : "Derrota";

        public static string HeroName(GameState s)
        {
            string id = s.Party.Members.Count > 0 ? s.Party.Members[0].CardId : "inicial-heroe";
            return id == "inicial-heroina" ? "El Mago" : "El Caballero";
        }

        static string RoadArticle(string road) => "el " + road; // los caminos son "Camino ..."

        static (string verb, string cont) VerbFor(string kind) => kind switch
        {
            "pueblo" => ("llegó a", "a"),
            "hero" => ("reclutó a", "a"),
            "general" => ("luchó contra", "contra"),
            _ => ("", ""),
        };

        static string JoinY(List<string> parts)
        {
            if (parts.Count == 0) return "";
            if (parts.Count == 1) return parts[0];
            return string.Join(", ", parts.Take(parts.Count - 1)) + " y " + parts[parts.Count - 1];
        }

        // Une un grupo de eventos que comparten el mismo camino en una frase.
        // El verbo se repite solo cuando cambia: "reclutó a A, a B y llegó a C".
        static string GroupLine(int idx, string road, List<ChronicleEvent> group)
        {
            var frags = new List<string>();
            string prevVerb = null;
            foreach (var e in group)
            {
                var (verb, cont) = VerbFor(e.Kind);
                frags.Add(verb == prevVerb ? $"{cont} {e.Name}" : $"{verb} {e.Name}");
                prevVerb = verb;
            }
            string body = JoinY(frags);
            if (string.IsNullOrEmpty(road)) return $"En su camino, {body}.";
            return $"{Conn[idx % Conn.Length]} {RoadArticle(road)}, {body}.";
        }

        static string CleanReason(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return "el destino le fue esquivo";
            int i = reason.IndexOf("(§");
            var r = i >= 0 ? reason.Substring(0, i) : reason;
            return r.Trim().TrimEnd('.');
        }

        /// <summary>Devuelve la crónica como párrafos. El primero empieza por "El ...".</summary>
        public static List<string> Build(GameState s)
        {
            var evs = s.Chronicle;
            var paras = new List<string>();
            string hero = HeroName(s);

            // 1) Introducción según la primera carta jugada.
            var start = evs.FirstOrDefault(e => e.Kind == "start");
            string intro = hero + " comenzó su aventura";
            if (start != null)
            {
                intro += start.Extra switch
                {
                    "camino" => $" recorriendo {RoadArticle(start.Name)}.",
                    "pueblo" => $" en {start.Name}.",
                    "hero" => $" conociendo a {start.Name}.",
                    "general" => $" enfrentándose a {start.Name}.",
                    _ => ".",
                };
            }
            else intro += ".";
            paras.Add(intro);

            // 2) Eventos importantes agrupados por camino consecutivo compartido.
            var important = evs.Where(e => e.Kind == "pueblo" || e.Kind == "hero" || e.Kind == "general").ToList();
            int idx = 0, i = 0;
            while (i < important.Count)
            {
                string road = important[i].Road;
                var group = new List<ChronicleEvent>();
                int j = i;
                while (j < important.Count && (important[j].Road ?? "") == (road ?? "")) { group.Add(important[j]); j++; }
                paras.Add(GroupLine(idx++, road, group));
                i = j;
            }

            // 3) Desenlace: victoria o derrota (usando la causa real).
            bool victory = evs.Any(e => e.Kind == "victory");
            var defeat = evs.FirstOrDefault(e => e.Kind == "defeat");
            if (victory)
            {
                paras.Add("Finalmente llegó hasta el Castillo del Rey Demonio. Allí libró su última batalla y triunfó contra el mal atravesando su corazón.");
                paras.Add("Su viaje había llegado a su fin.");
            }
            else if (defeat != null)
            {
                if (defeat.Name == "Rey Demonio")
                    paras.Add("Finalmente llegó hasta el Castillo del Rey Demonio, pero fue derrotado luchando valientemente contra el mal que había perseguido durante toda su aventura.");
                else if (!string.IsNullOrEmpty(defeat.Name))
                    paras.Add($"Finalmente se enfrentó a {defeat.Name} y fue derrotado luchando valientemente.");
                else
                    paras.Add($"Su viaje terminó antes de alcanzar su destino: {CleanReason(defeat.Extra)}.");
            }

            return paras;
        }
    }
}
