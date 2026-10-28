using UnityEngine;

// Комбо-множитель: серия сбитых подряд роботов, пока шар не сбросил скорость.
// Один таймер на всё: полоска в HUD показывает ровно его, серия сгорает, когда он пуст
public class ComboSystem : MonoBehaviour
{
    public float comboWindow = 6f;       // сколько живёт серия без нового сбитого
    public float minComboSpeed = 6f;     // «без замедления»: ниже этой скорости таймер тает быстрее
    public float slowDrain = 4f;         // во сколько раз быстрее тает таймер, когда шар медленный
    public int maxMultiplier = 8;

    public int Streak { get; private set; }
    public int Multiplier => Mathf.Clamp(Streak, 1, maxMultiplier);
    public float WindowLeft => Streak > 0 ? Mathf.Clamp01(timeLeft / comboWindow) : 0f;
    public float SlowDanger { get; private set; } // 0..1 — для подкраски HUD, плавно

    public System.Action<int> OnComboBroken;

    float timeLeft;

    public int RegisterKill()
    {
        Streak++;
        timeLeft = comboWindow;
        return Multiplier;
    }

    public void Tick(float playerSpeed)
    {
        if (Streak == 0) { SlowDanger = 0f; return; }

        bool slow = playerSpeed < minComboSpeed;
        SlowDanger = Mathf.MoveTowards(SlowDanger, slow ? 1f : 0f, Time.deltaTime * 6f);
        timeLeft -= Time.deltaTime * (slow ? slowDrain : 1f);
        if (timeLeft <= 0f) Break();
    }

    public void Break()
    {
        if (Streak == 0) return;
        int lost = Streak;
        Streak = 0;
        timeLeft = 0f;
        SlowDanger = 0f;
        OnComboBroken?.Invoke(lost);
    }
}
