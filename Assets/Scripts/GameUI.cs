using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// HUD: счёт, волна, жизни, комбо-множитель с таймером, спидометр, рывок, всплывающие очки.
// Плюс экран паузы / конца игры с кнопками
public class GameUI : MonoBehaviour
{
    GameManager gm;
    Text scoreText, waveText, livesText, comboText, speedText, dashText, messageText;
    RectTransform comboBar, speedBar, dashBar, speedThreshold;
    Image comboBarImg, speedBarImg;
    float messageUntil;
    readonly List<(Text text, Vector3 world, float born)> popups = new();
    Transform canvas;

    GameObject pausePanel;
    Text pauseTitle, pauseScore;
    Button continueButton, restartButton;
    bool panelWasShown;

    void Awake()
    {
        gm = GameManager.Instance;
        UIKit.EnsureEventSystem();
        BuildCanvas();
        BuildPausePanel();
        gm.OnMessage += ShowMessage;
        gm.OnPopup += SpawnPopup;
    }

    void BuildCanvas()
    {
        canvas = UIKit.Canvas("HUD");

        scoreText = MakeText("Score", new Vector2(0, 1), new Vector2(40, -30), 48, TextAnchor.UpperLeft);
        waveText = MakeText("Wave", new Vector2(0.5f, 1), new Vector2(0, -30), 40, TextAnchor.UpperCenter);
        livesText = MakeText("Lives", new Vector2(1, 1), new Vector2(-40, -30), 40, TextAnchor.UpperRight);

        comboText = MakeText("Combo", new Vector2(1, 0.5f), new Vector2(-60, 60), 96, TextAnchor.MiddleRight);
        (comboBar, comboBarImg) = MakeBar("ComboBar", new Vector2(1, 0.5f), new Vector2(-60, -10), new Vector2(320, 14), new Vector2(1, 0.5f));

        speedText = MakeText("Speed", new Vector2(0.5f, 0), new Vector2(0, 80), 28, TextAnchor.LowerCenter);
        (speedBar, speedBarImg) = MakeBar("SpeedBar", new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(500, 18), new Vector2(0.5f, 0));
        speedThreshold = UIKit.Rect("Threshold", speedBar.parent, new Color(1, 1, 1, 0.8f));
        speedThreshold.anchorMin = speedThreshold.anchorMax = new Vector2(0, 0.5f);
        speedThreshold.sizeDelta = new Vector2(4, 30);

        dashText = MakeText("Dash", new Vector2(0.5f, 0), new Vector2(0, 14), 22, TextAnchor.LowerCenter);
        (dashBar, _) = MakeBar("DashBar", new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(200, 6), new Vector2(0.5f, 0));

        messageText = MakeText("Message", new Vector2(0.5f, 0.5f), new Vector2(0, 120), 80, TextAnchor.MiddleCenter);

        var hint = MakeText("Hint", new Vector2(0, 0), new Vector2(40, 30), 22, TextAnchor.LowerLeft);
        hint.text = "WASD — разгон   SPACE — рывок   ESC — пауза\nТарань роботов и сталкивай в пропасть. Не тормози — сгорит комбо!";
        hint.color = new Color(1, 1, 1, 0.6f);
    }

    void BuildPausePanel()
    {
        var dim = UIKit.Stretch(UIKit.Rect("PausePanel", canvas, new Color(0, 0, 0, 0.6f), blocksClicks: true));
        pausePanel = dim.gameObject;

        var box = UIKit.Rect("Box", dim, UIKit.PanelColor);
        box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
        box.sizeDelta = new Vector2(560, 760);

        pauseTitle = UIKit.Text(box, "Title", new Vector2(0.5f, 0.5f), new Vector2(0, 290), 64, TextAnchor.MiddleCenter);
        pauseScore = UIKit.Text(box, "Score", new Vector2(0.5f, 0.5f), new Vector2(0, 210), 40, TextAnchor.MiddleCenter);
        pauseScore.color = new Color(0.8f, 0.82f, 0.85f);

        continueButton = UIKit.Button(box, "Продолжить", new Vector2(0, 100), () => gm.SetPaused(false));
        restartButton = UIKit.Button(box, "Начать заново", new Vector2(0, 0), gm.Restart);
        UIKit.Button(box, "Главное меню", new Vector2(0, -100), gm.ToMainMenu);

        // Громкость всей игры; музыка паузы играет — изменение слышно сразу
        var volumeLabel = UIKit.Text(box, "VolumeLabel", new Vector2(0.5f, 0.5f), new Vector2(0, -200), 30, TextAnchor.MiddleCenter);
        volumeLabel.color = new Color(0.8f, 0.82f, 0.85f);
        void ShowVolume(float v) => volumeLabel.text = $"Громкость: {Mathf.RoundToInt(v * 100)}%";
        ShowVolume(SoundManager.MasterVolume);
        UIKit.Slider(box, new Vector2(0, -250), SoundManager.MasterVolume, v =>
        {
            SoundManager.MasterVolume = v;
            ShowVolume(v);
        });

        pausePanel.SetActive(false);
    }

    void Update()
    {
        if (gm == null || scoreText == null) return;
        var player = gm.Player;
        var combo = gm.Combo;

        scoreText.text = $"СЧЁТ  {gm.Score:N0}";
        waveText.text = $"ВОЛНА {gm.Wave}/{gm.totalWaves}   роботов: {gm.EnemiesAlive}";
        livesText.text = "ЖИЗНИ  " + new string('●', Mathf.Max(gm.Lives, 0)) + new string('○', Mathf.Max(gm.startLives - gm.Lives, 0));

        // Комбо: множитель + полоска оставшегося времени серии; краснеет и дрожит, когда игрок тормозит
        bool hasCombo = combo.Streak > 0;
        comboText.enabled = hasCombo;
        comboBar.parent.gameObject.SetActive(hasCombo);
        if (hasCombo)
        {
            Color c = GameManager.ComboColor(combo.Multiplier);
            comboText.text = $"x{combo.Multiplier}";
            comboText.color = Color.Lerp(c, Color.red, combo.SlowDanger);
            comboText.transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 40f) * 0.04f * combo.SlowDanger);
            SetFill(comboBar, combo.WindowLeft);
            comboBarImg.color = Color.Lerp(c, Color.red, combo.SlowDanger);
        }

        if (player)
        {
            float max = player.maxSpeed * 1.3f;
            float speed = player.Speed;
            SetFill(speedBar, speed / max);
            float threshold = combo.minComboSpeed / max;
            speedThreshold.anchorMin = speedThreshold.anchorMax = new Vector2(threshold, 0.5f);
            bool slow = speed < combo.minComboSpeed;
            speedBarImg.color = slow ? new Color(1f, 0.3f, 0.3f) : new Color(0.3f, 0.9f, 1f);
            speedText.text = $"СКОРОСТЬ {speed:0.0}" + (slow && hasCombo ? "  — РАЗГОНЯЙСЯ!" : "");

            SetFill(dashBar, player.DashReady);
            dashText.text = player.DashReady >= 1f ? "РЫВОК ГОТОВ" : "";
        }

        if (Time.time > messageUntil) messageText.text = "";

        UpdatePausePanel();
        UpdatePopups();
    }

    void UpdatePausePanel()
    {
        bool show = gm.IsPaused || gm.IsGameOver;
        pausePanel.SetActive(show);
        if (!show) { panelWasShown = false; return; }
        pausePanel.transform.SetAsLastSibling(); // поверх всплывающих очков

        pauseTitle.text = gm.IsVictory ? "ПОБЕДА!" : gm.IsGameOver ? "ИГРА ОКОНЧЕНА" : "ПАУЗА";
        pauseTitle.color = gm.IsVictory ? new Color(0.6f, 0.9f, 0.6f)
                         : gm.IsGameOver ? new Color(0.9f, 0.45f, 0.4f) : new Color(0.9f, 0.9f, 0.92f);
        pauseScore.text = $"Очки: {gm.Score:N0}   Волна: {gm.Wave}/{gm.totalWaves}";
        continueButton.gameObject.SetActive(!gm.IsGameOver);

        // Фокус на первую кнопку при открытии — чтобы работали стрелки/геймпад
        if (!panelWasShown)
        {
            (gm.IsGameOver ? restartButton : continueButton).Select();
            panelWasShown = true;
        }
    }

    void ShowMessage(string msg, Color color)
    {
        messageText.text = msg;
        messageText.color = color;
        messageUntil = Time.time + 1.6f;
    }

    void SpawnPopup(Vector3 world, string msg, Color color)
    {
        var t = MakeText("Popup", new Vector2(0, 0), Vector2.zero, 44, TextAnchor.MiddleCenter);
        t.text = msg;
        t.color = color;
        // Упавший робот уже под ареной — показываем очки у края, где он сорвался
        world.y = Mathf.Max(world.y, 1f);
        popups.Add((t, world, Time.time));
    }

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
            text.rectTransform.position = screen;
            var c = text.color;
            c.a = 1f - age / 1.2f;
            text.color = c;
            text.transform.localScale = Vector3.one * (1f + Mathf.Max(0, 0.3f - age));
        }
    }

    Text MakeText(string name, Vector2 anchor, Vector2 pos, int size, TextAnchor align) =>
        UIKit.Text(canvas, name, anchor, pos, size, align);

    (RectTransform fill, Image img) MakeBar(string name, Vector2 anchor, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        var bg = UIKit.Rect(name, canvas, new Color(1, 1, 1, 0.15f));
        bg.anchorMin = bg.anchorMax = anchor;
        bg.pivot = pivot;
        bg.anchoredPosition = pos;
        bg.sizeDelta = size;

        var fill = UIKit.Stretch(UIKit.Rect("Fill", bg, Color.white));
        return (fill, fill.GetComponent<Image>());
    }

    static void SetFill(RectTransform fill, float t) => fill.anchorMax = new Vector2(Mathf.Clamp01(t), 1);
}
