using UnityEngine;

// Музыка и звуковые эффекты. Создаётся сам при первом обращении и живёт между сценами,
// чтобы музыка не обрывалась при переходе меню → игра. Клипы лежат в Resources/Audio
public class SoundManager : MonoBehaviour
{
    public enum Music { None, Menu, Game }

    public float musicVolume = 0.35f;     // игровая музыка
    public float menuMusicVolume = 0.12f; // музыка меню и паузы — тише, фоном
    public float sfxVolume = 0.8f;
    public float fadeSpeed = 1.5f; // скорость смены трека, громкость/сек

    static SoundManager instance;
    const string VolumeKey = "MasterVolume";

    // Общая громкость всей игры (музыка + эффекты), 0..1. Запоминается между запусками
    public static float MasterVolume
    {
        get { _ = Instance; return AudioListener.volume; }
        set
        {
            _ = Instance;
            AudioListener.volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumeKey, AudioListener.volume);
        }
    }

    AudioSource menuSource, gameSource;
    AudioSource[] sfxPool;
    int nextSfx;
    AudioClip hitRobot, hitBumper, shatter, playerDeath;
    Music current;
    float lastHitTime, lastBumperTime;

    static SoundManager Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("SoundManager");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<SoundManager>();
            }
            return instance;
        }
    }

    void Awake()
    {
        AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        menuSource = MakeSource(Resources.Load<AudioClip>("Audio/MusicMenu"), loop: true);
        gameSource = MakeSource(Resources.Load<AudioClip>("Audio/MusicGame"), loop: true);
        hitRobot = Resources.Load<AudioClip>("Audio/HitRobot");
        hitBumper = Resources.Load<AudioClip>("Audio/HitBumper");
        shatter = Resources.Load<AudioClip>("Audio/RobotShatter");
        playerDeath = Resources.Load<AudioClip>("Audio/PlayerDeath");

        sfxPool = new AudioSource[8];
        for (int i = 0; i < sfxPool.Length; i++) sfxPool[i] = MakeSource(null, loop: false);
    }

    AudioSource MakeSource(AudioClip clip, bool loop)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = loop;
        s.playOnAwake = false;
        // Музыка стартует с нуля и плавно нарастает в Fade; эффектам нужна полная громкость —
        // PlayOneShot умножает свой volume на громкость источника
        s.volume = loop ? 0f : 1f;
        s.spatialBlend = 0f; // 2D: камера всегда рядом с шаром
        return s;
    }

    // Плавно переключает музыку. restart — начать трек сначала (новая игра), иначе продолжить с места паузы
    public static void PlayMusic(Music music, bool restart = false)
    {
        var sm = Instance;
        if (restart && music == Music.Game) sm.gameSource.Stop();
        if (restart && music == Music.Menu) sm.menuSource.Stop();
        sm.current = music;
    }

    // strength 0..1 — от силы удара зависит громкость и чуть-чуть высота звука
    public static void PlayHitRobot(float strength)
    {
        var sm = Instance;
        if (Time.unscaledTime - sm.lastHitTime < 0.06f) return; // не дребезжать при длинном контакте
        sm.lastHitTime = Time.unscaledTime;
        sm.PlaySfx(sm.hitRobot, Mathf.Lerp(0.35f, 1f, strength), Random.Range(0.9f, 1.1f) + strength * 0.1f);
    }

    public static void PlayHitBumper(float strength)
    {
        var sm = Instance;
        if (Time.unscaledTime - sm.lastBumperTime < 0.08f) return;
        sm.lastBumperTime = Time.unscaledTime;
        sm.PlaySfx(sm.hitBumper, Mathf.Lerp(0.4f, 1f, strength), Random.Range(0.95f, 1.08f));
    }

    public static void PlayShatter()
    {
        var sm = Instance;
        sm.PlaySfx(sm.shatter, 0.9f, Random.Range(0.9f, 1.1f));
    }

    public static void PlayPlayerDeath()
    {
        var sm = Instance;
        sm.PlaySfx(sm.playerDeath, 1f, 1f);
    }

    void PlaySfx(AudioClip clip, float volume, float pitch)
    {
        if (clip == null) return;
        var s = sfxPool[nextSfx];
        nextSfx = (nextSfx + 1) % sfxPool.Length;
        s.pitch = pitch;
        s.PlayOneShot(clip, volume * sfxVolume);
    }

    // Громкость треков плавно тянется к целевой; на нулевой громкости трек ставится на паузу,
    // поэтому игровая музыка после паузы продолжается с того же места
    void Update()
    {
        Fade(menuSource, current == Music.Menu, menuMusicVolume);
        Fade(gameSource, current == Music.Game, musicVolume);
    }

    void Fade(AudioSource s, bool active, float volume)
    {
        if (s.clip == null) return;
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
