using System;
using UnityEngine;

// Интерфейс View в схеме MVP: что умеет показывать экран игры и о каких действиях игрока он сообщает.
// Presenter работает только с этим интерфейсом и не знает про конкретные Text, Slider и Button
public interface IHudView
{
    event Action ContinueClicked;       // кнопка «Продолжить»
    event Action RestartClicked;        // кнопка «Начать заново»
    event Action MenuClicked;           // кнопка «Главное меню»
    event Action<float> VolumeChanged;  // ползунок громкости, 0..1

    void SetScore(int score);
    void SetWave(int wave, int totalWaves, int robotsAlive);
    void SetLives(int lives, int maxLives);
    void SetCombo(int multiplier, float timeLeft01, float danger01, Color color); // danger — насколько игрок тормозит
    void HideCombo();
    void SetSpeed(float speed, float maxSpeed, float comboSpeed, bool warnSlow);
    void SetDash(float ready01);
    void SetVolume(float volume01);
    void ShowMessage(string text, Color color);
    void ShowPopup(Vector3 worldPoint, string text, Color color);
    void ShowPanel(string title, Color titleColor, string stats, bool canContinue);
    void HidePanel();
}
