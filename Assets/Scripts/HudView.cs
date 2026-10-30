using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// View в схеме MVP: пассивное отображение HUD и окна паузы. Висит на объекте HUD (Canvas) в сцене Arena.
// Ничего не знает про GameManager: только показывает то, что ему передал HudPresenter,
// и сообщает о нажатиях кнопок через события. Все элементы лежат в сцене и привязаны в инспекторе
public class HudView : MonoBehaviour, IHudView
{
    [Header("HUD")]
    public Text scoreText;               // «СЧЁТ» слева сверху
    public Text waveText;                // «ВОЛНА 3/15  роботов: 2» по центру сверху
    public Text livesText;               // «ЖИЗНИ ●●○» справа сверху
    public Text comboText;               // крупный множитель «x3» справа
    public Text speedText;               // «СКОРОСТЬ 12.3» над спидометром
    public Text dashText;                // «РЫВОК ГОТОВ»
    public Text messageText;             // крупная надпись по центру («ВОЛНА 2», «СОРВАЛСЯ!»)
    public RectTransform comboBar;       // заливка полоски серии
    public RectTransform speedBar;       // заливка спидометра
    public RectTransform speedThreshold; // отметка минимальной скорости для комбо
    public RectTransform dashBar;        // заливка полоски перезарядки рывка

    [Header("Пауза / конец игры")]
    public GameObject pausePanel;        // затемнение с окном; скрыто, пока игра идёт
    public Text pauseTitle;              // «ПАУЗА» / «ИГРА ОКОНЧЕНА» / «ПОБЕДА!»
    public Text pauseScore;              // «Очки: 12   Волна: 4/15»
    public Text volumeLabel;             // «Громкость: 80%»
    public Slider volumeSlider;          // ползунок общей громкости
    public Button continueButton;        // прячется в конце игры
    public Button restartButton;
    public Button menuButton;

    [Header("Префабы")]
    public Text popupPrefab;             // всплывающее «+1» над сброшенным роботом

    public event Action ContinueClicked;
    public event Action RestartClicked;
    public event Action MenuClicked;
    public event Action<float> VolumeChanged;

    Image comboBarImg, speedBarImg;      // картинки полосок — меняем их цвет
    float messageUntil;                  // до какого времени показывать центральную надпись
    readonly List<(Text text, Vector3 world, float born)> popups = new(); // живые «+1»: текст, точка в мире, время появления

    // При загрузке: кнопки и ползунок передают нажатия наружу через события, окно паузы скрыто
    void Awake()
    {
        comboBarImg = comboBar.GetComponent<Image>();
        speedBarImg = speedBar.GetComponent<Image>();
        continueButton.onClick.AddListener(() => ContinueClicked?.Invoke());
        restartButton.onClick.AddListener(() => RestartClicked?.Invoke());
        menuButton.onClick.AddListener(() => MenuClicked?.Invoke());
        volumeSlider.onValueChanged.AddListener(v =>
        {
            ShowVolume(v);
            VolumeChanged?.Invoke(v);
        });
        pausePanel.SetActive(false);
        messageText.text = "";
    }

    public void SetScore(int score) => scoreText.text = $"СЧЁТ  {score:N0}";

    public void SetWave(int wave, int totalWaves, int robotsAlive) =>
        waveText.text = $"ВОЛНА {wave}/{totalWaves}   роботов: {robotsAlive}";

    public void SetLives(int lives, int maxLives) =>
        livesText.text = "ЖИЗНИ  " + new string('●', Mathf.Max(lives, 0)) + new string('○', Mathf.Max(maxLives - lives, 0));

    // Множитель и полоска серии; при медленной езде краснеют и дрожат
    public void SetCombo(int multiplier, float timeLeft01, float danger01, Color color)
    {
        comboText.enabled = true;
        comboBar.parent.gameObject.SetActive(true);
        comboText.text = $"x{multiplier}";
        comboText.color = Color.Lerp(color, Color.red, danger01);
        comboText.transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 40f) * 0.04f * danger01);
        SetFill(comboBar, timeLeft01);
        comboBarImg.color = Color.Lerp(color, Color.red, danger01);
    }

    public void HideCombo()
    {
        comboText.enabled = false;
        comboBar.parent.gameObject.SetActive(false);
    }

    // Спидометр: заливка, отметка порога комбо, красный цвет ниже порога
    public void SetSpeed(float speed, float maxSpeed, float comboSpeed, bool warnSlow)
    {
        SetFill(speedBar, speed / maxSpeed);
        speedThreshold.anchorMin = speedThreshold.anchorMax = new Vector2(comboSpeed / maxSpeed, 0.5f);
        bool slow = speed < comboSpeed;
        speedBarImg.color = slow ? new Color(1f, 0.3f, 0.3f) : new Color(0.3f, 0.9f, 1f);
        speedText.text = $"СКОРОСТЬ {speed:0.0}" + (slow && warnSlow ? "  — РАЗГОНЯЙСЯ!" : "");
    }

    public void SetDash(float ready01)
    {
        SetFill(dashBar, ready01);
        dashText.text = ready01 >= 1f ? "РЫВОК ГОТОВ" : "";
    }

    // Поставить ползунок без вызова VolumeChanged (начальное значение)
    public void SetVolume(float volume01)
    {
        volumeSlider.SetValueWithoutNotify(volume01);
        ShowVolume(volume01);
    }

    // Крупная надпись по центру на 1,6 секунды
    public void ShowMessage(string text, Color color)
    {
        messageText.text = text;
        messageText.color = color;
        messageUntil = Time.time + 1.6f;
    }

    // «+N» из префаба ScorePopup в точке мира, где упал робот
    public void ShowPopup(Vector3 worldPoint, string text, Color color)
    {
        var t = Instantiate(popupPrefab, transform);
        t.text = text;
        t.color = color;
        // Упавший робот уже под ареной — показываем очки у края, где он сорвался
        worldPoint.y = Mathf.Max(worldPoint.y, 1f);
        popups.Add((t, worldPoint, Time.time));
    }

    // Окно паузы / конца игры. При первом показе выделяет кнопку — чтобы работали стрелки и геймпад
    public void ShowPanel(string title, Color titleColor, string stats, bool canContinue)
    {
        bool justOpened = !pausePanel.activeSelf;
        pausePanel.SetActive(true);
        pausePanel.transform.SetAsLastSibling(); // поверх всплывающих очков
        pauseTitle.text = title;
        pauseTitle.color = titleColor;
        pauseScore.text = stats;
        continueButton.gameObject.SetActive(canContinue);
        if (justOpened) (canContinue ? continueButton : restartButton).Select();
    }

    public void HidePanel() => pausePanel.SetActive(false);

    // Каждый кадр: гасим центральную надпись по таймеру и двигаем «+N»
    void Update()
    {
        if (Time.time > messageUntil) messageText.text = "";
        UpdatePopups();
    }

    // «+N» летят вверх и тают 1,2 секунды, потом удаляются.
    // Точка переводится из мирового пространства в экранное (WorldToScreenPoint);
    // если она оказалась позади камеры (z < 0), надпись прячем — иначе она отразилась бы зеркально
    void UpdatePopups()
    {
        var cam = Camera.main;
        for (int i = popups.Count - 1; i >= 0; i--)
        {
            var (text, world, born) = popups[i];
            float age = Time.time - born;
            if (age > 1.2f || cam == null)
            {
                Destroy(text.gameObject);
                popups.RemoveAt(i);
                continue;
            }
            Vector3 screen = cam.WorldToScreenPoint(world + Vector3.up * age * 2f);
            text.enabled = screen.z > 0f;
            text.rectTransform.position = screen;
            var c = text.color;
            c.a = 1f - age / 1.2f;
            text.color = c;
            text.transform.localScale = Vector3.one * (1f + Mathf.Max(0, 0.3f - age));
        }
    }

    void ShowVolume(float v) => volumeLabel.text = $"Громкость: {Mathf.RoundToInt(v * 100)}%";

    // Заполнить полоску на долю t (0..1): правый край заливки двигается через anchorMax.x
    static void SetFill(RectTransform fill, float t) => fill.anchorMax = new Vector2(Mathf.Clamp01(t), 1);
}
