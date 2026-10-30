using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Настройки редактора при открытии проекта:
// 1) если Unity открылся с пустой безымянной сценой — открываем сцену с игрой (Arena);
// 2) кнопка Play всегда запускает игру с главного меню, какая бы сцена ни была открыта
[InitializeOnLoad]
static class EditorStartup
{
    const string GameScene = "Assets/Scenes/Arena.unity";
    const string OpenedKey = "EditorStartup.ArenaOpened"; // чтобы открыть сцену только при запуске Unity, а не после каждой перекомпиляции

    static EditorStartup()
    {
        if (Application.isBatchMode) return; // автотесты и сборка из командной строки работают со своими сценами
        EditorApplication.delayCall += () =>
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuBuilder.MenuPath);
            OpenGameSceneIfEmpty();
        };
    }

    static void OpenGameSceneIfEmpty()
    {
        if (SessionState.GetBool(OpenedKey, false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(OpenedKey, true);

        var scene = EditorSceneManager.GetActiveScene();
        bool emptyUntitled = string.IsNullOrEmpty(scene.path) && !scene.isDirty;
        if (emptyUntitled) EditorSceneManager.OpenScene(GameScene);
    }
}
