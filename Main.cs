using ModLoader;
using ModLoader.Helpers;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace AMPartSearch
{

    [HarmonyPatch(typeof(SFS.Input.KeysNode), "ProcessInput")]
    internal static class BlockHotkeysWhileTyping
    {
        static bool Prefix() => !PartSearch.IsTyping;
    }

    public class Main : Mod
    {
        public static Main main;
        public Main() => main = this;

        public override string ModNameID => "AMPartSearch";
        public override string DisplayName => "Part Search";
        public override string Author => "A.M.";
        public override string MinimumGameVersionNecessary => "1.5.10";
        public override string ModVersion => "1.3.1";
        public override string Description => "Search all parts - with icons, scrolling and drag & drop";

        public override Dictionary<string, string> Dependencies => new Dictionary<string, string>
        {
            { "UITools", "1.1.5" }
        };

        public GameObject main_;

        public override void Early_Load()
        {
            Debug.Log("[AMPartSearch] Early_Load");
        }

        public override void Load()
        {
            new Harmony("AMPartSearch").PatchAll();
            SceneHelper.OnBuildSceneLoaded += OnBuildLoaded;
            SceneHelper.OnBuildSceneUnloaded += OnBuildUnloaded;
            Debug.Log("[AMPartSearch] Load ok, build-scene hooks armed");
        }

        private void OnBuildLoaded()
        {

            var holder = new GameObject("AMPartSearch");
            UnityEngine.Object.DontDestroyOnLoad(holder);
            holder.AddComponent<PartSearch>();
            Debug.Log("[AMPartSearch] build scene loaded - PartSearch attached");
        }

        private void OnBuildUnloaded()
        {

            Debug.Log("[AMPartSearch] build scene unloaded");
        }
    }
}
