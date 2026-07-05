using BepInEx.IL2CPP.Utils;
using HarmonyLib;
using SteamworksNative;
using System.Collections;
using System.IO;
using System.Reflection;
using UnhollowerRuntimeLib;
using UnityEngine;
using UnityEngine.UI;

namespace FoxyJumpscare
{
    internal static class JumpscareManager
    {
        internal static AssetBundle Bundle;
        internal static GameObject Prefab;
        internal static AudioClip Clip;

        internal static Deobf_DeleteOnScrollingGroundContact PrefabStore;
        internal static AudioSource ClipStore;
        

        internal static void Init()
        {
            Harmony harmony = new($"{MyPluginInfo.PLUGIN_NAME}.Manager");
            harmony.PatchAll(typeof(Patches));
        }


        internal static void Jumpscare()
        {
            PrefabStore.StartCoroutine(CoroJumpscare());
        }

        private static IEnumerator CoroJumpscare()
        {
            float start = Time.time;

            GameObject go = Object.Instantiate(Prefab);
            Object.DontDestroyOnLoad(go);
            LocalSfx.Instance.source.PlayOneShot(Clip, 1f);

            RawImage img = go.GetComponentInChildren<RawImage>();
            float frameHeight = 1f / 14f;
            while (true)
            {
                int currentFrame = (int)((Time.time - start) * 24f);
                if (currentFrame >= 14)
                    break;

                img.uvRect = new Rect(x: 0f, y: 1f - (frameHeight * (currentFrame + 1)), width: 1f, height: frameHeight);
                yield return null;
            }

            Object.Destroy(go);
        }


        private static void TryLoadBundle()
        {
            if (Bundle != null)
                return;

            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FoxyJumpscare.Assets.foxy");
            if (stream == null)
            {
                FoxyJumpscare.Instance.Log.LogWarning("The foxy asset bundle is missing!");
                return;
            }

            using var ms = new MemoryStream();
            stream.CopyTo(ms);

            Bundle = AssetBundle.LoadFromMemory(ms.ToArray());
            if (Bundle == null)
            {
                FoxyJumpscare.Instance.Log.LogWarning("Failed to load the foxy asset bundle!");
                return;
            }

            Prefab = new GameObject(Bundle.LoadAsset("Canvas", Il2CppType.Of<GameObject>()).Pointer);
            Clip = new AudioClip(Bundle.LoadAsset("scare", Il2CppType.Of<AudioClip>()).Pointer);


            // Without these assets being referenced anywhere in any Unity scene, Unity sees them as garbage to be cleaned and they get destroyed. The following keeps the assets alive.
            // I would've used my own injected MonoBehaviour, but Unity doesn't see its fields and doesn't count them as references, leading to the assets being destroyed anyways.
            // I use Deobf_DeleteOnScrollingGroundContact.other to store the prefab (since it's an empty MonoBehaviour with just the other GameObject field) and AudioSource.clip for the clip (since it's native to Unity)

            GameObject go = new("JumpscareAssets");
            Object.DontDestroyOnLoad(go);

            PrefabStore = go.AddComponent<Deobf_DeleteOnScrollingGroundContact>();
            PrefabStore.other = Prefab;
            
            ClipStore = go.AddComponent<AudioSource>();
            ClipStore.clip = Clip;
            ClipStore.volume = 0f;
        }

        private static IEnumerator CoroJumpscareLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);

                if (FoxyJumpscare.Instance.Chance.Value <= 0 || Random.Range(0, FoxyJumpscare.Instance.Chance.Value) != 0)
                    continue;
                
                if (SteamManager.Instance.IsLobbyOwner())
                {
                    Jumpscare();
                    if (FoxyJumpscare.Instance.Networked.Value)
                        JumpscareNet.ServerSendJumpscare();

                    continue;
                }

                if (!FoxyJumpscare.Instance.Networked.Value || SteamManager.Instance.currentLobby == CSteamID.Nil || SteamMatchmaking.GetLobbyData(SteamManager.Instance.currentLobby, $"{MyPluginInfo.PLUGIN_GUID}.Networked") != "1")
                    Jumpscare();
            }
        }


        private static class Patches
        {
            [HarmonyPatch(typeof(MainManager), nameof(MainManager.Awake))]
            [HarmonyPostfix]
            internal static void PostMainManagerAwake(MainManager __instance)
            {
                if (__instance != MainManager.Instance)
                    return;

                TryLoadBundle();

                if (Bundle == null)
                    return;

                PrefabStore.StartCoroutine(CoroJumpscareLoop());
            }
        }
    }
}