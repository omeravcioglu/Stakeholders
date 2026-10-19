using System;
using System.IO;
using Stakeholders.Core;
using UnityEngine;

namespace Stakeholders.App
{
    /// <summary>Solo save games: the full GameState as JSON in the persistent data folder.</summary>
    public static class SaveSystem
    {
        public const string AutoSlot = "autosave";
        public const string ManualSlot = "save1";

        static string Dir => Path.Combine(Application.persistentDataPath, "saves");
        static string PathFor(string slot) => Path.Combine(Dir, slot + ".json");

        public static bool Exists(string slot) => File.Exists(PathFor(slot));

        public static DateTime? Modified(string slot) => Exists(slot) ? File.GetLastWriteTime(PathFor(slot)) : (DateTime?)null;

        public static bool Save(GameState state, string slot)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                string tmp = PathFor(slot) + ".tmp";
                File.WriteAllText(tmp, Codec.ToJson(state));
                if (File.Exists(PathFor(slot))) File.Delete(PathFor(slot));
                File.Move(tmp, PathFor(slot));
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Stakeholders: save failed: " + ex.Message);
                return false;
            }
        }

        public static GameState Load(string slot)
        {
            try
            {
                if (!Exists(slot)) return null;
                var state = Codec.FromJson<GameState>(File.ReadAllText(PathFor(slot)));
                if (state == null || state.Players == null || state.Players.Count == 0) return null;
                return state;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Stakeholders: load failed: " + ex.Message);
                return null;
            }
        }

        public static string Describe(string slot)
        {
            var s = Load(slot);
            if (s == null) return null;
            return $"{s.Company.Name} · {Fmt.Date(s.Day)} · valuation {Fmt.Money(s.Company.Valuation)}";
        }
    }
}
