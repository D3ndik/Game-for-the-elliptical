using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Собирает префабы в Assets/Prefabs: робот, всплывающие очки, SoundManager.
// Вызывается из Tools → Build Arena / Build Main Menu
public static class PrefabBuilder
{
    const string PrefabDir = "Assets/Prefabs";
    const string MatDir = "Assets/Materials";
    const string AudioDir = "Assets/Audio";

    // Префаб робота: коробка-коллайдер с физ-материалом RobotPhysics, Rigidbody (масса 1.5, без наклона),
    // видимые части из примитивов и скрипт EnemyRobot
    public static EnemyRobot Robot(Material body, Material visor, Material light)
    {
        var root = new GameObject("EnemyRobot");

        var col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(1f, 1.2f, 1f);
        col.sharedMaterial = RobotPhysics();

        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 1.5f;
        rb.linearDamping = 0.3f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        Part(root.transform, PrimitiveType.Cube, "Body", Vector3.zero, new Vector3(1f, 1.2f, 1f), body);
        Part(root.transform, PrimitiveType.Cube, "Visor", new Vector3(0, 0.25f, 0.5f), new Vector3(0.8f, 0.2f, 0.05f), visor);
        Part(root.transform, PrimitiveType.Cylinder, "Antenna", new Vector3(0, 0.85f, 0), new Vector3(0.06f, 0.25f, 0.06f), body);
        Part(root.transform, PrimitiveType.Sphere, "AntennaLight", new Vector3(0, 1.15f, 0), Vector3.one * 0.18f, light);

        root.AddComponent<EnemyRobot>();
        return Save(root, "EnemyRobot").GetComponent<EnemyRobot>();
    }

    // Префаб всплывающего текста «+1» (uGUI Text с обводкой)
    public static Text ScorePopup()
    {
        var text = UIKit.Text(null, "ScorePopup", new Vector2(0.5f, 0.5f), Vector2.zero, 44, TextAnchor.MiddleCenter);
        text.text = "+1";
        return Save(text.gameObject, "ScorePopup").GetComponent<Text>();
    }

    // Префаб SoundManager: два музыкальных источника (меню, игра), четыре для эффектов и ссылки на клипы из Assets/Audio
    public static SoundManager Sound()
    {
        var root = new GameObject("SoundManager");
        var sm = root.AddComponent<SoundManager>();
        sm.menuSource = Source(root, Load("MusicMenu"), music: true);
        sm.gameSource = Source(root, Load("MusicGame"), music: true);
        sm.sfxSources = new AudioSource[4];
        for (int i = 0; i < sm.sfxSources.Length; i++) sm.sfxSources[i] = Source(root, null, music: false);
        sm.hitRobot = Load("HitRobot");
        sm.hitBumper = Load("HitBumper");
        sm.shatter = Load("RobotShatter");
        sm.playerDeath = Load("PlayerDeath");
        return Save(root, "SoundManager").GetComponent<SoundManager>();
    }

    // Источник звука: музыка — по кругу и с нулевой громкостью на старте, эффекты — разово
    static AudioSource Source(GameObject go, AudioClip clip, bool music)
    {
        var s = go.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = music;
        s.playOnAwake = false;
        s.volume = music ? 0f : 1f; // музыка плавно нарастает из нуля в SoundManager.Fade
        s.spatialBlend = 0f;        // 2D: камера всегда рядом с шаром
        return s;
    }

    // Клип из Assets/Audio по имени файла
    static AudioClip Load(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{name}.wav");

    // Физ-материал корпуса робота: слегка пружинит и не скользит, как шайба
    static PhysicsMaterial RobotPhysics()
    {
        string path = $"{MatDir}/RobotPhysics.physicMaterial";
        var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (mat == null)
        {
            mat = new PhysicsMaterial("RobotPhysics");
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.bounciness = 0.3f;
        mat.dynamicFriction = 0.4f;
        mat.staticFriction = 0.4f;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Видимая часть робота из примитива, без собственного коллайдера
    static void Part(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var part = GameObject.CreatePrimitive(type);
        part.name = name;
        Object.DestroyImmediate(part.GetComponent<Collider>()); // столкновения — только у корпуса-коробки
        part.transform.SetParent(parent, false);
        part.transform.localPosition = pos;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = mat;
    }

    // Сохранить объект как префаб Assets/Prefabs/<name>.prefab, временный объект удалить
    static GameObject Save(GameObject go, string name)
    {
        System.IO.Directory.CreateDirectory(PrefabDir);
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }
}
