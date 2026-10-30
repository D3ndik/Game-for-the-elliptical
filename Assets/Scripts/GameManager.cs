using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Правила раунда: волны роботов, очки, жизни, комбо, пауза, победа и проигрыш.
// В схеме UI MVP это Model: хранит состояние игры и сообщает об изменениях событиями, про интерфейс ничего не знает.
// Висит на объекте GameManager в сцене Arena (рядом — ComboSystem). Все объекты арены лежат в сцене,
// роботы появляются из префаба Prefabs/EnemyRobot
[RequireComponent(typeof(ComboSystem))]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; } // доступ из других скриптов (KillZone)

    [Header("Сцена")]
    public PlayerBall player;           // шар игрока
    public Transform platform;          // платформа — по её размеру выбираются точки появления роботов
    public Bumper[] bumpers;            // колонны — чтобы роботы не появлялись прямо на них
    public EnemyRobot robotPrefab;      // префаб робота, из него создаются все враги

    [Header("Правила")]
    public int startLives = 3;          // жизней в начале
    public int baseKillScore = 1;       // очков за сброшенного робота (умножается на комбо)

    [Header("Волны")]
    public int totalWaves = 15;          // после этой волны — победа
    public int maxAliveRobots = 5;       // больше одновременно на арене не бывает — остальные выходят по мере сбрасывания
    public float extraForceAtLastWave = 8f;  // насколько сильнее тяга робота к последней волне
    public float extraSpeedAtLastWave = 2f;  // насколько выше его предел скорости к последней волне

    public int Score { get; private set; }
    public int Lives { get; private set; }
    public int Wave { get; private set; }               // номер текущей волны (с 1)
    public int EnemiesAlive => enemies.Count;
    public bool IsGameOver { get; private set; }        // true и при проигрыше, и при победе
    public bool IsVictory { get; private set; }
    public bool IsPaused { get; private set; }

    public const string MenuScene = "MainMenu";
    public PlayerBall Player => player;
    public ComboSystem Combo { get; private set; }

    // События модели — на них подписан HudPresenter
    public System.Action Changed;                           // изменились счёт, жизни, волна, число роботов или состояние (пауза/конец)
    public System.Action<string, Color> OnMessage;          // крупная надпись по центру («ВОЛНА 3»)
    public System.Action<Vector3, string, Color> OnPopup;   // всплывающее «+1» в точке мира

    readonly List<EnemyRobot> enemies = new(); // живые роботы на арене
    float arenaHalfSize = 10f;
    bool spawning;   // идёт выпуск роботов текущей волны
    bool respawning; // шар упал и ждёт возрождения

    // При загрузке сцены: сбрасываем время (могли выйти из паузы), подключаемся к комбо, ставим жизни
    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        Combo = GetComponent<ComboSystem>();
        Combo.OnComboBroken += lost =>
        {
            // После победы/проигрыша серия обнуляется сама — сообщать об этом не нужно
            if (lost >= 2 && !IsGameOver) OnMessage?.Invoke($"КОМБО x{Mathf.Min(lost, Combo.maxMultiplier)} ПОТЕРЯНО", new Color(0.9f, 0.4f, 0.35f));
        };

        Lives = startLives;
        if (platform) arenaHalfSize = platform.localScale.x * 0.5f;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Старт игры: игровая музыка с начала и первая волна
    void Start()
    {
        SoundManager.PlayMusic(SoundManager.Music.Game, restart: true);
        StartCoroutine(NextWave());
    }

    // Пауза: останавливает время (Time.timeScale = 0) и переключает музыку. Вызывают Esc и кнопка «Продолжить»
    public void SetPaused(bool paused)
    {
        if (IsGameOver) return;
        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        Changed?.Invoke();
        // На паузе — спокойная музыка меню, после паузы игровая продолжается с того же места
        SoundManager.PlayMusic(paused ? SoundManager.Music.Menu : SoundManager.Music.Game);
    }

    // Кнопка «Начать заново» и клавиша R: перезагружает сцену арены
    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Кнопка «Главное меню»
    public void ToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(MenuScene);
    }

    // Каждый кадр: клавиши паузы/рестарта, таймер комбо, проверка конца волны
    void Update()
    {
        var k = Keyboard.current;
        var pad = Gamepad.current;
        if ((k != null && k.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame))
            SetPaused(!IsPaused);
        if (IsPaused) return;

        if (Player && !IsGameOver) Combo.Tick(Player.Speed);
        if (k != null && k.rKey.wasPressedThisFrame) Restart();

        if (enemies.RemoveAll(e => e == null) > 0) Changed?.Invoke();

        // Подстраховка: робот, провалившийся ниже арены мимо KillZone, тоже считается сброшенным
        for (int i = enemies.Count - 1; i >= 0; i--)
            if (enemies[i].transform.position.y < -1f) OnEnemyFell(enemies[i]);

        // Волна закончилась, когда все её роботы выпущены и сброшены
        if (IsGameOver || spawning || enemies.Count > 0) return;
        if (Wave >= totalWaves) Win();
        else StartCoroutine(NextWave());
    }

    // Корутина волны: надпись «ВОЛНА N», пауза 1,5 с, затем N роботов по одному (не больше maxAliveRobots сразу)
    IEnumerator NextWave()
    {
        spawning = true;
        Wave++;
        Changed?.Invoke();
        OnMessage?.Invoke(Wave == totalWaves ? "ПОСЛЕДНЯЯ ВОЛНА" : $"ВОЛНА {Wave}", new Color(0.85f, 0.9f, 0.95f));
        yield return new WaitForSeconds(1.5f);

        // Сложность растёт плавно от первой волны к последней, а не без предела
        float t = totalWaves > 1 ? (Wave - 1f) / (totalWaves - 1f) : 1f;
        int count = Wave; // 1, 2, 3… роботов
        for (int i = 0; i < count && !IsGameOver; i++)
        {
            while (enemies.Count >= maxAliveRobots && !IsGameOver) yield return null;
            var e = Instantiate(robotPrefab, SpawnPoint(), Quaternion.Euler(0, Random.Range(0f, 360f), 0));
            e.Init(Player.transform);
            e.moveForce += extraForceAtLastWave * t;
            e.maxSpeed += extraSpeedAtLastWave * t;
            enemies.Add(e);
            Changed?.Invoke();
            yield return new WaitForSeconds(0.4f);
        }
        spawning = false;
    }

    // Случайная точка над ареной (робот падает сверху): не на колонне и не прямо на игроке
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

    // Расстояние между точками по полу, без учёта высоты
    static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    // Все волны пройдены: экран «ПОБЕДА!» (его показывает HUD по флагу IsVictory)
    void Win()
    {
        IsGameOver = true;
        IsVictory = true;
        Combo.Break();
        if (Player) Player.Freeze();
        SoundManager.PlayMusic(SoundManager.Music.Menu, restart: true);
        Changed?.Invoke();
    }

    // Робот упал с арены (вызывает KillZone): звук, очки с учётом комбо, всплывающее «+N»
    public void OnEnemyFell(EnemyRobot enemy)
    {
        if (enemy.Dead) return;
        enemy.Die();
        enemies.Remove(enemy);
        SoundManager.PlayShatter();

        // Засчитывается любой робот, улетевший с арены, — неважно, тараном или просто вытолкнули
        if (!IsGameOver)
        {
            int mult = Combo.RegisterKill();
            int points = baseKillScore * mult;
            Score += points;
            // Робот уже под ареной — HudView поднимет надпись к уровню пола у того края, где он сорвался
            OnPopup?.Invoke(enemy.transform.position, $"+{points}" + (mult > 1 ? $"  x{mult}" : ""), ComboColor(mult));
        }
        Changed?.Invoke();
        Destroy(enemy.gameObject, 0.5f);
    }

    // Шар упал с арены (вызывает KillZone): минус жизнь, серия сгорает; если жизни кончились — конец игры
    public void OnPlayerFell(PlayerBall ball)
    {
        if (respawning || IsGameOver) return;
        SoundManager.PlayPlayerDeath();
        Combo.Break();
        Lives--;
        if (Lives <= 0)
        {
            IsGameOver = true;
            ball.Freeze(); // дальше HUD показывает экран конца игры
            SoundManager.PlayMusic(SoundManager.Music.Menu, restart: true);
        }
        Changed?.Invoke();
        if (IsGameOver) return;
        StartCoroutine(RespawnPlayer(ball));
    }

    // Надпись «СОРВАЛСЯ!», секунда ожидания и возвращение шара на старт
    IEnumerator RespawnPlayer(PlayerBall ball)
    {
        respawning = true;
        OnMessage?.Invoke("СОРВАЛСЯ!", new Color(0.9f, 0.6f, 0.35f));
        yield return new WaitForSeconds(1f);
        ball.Respawn();
        respawning = false;
    }

    // Цвет надписей комбо: чем больше множитель, тем «теплее»
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
