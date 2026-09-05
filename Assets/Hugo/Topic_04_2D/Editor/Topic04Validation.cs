#if UNITY_EDITOR
using System.Collections.Generic;
using NetworkPrototype.Topic04.Combat;
using NetworkPrototype.Topic04.Networking;
using NetworkPrototype.Topic04.Player;
using NetworkPrototype.Topic04.Pooling;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NetworkPrototype.Topic04.Editor
{
    public static class Topic04Validation
    {
        private const string Root = "Assets/Hugo/Topic_04_2D";
        private const string ScenePath = Root + "/Scenes/Topic_04_2D.unity";
        private const string PlayerPath = Root + "/Prefabs/NetworkPlayer2D.prefab";
        private const string ProjectilePath = Root + "/Prefabs/NetworkProjectile2D.prefab";
        private const string ListPath = Root + "/Network/Topic04NetworkPrefabs.asset";
        private const string Topic03ListPath = "Assets/Hugo/Topic_03/Network/DefaultNetworkPrefabs.asset";

        public static void Validate()
        {
            var errors = new List<string>();
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            GameObject projectile = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePath);
            NetworkPrefabsList list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(ListPath);

            Require(player != null, "Player prefab ausente.", errors);
            Require(projectile != null, "Projectile prefab ausente.", errors);
            Require(list != null, "Network Prefabs List ausente.", errors);

            if (player != null)
            {
                Require(player.GetComponent<NetworkObject>() != null, "Player sem NetworkObject.", errors);
                Require(player.GetComponent<NetworkPlayer2DController>() != null, "Player sem controller 2D.", errors);
                NetworkTransform transform = player.GetComponent<NetworkTransform>();
                Require(transform != null && transform.AuthorityMode == NetworkTransform.AuthorityModes.Owner,
                    "Player precisa de NetworkTransform Owner-authoritative.", errors);
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(player) == 0,
                    "Player prefab possui Missing Script.", errors);
            }

            if (projectile != null)
            {
                Require(projectile.GetComponent<NetworkObject>() != null, "Projectile sem NetworkObject.", errors);
                Require(projectile.GetComponent<NetworkProjectile2D>() != null, "Projectile sem comportamento de rede.", errors);
                NetworkTransform transform = projectile.GetComponent<NetworkTransform>();
                Require(transform != null && transform.AuthorityMode == NetworkTransform.AuthorityModes.Server,
                    "Projectile precisa de NetworkTransform server-authoritative.", errors);
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(projectile) == 0,
                    "Projectile prefab possui Missing Script.", errors);
            }

            if (list != null)
            {
                Require(list.Contains(player), "Lista Topic04 nao registra o Player.", errors);
                Require(list.Contains(projectile), "Lista Topic04 nao registra o Projectile.", errors);
                Require(list.PrefabList.Count == 2, "Lista Topic04 deve conter somente Player e Projectile.", errors);
            }

            NetworkPrefabsList topic03List = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(Topic03ListPath);
            if (topic03List != null)
            {
                Require(!topic03List.Contains(player) && !topic03List.Contains(projectile),
                    "Prefabs Topic04 vazaram para a lista do Topic03.", errors);
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            NetworkManager manager = Object.FindFirstObjectByType<NetworkManager>();
            Require(manager != null, "Cena sem NetworkManager.", errors);
            Require(Object.FindFirstObjectByType<PoolManager>() != null, "Cena sem PoolManager.", errors);
            Require(Object.FindFirstObjectByType<Topic04MatchManager>() != null, "Cena sem MatchManager.", errors);
            Require(Object.FindFirstObjectByType<Topic04NetworkUI>() != null, "Cena sem Network UI.", errors);
            Require(Object.FindFirstObjectByType<Topic04ConnectionApproval>() != null,
                "Cena sem limite/aprovacao de conexoes.", errors);

            if (manager != null)
            {
                Require(manager.NetworkConfig.PlayerPrefab == player, "Player Prefab incorreto no NetworkManager.", errors);
                Require(manager.NetworkConfig.ConnectionApproval, "Connection Approval precisa estar habilitado.", errors);
                Require(manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Count == 1 &&
                        manager.NetworkConfig.Prefabs.NetworkPrefabsLists[0] == list,
                    "NetworkManager nao usa exclusivamente a lista Topic04.", errors);
            }

            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root) == 0,
                    $"Missing Script no objeto de cena '{root.name}'.", errors);
            }

            if (errors.Count > 0)
            {
                throw new System.InvalidOperationException("Validacao Topic04 falhou:\n- " + string.Join("\n- ", errors));
            }

            Debug.Log("[Topic04Validation] Cena, prefabs, ownership e listas validados sem erros.");
        }

        private static void Require(bool condition, string error, ICollection<string> errors)
        {
            if (!condition)
            {
                errors.Add(error);
            }
        }
    }
}
#endif
