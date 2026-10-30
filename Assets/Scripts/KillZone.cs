using UnityEngine;

// Зона смерти — невидимый триггер под ареной (объект KillZone в сцене Arena, y = −2).
// Всё, что в неё упало, выбыло: робот засчитывается игроку, шар теряет жизнь
public class KillZone : MonoBehaviour
{
    // При загрузке: делаем коллайдер триггером (сквозь него падают, а не стоят на нём) и прячем куб
    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        var r = GetComponent<Renderer>();
        if (r) r.enabled = false;
    }

    // Кто-то влетел в зону: определяем, робот это или шар, и сообщаем GameManager
    void OnTriggerEnter(Collider other)
    {
        var gm = GameManager.Instance;
        var rb = other.attachedRigidbody;
        if (rb == null || gm == null) return;

        var enemy = rb.GetComponent<EnemyRobot>();
        if (enemy) { gm.OnEnemyFell(enemy); return; }

        var player = rb.GetComponent<PlayerBall>();
        if (player) gm.OnPlayerFell(player);
    }
}
