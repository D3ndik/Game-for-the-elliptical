using UnityEngine;
using UnityEngine.InputSystem;

// Шар игрока: разгон (WASD / стик), рывок (Space / A на геймпаде) и таран роботов
public class PlayerBall : MonoBehaviour
{
    [Header("Движение")]
    public float force = 90f;
    public float maxSpeed = 18f;
    public float grip = 6f;             // как быстро гасится боковой занос при повороте
    public float turnBoost = 2f;        // во сколько раз сильнее тяга, когда жмёшь против движения
    public float dashImpulse = 40f;
    public float dashCooldown = 0.8f;

    [Header("Таран")]
    public float minRamSpeed = 2.5f;    // ниже этой скорости сближения удар не считается тараном
    public float ramPower = 1.95f;      // аркадный множитель поверх физического импульса
    public float ramLift = 0.25f;       // доля импульса вверх, чтобы робот подлетал
    public float velocityBlend = 0.4f;  // насколько направление удара тянется к направлению движения шара

    public Vector3 SpawnPoint { get; private set; }
    public float Speed => Flat(rb.linearVelocity).magnitude;
    public float DashReady => Mathf.Clamp01((Time.time - lastDash) / dashCooldown);
    public bool ControlsEnabled { get; set; } = true;

    Rigidbody rb;
    Vector2 move;
    bool dashPressed;
    float lastDash = -99f;
    Vector3 lastVelocity; // скорость до решения столкновения — в OnCollisionEnter она уже изменена

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        SpawnPoint = transform.position;
    }

    void Update()
    {
        move = Vector2.zero;
        if (!ControlsEnabled || Time.timeScale == 0f) return; // на паузе ввод не копим

        var k = Keyboard.current;
        if (k != null)
        {
            float x = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0);
            float z = (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0);
            move = new Vector2(x, z);
            if (k.spaceKey.wasPressedThisFrame) dashPressed = true;
        }

        var g = Gamepad.current;
        if (g != null)
        {
            if (g.leftStick.ReadValue().sqrMagnitude > move.sqrMagnitude) move = g.leftStick.ReadValue();
            if (g.buttonSouth.wasPressedThisFrame) dashPressed = true;
        }

        move = Vector2.ClampMagnitude(move, 1f);
    }

    void FixedUpdate()
    {
        Vector3 dir = new Vector3(move.x, 0, move.y);
        Vector3 vel = Flat(rb.linearVelocity);
        if (dir.sqrMagnitude > 0.01f)
        {
            Vector3 n = dir.normalized;
            // Разворот против текущего движения — с усиленной тягой, чтобы шар не «плыл»
            float boost = Vector3.Dot(vel, n) < 0f ? turnBoost : 1f;
            rb.AddForce(dir * force * boost);
            // Сцепление: гасим скорость поперёк направления ввода — повороты становятся резкими
            Vector3 lateral = vel - Vector3.Project(vel, n);
            rb.AddForce(-lateral * grip * dir.magnitude, ForceMode.Acceleration);
        }

        if (dashPressed && Time.time - lastDash >= dashCooldown)
        {
            Vector3 dashDir = dir.sqrMagnitude > 0.01f ? dir.normalized : Flat(rb.linearVelocity).normalized;
            if (dashDir.sqrMagnitude > 0.01f)
            {
                rb.AddForce(dashDir * dashImpulse, ForceMode.Impulse);
                lastDash = Time.time;
            }
        }
        dashPressed = false;

        // Ограничение горизонтальной скорости (рывок и бамперы могут ненадолго её превысить)
        Vector3 flat = Flat(rb.linearVelocity);
        float cap = maxSpeed * (Time.time - lastDash < 0.4f ? 1.6f : 1f);
        if (flat.magnitude > cap)
        {
            Vector3 limited = flat.normalized * Mathf.Lerp(flat.magnitude, cap, 0.1f);
            rb.linearVelocity = new Vector3(limited.x, rb.linearVelocity.y, limited.z);
        }

        lastVelocity = rb.linearVelocity;
    }

    void OnCollisionEnter(Collision c)
    {
        var enemy = c.rigidbody ? c.rigidbody.GetComponent<EnemyRobot>() : null;
        if (enemy == null) return;

        // Звук на любой заметный удар, не только на засчитанный таран
        float hitSpeed = c.relativeVelocity.magnitude;
        if (hitSpeed > 1.5f) SoundManager.PlayHitRobot(Mathf.Clamp01(hitSpeed / maxSpeed));

        Vector3 impulse = CalculateRamImpulse(enemy.Body);
        if (impulse == Vector3.zero) return;

        enemy.Knockback(impulse);
    }

    // Импульс удара: J = (1 + e) * μ * v_сближения, где μ — приведённая масса пары,
    // затем направление смешивается с курсом шара и добавляется подброс вверх
    Vector3 CalculateRamImpulse(Rigidbody target)
    {
        Vector3 normal = Flat(target.position - rb.position).normalized;
        if (normal == Vector3.zero) return Vector3.zero;

        float approach = Vector3.Dot(lastVelocity - target.linearVelocity, normal);
        if (approach < minRamSpeed) return Vector3.zero;

        const float restitution = 1f;
        float reducedMass = rb.mass * target.mass / (rb.mass + target.mass);
        float magnitude = (1f + restitution) * reducedMass * approach * ramPower;

        Vector3 heading = Flat(lastVelocity).normalized;
        Vector3 dir = Vector3.Lerp(normal, heading, velocityBlend).normalized;
        dir = (dir + Vector3.up * ramLift).normalized;
        return dir * magnitude;
    }

    public void Respawn()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.position = SpawnPoint + Vector3.up * 3f;
        transform.position = rb.position;
        lastVelocity = Vector3.zero;
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
}
