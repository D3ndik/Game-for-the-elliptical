using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Достраивает арену прямо в сцене: материалы, текстуры, колонны-бамперы, края, GameManager,
// HUD с окном паузы, EventSystem, SoundManager; префабы — через PrefabBuilder.
// Запуск: Tools → Build Arena (можно повторять — старые объекты пересоздаются)
public static class ArenaBuilder
{
    const string ScenePath = "Assets/Scenes/Arena.unity";
    const string MatDir = "Assets/Materials";
    const string TexDir = "Assets/Textures";

    static readonly Vector2[] BumperPositions = { new(-5.5f, -5.5f), new(5.5f, -5.5f), new(-5.5f, 5.5f), new(5.5f, 5.5f) };
    const float BumperRadius = 1.2f;
    const float BumperHeight = 1.2f;

    // Tools → Build Arena: текстуры, материалы, префабы, колонны, кромка, ссылки GameManager, HUD — и сохранение сцены
    [MenuItem("Tools/Build Arena")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);

        Directory.CreateDirectory(TexDir);
        var panelTex = SaveTexture("PlatformPanels", MakePanelTexture(512));
        var brushedTex = SaveTexture("BrushedMetal", MakeBrushedTexture(256));

        var platformMat = SaveMaterial("Platform", new Color(0.55f, 0.56f, 0.58f), 0.15f, 0.35f, panelTex, new Vector2(5, 5));
        var ballMat = SaveMaterial("BallMetal", new Color(0.78f, 0.78f, 0.8f), 1f, 0.85f, brushedTex, Vector2.one);
        var bumperMat = SaveMaterial("BumperRubber", new Color(0.12f, 0.28f, 0.3f), 0.1f, 0.45f, null, Vector2.one, new Color(0.03f, 0.08f, 0.08f));
        var bumperRingMat = SaveMaterial("BumperRing", new Color(0.35f, 0.36f, 0.38f), 0.9f, 0.7f, brushedTex, Vector2.one);
        var edgeMat = SaveMaterial("EdgeStripe", new Color(0.62f, 0.45f, 0.12f), 0.2f, 0.4f, null, Vector2.one);
        var robotBody = SaveMaterial("RobotBody", new Color(0.32f, 0.34f, 0.38f), 0.6f, 0.5f, brushedTex, Vector2.one);
        var robotVisor = SaveMaterial("RobotVisor", new Color(0.35f, 0.05f, 0.05f), 0f, 0.8f, null, Vector2.one, new Color(0.6f, 0.08f, 0.06f));
        var robotLight = SaveMaterial("RobotLight", new Color(0.5f, 0.35f, 0.1f), 0f, 0.6f, null, Vector2.one, new Color(0.5f, 0.3f, 0.05f));
        var bounce = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>($"{MatDir}/MaxBounce.physicMaterial");

        var robotPrefab = PrefabBuilder.Robot(robotBody, robotVisor, robotLight);
        var popupPrefab = PrefabBuilder.ScorePopup();
        var soundPrefab = PrefabBuilder.Sound();

        var platform = GameObject.Find("Platform");
        float half = 10f, top = 0.5f;
        if (platform)
        {
            platform.GetComponent<Renderer>().sharedMaterial = platformMat;
            half = platform.transform.localScale.x * 0.5f;
            top = platform.transform.position.y + platform.transform.localScale.y * 0.5f;
        }

        var player = GameObject.Find("Player");
        if (player) player.GetComponent<Renderer>().sharedMaterial = ballMat;

        var kill = GameObject.Find("KillZone");
        if (kill)
        {
            if (!kill.GetComponent<KillZone>()) kill.AddComponent<KillZone>();
            var r = kill.GetComponent<Renderer>();
            if (r) r.enabled = false;
        }

        Recreate("Bumpers", root =>
        {
            foreach (var p in BumperPositions)
                CreateBumper(root, new Vector3(p.x, top, p.y), bumperMat, bumperRingMat, bounce);
        });

        Recreate("EdgeStripes", root =>
        {
            for (int i = 0; i < 4; i++)
            {
                bool alongX = i < 2;
                float sign = i % 2 == 0 ? 1 : -1;
                var strip = Part(root, PrimitiveType.Cube, "EdgeStripe", edgeMat);
                strip.transform.position = alongX ? new Vector3(0, top + 0.01f, sign * (half - 0.15f)) : new Vector3(sign * (half - 0.15f), top + 0.01f, 0);
                strip.transform.localScale = alongX ? new Vector3(half * 2f, 0.02f, 0.3f) : new Vector3(0.3f, 0.02f, half * 2f);
            }
        });

        var gmGo = GameObject.Find("GameManager");
        if (gmGo == null) gmGo = new GameObject("GameManager");
        var gm = gmGo.GetComponent<GameManager>();
        if (gm == null) gm = gmGo.AddComponent<GameManager>();
        if (!gmGo.GetComponent<ComboSystem>()) gmGo.AddComponent<ComboSystem>();
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gmGo); // убрать компоненты удалённых скриптов (бывший AttackDirector)
        gm.player = player ? player.GetComponent<PlayerBall>() : null;
        gm.platform = platform ? platform.transform : null;
        gm.bumpers = GameObject.Find("Bumpers").GetComponentsInChildren<Bumper>();
        gm.robotPrefab = robotPrefab;
        EditorUtility.SetDirty(gm);

        DestroyIfExists("HUD");
        BuildHud(gm, popupPrefab);

        DestroyIfExists("EventSystem");
        UIKit.CreateEventSystem();

        DestroyIfExists("SoundManager");
        PrefabUtility.InstantiatePrefab(soundPrefab.gameObject);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("ArenaBuilder: арена собрана");
    }

    // Пересобирает арену, префабы и главное меню разом
    [MenuItem("Tools/Build All")]
    public static void BuildAll()
    {
        Build();
        MainMenuBuilder.Build();
    }

    // Удалить объект сцены с таким именем, если он есть (перед пересозданием)
    public static void DestroyIfExists(string name)
    {
        var go = GameObject.Find(name);
        if (go) Object.DestroyImmediate(go);
    }

    // HUD и окно паузы по схеме MVP: HudView (элементы экрана) + HudPresenter (связь с GameManager).
    // Здесь только раскладка элементов и ссылки на них
    static void BuildHud(GameManager gm, UnityEngine.UI.Text popupPrefab)
    {
        var canvas = UIKit.Canvas("HUD");
        var ui = canvas.gameObject.AddComponent<HudView>();
        ui.popupPrefab = popupPrefab;
        var presenter = canvas.gameObject.AddComponent<HudPresenter>();
        presenter.model = gm;
        presenter.view = ui;

        var topLeft = new Vector2(0, 1);
        var topCenter = new Vector2(0.5f, 1);
        var topRight = new Vector2(1, 1);
        var midRight = new Vector2(1, 0.5f);
        var bottomCenter = new Vector2(0.5f, 0);
        var center = new Vector2(0.5f, 0.5f);

        ui.scoreText = UIKit.Text(canvas, "Score", topLeft, new Vector2(40, -30), 48, TextAnchor.UpperLeft);
        ui.waveText = UIKit.Text(canvas, "Wave", topCenter, new Vector2(0, -30), 40, TextAnchor.UpperCenter);
        ui.livesText = UIKit.Text(canvas, "Lives", topRight, new Vector2(-40, -30), 40, TextAnchor.UpperRight);

        ui.comboText = UIKit.Text(canvas, "Combo", midRight, new Vector2(-60, 60), 96, TextAnchor.MiddleRight);
        ui.comboBar = UIKit.Bar(canvas, "ComboBar", midRight, new Vector2(-60, -10), new Vector2(320, 14), midRight);

        ui.speedText = UIKit.Text(canvas, "Speed", bottomCenter, new Vector2(0, 80), 28, TextAnchor.LowerCenter);
        ui.speedBar = UIKit.Bar(canvas, "SpeedBar", bottomCenter, new Vector2(0, 50), new Vector2(500, 18), bottomCenter);
        ui.speedThreshold = UIKit.Rect("Threshold", ui.speedBar.parent, new Color(1, 1, 1, 0.8f));
        ui.speedThreshold.anchorMin = ui.speedThreshold.anchorMax = new Vector2(0, 0.5f);
        ui.speedThreshold.sizeDelta = new Vector2(4, 30);

        ui.dashText = UIKit.Text(canvas, "Dash", bottomCenter, new Vector2(0, 14), 22, TextAnchor.LowerCenter);
        ui.dashBar = UIKit.Bar(canvas, "DashBar", bottomCenter, new Vector2(0, 8), new Vector2(200, 6), bottomCenter);

        ui.messageText = UIKit.Text(canvas, "Message", center, new Vector2(0, 120), 80, TextAnchor.MiddleCenter);
        ui.messageText.text = "";

        var hint = UIKit.Text(canvas, "Hint", Vector2.zero, new Vector2(40, 30), 22, TextAnchor.LowerLeft);
        hint.text = "WASD — разгон   SPACE — рывок   ESC — пауза\nТарань роботов и сталкивай в пропасть. Не тормози — сгорит комбо!";
        hint.color = new Color(1, 1, 1, 0.6f);

        // Окно паузы / конца игры (по умолчанию скрыто, показывает HudView по команде HudPresenter)
        var dim = UIKit.Stretch(UIKit.Rect("PausePanel", canvas, new Color(0, 0, 0, 0.6f), blocksClicks: true));
        ui.pausePanel = dim.gameObject;

        var box = UIKit.Rect("Box", dim, UIKit.PanelColor);
        box.anchorMin = box.anchorMax = box.pivot = center;
        box.sizeDelta = new Vector2(560, 760);

        ui.pauseTitle = UIKit.Text(box, "Title", center, new Vector2(0, 290), 64, TextAnchor.MiddleCenter);
        ui.pauseTitle.text = "ПАУЗА";
        ui.pauseScore = UIKit.Text(box, "Score", center, new Vector2(0, 210), 40, TextAnchor.MiddleCenter);
        ui.pauseScore.color = UIKit.MutedText;

        // Кнопки и ползунок слушает сам HudView (в Awake) и передаёт нажатия в HudPresenter
        ui.continueButton = UIKit.Button(box, "Продолжить", new Vector2(0, 100));
        ui.restartButton = UIKit.Button(box, "Начать заново", new Vector2(0, 0));
        ui.menuButton = UIKit.Button(box, "Главное меню", new Vector2(0, -100));

        ui.volumeLabel = UIKit.Text(box, "VolumeLabel", center, new Vector2(0, -200), 30, TextAnchor.MiddleCenter);
        ui.volumeLabel.color = UIKit.MutedText;
        ui.volumeLabel.text = "Громкость: 100%";
        ui.volumeSlider = UIKit.Slider(box, new Vector2(0, -250));

        dim.gameObject.SetActive(false);
        EditorUtility.SetDirty(ui);
        EditorUtility.SetDirty(presenter);
    }

    // Колонна: резиновый цилиндр с металлическим ободом сверху; коллайдер — только у корпуса
    static void CreateBumper(Transform parent, Vector3 basePos, Material body, Material ring, PhysicsMaterial bounce)
    {
        var b = Part(parent, PrimitiveType.Cylinder, "Bumper", body, keepCollider: false);
        b.transform.position = basePos + Vector3.up * BumperHeight * 0.5f;
        b.transform.localScale = new Vector3(BumperRadius * 2f, BumperHeight * 0.5f, BumperRadius * 2f);
        var mc = b.AddComponent<MeshCollider>();
        mc.convex = true;
        mc.sharedMaterial = bounce;
        b.AddComponent<Bumper>();

        var cap = Part(b.transform, PrimitiveType.Cylinder, "Cap", ring);
        cap.transform.localPosition = new Vector3(0, 1.02f, 0);
        cap.transform.localScale = new Vector3(1.05f, 0.08f, 1.05f);
    }

    // Примитив (куб, цилиндр) с материалом; коллайдер по умолчанию убираем — он нужен только корпусам
    static GameObject Part(Transform parent, PrimitiveType type, string name, Material mat, bool keepCollider = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    // Удалить старую группу объектов с таким именем и собрать её заново через fill
    static void Recreate(string name, System.Action<Transform> fill)
    {
        var old = GameObject.Find(name);
        if (old) Object.DestroyImmediate(old);
        var root = new GameObject(name).transform;
        fill(root);
    }

    // Создать или обновить материал Assets/Materials/<name>.mat на шейдере URP Lit:
    // цвет, металличность, гладкость (чёткость отражений), текстура с повтором, свечение (emission)
    static Material SaveMaterial(string name, Color color, float metallic, float smoothness, Texture2D tex, Vector2 tiling, Color? emission = null)
    {
        string path = $"{MatDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetTexture("_BaseMap", tex);
        mat.SetTextureScale("_BaseMap", tiling);
        if (emission.HasValue)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission.Value);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Сохранить нарисованную текстуру в Assets/Textures/<name>.png и включить повтор плиткой (Repeat)
    static Texture2D SaveTexture(string name, Texture2D tex)
    {
        string path = $"{TexDir}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Плиты 2×2 с тёмными швами, болтами по углам и лёгкими потёртостями
    static Texture2D MakePanelTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGB24, true);
        int cell = size / 2;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int cx = x % cell, cy = y % cell;
            float v = 0.78f + (Mathf.PerlinNoise(x * 0.03f, y * 0.03f) - 0.5f) * 0.12f
                            + (Mathf.PerlinNoise(x * 0.4f, y * 0.4f) - 0.5f) * 0.05f;
            int edge = Mathf.Min(Mathf.Min(cx, cell - 1 - cx), Mathf.Min(cy, cell - 1 - cy));
            if (edge < 3) v *= 0.45f;
            else if (edge < 6) v *= 0.85f;
            float bolt = Vector2.Distance(new Vector2(Mathf.Min(cx, cell - cx), Mathf.Min(cy, cell - cy)), new Vector2(18, 18));
            if (bolt < 5) v *= 0.6f + bolt * 0.06f;
            tex.SetPixel(x, y, new Color(v, v, v));
        }
        tex.Apply();
        return tex;
    }

    // Шлифованный металл: горизонтальные полосы шума
    static Texture2D MakeBrushedTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGB24, true);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float v = 0.85f + (Mathf.PerlinNoise(x * 0.02f, y * 1.3f) - 0.5f) * 0.2f;
            tex.SetPixel(x, y, new Color(v, v, v));
        }
        tex.Apply();
        return tex;
    }
}
