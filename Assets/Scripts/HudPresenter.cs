using UnityEngine;

// Presenter в схеме MVP: единственный посредник между моделью (GameManager, ComboSystem, PlayerBall)
// и View (HudView через интерфейс IHudView). View и Model напрямую друг с другом не общаются.
// Висит на объекте HUD рядом с HudView
public class HudPresenter : MonoBehaviour
{
    public GameManager model;   // состояние игры
    public HudView view;        // экран (используется только как IHudView)

    IHudView hud;

    // При загрузке: подписываемся на события модели и на действия игрока во View
    void Awake()
    {
        hud = view;

        model.Changed += RefreshStats;
        model.OnMessage += hud.ShowMessage;
        model.OnPopup += hud.ShowPopup;

        hud.ContinueClicked += () => model.SetPaused(false);
        hud.RestartClicked += model.Restart;
        hud.MenuClicked += model.ToMainMenu;
        hud.VolumeChanged += v => SoundManager.MasterVolume = v;
    }

    // На старте: показать начальные значения
    void Start()
    {
        hud.SetVolume(SoundManager.MasterVolume);
        RefreshStats();
    }

    void OnDestroy()
    {
        if (model == null) return;
        model.Changed -= RefreshStats;
        model.OnMessage -= hud.ShowMessage;
        model.OnPopup -= hud.ShowPopup;
    }

    // Модель сообщила об изменении: счёт, волна, жизни и окно паузы / конца игры
    void RefreshStats()
    {
        hud.SetScore(model.Score);
        hud.SetWave(model.Wave, model.totalWaves, model.EnemiesAlive);
        hud.SetLives(model.Lives, model.startLives);

        if (!model.IsPaused && !model.IsGameOver) { hud.HidePanel(); return; }

        string title = model.IsVictory ? "ПОБЕДА!" : model.IsGameOver ? "ИГРА ОКОНЧЕНА" : "ПАУЗА";
        Color color = model.IsVictory ? new Color(0.6f, 0.9f, 0.6f)
                    : model.IsGameOver ? new Color(0.9f, 0.45f, 0.4f) : new Color(0.9f, 0.9f, 0.92f);
        hud.ShowPanel(title, color, $"Очки: {model.Score:N0}   Волна: {model.Wave}/{model.totalWaves}", canContinue: !model.IsGameOver);
    }

    // Каждый кадр: величины, которые меняются непрерывно (таймер комбо, скорость, заряд рывка)
    void Update()
    {
        var combo = model.Combo;
        if (combo.Streak > 0)
            hud.SetCombo(combo.Multiplier, combo.WindowLeft, combo.SlowDanger, GameManager.ComboColor(combo.Multiplier));
        else
            hud.HideCombo();

        var player = model.Player;
        if (player)
        {
            hud.SetSpeed(player.Speed, player.maxSpeed * 1.3f, combo.minComboSpeed, warnSlow: combo.Streak > 0);
            hud.SetDash(player.DashReady);
        }
    }
}
