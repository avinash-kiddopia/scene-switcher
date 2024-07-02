using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace PBA.Utils.Scene.Switch{
    public class SceneSwitcher : EditorWindow{
        private const string SCENE_SWITCHER = "Scene Switcher";
        private const string CUSTOM_ROOT_FOLDER_PREF_KEY = "SceneSwitcher_CustomRootFolderScene";
        private const string PLAY_START_SCENE_PREF_KEY = "SceneSwitcher_PlayStartScene";
        private const string SCENE_ROOT_FOLDER = "Assets";
        private const string SCENE_SUFFIX = ".unity";
        private const string PLAY_START_SCENE = "Play Start Scene";
        private const string NONE = "None";
        private const string SCENE_FILTER = "t: scene";
        private Dictionary<string, string> scenePathDict;

        private List<Button> buttonsList;

        [MenuItem("Tools/Utils/Scene Switcher &s")]
        private static void ShowWindow() {
            var window = GetWindow<SceneSwitcher>();
            window.titleContent = new GUIContent(SCENE_SWITCHER);
            window.Show();
        }

        private void OnBecameVisible() {
            EditorSceneManager.activeSceneChangedInEditMode += OnSceneChanged;
            SceneManager.activeSceneChanged += OnSceneChanged;
        }

        private void OnBecameInvisible() {
            EditorSceneManager.activeSceneChangedInEditMode -= OnSceneChanged;
            SceneManager.activeSceneChanged -= OnSceneChanged;
        }

        private void CreateGUI() {
            GenerateBuildSettingsSceneButtons();
        }

        private void RefreshGUI() {
            rootVisualElement.Clear();
            CreateGUI();
        }

        private void GenerateBuildSettingsSceneButtons() {
            AddPaddingToVisualElement(rootVisualElement, 8f);

            rootVisualElement.Add(GetCustomFolderButton());

            var dropdown = GetSceneDropDown();
            rootVisualElement.Add(dropdown);
            rootVisualElement.Add(new ToolbarSpacer());

            var scrollView = new ScrollView();

            buttonsList = new List<Button>();
            scenePathDict = new Dictionary<string, string>();
            foreach (var guid in AssetDatabase.FindAssets(SCENE_FILTER, new[] { $"{SCENE_ROOT_FOLDER}{EditorPrefs.GetString(CUSTOM_ROOT_FOLDER_PREF_KEY)}" })) {
                var scenePath = AssetDatabase.GUIDToAssetPath(guid);
                var sceneName = scenePath.Split('/')[^1].Replace(SCENE_SUFFIX, string.Empty);
                scenePathDict[sceneName] = scenePath;
                var button = new Button {
                    text = sceneName,
                    tooltip = scenePath,
                    userData = scenePath,
                    style = {
                        paddingTop = 2f,
                        paddingBottom = 2f,
                        marginTop = 4f
                    }
                };
                button.clicked += () => OpenScene(scenePath);
                button.SetEnabled(SceneManager.GetActiveScene().path != scenePath);
                scrollView.Add(button);
                buttonsList.Add(button);

                dropdown.choices.Add(sceneName);
            }

            dropdown.RegisterValueChangedCallback(SetPlayStartScene);

            var savedSceneName = EditorPrefs.GetString(PLAY_START_SCENE_PREF_KEY);
            if (!dropdown.choices.Contains(savedSceneName)) {
                SetPlayStartScene();
            } else {
                dropdown.value = savedSceneName;
            }

            rootVisualElement.Add(scrollView);
        }

        private static void AddPaddingToVisualElement(VisualElement visualElement, float paddingValue) {
            visualElement.style.paddingTop = paddingValue;
            visualElement.style.paddingBottom = paddingValue;
            visualElement.style.paddingLeft = paddingValue;
            visualElement.style.paddingRight = paddingValue;
        }

        private static DropdownField GetSceneDropDown() {
            var dropdown = new DropdownField {
                label = PLAY_START_SCENE
            };

            dropdown.choices.Add(NONE);
            return dropdown;
        }

        #region Custom Root Folder

        private VisualElement GetCustomFolderButton() {
            const string changeLabel = "Change";
            const string rootDirectoryLabel = "Root Directory";
            var rowVisualElement = new VisualElement {
                style = {
                    flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Row),
                    minHeight = 25
                }
            };

            var label = new Label($"{rootDirectoryLabel}: {GetRootFolder()}") {
                style = {
                    alignSelf = new StyleEnum<Align>(Align.Center),
                    flexGrow = 1,
                    flexShrink = 1,
                    overflow = new StyleEnum<Overflow>(Overflow.Hidden),
                    textOverflow = new StyleEnum<TextOverflow>(TextOverflow.Ellipsis)
                }
            };

            var changeCustomFolderButton = new Button {
                text = changeLabel
            };
            changeCustomFolderButton.clicked += UpdateCustomFolder;

            rowVisualElement.Add(label);
            rowVisualElement.Add(changeCustomFolderButton);

            return rowVisualElement;
        }

        private void UpdateCustomFolder() {
            const string folderSelectPanelTitle = "Select Custom Root Directory";
            var fullPath = EditorUtility.OpenFolderPanel(folderSelectPanelTitle, SCENE_ROOT_FOLDER, string.Empty);
            if (TryGetCustomRootFolder(fullPath, out var customRootFolder)) {
                EditorPrefs.SetString(CUSTOM_ROOT_FOLDER_PREF_KEY, customRootFolder);
                RefreshGUI();
            } else {
                const string errorTitle = "Invalid Folder";
                const string errorMessage = "Please select a sub directory of Assets folder";
                EditorUtility.DisplayDialog(errorTitle, errorMessage, "Ok");
            }
        }

        private static bool TryGetCustomRootFolder(string fullPath, out string customRootFolder) {
            customRootFolder = string.Empty;
            if (!fullPath.Contains(SCENE_ROOT_FOLDER)) return false;
            var pathArray = fullPath.Split(SCENE_ROOT_FOLDER);
            if (pathArray.Length == 2) {
                customRootFolder = pathArray[1];
            }

            return true;
        }

        #endregion

        private void OpenScene(string scenePath) {
            EditorSceneManager.SaveOpenScenes();
            EditorSceneManager.OpenScene(scenePath);
        }

        private void SetPlayStartScene(ChangeEvent<string> changeEvent = null) {
            if (changeEvent == null) {
                EditorSceneManager.playModeStartScene = null;
                EditorPrefs.DeleteKey(PLAY_START_SCENE_PREF_KEY);
                return;
            }

            var sceneName = changeEvent.newValue;
            UpdatePlayModeStartScene(sceneName);
            EditorPrefs.SetString(PLAY_START_SCENE_PREF_KEY, sceneName);
        }

        private void UpdatePlayModeStartScene(string sceneName) {
            if (!scenePathDict.TryGetValue(sceneName, out var scenePath)) {
                EditorSceneManager.playModeStartScene = null;
                return;
            }

            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            EditorSceneManager.playModeStartScene = sceneAsset;
        }

        private void OnSceneChanged(UnityEngine.SceneManagement.Scene oldScene, UnityEngine.SceneManagement.Scene newScene) {
            if (buttonsList == null) return;
            foreach (var button in buttonsList) {
                button.SetEnabled(newScene.path != (string)button.userData);
            }
        }

        private static string GetRootFolder() {
            return !EditorPrefs.HasKey(CUSTOM_ROOT_FOLDER_PREF_KEY) ? SCENE_ROOT_FOLDER : $"{SCENE_ROOT_FOLDER}{EditorPrefs.GetString(CUSTOM_ROOT_FOLDER_PREF_KEY)}";
        }
    }
}