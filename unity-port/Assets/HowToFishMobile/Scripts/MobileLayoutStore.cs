using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HowToFish.Mobile
{
    [Serializable]
    public sealed class MobileControlPlacement
    {
        public string id;
        public Vector2 normalizedPosition;
        public float scale = 1f;

        public MobileControlPlacement(string id, Vector2 normalizedPosition, float scale = 1f)
        {
            this.id = id;
            this.normalizedPosition = normalizedPosition;
            this.scale = scale;
        }
    }

    [Serializable]
    public sealed class MobileControlLayoutData
    {
        public int version = 1;
        public List<MobileControlPlacement> controls = new List<MobileControlPlacement>();
    }

    public static class MobileLayoutStore
    {
        private const string FileName = "mobile-controls.json";
        private static MobileControlLayoutData _data;

        public static MobileControlLayoutData Data => _data ??= Load();
        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public static MobileControlPlacement Get(string id)
        {
            var data = Data;
            for (int i = 0; i < data.controls.Count; i++)
                if (string.Equals(data.controls[i].id, id, StringComparison.Ordinal))
                    return data.controls[i];

            var placement = GetDefault(id);
            data.controls.Add(placement);
            return placement;
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(SavePath, JsonUtility.ToJson(Data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HowToFish.Mobile] Could not save layout: {e.Message}");
            }
        }

        public static void ResetToDefaults()
        {
            _data = CreateDefaults();
            Save();
        }

        private static MobileControlLayoutData Load()
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    var parsed = JsonUtility.FromJson<MobileControlLayoutData>(File.ReadAllText(SavePath));
                    if (parsed != null && parsed.controls != null)
                        return parsed;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HowToFish.Mobile] Could not read layout: {e.Message}");
            }
            return CreateDefaults();
        }

        private static MobileControlLayoutData CreateDefaults()
        {
            var data = new MobileControlLayoutData();
            string[] ids =
            {
                "move", "look", "primary", "secondary", "interact", "drop", "jump",
                "sprint", "crouch", "inspect", "reload", "bait", "journal", "ptt",
                "pause", "skin-prev", "skin-next"
            };
            foreach (var id in ids)
                data.controls.Add(GetDefault(id));
            return data;
        }

        private static MobileControlPlacement GetDefault(string id)
        {
            return id switch
            {
                "move"       => new MobileControlPlacement(id, new Vector2(0.115f, 0.225f), 1.00f),
                "look"       => new MobileControlPlacement(id, new Vector2(0.690f, 0.500f), 1.00f),
                "primary"    => new MobileControlPlacement(id, new Vector2(0.925f, 0.190f), 1.15f),
                "secondary"  => new MobileControlPlacement(id, new Vector2(0.825f, 0.295f), 1.00f),
                "interact"   => new MobileControlPlacement(id, new Vector2(0.915f, 0.470f), 1.00f),
                "drop"       => new MobileControlPlacement(id, new Vector2(0.815f, 0.555f), 1.00f),
                "jump"       => new MobileControlPlacement(id, new Vector2(0.720f, 0.175f), 1.00f),
                "sprint"     => new MobileControlPlacement(id, new Vector2(0.220f, 0.355f), 1.00f),
                "crouch"     => new MobileControlPlacement(id, new Vector2(0.220f, 0.195f), 1.00f),
                "inspect"    => new MobileControlPlacement(id, new Vector2(0.625f, 0.895f), 0.82f),
                "reload"     => new MobileControlPlacement(id, new Vector2(0.690f, 0.895f), 0.82f),
                "bait"       => new MobileControlPlacement(id, new Vector2(0.755f, 0.895f), 0.82f),
                "journal"    => new MobileControlPlacement(id, new Vector2(0.820f, 0.895f), 0.82f),
                "ptt"        => new MobileControlPlacement(id, new Vector2(0.885f, 0.895f), 0.82f),
                "pause"      => new MobileControlPlacement(id, new Vector2(0.950f, 0.895f), 0.82f),
                "skin-prev"  => new MobileControlPlacement(id, new Vector2(0.050f, 0.895f), 0.72f),
                "skin-next"  => new MobileControlPlacement(id, new Vector2(0.105f, 0.895f), 0.72f),
                _             => new MobileControlPlacement(id, new Vector2(0.5f, 0.5f), 1f)
            };
        }
    }
}
