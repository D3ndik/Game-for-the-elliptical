using UnityEngine;

// Наступающий робот: едет на игрока, после тарана теряет управление и кувыркается
public class EnemyRobot : MonoBehaviour
{
    public float moveForce = 17.5f;   // +25% к прежним 14
    public float maxSpeed = 5.6f;     // +25% к прежним 4.5
    public float stunTime = 1.1f;
    public float fallPull = 6f;       // доп. ускорение вниз/наружу, когда робот сорвался с края

    public Rigidbody Body { get; private set; }
    public bool Dead { get; set; }

    Transform player;
    float stunnedUntil;
    bool landed; // после спавна робот падает сверху — до приземления «сорвался с края» не считаем
    float groundY;

    void Awake()
    {
        Body = GetComponent<Rigidbody>();
        Body.interpolation = RigidbodyInterpolation.Interpolate;
        Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        Body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    public void Init(Transform target) => player = target;

    bool Stunned => Time.time < stunnedUntil;

    bool Grounded => Physics.Raycast(Body.position, Vector3.down, 0.75f);

    void FixedUpdate()
    {
        if (player == null || Dead) return;

        bool grounded = Grounded;
        if (grounded) { landed = true; groundY = Body.position.y; }

        // Без опоры под собой робот не едет: иначе тяга к игроку прижимает его к боковой стенке
        // платформы, и он висит у края, не долетая до KillZone
        if (landed && !grounded)
        {
            Body.constraints = RigidbodyConstraints.None;
            // Ниже уровня пола — значит сорвался с края: тянем вниз и от арены, чтобы не зацепился
            if (Body.position.y < groundY - 0.2f)
            {
                Vector3 outward = new Vector3(Body.position.x, 0, Body.position.z).normalized;
                Body.AddForce((Vector3.down * 2f + outward) * fallPull, ForceMode.Acceleration);
            }
            return;
        }

        if (!landed || Stunned) return;

        if (Body.constraints == RigidbodyConstraints.None)
        {
            // Встаём обратно на колёса
            Body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            Body.angularVelocity = Vector3.zero;
            Body.MoveRotation(Quaternion.Euler(0, Body.rotation.eulerAngles.y, 0));
        }

        Vector3 toPlayer = player.position - Body.position;
        toPlayer.y = 0;
        if (toPlayer.sqrMagnitude < 0.01f) return;
        Vector3 dir = toPlayer.normalized;

        Vector3 flatVel = new Vector3(Body.linearVelocity.x, 0, Body.linearVelocity.z);
        if (Vector3.Dot(flatVel, dir) < maxSpeed) Body.AddForce(dir * moveForce);

        Quaternion look = Quaternion.LookRotation(dir);
        Body.MoveRotation(Quaternion.RotateTowards(Body.rotation, look, 360f * Time.fixedDeltaTime));
    }

    public void Knockback(Vector3 impulse)
    {
        stunnedUntil = Time.time + stunTime;
        Body.constraints = RigidbodyConstraints.None;
        Body.AddForce(impulse, ForceMode.Impulse);
    }

    // Строит робота из примитивов: корпус + визор + антенна
    public static EnemyRobot Create(Vector3 position, Transform target, PhysicsMaterial material, Material body, Material visor, Material light)
    {
        var root = new GameObject("EnemyRobot");
        root.transform.position = position;

        var col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(1f, 1.2f, 1f);
        col.sharedMaterial = material;

        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 1.5f;
        rb.linearDamping = 0.3f;

        AddPart(root.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(1f, 1.2f, 1f), body);
        AddPart(root.transform, PrimitiveType.Cube, new Vector3(0, 0.25f, 0.5f), new Vector3(0.8f, 0.2f, 0.05f), visor);
        AddPart(root.transform, PrimitiveType.Cylinder, new Vector3(0, 0.85f, 0), new Vector3(0.06f, 0.25f, 0.06f), body);
        AddPart(root.transform, PrimitiveType.Sphere, new Vector3(0, 1.15f, 0), Vector3.one * 0.18f, light);

        var enemy = root.AddComponent<EnemyRobot>();
        enemy.Init(target);
        return enemy;
    }

    static void AddPart(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
    {
        var part = GameObject.CreatePrimitive(type);
        DestroyImmediate(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = pos;
        part.transform.localScale = scale;
        if (mat) part.GetComponent<Renderer>().sharedMaterial = mat;
    }
}
