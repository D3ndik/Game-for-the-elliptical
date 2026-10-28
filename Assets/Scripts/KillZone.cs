using UnityEngine;

// Триггер под ареной: всё, что сюда упало, — выбыло
public class KillZone : MonoBehaviour
{
    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        var r = GetComponent<Renderer>();
        if (r) r.enabled = false;
    }

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
