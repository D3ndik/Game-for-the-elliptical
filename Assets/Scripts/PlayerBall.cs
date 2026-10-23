using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerBall : MonoBehaviour
{
    public float force = 30f;
    Rigidbody rb;
    Vector2 move;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        var k = Keyboard.current;
        if (k == null) return;
        float x = (k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0);
        float z = (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0);
        move = new Vector2(x, z);
    }

    void FixedUpdate()
    {
        rb.AddForce(new Vector3(move.x, 0, move.y) * force);
    }
}