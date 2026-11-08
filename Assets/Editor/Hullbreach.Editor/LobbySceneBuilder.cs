using System.IO;
using System.Linq;
using Hullbreach.NetCode.Entities;
using Hullbreach.Platform;
using Hullbreach.Game;
using TMPro;
using Unity.NetCode;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hullbreach.Editor
{
    /// <summary>Creates the editable uGUI lobby prefab and the two online-flow scenes.</summary>
    public static class LobbySceneBuilder
    {
        public const string LobbyPrefabPath = "Assets/Prefabs/UI/LobbyCanvas.prefab";
        public const string SignInPrefabPath = "Assets/Prefabs/UI/SignInCanvas.prefab";
        public const string TitlePrefabPath = "Assets/Prefabs/UI/TitleCanvas.prefab";
        public const string SettingsPrefabPath = "Assets/Prefabs/UI/SettingsCanvas.prefab";
        public const string QueuePrefabPath = "Assets/Prefabs/UI/QueueCanvas.prefab";
        public const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
        public const string LobbyScenePath = "Assets/Scenes/LobbyScene.unity";
        public const string GameScenePath = "Assets/Scenes/MultiplayerGame.unity";
        public const string GhostPrefabPath = "Assets/Prefabs/NetCode/HullbreachGameplayGhost.prefab";
        public const string GhostSubScenePath = "Assets/Scenes/MultiplayerGhosts.unity";

        [MenuItem("Hullbreach/Online/Rebuild lobby and multiplayer scenes")]
        public static void RebuildMenu() => Build(force: true);

        // Adds the sign-in screen to an existing lobby scene without resetting the lobby
        // prefab or the multiplayer scenes, so restyled layouts survive.
        // frob:doc docs/platform-link.md#sign-in-screen
        [MenuItem("Hullbreach/Online/Add sign-in screen to lobby scene")]
        public static void AddSignInMenu()
        {
            Directory.CreateDirectory("Assets/Prefabs/UI");
            if (!File.Exists(SignInPrefabPath)) BuildSignInPrefab();
            Scene scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
            if (Object.FindFirstObjectByType<HullbreachSignInView>() == null)
                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SignInPrefabPath));
            EditorSceneManager.SaveScene(scene, LobbyScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("LobbySceneBuilder: lobby scene has the sign-in screen.");
        }

        public static void Build(bool force)
        {
            Directory.CreateDirectory("Assets/Prefabs/UI");
            Directory.CreateDirectory("Assets/Prefabs/NetCode");
            Directory.CreateDirectory("Assets/Scenes");

            if (force || !File.Exists(LobbyPrefabPath)) BuildLobbyPrefab();
            if (force || !File.Exists(SignInPrefabPath)) BuildSignInPrefab();
            if (force || !File.Exists(SettingsPrefabPath)) BuildSettingsPrefab();
            if (force || !File.Exists(TitlePrefabPath)) BuildTitlePrefab();
            if (force || !File.Exists(QueuePrefabPath)) BuildQueuePrefab();
            if (force || !File.Exists(LobbyScenePath)) BuildLobbyScene();
            if (force || !File.Exists(TitleScenePath)) BuildTitleScene();
            if (force || !File.Exists(GhostPrefabPath)) BuildGhostPrefab();
            if (force || !File.Exists(GhostSubScenePath)) BuildGhostSubScene();
            if (force || !File.Exists(GameScenePath)) BuildGameScene();
            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("LobbySceneBuilder: online lobby prefab and scenes are ready.");
        }

        static void BuildLobbyPrefab()
        {
            var canvasRoot = new GameObject("LobbyCanvas", typeof(RectTransform));
            Canvas canvas = canvasRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasRoot.AddComponent<GraphicRaycaster>();

            Image background = AddImage(canvasRoot.transform, "Background", BrandColors.Background);
            Stretch(background.rectTransform);

            GameObject panel = AddPanel(background.transform, "MainPanel", new Vector2(1180f, 760f),
                BrandColors.Panel);
            VerticalLayoutGroup mainLayout = panel.AddComponent<VerticalLayoutGroup>();
            mainLayout.padding = new RectOffset(34, 34, 26, 26);
            mainLayout.spacing = 12f;
            mainLayout.childControlHeight = true;
            mainLayout.childControlWidth = true;
            mainLayout.childForceExpandHeight = false;
            mainLayout.childForceExpandWidth = true;

            TMP_Text title = AddText(panel.transform, "Title", "HULLBREACH ONLINE", 34f, TextAlignmentOptions.Center);
            title.fontStyle = FontStyles.Bold;
            TMP_Text status = AddText(panel.transform, "StatusText", "Connecting to Unity Services...", 18f,
                TextAlignmentOptions.Center);
            TMP_Text error = AddText(panel.transform, "ErrorText", string.Empty, 17f, TextAlignmentOptions.Center);
            error.color = BrandColors.Orange;
            error.gameObject.SetActive(false);

            GameObject browser = AddUiObject(panel.transform, "BrowserRoot");
            browser.AddComponent<LayoutElement>().flexibleHeight = 1f;
            HorizontalLayoutGroup browserLayout = browser.AddComponent<HorizontalLayoutGroup>();
            browserLayout.spacing = 24f;
            browserLayout.childControlHeight = true;
            browserLayout.childControlWidth = true;
            browserLayout.childForceExpandHeight = true;
            browserLayout.childForceExpandWidth = true;

            GameObject actions = AddPanel(browser.transform, "HostAndJoin", Vector2.zero, BrandColors.Raised);
            VerticalLayoutGroup actionLayout = actions.AddComponent<VerticalLayoutGroup>();
            actionLayout.padding = new RectOffset(22, 22, 20, 20);
            actionLayout.spacing = 8f;
            actionLayout.childControlHeight = true;
            actionLayout.childControlWidth = true;
            actionLayout.childForceExpandHeight = false;
            actionLayout.childForceExpandWidth = true;

            AddSectionTitle(actions.transform, "HOST A LOBBY");
            AddText(actions.transform, "LobbyNameLabel", "Lobby name", 16f);
            TMP_InputField lobbyName = AddInput(actions.transform, "LobbyNameInput", "Hullbreach Lobby", false);
            AddText(actions.transform, "MaxPlayersLabel", "Maximum players", 16f);
            TMP_InputField maxPlayers = AddInput(actions.transform, "MaxPlayersInput", "8", false);
            maxPlayers.contentType = TMP_InputField.ContentType.IntegerNumber;
            Toggle privateToggle = AddToggle(actions.transform, "PrivateToggle", "Private (join code only)");
            AddText(actions.transform, "HostPasswordLabel", "Password (optional, 8-64 characters)", 16f);
            TMP_InputField hostPassword = AddInput(actions.transform, "HostPasswordInput", string.Empty, true);
            Button hostButton = AddButton(actions.transform, "HostButton", "HOST WITH RELAY", 46f, primary: true);

            AddSpacer(actions.transform, 10f);
            AddSectionTitle(actions.transform, "JOIN BY CODE");
            TMP_InputField joinCode = AddInput(actions.transform, "JoinCodeInput", "Lobby code", false);
            TMP_InputField joinPassword = AddInput(actions.transform, "JoinPasswordInput", "Password if required", true);
            Button joinButton = AddButton(actions.transform, "JoinCodeButton", "JOIN CODE", 44f);

            // LAN play needs no Unity Services; see docs/netcode.md#lan-host-and-join-from-the-lobby.
            AddSpacer(actions.transform, 10f);
            AddSectionTitle(actions.transform, "PLAY ON LAN");
            Button lanHostButton = AddButton(actions.transform, "LanHostButton", "HOST LAN", 44f);
            TMP_InputField lanAddress = AddInput(actions.transform, "LanAddressInput", "Host IP, e.g. 192.168.1.23", false);
            Button lanJoinButton = AddButton(actions.transform, "LanJoinButton", "JOIN ADDRESS", 44f);

            GameObject publicPanel = AddPanel(browser.transform, "PublicLobbies", Vector2.zero, BrandColors.Raised);
            VerticalLayoutGroup publicLayout = publicPanel.AddComponent<VerticalLayoutGroup>();
            publicLayout.padding = new RectOffset(22, 22, 20, 20);
            publicLayout.spacing = 10f;
            publicLayout.childControlHeight = true;
            publicLayout.childControlWidth = true;
            publicLayout.childForceExpandHeight = false;
            publicLayout.childForceExpandWidth = true;
            AddSectionTitle(publicPanel.transform, "PUBLIC LOBBIES");
            Button refreshButton = AddButton(publicPanel.transform, "RefreshButton", "REFRESH", 40f);
            Transform listContent = AddScrollList(publicPanel.transform, out GameObject rowTemplate);

            GameObject room = AddPanel(panel.transform, "RoomRoot", Vector2.zero, BrandColors.Raised);
            room.AddComponent<LayoutElement>().flexibleHeight = 1f;
            VerticalLayoutGroup roomLayout = room.AddComponent<VerticalLayoutGroup>();
            roomLayout.padding = new RectOffset(40, 40, 30, 30);
            roomLayout.spacing = 16f;
            roomLayout.childControlHeight = true;
            roomLayout.childControlWidth = true;
            roomLayout.childForceExpandHeight = false;
            roomLayout.childForceExpandWidth = true;
            TMP_Text roomTitle = AddText(room.transform, "RoomTitleText", "Lobby", 30f, TextAlignmentOptions.Center);
            TMP_Text roomDetails = AddText(room.transform, "RoomDetailsText", string.Empty, 18f, TextAlignmentOptions.Center);
            AddText(room.transform, "JoinCodeLabel", "INVITE CODE", 16f, TextAlignmentOptions.Center);
            TMP_Text roomCode = AddText(room.transform, "JoinCodeText", "------", 38f, TextAlignmentOptions.Center);
            roomCode.fontStyle = FontStyles.Bold;
            AddText(room.transform, "PlayersLabel", "PLAYERS", 16f, TextAlignmentOptions.Center);
            TMP_Text players = AddText(room.transform, "PlayersText", string.Empty, 20f, TextAlignmentOptions.Center);
            players.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            Button start = AddButton(room.transform, "StartGameButton", "START GAME", 52f, primary: true);
            Button leave = AddButton(room.transform, "LeaveButton", "LEAVE LOBBY", 44f);
            room.SetActive(false);
            Button quit = AddButton(panel.transform, "QuitButton", "QUIT GAME", 36f);

            HullbreachLobbyView view = canvasRoot.AddComponent<HullbreachLobbyView>();
            SerializedObject serialized = new SerializedObject(view);
            Set(serialized, "browserRoot", browser);
            Set(serialized, "roomRoot", room);
            Set(serialized, "statusText", status);
            Set(serialized, "errorText", error);
            Set(serialized, "lobbyNameInput", lobbyName);
            Set(serialized, "maxPlayersInput", maxPlayers);
            Set(serialized, "privateToggle", privateToggle);
            Set(serialized, "hostPasswordInput", hostPassword);
            Set(serialized, "hostButton", hostButton);
            Set(serialized, "joinCodeInput", joinCode);
            Set(serialized, "joinPasswordInput", joinPassword);
            Set(serialized, "joinCodeButton", joinButton);
            Set(serialized, "lanAddressInput", lanAddress);
            Set(serialized, "lanHostButton", lanHostButton);
            Set(serialized, "lanJoinButton", lanJoinButton);
            Set(serialized, "refreshButton", refreshButton);
            Set(serialized, "publicLobbyList", listContent);
            Set(serialized, "publicLobbyRowTemplate", rowTemplate);
            Set(serialized, "roomTitleText", roomTitle);
            Set(serialized, "roomDetailsText", roomDetails);
            Set(serialized, "joinCodeText", roomCode);
            Set(serialized, "playersText", players);
            Set(serialized, "startGameButton", start);
            Set(serialized, "leaveButton", leave);
            Set(serialized, "quitButton", quit);
            serialized.FindProperty("gameplaySceneName").stringValue = HullbreachLobbyService.DefaultGameScene;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(canvasRoot, LobbyPrefabPath);
            Object.DestroyImmediate(canvasRoot);
        }

        // A separate overlay canvas above the lobby: the form blocks the lobby until the
        // player signs in or picks offline, then only a small account bar remains.
        static void BuildSignInPrefab()
        {
            var canvasRoot = new GameObject("SignInCanvas", typeof(RectTransform));
            Canvas canvas = canvasRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasRoot.AddComponent<GraphicRaycaster>();

            Image blocker = AddImage(canvasRoot.transform, "SignInRoot", BrandColors.Background);
            Stretch(blocker.rectTransform);
            GameObject panel = AddPanel(blocker.transform, "SignInPanel", new Vector2(620f, 560f),
                BrandColors.Panel);
            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 32, 32);
            layout.spacing = 10f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            TMP_Text title = AddText(panel.transform, "Title", "HULLBREACH", 34f, TextAlignmentOptions.Center);
            title.fontStyle = FontStyles.Bold;
            TMP_Text tagline = AddText(panel.transform, "Tagline", BrandPalette.Tagline, 18f, TextAlignmentOptions.Center);
            tagline.color = BrandColors.Cyan;
            AddText(panel.transform, "Subtitle", "Sign in with your Hullbreach account", 18f, TextAlignmentOptions.Center);
            AddSpacer(panel.transform, 6f);
            AddText(panel.transform, "UsernameLabel", "Username", 16f);
            TMP_InputField username = AddInput(panel.transform, "UsernameInput", "Username", false);
            AddText(panel.transform, "PasswordLabel", "Password", 16f);
            TMP_InputField password = AddInput(panel.transform, "PasswordInput", "Password", true);
            password.characterLimit = 128;
            TMP_Text message = AddText(panel.transform, "MessageText", string.Empty, 16f, TextAlignmentOptions.Center);
            message.textWrappingMode = TextWrappingModes.Normal;
            message.gameObject.GetComponent<LayoutElement>().preferredHeight = 48f;
            Button signIn = AddButton(panel.transform, "SignInButton", "SIGN IN", 48f, primary: true);
            Button offline = AddButton(panel.transform, "PlayOfflineButton", "PLAY OFFLINE", 42f);
    
            GameObject account = AddPanel(canvasRoot.transform, "AccountRoot", new Vector2(520f, 56f),
                BrandColors.Panel);
            RectTransform accountRect = account.GetComponent<RectTransform>();
            accountRect.anchorMin = accountRect.anchorMax = accountRect.pivot = new Vector2(1f, 1f);
            accountRect.anchoredPosition = new Vector2(-18f, -18f);
            HorizontalLayoutGroup accountLayout = account.AddComponent<HorizontalLayoutGroup>();
            accountLayout.padding = new RectOffset(16, 8, 8, 8);
            accountLayout.spacing = 10f;
            accountLayout.childControlHeight = true;
            accountLayout.childControlWidth = true;
            accountLayout.childForceExpandHeight = true;
            accountLayout.childForceExpandWidth = false;
            TMP_Text accountText = AddText(account.transform, "AccountText", string.Empty, 16f);
            accountText.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;
            Button signOut = AddButton(account.transform, "SignOutButton", "SIGN OUT", 40f);
            signOut.gameObject.GetComponent<LayoutElement>().preferredWidth = 130f;
            account.SetActive(false);

            HullbreachSignInView view = canvasRoot.AddComponent<HullbreachSignInView>();
            SerializedObject serialized = new SerializedObject(view);
            Set(serialized, "formRoot", blocker.gameObject);
            Set(serialized, "usernameInput", username);
            Set(serialized, "passwordInput", password);
            Set(serialized, "signInButton", signIn);
            Set(serialized, "playOfflineButton", offline);
            Set(serialized, "messageText", message);
            Set(serialized, "accountRoot", account);
            Set(serialized, "accountText", accountText);
            Set(serialized, "signOutButton", signOut);
            Set(serialized, "signOutLabel", signOut.GetComponentInChildren<TMP_Text>());
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(canvasRoot, SignInPrefabPath);
            Object.DestroyImmediate(canvasRoot);
        }

        static void BuildLobbyScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = HullbreachLobbyService.DefaultLobbyScene;
            CreateCamera(BrandColors.Background);
            CreateEventSystem();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyPrefabPath);
            PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SignInPrefabPath));
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(QueuePrefabPath));
            EditorSceneManager.SaveScene(scene, LobbyScenePath);
        }

        // The title scene is the first build scene: title menu plus the settings panel it opens.
        // frob:ticket 01M3DG5Y1D78FFHNKW4AB7YB9B
        static void BuildTitleScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera(BrandColors.Background);
            CreateEventSystem();
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TitlePrefabPath));
            GameObject settings = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefabPath));
            HullbreachTitleView title = Object.FindFirstObjectByType<HullbreachTitleView>();
            SerializedObject serialized = new SerializedObject(title);
            Set(serialized, "settings", settings.GetComponent<HullbreachSettingsView>());
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, TitleScenePath);
        }

        static Canvas AddOverlayCanvas(string name, int sortingOrder, out GameObject canvasRoot)
        {
            canvasRoot = new GameObject(name, typeof(RectTransform));
            Canvas canvas = canvasRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasRoot.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        static VerticalLayoutGroup AddVerticalLayout(GameObject panel, int padding, float spacing)
        {
            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding - 8, padding - 8);
            layout.spacing = spacing;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            return layout;
        }

        // Title: wordmark, tagline, the five buttons (Play is the one orange), account line at top right.
        // frob:ticket 01M3DG5Y1ECEZ8A5Y7AJT2NM7X
        static void BuildTitlePrefab()
        {
            AddOverlayCanvas("TitleCanvas", 0, out GameObject canvasRoot);
            Image backdrop = AddImage(canvasRoot.transform, "TitleRoot", BrandColors.Background);
            Stretch(backdrop.rectTransform);
            GameObject panel = AddPanel(backdrop.transform, "TitlePanel", new Vector2(520f, 640f), BrandColors.Panel);
            AddVerticalLayout(panel, 48, 12f);
            TMP_Text title = AddText(panel.transform, "Title", "HULLBREACH", 52f, TextAlignmentOptions.Center);
            title.fontStyle = FontStyles.Bold;
            TMP_Text tagline = AddText(panel.transform, "Tagline", BrandPalette.Tagline, 18f, TextAlignmentOptions.Center);
            tagline.color = BrandColors.Cyan;
            AddSpacer(panel.transform, 20f);
            Button play = AddButton(panel.transform, "PlayButton", "PLAY", 60f, primary: true);
            Button build = AddButton(panel.transform, "BuildButton", "BUILD", 52f);
            Button settings = AddButton(panel.transform, "SettingsButton", "SETTINGS", 52f);
            Button signIn = AddButton(panel.transform, "SignInButton", "SIGN IN", 52f);
            Button quit = AddButton(panel.transform, "QuitButton", "QUIT", 52f);

            TMP_Text account = AddText(backdrop.transform, "AccountText", string.Empty, 18f, TextAlignmentOptions.Right);
            account.color = BrandColors.Muted;
            RectTransform accountRect = account.rectTransform;
            accountRect.anchorMin = accountRect.anchorMax = accountRect.pivot = new Vector2(1f, 1f);
            accountRect.sizeDelta = new Vector2(520f, 30f);
            accountRect.anchoredPosition = new Vector2(-24f, -20f);

            HullbreachTitleView view = canvasRoot.AddComponent<HullbreachTitleView>();
            SerializedObject serialized = new SerializedObject(view);
            Set(serialized, "playButton", play);
            Set(serialized, "buildButton", build);
            Set(serialized, "settingsButton", settings);
            Set(serialized, "signInButton", signIn);
            Set(serialized, "quitButton", quit);
            Set(serialized, "accountText", account);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(canvasRoot, TitlePrefabPath);
            Object.DestroyImmediate(canvasRoot);
        }

        // One row: label, slider, percent text.
        static Slider AddSliderRow(Transform parent, string name, string label, out TMP_Text value)
        {
            GameObject row = AddUiObject(parent, name + "Row");
            row.AddComponent<LayoutElement>().preferredHeight = 38f;
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = false;
            TMP_Text text = AddText(row.transform, name + "Label", label, 18f);
            text.gameObject.GetComponent<LayoutElement>().preferredWidth = 150f;

            GameObject sliderRoot = AddUiObject(row.transform, name + "Slider");
            sliderRoot.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Image track = AddImage(sliderRoot.transform, "Track", BrandColors.Background);
            RectTransform trackRect = track.rectTransform;
            trackRect.anchorMin = new Vector2(0f, 0.5f);
            trackRect.anchorMax = new Vector2(1f, 0.5f);
            trackRect.sizeDelta = new Vector2(0f, 8f);
            GameObject fillArea = AddUiObject(sliderRoot.transform, "Fill Area");
            Stretch(fillArea.GetComponent<RectTransform>(), 0f, 0f);
            Image fill = AddImage(fillArea.transform, "Fill", BrandColors.Cyan);
            fill.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            fill.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            fill.rectTransform.sizeDelta = new Vector2(0f, 8f);
            GameObject handleArea = AddUiObject(sliderRoot.transform, "Handle Slide Area");
            Stretch(handleArea.GetComponent<RectTransform>(), 8f, 0f);
            Image handle = AddImage(handleArea.transform, "Handle", BrandColors.Ink);
            handle.rectTransform.sizeDelta = new Vector2(16f, 30f);
            Slider slider = sliderRoot.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            value = AddText(row.transform, name + "Value", "100%", 18f, TextAlignmentOptions.Right);
            value.gameObject.GetComponent<LayoutElement>().preferredWidth = 70f;
            return slider;
        }

        // Settings panel (hidden until opened): three volumes, resolution, fullscreen, colorblind palette.
        // frob:ticket 01M3DG5Y1GSP9K4A0MY9MSHS37
        // frob:ticket 01M3DG5Y2DX8GJYANMCGM0B19P
        static void BuildSettingsPrefab()
        {
            AddOverlayCanvas("SettingsCanvas", 20, out GameObject canvasRoot);
            Image dim = AddImage(canvasRoot.transform, "SettingsRoot", new Color(0.043f, 0.086f, 0.125f, 0.92f));
            Stretch(dim.rectTransform);
            GameObject panel = AddPanel(dim.transform, "SettingsPanel", new Vector2(720f, 620f), BrandColors.Panel);
            AddVerticalLayout(panel, 40, 12f);
            AddSectionTitle(panel.transform, "SETTINGS");
            Slider master = AddSliderRow(panel.transform, "Master", "Master volume", out TMP_Text masterValue);
            Slider music = AddSliderRow(panel.transform, "Music", "Music volume", out TMP_Text musicValue);
            Slider effects = AddSliderRow(panel.transform, "Effects", "Effects volume", out TMP_Text effectsValue);

            GameObject resRow = AddUiObject(panel.transform, "ResolutionRow");
            resRow.AddComponent<LayoutElement>().preferredHeight = 42f;
            HorizontalLayoutGroup resLayout = resRow.AddComponent<HorizontalLayoutGroup>();
            resLayout.spacing = 12f;
            resLayout.childControlHeight = true;
            resLayout.childControlWidth = true;
            resLayout.childForceExpandHeight = true;
            resLayout.childForceExpandWidth = false;
            TMP_Text resLabel = AddText(resRow.transform, "ResolutionLabel", "Resolution", 18f);
            resLabel.gameObject.GetComponent<LayoutElement>().preferredWidth = 150f;
            Button previous = AddButton(resRow.transform, "ResolutionPrevious", "<", 42f);
            previous.gameObject.GetComponent<LayoutElement>().preferredWidth = 50f;
            TMP_Text resText = AddText(resRow.transform, "ResolutionText", "1280 x 720", 18f, TextAlignmentOptions.Center);
            resText.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;
            Button next = AddButton(resRow.transform, "ResolutionNext", ">", 42f);
            next.gameObject.GetComponent<LayoutElement>().preferredWidth = 50f;

            Toggle fullscreen = AddToggle(panel.transform, "FullscreenToggle", "Fullscreen");
            Toggle colorblind = AddToggle(panel.transform, "ColorblindToggle", "Colorblind-safe stress colors");
            TMP_Text message = AddText(panel.transform, "MessageText", string.Empty, 16f, TextAlignmentOptions.Center);
            AddSpacer(panel.transform, 8f);
            Button apply = AddButton(panel.transform, "ApplyButton", "APPLY", 48f, primary: true);
            Button close = AddButton(panel.transform, "CloseButton", "BACK", 44f);

            HullbreachSettingsView view = canvasRoot.AddComponent<HullbreachSettingsView>();
            SerializedObject serialized = new SerializedObject(view);
            Set(serialized, "root", dim.gameObject);
            Set(serialized, "masterSlider", master);
            Set(serialized, "musicSlider", music);
            Set(serialized, "effectsSlider", effects);
            Set(serialized, "masterValue", masterValue);
            Set(serialized, "musicValue", musicValue);
            Set(serialized, "effectsValue", effectsValue);
            Set(serialized, "resolutionPrevious", previous);
            Set(serialized, "resolutionNext", next);
            Set(serialized, "resolutionText", resText);
            Set(serialized, "fullscreenToggle", fullscreen);
            Set(serialized, "colorblindToggle", colorblind);
            Set(serialized, "applyButton", apply);
            Set(serialized, "closeButton", close);
            Set(serialized, "messageText", message);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(canvasRoot, SettingsPrefabPath);
            Object.DestroyImmediate(canvasRoot);
        }

        // Queue: a FIND MATCH launcher at the bottom of the lobby and a panel with status and cancel.
        // frob:ticket 01M3DG5Y1PX311E24E62K5FAKG
        static void BuildQueuePrefab()
        {
            AddOverlayCanvas("QueueCanvas", 5, out GameObject canvasRoot);
            GameObject launcher = AddPanel(canvasRoot.transform, "OpenButtonHolder", new Vector2(220f, 52f), BrandColors.Panel);
            RectTransform launcherRect = launcher.GetComponent<RectTransform>();
            launcherRect.anchorMin = launcherRect.anchorMax = launcherRect.pivot = new Vector2(0.5f, 0f);
            launcherRect.anchoredPosition = new Vector2(0f, 24f);
            VerticalLayoutGroup holder = AddVerticalLayout(launcher, 4, 0f);
            holder.padding = new RectOffset(4, 4, 4, 4);
            Button open = AddButton(launcher.transform, "OpenButton", "FIND MATCH", 44f, primary: true);

            Image dim = AddImage(canvasRoot.transform, "QueueRoot", new Color(0.043f, 0.086f, 0.125f, 0.92f));
            Stretch(dim.rectTransform);
            GameObject panel = AddPanel(dim.transform, "QueuePanel", new Vector2(620f, 380f), BrandColors.Panel);
            AddVerticalLayout(panel, 40, 12f);
            AddSectionTitle(panel.transform, "FIND MATCH");
            TMP_Text status = AddText(panel.transform, "StatusText", string.Empty, 20f, TextAlignmentOptions.Center);
            status.textWrappingMode = TextWrappingModes.Normal;
            status.gameObject.GetComponent<LayoutElement>().preferredHeight = 80f;
            Button find = AddButton(panel.transform, "FindButton", "FIND MATCH", 52f, primary: true);
            Button cancel = AddButton(panel.transform, "CancelButton", "CANCEL SEARCH", 46f);
            Button close = AddButton(panel.transform, "CloseButton", "BACK", 42f);

            HullbreachQueueView view = canvasRoot.AddComponent<HullbreachQueueView>();
            SerializedObject serialized = new SerializedObject(view);
            Set(serialized, "panelRoot", dim.gameObject);
            Set(serialized, "openButton", open);
            Set(serialized, "findButton", find);
            Set(serialized, "cancelButton", cancel);
            Set(serialized, "closeButton", close);
            Set(serialized, "statusText", status);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(canvasRoot, QueuePrefabPath);
            Object.DestroyImmediate(canvasRoot);
        }

        static void BuildGameScene()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/DemoScene.unity", OpenSceneMode.Single);
            EditorSceneManager.SaveScene(scene, GameScenePath);

            foreach (string rootName in new[] { "Demo", "PlayerShip", "TargetShip", "Powerups", "HudCanvas", "EventSystem" })
            {
                GameObject root = GameObject.Find(rootName);
                if (root != null) Object.DestroyImmediate(root);
            }

            Camera camera = Camera.main;
            if (camera == null) camera = CreateCamera(BrandColors.Background);
            CameraFollow oldFollow = camera.GetComponent<CameraFollow>();
            if (oldFollow != null) Object.DestroyImmediate(oldFollow);
            camera.orthographic = true;
            camera.orthographicSize = 16f;

            var presenter = new GameObject("NetworkShipPresenter");
            presenter.AddComponent<HullbreachNetCodeView>();

            var gameplay = new GameObject("NetworkGameplay");
            HullbreachNetworkGameplayController controller = gameplay.AddComponent<HullbreachNetworkGameplayController>();
            SerializedObject controllerSerialized = new SerializedObject(controller);
            Set(controllerSerialized, "gameplayCamera", camera);
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            var subSceneObject = new GameObject("MultiplayerGhosts");
            SubScene subScene = subSceneObject.AddComponent<SubScene>();
            subScene.SceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(GhostSubScenePath);
            subScene.AutoLoadScene = true;

            GameObject canvasGo = new GameObject("NetworkHudCanvas", typeof(RectTransform));
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            GameObject statusPanel = AddPanel(canvasGo.transform, "NetworkStatusPanel", new Vector2(900f, 130f),
                BrandColors.Panel);
            RectTransform statusRect = statusPanel.GetComponent<RectTransform>();
            statusRect.anchorMin = statusRect.anchorMax = statusRect.pivot = new Vector2(0f, 1f);
            statusRect.anchoredPosition = new Vector2(18f, -18f);
            HorizontalLayoutGroup statusLayout = statusPanel.AddComponent<HorizontalLayoutGroup>();
            statusLayout.padding = new RectOffset(18, 18, 14, 14);
            statusLayout.spacing = 12f;
            statusLayout.childControlHeight = true;
            statusLayout.childControlWidth = true;
            statusLayout.childForceExpandHeight = true;
            statusLayout.childForceExpandWidth = false;
            TMP_Text status = AddText(statusPanel.transform, "StatusText", "Waiting for Netcode connection...", 18f);
            LayoutElement statusElement = status.gameObject.AddComponent<LayoutElement>();
            statusElement.flexibleWidth = 1f;
            Button leave = AddButton(statusPanel.transform, "LeaveButton", "LEAVE", 50f);
            leave.gameObject.GetComponent<LayoutElement>().preferredWidth = 130f;

            HullbreachNetworkGameHud hud = canvasGo.AddComponent<HullbreachNetworkGameHud>();
            SerializedObject serialized = new SerializedObject(hud);
            Set(serialized, "statusText", status);
            Set(serialized, "leaveButton", leave);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            CreateEventSystem();
            EditorSceneManager.SaveScene(scene, GameScenePath);
        }

        static void BuildGhostPrefab()
        {
            var root = new GameObject("HullbreachGameplayGhost");
            root.AddComponent<HullbreachGameplayGhostAuthoring>();
            GhostAuthoringComponent ghost = root.AddComponent<GhostAuthoringComponent>();
            ghost.SupportedGhostModes = GhostModeMask.Interpolated;
            ghost.DefaultGhostMode = GhostMode.Interpolated;
            ghost.OptimizationMode = GhostOptimizationMode.Dynamic;
            ghost.Importance = 100;
            ghost.MaxSendRate = HullbreachNetCodeConstants.SimulationTickRate;
            ghost.HasOwner = true;
            ghost.SupportAutoCommandTarget = false;
            ghost.TrackInterpolationDelay = true;
            PrefabUtility.SaveAsPrefabAsset(root, GhostPrefabPath);
            Object.DestroyImmediate(root);
        }

        static void BuildGhostSubScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("HullbreachGhostPrefabs");
            HullbreachGhostPrefabAuthoring authoring = root.AddComponent<HullbreachGhostPrefabAuthoring>();
            authoring.GhostPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GhostPrefabPath);
            EditorSceneManager.SaveScene(scene, GhostSubScenePath);
        }

        static void UpdateBuildSettings()
        {
            string[] preferred = { TitleScenePath, LobbyScenePath, GameScenePath, "Assets/Scenes/DemoScene.unity", "Assets/Scenes/RocketScene.unity" };
            EditorBuildSettings.scenes = preferred.Where(File.Exists)
                .Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
        }

        static Camera CreateCamera(Color background)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            Camera camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            return camera;
        }

        static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        static GameObject AddUiObject(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.GetComponent<RectTransform>().SetParent(parent, false);
            return go;
        }

        static Image AddImage(Transform parent, string name, Color color)
        {
            GameObject go = AddUiObject(parent, name);
            Image image = go.AddComponent<Image>();
            image.color = color;
            return image;
        }

        static GameObject AddPanel(Transform parent, string name, Vector2 size, Color color)
        {
            Image image = AddImage(parent, name, color);
            image.rectTransform.sizeDelta = size;
            return image.gameObject;
        }

        static TMP_Text AddText(Transform parent, string name, string text, float size,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            GameObject go = AddUiObject(parent, name);
            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = BrandColors.Ink;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            LayoutElement layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = Mathf.Max(26f, size + 8f);
            return label;
        }

        static void AddSectionTitle(Transform parent, string text)
        {
            TMP_Text label = AddText(parent, text.Replace(" ", string.Empty) + "Title", text, 22f);
            label.fontStyle = FontStyles.Bold;
            label.color = BrandColors.Cyan;
        }

        static TMP_InputField AddInput(Transform parent, string name, string placeholderText, bool password)
        {
            GameObject root = AddUiObject(parent, name);
            Image image = root.AddComponent<Image>();
            image.color = BrandColors.Background;
            LayoutElement layout = root.AddComponent<LayoutElement>();
            layout.preferredHeight = 42f;

            GameObject viewport = AddUiObject(root.transform, "Text Area");
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            Stretch(viewportRect, 12f, 10f);
            viewport.AddComponent<RectMask2D>();

            TMP_Text placeholder = AddText(viewport.transform, "Placeholder", placeholderText, 17f);
            placeholder.color = BrandColors.Muted;
            Stretch(placeholder.rectTransform);
            TMP_Text text = AddText(viewport.transform, "Text", string.Empty, 17f);
            Stretch(text.rectTransform);

            TMP_InputField input = root.AddComponent<TMP_InputField>();
            input.textViewport = viewportRect;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 64;
            return input;
        }

        static Toggle AddToggle(Transform parent, string name, string text)
        {
            GameObject root = AddUiObject(parent, name);
            LayoutElement layout = root.AddComponent<LayoutElement>();
            layout.preferredHeight = 34f;
            Toggle toggle = root.AddComponent<Toggle>();
            Image background = AddImage(root.transform, "Background", BrandColors.Background);
            RectTransform backgroundRect = background.rectTransform;
            backgroundRect.anchorMin = backgroundRect.anchorMax = new Vector2(0f, 0.5f);
            backgroundRect.sizeDelta = new Vector2(26f, 26f);
            backgroundRect.anchoredPosition = new Vector2(13f, 0f);
            Image check = AddImage(background.transform, "Checkmark", BrandColors.Cyan);
            Stretch(check.rectTransform, 5f, 5f);
            TMP_Text label = AddText(root.transform, "Label", text, 16f);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(38f, 0f);
            label.rectTransform.offsetMax = Vector2.zero;
            toggle.targetGraphic = background;
            toggle.graphic = check;
            toggle.isOn = false;
            return toggle;
        }

        // Primary buttons are signal orange with dark text; every other button is raised with a cyan label.
        // frob:ticket 01M6WV5HP3KVSZNJT6H99MFS3Z
        static Button AddButton(Transform parent, string name, string text, float height, bool primary = false)
        {
            GameObject root = AddUiObject(parent, name);
            Image image = root.AddComponent<Image>();
            image.color = primary ? BrandColors.Orange : BrandColors.Raised;
            Button button = root.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            colors.selectedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.7f);
            button.colors = colors;
            LayoutElement layout = root.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            TMP_Text label = AddText(root.transform, "Label", text, 17f, TextAlignmentOptions.Center);
            label.fontStyle = FontStyles.Bold;
            label.color = primary ? BrandColors.Background : BrandColors.Cyan;
            Stretch(label.rectTransform);
            return button;
        }

        static Transform AddScrollList(Transform parent, out GameObject rowTemplate)
        {
            GameObject scrollGo = AddPanel(parent, "LobbyScroll", Vector2.zero, BrandColors.Background);
            scrollGo.AddComponent<LayoutElement>().flexibleHeight = 1f;
            ScrollRect scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;

            GameObject viewport = AddUiObject(scrollGo.transform, "Viewport");
            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            Stretch(viewport.GetComponent<RectTransform>(), 8f, 8f);

            GameObject content = AddUiObject(viewport.transform, "Content");
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRect;

            rowTemplate = AddPanel(content.transform, "LobbyRowTemplate", Vector2.zero, BrandColors.Panel);
            rowTemplate.AddComponent<LayoutElement>().preferredHeight = 76f;
            HorizontalLayoutGroup rowLayout = rowTemplate.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(14, 10, 8, 8);
            rowLayout.spacing = 8f;
            rowLayout.childControlHeight = true;
            rowLayout.childControlWidth = true;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childForceExpandWidth = false;
            TMP_Text name = AddText(rowTemplate.transform, "NameText", "Lobby Name", 18f);
            name.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;
            TMP_Text details = AddText(rowTemplate.transform, "DetailsText", "1/8 - Open", 15f);
            details.gameObject.GetComponent<LayoutElement>().preferredWidth = 150f;
            Button join = AddButton(rowTemplate.transform, "JoinButton", "JOIN", 44f);
            join.gameObject.GetComponent<LayoutElement>().preferredWidth = 90f;
            rowTemplate.SetActive(false);
            return content.transform;
        }

        static void AddSpacer(Transform parent, float height)
        {
            GameObject spacer = AddUiObject(parent, "Spacer");
            spacer.AddComponent<LayoutElement>().preferredHeight = height;
        }

        static void Stretch(RectTransform rect, float horizontal = 0f, float vertical = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontal, vertical);
            rect.offsetMax = new Vector2(-horizontal, -vertical);
        }

        static void Set(SerializedObject serialized, string property, Object value)
        {
            serialized.FindProperty(property).objectReferenceValue = value;
        }
    }
}
