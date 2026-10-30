using UnityEngine;

// Музыка и звуковые эффекты. Префаб Prefabs/SoundManager стоит в каждой сцене;
// первый экземпляр живёт между сценами (музыка не обрывается при переходе меню → игра), лишние удаляются
public class SoundManager : MonoBehaviour
{
    public enum Music { None, Menu, Game } // какой трек должен играть

    [Header("Громкость")]
    public float musicVolume = 0.35f;     // игровая музыка
    public float menuMusicVolume = 0.12f; // музыка меню и паузы — тише, фоном
    public float sfxVolume = 0.8f;        // эффекты (удары, разбивание, падение)
    public float fadeSpeed = 1.5f;        // скорость смены трека, громкость/сек

    [Header("Источники (музыка — в clip)")]
    public AudioSource menuSource;        // играет Audio/MusicMenu по кругу
    public AudioSource gameSource;        // играет Audio/MusicGame по кругу
    public AudioSource[] sfxSources;      // по очереди проигрывают эффекты, чтобы они не обрывали друг друга

    [Header("Эффекты")]
    public AudioClip hitRobot;            // шар ударился о робота
    public AudioClip hitBumper;           // кто-то отскочил от колонны
    public AudioClip shatter;             // робот разбился в пропасти
    public AudioClip playerDeath;         // шар упал с арены

    static SoundManager instance;               // единственный живой экземпляр
    const string VolumeKey = "MasterVolume";    // ключ сохранённой громкости в PlayerPrefs

    int nextSfx;                          // какой из sfxSources взять следующим
    Music current;                        // какой трек сейчас должен звучать
    float lastHitTime, lastBumperTime;    // защита от «дребезга» — не чаще раза в 0,06–0,08 с

    // Общая громкость всей игры (музыка + эффекты), 0..1. Запоминается между запусками
    public static float MasterVolume
    {
        get => PlayerPrefs.GetFloat(VolumeKey, 1f);
        set
        {
            AudioListener.volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumeKey, AudioListener.volume);
        }
    }

    // При загрузке сцены: если SoundManager уже есть (пришёл из прошлой сцены) — этот лишний, удаляем.
    // Иначе становимся главным, переживаем смену сцен и применяем сохранённую громкость
    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        AudioListener.volume = MasterVolume;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // Плавно переключает музыку. restart — начать трек сначала (новая игра), иначе продолжить с места паузы
    public static void PlayMusic(Music music, bool restart = false)
    {
        var sm = instance;
        if (sm == null) return;
        if (restart && music == Music.Game) sm.gameSource.Stop();
        if (restart && music == Music.Menu) sm.menuSource.Stop();
        sm.current = music;
    }

    // Удар шара о робота (вызывает PlayerBall).
    // strength 0..1 — от силы удара зависит громкость и чуть-чуть высота звука
    public static void PlayHitRobot(float strength)
    {
        var sm = instance;
        if (sm == null || Time.unscaledTime - sm.lastHitTime < 0.06f) return; // не дребезжать при длинном контакте
        sm.lastHitTime = Time.unscaledTime;
        sm.PlaySfx(sm.hitRobot, Mathf.Lerp(0.35f, 1f, strength), Random.Range(0.9f, 1.1f) + strength * 0.1f);
    }

    // Отскок от колонны (вызывает Bumper), strength 0..1
    public static void PlayHitBumper(float strength)
    {
        var sm = instance;
        if (sm == null || Time.unscaledTime - sm.lastBumperTime < 0.08f) return;
        sm.lastBumperTime = Time.unscaledTime;
        sm.PlaySfx(sm.hitBumper, Mathf.Lerp(0.4f, 1f, strength), Random.Range(0.95f, 1.08f));
    }

    // Робот разбился (вызывает GameManager.OnEnemyFell)
    public static void PlayShatter()
    {
        var sm = instance;
        if (sm) sm.PlaySfx(sm.shatter, 0.9f, Random.Range(0.9f, 1.1f));
    }

    // Шар упал с арены (вызывает GameManager.OnPlayerFell)
    public static void PlayPlayerDeath()
    {
        var sm = instance;
        if (sm) sm.PlaySfx(sm.playerDeath, 1f, 1f);
    }

    // Проиграть эффект на следующем свободном источнике с заданной громкостью и высотой (pitch)
    void PlaySfx(AudioClip clip, float volume, float pitch)
    {
        if (clip == null || sfxSources == null || sfxSources.Length == 0) return;
        var s = sfxSources[nextSfx];
        nextSfx = (nextSfx + 1) % sfxSources.Length;
        s.pitch = pitch;
        s.PlayOneShot(clip, volume * sfxVolume);
    }

    // Громкость треков плавно тянется к целевой; на нулевой громкости трек ставится на паузу,
    // поэтому игровая музыка после паузы продолжается с того же места
    void Update()
    {
        if (instance != this) return;
        Fade(menuSource, current == Music.Menu, menuMusicVolume);
        Fade(gameSource, current == Music.Game, musicVolume);
    }

    // Один трек: громкость к volume (если он нужен) или к нулю; запуск, пауза и продолжение
    void Fade(AudioSource s, bool active, float volume)
    {
        if (s == null || s.clip == null) return;
        float target = active ? volume : 0f;
        s.volume = Mathf.MoveTowards(s.volume, target, fadeSpeed * musicVolume * Time.unscaledDeltaTime);

        if (active && !s.isPlaying)
        {
            if (s.time > 0f) s.UnPause();
            else s.Play();
        }
        else if (!active && s.isPlaying && s.volume <= 0f) s.Pause();
    }
}
