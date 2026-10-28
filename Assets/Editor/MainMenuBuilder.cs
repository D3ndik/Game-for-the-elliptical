using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Создаёт сцену главного меню и ставит её первой в Build Settings.
// Запуск: Tools → Build Main Menu
public static class MainMenuBuilder
{
    public const string MenuPath = "Assets/Scenes/MainMenu.unity";
    const string ArenaPath = "Assets/Scenes/Arena.unity";

    [MenuItem("Tools/Build Main Menu")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cam.tag = "MainCamera";
        var c = cam.GetComponent<Camera>();
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = new Color(0.07f, 0.08f, 0.09f);

        new GameObject("MainMenu").AddComponent<MainMenu>();

        EditorSceneManager.SaveScene(scene, MenuPath);

        var others = EditorBuildSettings.scenes.Where(s => s.path != MenuPath && s.path != ArenaPath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MenuPath, true), new EditorBuildSettingsScene(ArenaPath, true) }
            .Concat(others).ToArray();

        EditorSceneManager.OpenScene(ArenaPath);
        Debug.Log("MainMenuBuilder: главное меню создано");
    }
}

// Кнопка Play в редакторе всегда запускает игру с главного меню, какая бы сцена ни была открыта
[InitializeOnLoad]
static class PlayFromMainMenu
{
    static PlayFromMainMenu()
    {
        EditorApplication.delayCall += () =>
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuBuilder.MenuPath);
    }
}
