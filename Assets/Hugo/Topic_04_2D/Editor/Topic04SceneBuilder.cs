#if UNITY_EDITOR
using System.Collections.Generic;
using NetworkPrototype.Topic04.Combat;
using NetworkPrototype.Topic04.Networking;
using NetworkPrototype.Topic04.Player;
using NetworkPrototype.Topic04.Pooling;
using NetworkPrototype.Topic04.UI;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NetworkPrototype.Topic04.Editor
{
    public static class Topic04SceneBuilder
    {
        private const string Root = "Assets/Hugo/Topic_04_2D";
        private const string InputPath = Root + "/Input/Topic04InputActions.inputactions";
        private const string PlayerPrefabPath = Root + "/Prefabs/NetworkPlayer2D.prefab";
        private const string ProjectilePrefabPath = Root + "/Prefabs/NetworkProjectile2D.prefab";
        private const string PrefabListPath = Root + "/Network/Topic04NetworkPrefabs.asset";
        private const string ScenePath = Root + "/Scenes/Topic_04_2D.unity";
        private const string Topic03ScenePath = "Assets/Hugo/Topic_03/Scenes/Topic_03.unity";
        private const string Topic03PrefabListPath = "Assets/Hugo/Topic_03/Network/DefaultNetworkPrefabs.asset";

        [MenuItem("Tools/Network Prototype/Build Topic 04 2D")]
        public static void Build()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            if (inputActions == null)
            {
                throw new System.InvalidOperationException($"Input Actions nao encontrado em {InputPath}.");
            }

            GameObject playerPrefab = CreatePlayerPrefab(inputActions);
            GameObject projectilePrefab = CreateProjectilePrefab();
            NetworkPrefabsList prefabList = CreateNetworkPrefabsList(playerPrefab, projectilePrefab);
            RemoveTopic04PrefabsFromTopic03List(playerPrefab, projectilePrefab);
            CreateScene(playerPrefab, projectilePrefab, prefabList);
            UpdateBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Topic04Validation.Validate();
            Debug.Log("[Topic04SceneBuilder] Topic_04_2D criado e configurado com sucesso.");
        }

        private static GameObject CreatePlayerPrefab(InputActionAsset inputActions)
        {
            var root = new GameObject("NetworkPlayer2D");
            root.AddComponent<NetworkObject>();

            NetworkTransform networkTransform = root.AddComponent<NetworkTransform>();
            ConfigureNetworkTransform(networkTransform, NetworkTransform.AuthorityModes.Owner);

            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 2.5f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.9f, 1.4f);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = new Vector3(0.9f, 1.4f, 1f);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = GetBuiltinSprite();
            renderer.color = Color.white;
            renderer.sortingOrder = 10;

            PlayerInput playerInput = root.AddComponent<PlayerInput>();
            playerInput.actions = inputActions;
            playerInput.defaultActionMap = "Player";
            playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;

            NetworkPlayer2DController controller = root.AddComponent<NetworkPlayer2DController>();
            SetObjectReference(controller, "playerInput", playerInput);
            SetObjectReference(controller, "body", body);
            SetObjectReference(controller, "bodyCollider", collider);
            SetObjectReference(controller, "playerRenderer", renderer);

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return savedPrefab;
        }

        private static GameObject CreateProjectilePrefab()
        {
            var root = new GameObject("NetworkProjectile2D");
            root.AddComponent<NetworkObject>();

            NetworkTransform networkTransform = root.AddComponent<NetworkTransform>();
            ConfigureNetworkTransform(networkTransform, NetworkTransform.AuthorityModes.Server);

            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            collider.radius = 0.18f;
            collider.isTrigger = true;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * 0.35f;
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = GetBuiltinSprite();
            renderer.color = new Color(1f, 0.82f, 0.1f);
            renderer.sortingOrder = 20;

            NetworkProjectile2D projectile = root.AddComponent<NetworkProjectile2D>();
            SetObjectReference(projectile, "body", body);
            SetObjectReference(projectile, "projectileCollider", collider);

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, ProjectilePrefabPath);
            Object.DestroyImmediate(root);
            return savedPrefab;
        }

        private static NetworkPrefabsList CreateNetworkPrefabsList(
            GameObject playerPrefab,
            GameObject projectilePrefab)
        {
            NetworkPrefabsList list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(PrefabListPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(list, PrefabListPath);
            }
            else
            {
                var existingEntries = new List<NetworkPrefab>(list.PrefabList);
                foreach (NetworkPrefab entry in existingEntries)
                {
                    list.Remove(entry);
                }
            }

            list.Add(new NetworkPrefab
            {
                Override = NetworkPrefabOverride.None,
                Prefab = playerPrefab
            });
            list.Add(new NetworkPrefab
            {
                Override = NetworkPrefabOverride.None,
                Prefab = projectilePrefab
            });
            EditorUtility.SetDirty(list);
            return list;
        }

        private static void RemoveTopic04PrefabsFromTopic03List(
            GameObject playerPrefab,
            GameObject projectilePrefab)
        {
            NetworkPrefabsList topic03List =
                AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(Topic03PrefabListPath);
            if (topic03List == null)
            {
                return;
            }

            var entriesToRemove = new List<NetworkPrefab>();
            foreach (NetworkPrefab entry in topic03List.PrefabList)
            {
                if (entry.Prefab == playerPrefab || entry.Prefab == projectilePrefab)
                {
                    entriesToRemove.Add(entry);
                }
            }

            foreach (NetworkPrefab entry in entriesToRemove)
            {
                topic03List.Remove(entry);
            }

            if (entriesToRemove.Count > 0)
            {
                EditorUtility.SetDirty(topic03List);
            }
        }

        private static void CreateScene(
            GameObject playerPrefab,
            GameObject projectilePrefab,
            NetworkPrefabsList prefabList)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();

            var arenaRoot = new GameObject("Arena");
            CreateSurface(arenaRoot.transform, "Floor", new Vector2(0f, -4.5f), new Vector2(18f, 1f));
            CreateSurface(arenaRoot.transform, "LeftWall", new Vector2(-9.5f, 0f), new Vector2(1f, 10f));
            CreateSurface(arenaRoot.transform, "RightWall", new Vector2(9.5f, 0f), new Vector2(1f, 10f));
            CreateSurface(arenaRoot.transform, "PlatformLeft", new Vector2(-4.2f, -0.8f), new Vector2(4f, 0.45f));
            CreateSurface(arenaRoot.transform, "PlatformRight", new Vector2(4.2f, 1.1f), new Vector2(4f, 0.45f));

            var systems = new GameObject("Topic04Systems");
            PoolManager poolManager = systems.AddComponent<PoolManager>();
            ConfigurePool(poolManager, projectilePrefab);

            Topic04MatchManager matchManager = systems.AddComponent<Topic04MatchManager>();
            systems.AddComponent<Topic04ConnectionApproval>();
            Transform[] spawnPoints = CreateSpawnPoints(systems.transform);
            SetObjectReference(matchManager, "poolManager", poolManager);
            SetObjectReferenceArray(matchManager, "spawnPoints", spawnPoints);

            CreateNetworkManager(playerPrefab, prefabList);
            CreateUserInterface();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.075f, 0.12f);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.AddComponent<AudioListener>();
        }

        private static void CreateSurface(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size)
        {
            var surface = new GameObject(name);
            surface.transform.SetParent(parent, false);
            surface.transform.position = position;
            surface.transform.localScale = new Vector3(size.x, size.y, 1f);

            SpriteRenderer renderer = surface.AddComponent<SpriteRenderer>();
            renderer.sprite = GetBuiltinSprite();
            renderer.color = new Color(0.20f, 0.27f, 0.38f);
            renderer.sortingOrder = 0;

            BoxCollider2D collider = surface.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            surface.AddComponent<Topic04ArenaSurface>();
        }

        private static Transform[] CreateSpawnPoints(Transform parent)
        {
            Vector2[] positions =
            {
                new(-6.5f, -3.25f),
                new(-2.2f, -3.25f),
                new(2.2f, -3.25f),
                new(6.5f, -3.25f)
            };

            var points = new Transform[positions.Length];
            var root = new GameObject("SpawnPoints");
            root.transform.SetParent(parent, false);

            for (int i = 0; i < positions.Length; i++)
            {
                var point = new GameObject($"SpawnPoint_{i + 1}");
                point.transform.SetParent(root.transform, false);
                point.transform.position = positions[i];
                points[i] = point.transform;
            }

            return points;
        }

        private static void CreateNetworkManager(GameObject playerPrefab, NetworkPrefabsList prefabList)
        {
            var managerObject = new GameObject("NetworkManager");
            NetworkManager manager = managerObject.AddComponent<NetworkManager>();
            UnityTransport transport = managerObject.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", 7777, "0.0.0.0");

            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                PlayerPrefab = playerPrefab,
                TickRate = 60,
                ConnectionApproval = true,
                EnableSceneManagement = true,
                ForceSamePrefabs = true,
                RecycleNetworkIds = true,
                AutoSpawnPlayerPrefabClientSide = true
            };
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabList);
        }

        private static void CreateUserInterface()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasObject = new GameObject("Canvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject connectionMenu = CreatePanel(
                canvasObject.transform,
                "ConnectionMenu",
                new Vector2(520f, 500f),
                new Color(0.06f, 0.08f, 0.13f, 0.96f));
            CreateText(connectionMenu.transform, "Title", "TEMA 4 - COMBATE 2D", font, 32,
                new Vector2(0f, 175f), new Vector2(460f, 55f), TextAnchor.MiddleCenter);
            CreateText(connectionMenu.transform, "Subtitle", "Netcode for GameObjects", font, 21,
                new Vector2(0f, 128f), new Vector2(460f, 40f), TextAnchor.MiddleCenter);
            Button hostButton = CreateButton(connectionMenu.transform, "HostButton", "HOST", font, new Vector2(0f, 60f));
            Button clientButton = CreateButton(connectionMenu.transform, "ClientButton", "CLIENT", font, new Vector2(0f, -5f));
            Button serverButton = CreateButton(connectionMenu.transform, "ServerButton", "SERVER", font, new Vector2(0f, -70f));
            Text statusText = CreateText(connectionMenu.transform, "StatusText", "", font, 18,
                new Vector2(0f, -150f), new Vector2(460f, 65f), TextAnchor.MiddleCenter);

            GameObject pauseMenu = CreatePanel(
                canvasObject.transform,
                "PauseMenu",
                new Vector2(480f, 420f),
                new Color(0.06f, 0.08f, 0.13f, 0.96f));
            CreateText(pauseMenu.transform, "Title", "PAUSE LOCAL", font, 34,
                new Vector2(0f, 135f), new Vector2(420f, 60f), TextAnchor.MiddleCenter);
            Button resumeButton = CreateButton(pauseMenu.transform, "ResumeButton", "VOLTAR AO JOGO", font, new Vector2(0f, 55f));
            Button disconnectButton = CreateButton(pauseMenu.transform, "DisconnectButton", "DESCONECTAR", font, new Vector2(0f, -15f));
            Button quitButton = CreateButton(pauseMenu.transform, "QuitButton", "SAIR", font, new Vector2(0f, -85f));
            pauseMenu.SetActive(false);

            GameObject gameplayHud = new GameObject("GameplayHUD", typeof(RectTransform));
            gameplayHud.transform.SetParent(canvasObject.transform, false);
            RectTransform hudRect = gameplayHud.GetComponent<RectTransform>();
            hudRect.anchorMin = Vector2.zero;
            hudRect.anchorMax = Vector2.one;
            hudRect.offsetMin = Vector2.zero;
            hudRect.offsetMax = Vector2.zero;

            Text scoreText = CreateText(gameplayHud.transform, "ScoreText", "PLACAR", font, 24,
                new Vector2(25f, -25f), new Vector2(360f, 240f), TextAnchor.UpperLeft);
            RectTransform scoreRect = scoreText.rectTransform;
            scoreRect.anchorMin = new Vector2(0f, 1f);
            scoreRect.anchorMax = new Vector2(0f, 1f);
            scoreRect.pivot = new Vector2(0f, 1f);

            Text helpText = CreateText(
                gameplayHud.transform,
                "HelpText",
                "A/D ou setas: mover   ESPACO: pular   F/clique: atirar   ESC: pause",
                font,
                18,
                new Vector2(0f, 24f),
                new Vector2(1000f, 42f),
                TextAnchor.MiddleCenter);
            RectTransform helpRect = helpText.rectTransform;
            helpRect.anchorMin = new Vector2(0.5f, 0f);
            helpRect.anchorMax = new Vector2(0.5f, 0f);
            gameplayHud.SetActive(false);

            Topic04ScoreboardUI scoreboard = gameplayHud.AddComponent<Topic04ScoreboardUI>();
            SetObjectReference(scoreboard, "scoreText", scoreText);

            Topic04NetworkUI networkUi = canvasObject.AddComponent<Topic04NetworkUI>();
            SetObjectReference(networkUi, "connectionMenu", connectionMenu);
            SetObjectReference(networkUi, "pauseMenu", pauseMenu);
            SetObjectReference(networkUi, "gameplayHud", gameplayHud);
            SetObjectReference(networkUi, "hostButton", hostButton);
            SetObjectReference(networkUi, "clientButton", clientButton);
            SetObjectReference(networkUi, "serverButton", serverButton);
            SetObjectReference(networkUi, "statusText", statusText);
            SetObjectReference(networkUi, "resumeButton", resumeButton);
            SetObjectReference(networkUi, "disconnectButton", disconnectButton);
            SetObjectReference(networkUi, "quitButton", quitButton);

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        private static GameObject CreatePanel(
            Transform parent,
            string name,
            Vector2 size,
            Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            string content,
            Font font,
            int fontSize,
            Vector2 anchoredPosition,
            Vector2 size,
            TextAnchor alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Text text = textObject.GetComponent<Text>();
            text.text = content;
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Font font,
            Vector2 anchoredPosition)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(330f, 52f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.15f, 0.42f, 0.72f);
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            Text text = CreateText(buttonObject.transform, "Label", label, font, 21, Vector2.zero, rect.sizeDelta, TextAnchor.MiddleCenter);
            text.raycastTarget = false;
            return button;
        }

        private static void ConfigurePool(PoolManager manager, GameObject projectilePrefab)
        {
            var serialized = new SerializedObject(manager);
            SerializedProperty configs = serialized.FindProperty("poolConfigs");
            configs.arraySize = 1;
            SerializedProperty config = configs.GetArrayElementAtIndex(0);
            config.FindPropertyRelative("poolKey").stringValue = "Projectile2D";
            config.FindPropertyRelative("prefab").objectReferenceValue = projectilePrefab;
            config.FindPropertyRelative("initialSize").intValue = 12;
            config.FindPropertyRelative("canExpand").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureNetworkTransform(
            NetworkTransform networkTransform,
            NetworkTransform.AuthorityModes authority)
        {
            networkTransform.AuthorityMode = authority;
            networkTransform.SyncPositionX = true;
            networkTransform.SyncPositionY = true;
            networkTransform.SyncPositionZ = false;
            networkTransform.SyncRotAngleX = false;
            networkTransform.SyncRotAngleY = false;
            networkTransform.SyncRotAngleZ = false;
            networkTransform.SyncScaleX = false;
            networkTransform.SyncScaleY = false;
            networkTransform.SyncScaleZ = false;
            networkTransform.Interpolate = true;
            networkTransform.UseUnreliableDeltas = true;
            networkTransform.PositionThreshold = 0.001f;
        }

        private static Sprite GetBuiltinSprite()
        {
            Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            if (sprite == null)
            {
                throw new System.InvalidOperationException("Sprite primitivo interno do Unity nao encontrado.");
            }

            return sprite;
        }

        private static void SetObjectReference(Object target, string propertyName, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new System.InvalidOperationException($"Campo serializado '{propertyName}' nao encontrado em {target.GetType().Name}.");
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectReferenceArray(Object target, string propertyName, IReadOnlyList<Transform> values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void UpdateBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new(ScenePath, true)
            };

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Topic03ScenePath) != null)
            {
                scenes.Add(new EditorBuildSettingsScene(Topic03ScenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }


    }
}
#endif
