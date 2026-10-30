using UnityEngine;

// Наступающий робот. ИИ построен по циклу Sense → Think → Act и устроен как конечный автомат (FSM):
// робот в каждый момент находится ровно в одном состоянии из State.
// Внешний вид, коллайдер, физ-материал и Rigidbody настроены в префабе Prefabs/EnemyRobot;
// GameManager создаёт копии префаба в начале каждой волны
public class EnemyRobot : MonoBehaviour
{
    // Состояния конечного автомата
    public enum State
    {
        Dropping,   // только появился и падает сверху на арену
        Chase,      // едет на игрока
        Stunned,    // получил удар (таран или колонна) — не управляет собой
        Falling     // под ним нет пола — летит в пропасть
    }

    public float moveForce = 17.5f;   // сила, с которой робот едет к игроку (GameManager добавляет к ней на поздних волнах)
    public float maxSpeed = 5.6f;     // предел скорости робота
    public float stunTime = 1.1f;     // сколько секунд робот «оглушён» после удара
    public float fallPull = 6f;       // доп. ускорение вниз/наружу, когда робот сорвался с края

    public State Current { get; private set; } = State.Dropping;
    public Rigidbody Body { get; private set; }
    public bool Dead { get; private set; } // уже упал в пропасть и засчитан — больше ничего не делает
    public float LastHitTime { get; private set; } = -99f; // когда робота последний раз ударили (для тарана при долгом контакте)

    Transform player;          // за кем охотится
    float stunnedUntil;        // до какого момента оглушён

    // Результаты фазы Sense — то, что робот «знает» о мире в этом шаге физики
    bool grounded;             // под ним есть пол
    bool landed;               // хотя бы раз стоял на полу (после спавна)
    bool belowFloor;           // уже ниже уровня пола — точно сорвался с края
    float groundY;             // высота, на которой стоял на полу в последний раз
    Vector3 toPlayer;          // вектор к игроку по полу

    void Awake() => Body = GetComponent<Rigidbody>();

    // Указать цель — шар игрока (вызывает GameManager сразу после создания робота)
    public void Init(Transform target) => player = target;

    // Шаг физики: цикл ИИ Sense → Think → Act
    void FixedUpdate()
    {
        if (player == null || Dead) return;
        Sense();
        Think();
        Act();
    }

    // SENSE — сбор данных: есть ли пол (луч вниз), не ниже ли мы пола, где игрок
    void Sense()
    {
        grounded = Physics.Raycast(Body.position, Vector3.down, 0.75f);
        if (grounded) { landed = true; groundY = Body.position.y; }
        belowFloor = landed && !grounded && Body.position.y < groundY - 0.2f;

        toPlayer = player.position - Body.position;
        toPlayer.y = 0;
    }

    // THINK — переходы между состояниями по данным Sense
    void Think()
    {
        switch (Current)
        {
            case State.Dropping:
                if (grounded) Enter(State.Chase);
                break;

            case State.Chase:
                // Без опоры не едет: иначе тяга прижмёт робота к боковой стенке платформы и он повиснет у края
                if (!grounded) Enter(State.Falling);
                break;

            case State.Stunned:
                if (belowFloor) Enter(State.Falling);
                else if (Time.time >= stunnedUntil && grounded) Enter(State.Chase);
                break;

            case State.Falling:
                if (grounded) Enter(State.Chase); // отскочил обратно на арену — снова в бой
                break;
        }
    }

    // ACT — действие в текущем состоянии
    void Act()
    {
        switch (Current)
        {
            case State.Chase:
                ChasePlayer();
                break;

            case State.Falling:
                // Ниже уровня пола — тянем вниз и от арены, чтобы не зацепился за край
                if (belowFloor)
                {
                    Vector3 outward = new Vector3(Body.position.x, 0, Body.position.z).normalized;
                    Body.AddForce((Vector3.down * 2f + outward) * fallPull, ForceMode.Acceleration);
                }
                break;
        }
    }

    // Смена состояния: что сделать на входе в новое состояние
    void Enter(State next)
    {
        Current = next;
        switch (next)
        {
            case State.Chase:
                // Встаём ровно на колёса и запрещаем наклон
                Body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                Body.angularVelocity = Vector3.zero;
                Body.MoveRotation(Quaternion.Euler(0, Body.rotation.eulerAngles.y, 0));
                break;

            case State.Stunned:
            case State.Falling:
                Body.constraints = RigidbodyConstraints.None; // можно кувыркаться
                break;
        }
    }

    // Толкать робота к игроку, пока скорость в его сторону меньше maxSpeed, и поворачивать «лицом» к нему
    void ChasePlayer()
    {
        if (toPlayer.sqrMagnitude < 0.01f) return;
        Vector3 dir = toPlayer.normalized;

        Vector3 flatVel = new Vector3(Body.linearVelocity.x, 0, Body.linearVelocity.z);
        if (Vector3.Dot(flatVel, dir) < maxSpeed) Body.AddForce(dir * moveForce);

        Body.MoveRotation(Quaternion.RotateTowards(Body.rotation, Quaternion.LookRotation(dir), 360f * Time.fixedDeltaTime));
    }

    // Удар по роботу (таран игрока или колонна): оглушить и толкнуть импульсом
    public void Knockback(Vector3 impulse)
    {
        if (Dead) return;
        LastHitTime = Time.time;
        stunnedUntil = Time.time + stunTime;
        Enter(State.Stunned);
        Body.AddForce(impulse, ForceMode.Impulse);
    }

    // Робот упал в пропасть и засчитан (вызывает GameManager): больше не участвует
    public void Die() => Dead = true;

    // В окне Scene над роботом рисуется цветной шар — видно, в каком он состоянии
    void OnDrawGizmos()
    {
        Gizmos.color = Current switch
        {
            State.Chase => Color.red,
            State.Stunned => Color.yellow,
            State.Falling => Color.magenta,
            _ => Color.white,
        };
        Gizmos.DrawSphere(transform.position + Vector3.up * 1.6f, 0.2f);
    }
}
