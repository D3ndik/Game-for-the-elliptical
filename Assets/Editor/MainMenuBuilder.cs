using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

// Создаёт сцену главного меню (камера, интерфейс, EventSystem, SoundManager) и ставит её первой в Build Settings.
// Запуск: Tools → Build Main Menu
public static class MainMenuBuilder
{
    public const string MenuPath = "Assets/Scenes/MainMenu.unity";
    const string ArenaPath = "Assets/Scenes/Arena.unity";

    // Tools → Build Main Menu: новая сцена с камерой, интерфейсом, EventSystem и SoundManager;
    // сохраняется в Scenes/MainMenu и ставится первой в списке сцен сборки
    [MenuItem("Tools/Build Main Menu")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cam.tag = "MainCamera";
        var c = cam.GetComponent<Camera>();
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = new Color(0.07f, 0.08f, 0.09f);

        var menu = new GameObject("MainMenu").AddComponent<MainMenu>();
        BuildUI(menu);
        UIKit.CreateEventSystem();

        var soundPrefab = AssetDatabase.LoadAssetAtPath<SoundManager>("Assets/Prefabs/SoundManager.prefab");
        if (soundPrefab == null) soundPrefab = PrefabBuilder.Sound();
        PrefabUtility.InstantiatePrefab(soundPrefab.gameObject);

        EditorSceneManager.SaveScene(scene, MenuPath);

        var others = EditorBuildSettings.scenes.Where(s => s.path != MenuPath && s.path != ArenaPath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MenuPath, true), new EditorBuildSettingsScene(ArenaPath, true) }
            .Concat(others).ToArray();

        EditorSceneManager.OpenScene(ArenaPath);
        Debug.Log("MainMenuBuilder: главное меню создано");
    }

    // Фон, название, подзаголовок, кнопки «Начать»/«Выйти» (с сохранёнными обработчиками) и подсказка
    static void BuildUI(MainMenu menu)
    {
        var canvas = UIKit.Canvas("MainMenuUI");
        var center = new Vector2(0.5f, 0.5f);
        UIKit.Stretch(UIKit.Rect("Background", canvas, new Color(0.07f, 0.08f, 0.09f), blocksClicks: true));

        var title = UIKit.Text(canvas, "Title", center, new Vector2(0, 260), 140, TextAnchor.MiddleCenter);
        title.text = "ТАРАН";
        title.color = new Color(0.9f, 0.9f, 0.92f);

        var subtitle = UIKit.Text(canvas, "Subtitle", center, new Vector2(0, 150), 32, TextAnchor.MiddleCenter);
        subtitle.text = "разгоняйся, тарань роботов и сталкивай их с арены";
        subtitle.color = new Color(0.65f, 0.68f, 0.7f);

        menu.startButton = UIKit.Button(canvas, "Начать", new Vector2(0, 0));
        UnityEventTools.AddPersistentListener(menu.startButton.onClick, menu.StartGame);
        var quit = UIKit.Button(canvas, "Выйти", new Vector2(0, -110));
        UnityEventTools.AddPersistentListener(quit.onClick, menu.Quit);

        var hint = UIKit.Text(canvas, "Hint", new Vector2(0.5f, 0), new Vector2(0, 40), 24, TextAnchor.LowerCenter);
        hint.text = "WASD — разгон   SPACE — рывок   ESC — пауза";
        hint.color = new Color(1, 1, 1, 0.45f);

        EditorUtility.SetDirty(menu);
    }
}
