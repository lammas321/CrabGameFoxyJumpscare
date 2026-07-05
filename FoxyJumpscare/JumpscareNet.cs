using CrabDevKit.Intermediary;
using CrabDevKit.Utilities;
using HarmonyLib;
using SteamworksNative;
using System.Collections.Generic;

namespace FoxyJumpscare
{
    internal static class JumpscareNet
    {
        private const string SERVER_JUMPSCARE = $"{MyPluginInfo.PLUGIN_GUID}:ServerJumpscare";

        private const string CLIENT_NETWORKED = $"{MyPluginInfo.PLUGIN_GUID}:ClientNetworked";

        private static readonly HashSet<CSteamID> NetworkedSteamIds = [];


        internal static void Init()
        {
            FoxyJumpscare.Instance.Networked.SettingChanged += (sender, e) =>
            {
                if (SteamManager.Instance.currentLobby == CSteamID.Nil)
                    return;

                if (SteamManager.Instance.IsLobbyOwner())
                {
                    SteamMatchmaking.SetLobbyData(SteamManager.Instance.currentLobby, $"{MyPluginInfo.PLUGIN_GUID}.Networked", FoxyJumpscare.Instance.Networked.Value ? "1" : "0");
                    return;
                }

                ClientSendNetworked(FoxyJumpscare.Instance.Networked.Value);
            };

            CrabNet.RegisterMessageHandler(SERVER_JUMPSCARE, ServerJumpscare);
            CrabNet.RegisterMessageHandler(CLIENT_NETWORKED, ClientNetworked);

            Harmony harmony = new($"{MyPluginInfo.PLUGIN_NAME}.Net");
            harmony.PatchAll(typeof(Patches));
        }


        private static IEnumerable<CSteamID> GetAllSteamIds()
        {
            int members = SteamMatchmaking.GetNumLobbyMembers(SteamManager.Instance.currentLobby);
            for (int i = 0; i < members; i++)
            {
                CSteamID steamId = SteamMatchmaking.GetLobbyMemberByIndex(SteamManager.Instance.currentLobby, i);
                if (NetworkedSteamIds.Contains(steamId))
                    yield return steamId;
            }
        }


        internal static void ServerSendJumpscare()
        {
            Packet packet = new();

            CrabNet.SendMessage(SERVER_JUMPSCARE, packet, GetAllSteamIds());
            packet.Dispose();
        }

        private static void ServerJumpscare(ulong clientId, Packet packet)
        {
            if (clientId != SteamManager.Instance.get_lobbyOwnerSteamId().m_SteamID)
                return;

            if (!FoxyJumpscare.Instance.Networked.Value)
                return;

            if (JumpscareManager.Bundle == null)
                return;

            JumpscareManager.Jumpscare();
        }


        internal static void ClientSendNetworked(bool networked)
        {
            Packet packet = new();
            packet.Write(networked);
            
            CrabNet.SendMessage(CLIENT_NETWORKED, packet, SteamManager.Instance.get_lobbyOwnerSteamId());
            packet.Dispose();
        }

        private static void ClientNetworked(ulong clientId, Packet packet)
        {
            if (!SteamManager.Instance.IsLobbyOwner())
                return;

            bool networked = packet.ReadBool();
            if (networked)
                NetworkedSteamIds.Add(new(clientId));
            else
                NetworkedSteamIds.Remove(new(clientId));
        }


        private static class Patches
        {
            private static bool _shouldSend = false;
            [HarmonyPatch(typeof(SteamManager), nameof(SteamManager.JoinLobby))]
            [HarmonyPrefix]
            internal static void PreSteamManagerJoinLobby()
            {
                if (SteamManager.Instance.IsLobbyOwner())
                    return;

                _shouldSend = true;
            }

            [HarmonyPatch(typeof(ClientHandle), nameof(ClientHandle.LoadMap))]
            [HarmonyPrefix]
            internal static void PreClientHandleLoadMap()
            {
                if (SteamManager.Instance.IsLobbyOwner() || !_shouldSend)
                    return;

                _shouldSend = false;
                if (FoxyJumpscare.Instance.Networked.Value)
                    ClientSendNetworked(true);
            }


            [HarmonyPatch(typeof(LobbyManager), nameof(LobbyManager.StartLobby))]
            [HarmonyPatch(typeof(LobbyManager), nameof(LobbyManager.StartPracticeLobby))]
            [HarmonyPostfix]
            internal static void PostLobbyManagerStartLobby()
            {
                NetworkedSteamIds.Clear();

                SteamMatchmaking.SetLobbyData(SteamManager.Instance.currentLobby, MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_VERSION);
                SteamMatchmaking.SetLobbyData(SteamManager.Instance.currentLobby, $"{MyPluginInfo.PLUGIN_GUID}.Networked", FoxyJumpscare.Instance.Networked.Value ? "1" : "0");
            }

            [HarmonyPatch(typeof(LobbyManager), nameof(LobbyManager.OnPlayerJoinLeaveUpdate))]
            [HarmonyPostfix]
            internal static void PostLobbyManagerOnPlayerJoinLeaveUpdate(CSteamID param_1, bool param_2)
            {
                if (!SteamManager.Instance.IsLobbyOwner() || param_2)
                    return;

                NetworkedSteamIds.Remove(param_1);
            }

            [HarmonyPatch(typeof(LobbyManager), nameof(LobbyManager.CloseLobby))]
            [HarmonyPostfix]
            internal static void PostLobbyManagerCloseLobby()
            {
                NetworkedSteamIds.Clear();
            }
        }
    }
}