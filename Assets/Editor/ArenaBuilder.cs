using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Достраивает арену прямо в сцене: материалы, текстуры, колонны-бамперы, края, GameManager.
// Запуск: Tools → Build Arena (можно повторять — старые объекты пересоздаются)
public static class ArenaBuilder
{
    const string ScenePath = "Assets/Scenes/Arena.unity";
    const string MatDir = "Assets/Materials";
    const string TexDir = "Assets/Textures";

    static readonly Vector2[] BumperPositions = { new(-5.5f, -5.5f), new(5.5f, -5.5f), new(-5.5f, 5.5f), new(5.5f, 5.5f) };
    const float BumperRadius = 1.2f;
    const float BumperHeight = 1.2f;

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
        var bounce = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Resources/MaxBounce.physicMaterial");

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
        gm.robotBody = robotBody;
        gm.robotVisor = robotVisor;
        gm.robotLight = robotLight;
        EditorUtility.SetDirty(gm);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("ArenaBuilder: арена собрана");
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

    static GameObject Part(Transform parent, PrimitiveType type, string name, Material mat, bool keepCollider = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static void Recreate(string name, System.Action<Transform> fill)
    {
        var old = GameObject.Find(name);
        if (old) Object.DestroyImmediate(old);
        var root = new GameObject(name).transform;
        fill(root);
    }

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
