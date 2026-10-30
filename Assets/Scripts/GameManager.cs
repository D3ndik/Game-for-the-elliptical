using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Правила раунда: волны роботов, очки, жизни, комбо. Арену (колонны, материалы) собирает Tools → Build Arena
[RequireComponent(typeof(ComboSystem))]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Правила")]
    public int startLives = 3;
    public int baseKillScore = 1; // очков за сброшенного робота (умножается на комбо)

    [Header("Волны")]
    public int totalWaves = 15;
    public int maxAliveRobots = 5;       // больше одновременно на арене не бывает — остальные выходят по мере сбрасывания
    public float extraForceAtLastWave = 8f;
    public float extraSpeedAtLastWave = 2f;

    [Header("Материалы роботов")]
    public Material robotBody;
    public Material robotVisor;
    public Material robotLight;

    public int Score { get; private set; }
    public int Lives { get; private set; }
    public int Wave { get; private set; }
    public int EnemiesAlive => enemies.Count;
    public bool IsGameOver { get; private set; }
    public bool IsVictory { get; private set; }
    public bool IsPaused { get; private set; }

    public const string MenuScene = "MainMenu";
    public PlayerBall Player { get; private set; }
    public ComboSystem Combo { get; private set; }

    public System.Action<string, Color> OnMessage;
    public System.Action<Vector3, string, Color> OnPopup;

    readonly List<EnemyRobot> enemies = new();
    PhysicsMaterial robotMaterial;
    float arenaHalfSize = 10f;
    Bumper[] bumpers;
    bool spawning;
    bool respawning;

    // Подстраховка: если в сцене нет GameManager, но есть шар игрока — создаём его сами
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded += (_, _) => TryCreate();
        TryCreate();
    }

    static void TryCreate()
    {
        if (FindAnyObjectByType<GameManager>() || FindAnyObjectByType<PlayerBall>() == null) return;
        new GameObject("GameManager").AddComponent<GameManager>();
    }

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        Combo = GetComponent<ComboSystem>();
        if (Combo == null) Combo = gameObject.AddComponent<ComboSystem>();
        Combo.OnComboBroken += lost =>
        {
            if (lost >= 2) OnMessage?.Invoke($"КОМБО x{Mathf.Min(lost, Combo.maxMultiplier)} ПОТЕРЯНО", new Color(0.9f, 0.4f, 0.35f));
        };

        Player = FindAnyObjectByType<PlayerBall>();
        Lives = startLives;
        robotMaterial = new PhysicsMaterial("Robot") { bounciness = 0.3f, dynamicFriction = 0.4f, staticFriction = 0.4f };

        var kill = GameObject.Find("KillZone");
        if (kill && !kill.GetComponent<KillZone>()) kill.AddComponent<KillZone>();
        var platform = GameObject.Find("Platform");
        if (platform) arenaHalfSize = platform.transform.localScale.x * 0.5f;
        bumpers = FindObjectsByType<Bumper>();

        gameObject.AddComponent<GameUI>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        SoundManager.PlayMusic(SoundManager.Music.Game, restart: true);
        StartCoroutine(NextWave());
    }

    public void SetPaused(bool paused)
    {
        if (IsGameOver) return;
        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        // На паузе — спокойная музыка меню, после паузы игровая продолжается с того же места
        SoundManager.PlayMusic(paused ? SoundManager.Music.Menu : SoundManager.Music.Game);
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(MenuScene);
    }

    void Update()
    {
        var k = Keyboard.current;
        var pad = Gamepad.current;
        if ((k != null && k.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame))
            SetPaused(!IsPaused);
        if (IsPaused) return;

        if (Player && !IsGameOver) Combo.Tick(Player.Speed);
        if (k != null && k.rKey.wasPressedThisFrame) Restart();

        enemies.RemoveAll(e => e == null);

        // Подстраховка: робот, провалившийся ниже арены мимо KillZone, тоже считается сброшенным
        for (int i = enemies.Count - 1; i >= 0; i--)
            if (enemies[i].transform.position.y < -1f) OnEnemyFell(enemies[i]);

        if (IsGameOver || spawning || enemies.Count > 0) return;
        if (Wave >= totalWaves) Win();
        else StartCoroutine(NextWave());
    }

    IEnumerator NextWave()
    {
        spawning = true;
        Wave++;
        OnMessage?.Invoke(Wave == totalWaves ? "ПОСЛЕДНЯЯ ВОЛНА" : $"ВОЛНА {Wave}", new Color(0.85f, 0.9f, 0.95f));
        yield return new WaitForSeconds(1.5f);

        // Сложность растёт плавно от первой волны к последней, а не без предела
        float t = totalWaves > 1 ? (Wave - 1f) / (totalWaves - 1f) : 1f;
        int count = Wave; // 1, 2, 3… роботов
        for (int i = 0; i < count && !IsGameOver; i++)
        {
            while (enemies.Count >= maxAliveRobots && !IsGameOver) yield return null;
            var e = EnemyRobot.Create(SpawnPoint(), Player.transform, robotMaterial, robotBody, robotVisor, robotLight);
            e.moveForce += extraForceAtLastWave * t;
            e.maxSpeed += extraSpeedAtLastWave * t;
            enemies.Add(e);
            yield return new WaitForSeconds(0.4f);
        }
        spawning = false;
    }

    // Случайная точка на арене: не на колонне и не прямо на игроке
    Vector3 SpawnPoint()
    {
        float lim = arenaHalfSize * 0.8f;
        Vector3 pos = Vector3.zero;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            pos = new Vector3(Random.Range(-lim, lim), 6f, Random.Range(-lim, lim));
            if (FlatDistance(pos, Player.transform.position) < 4f) continue;
            bool onBumper = false;
            foreach (var b in bumpers)
                if (b && FlatDistance(pos, b.transform.position) < b.transform.localScale.x * 0.5f + 1.2f) onBumper = true;
            if (!onBumper) break;
        }
        return pos;
    }

    static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    void Win()
    {
        IsGameOver = true;
        IsVictory = true;
        Combo.Break();
        if (Player) Player.ControlsEnabled = false;
        SoundManager.PlayMusic(SoundManager.Music.Menu, restart: true);
    }

    public void OnEnemyFell(EnemyRobot enemy)
    {
        if (enemy.Dead) return;
        enemy.Dead = true;
        enemies.Remove(enemy);
        SoundManager.PlayShatter();

        // Засчитывается любой робот, улетевший с арены, — неважно, тараном или просто вытолкнули
        if (!IsGameOver)
        {
            int mult = Combo.RegisterKill();
            int points = baseKillScore * mult;
            Score += points;
            OnPopup?.Invoke(enemy.transform.position + Vector3.up * 10f, $"+{points}" + (mult > 1 ? $"  x{mult}" : ""), ComboColor(mult));
        }
        Destroy(enemy.gameObject, 0.5f);
    }

    public void OnPlayerFell(PlayerBall player)
    {
        if (respawning || IsGameOver) return;
        SoundManager.PlayPlayerDeath();
        Combo.Break();
        Lives--;
        if (Lives <= 0)
        {
            IsGameOver = true;
            player.ControlsEnabled = false; // дальше GameUI показывает экран конца игры
            SoundManager.PlayMusic(SoundManager.Music.Menu, restart: true);
            return;
        }
        StartCoroutine(RespawnPlayer(player));
    }

    IEnumerator RespawnPlayer(PlayerBall player)
    {
        respawning = true;
        OnMessage?.Invoke("СОРВАЛСЯ!", new Color(0.9f, 0.6f, 0.35f));
        yield return new WaitForSeconds(1f);
        player.Respawn();
        respawning = false;
    }

    public static Color ComboColor(int mult) => mult switch
    {
        <= 1 => Color.white,
        2 => new Color(0.7f, 0.85f, 0.9f),
        3 => new Color(0.7f, 0.9f, 0.65f),
        4 => new Color(0.95f, 0.9f, 0.55f),
        5 => new Color(0.95f, 0.7f, 0.45f),
        _ => new Color(0.95f, 0.5f, 0.45f),
    };
}
