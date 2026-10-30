using UnityEngine;
using UnityEngine.InputSystem;

// Шар игрока: разгон (WASD / стик), рывок (Space / A на геймпаде) и таран роботов.
// Висит на объекте Player в сцене Arena; нужен Rigidbody и SphereCollider (физ-материал BallBounce)
public class PlayerBall : MonoBehaviour
{
    [Header("Движение")]
    public float force = 90f;           // сила разгона от клавиш (Н); больше — быстрее набирает скорость
    public float maxSpeed = 18f;        // предел горизонтальной скорости (м/с)
    public float grip = 6f;             // как быстро гасится боковой занос при повороте
    public float turnBoost = 2f;        // во сколько раз сильнее тяга, когда жмёшь против движения
    public float dashImpulse = 40f;     // сила рывка (мгновенный импульс)
    public float dashCooldown = 0.8f;   // перезарядка рывка, секунд

    [Header("Таран")]
    public float minRamSpeed = 2.5f;    // ниже этой скорости сближения удар не считается тараном
    public float ramPower = 1.95f;      // аркадный множитель поверх физического импульса
    public float ramLift = 0.25f;       // доля импульса вверх, чтобы робот подлетал
    public float velocityBlend = 0.4f;  // насколько направление удара тянется к направлению движения шара
    public float ramCooldown = 0.3f;    // не чаще одного тарана по одному роботу за это время
    public float contactRamSpeed = 1f;  // порог тарана, когда шар уже касается робота и давит на него в упор
    public float minKnockSpeed = 9f;    // с какой скоростью робот отлетает минимум (даже от слабого тарана)

    public Vector3 SpawnPoint { get; private set; }                                  // где шар появляется после падения
    public float Speed => Flat(rb.linearVelocity).magnitude;                          // текущая скорость по горизонтали (для HUD и комбо)
    public float DashReady => Mathf.Clamp01((Time.time - lastDash) / dashCooldown);   // 0..1 — заряд рывка (полоска в HUD)
    public bool ControlsEnabled { get; set; } = true;                                // false после конца игры

    Rigidbody rb;
    Vector2 move;          // направление с клавиатуры/стика, считывается в Update
    bool dashPressed;      // нажат рывок — выполнится в ближайшем FixedUpdate
    float lastDash = -99f; // время последнего рывка
    Vector3 lastVelocity;  // скорость до решения столкновения — в OnCollisionEnter она уже изменена

    // При загрузке: находим Rigidbody, включаем плавное движение и точные столкновения, запоминаем точку старта
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        SpawnPoint = transform.position;
    }

    // Каждый кадр: читаем ввод с клавиатуры и геймпада (физику здесь не трогаем — она в FixedUpdate)
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

        move = Vector2.ClampMagnitude(move, 1f); // по диагонали не быстрее, чем по прямой
    }

    // Шаг физики (50 раз в секунду): тяга, сцепление при поворотах, рывок и ограничение скорости
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

        // Рывок: мгновенный импульс по направлению ввода (или по текущему движению, если клавиши не нажаты)
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

    // Столкновение шара с чем-либо: если это робот — звук удара и, при достаточной скорости, таран
    void OnCollisionEnter(Collision c)
    {
        var enemy = EnemyIn(c);
        if (enemy == null) return;

        // Звук на любой заметный удар, не только на засчитанный таран
        float hitSpeed = c.relativeVelocity.magnitude;
        if (hitSpeed > 1.5f) SoundManager.PlayHitRobot(Mathf.Clamp01(hitSpeed / maxSpeed));

        TryRam(enemy, minRamSpeed);
    }

    // Шар и робот продолжают касаться: если робот прижался к шару, а игрок давит на него в упор,
    // OnCollisionEnter больше не вызывается, а разогнаться выше minRamSpeed мешает сам робот —
    // поэтому здесь порог ниже (contactRamSpeed)
    void OnCollisionStay(Collision c)
    {
        var enemy = EnemyIn(c);
        if (enemy) TryRam(enemy, contactRamSpeed);
    }

    // Таран, если шар едет на робота не медленнее threshold и этого робота не били только что
    void TryRam(EnemyRobot enemy, float threshold)
    {
        if (Time.time - enemy.LastHitTime < ramCooldown) return;
        Vector3 impulse = CalculateRamImpulse(enemy.Body, threshold);
        if (impulse != Vector3.zero) enemy.Knockback(impulse);
    }

    static EnemyRobot EnemyIn(Collision c) => c.rigidbody ? c.rigidbody.GetComponent<EnemyRobot>() : null;

    // Импульс удара: J = (1 + e) * μ * v_сближения, где μ — приведённая масса пары,
    // затем направление смешивается с курсом шара и добавляется подброс вверх.
    // Возвращает Vector3.zero, если сам шар ехал на робота медленнее threshold:
    // робот, который врезался в стоящий шар, — это не таран игрока, а просто касание.
    // Сила не меньше target.mass * minKnockSpeed — робот от тарана всегда заметно отлетает
    Vector3 CalculateRamImpulse(Rigidbody target, float threshold)
    {
        Vector3 normal = Flat(target.position - rb.position).normalized;
        if (normal == Vector3.zero) return Vector3.zero;

        float ownSpeed = Vector3.Dot(lastVelocity, normal);                        // скорость шара в сторону робота
        if (ownSpeed < threshold) return Vector3.zero;
        float approach = Mathf.Max(Vector3.Dot(lastVelocity - target.linearVelocity, normal), ownSpeed); // скорость сближения пары

        const float restitution = 1f; // e — коэффициент упругости (1 = абсолютно упругий удар)
        float reducedMass = rb.mass * target.mass / (rb.mass + target.mass);
        float magnitude = (1f + restitution) * reducedMass * approach * ramPower;
        magnitude = Mathf.Max(magnitude, target.mass * minKnockSpeed);

        Vector3 heading = Flat(lastVelocity).normalized;
        Vector3 dir = Vector3.Lerp(normal, heading, velocityBlend).normalized;
        dir = (dir + Vector3.up * ramLift).normalized;
        return dir * magnitude;
    }

    // Вернуть шар на старт после падения (вызывает GameManager через секунду после падения)
    public void Respawn()
    {
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.position = SpawnPoint + Vector3.up * 3f;
        transform.position = rb.position;
        lastVelocity = Vector3.zero;
    }

    // Конец игры: шар перестаёт слушаться и замирает (иначе бесконечно падает в пропасть)
    public void Freeze()
    {
        ControlsEnabled = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
    }

    // Убирает вертикальную составляющую вектора — нужна только скорость по полу
    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
}
