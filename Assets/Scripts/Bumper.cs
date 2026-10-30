using UnityEngine;

// Пружинящая колонна-бампер: отталкивает всё, что в неё врезается, и слегка подсвечивается при ударе.
// Висит на каждой колонне (Bumpers/Bumper в сцене Arena); коллайдер с физ-материалом MaxBounce (упругость 1)
public class Bumper : MonoBehaviour
{
    public float springImpulse = 10f;   // доп. толчок от пружины, на единицу массы — лёгкие и тяжёлые отлетают одинаково
    public float minOutSpeed = 12f;     // скорость, с которой гарантированно отлетаешь
    public Color hitGlow = new Color(0.25f, 0.55f, 0.55f); // цвет подсветки в момент удара

    Material mat;          // своя копия материала, чтобы вспыхивала только эта колонна
    Color baseEmission;    // обычное (спокойное) свечение
    Vector3 baseScale;     // обычный размер — колонна «пружинит», чуть раздуваясь при ударе
    float pulse;           // 1 в момент удара, плавно спадает до 0

    // При загрузке: запоминаем размер и делаем копию материала для вспышки
    void Awake()
    {
        baseScale = transform.localScale;
        var r = GetComponent<Renderer>();
        if (r == null) return;
        mat = r.material; // копия материала из сцены, чтобы вспышка не задевала другие колонны
        mat.EnableKeyword("_EMISSION");
        baseEmission = mat.GetColor("_EmissionColor");
    }

    // Что-то врезалось в колонну: оттолкнуть от центра колонны, оглушить робота, звук, вспышка
    void OnCollisionEnter(Collision c)
    {
        var rb = c.rigidbody;
        if (rb == null) return;

        Vector3 normal = rb.position - transform.position;
        normal.y = 0;
        normal.Normalize();

        // Материал с упругостью 1 уже отразил скорость — добиваем пружиной до минимальной скорости вылета
        rb.AddForce(normal * springImpulse * rb.mass, ForceMode.Impulse);
        float outSpeed = Vector3.Dot(rb.linearVelocity, normal);
        if (outSpeed < minOutSpeed)
            rb.AddForce(normal * (minOutSpeed - outSpeed) * rb.mass, ForceMode.Impulse);

        var enemy = rb.GetComponent<EnemyRobot>();
        if (enemy) enemy.Knockback(Vector3.zero);
        // Отскок звучит одинаково и для шара, и для робота
        SoundManager.PlayHitBumper(Mathf.Clamp01(c.relativeVelocity.magnitude / 16f));

        pulse = 1f;
    }

    // Каждый кадр после удара: колонна плавно возвращается к обычному размеру и свечению
    void Update()
    {
        if (pulse <= 0f) return;
        pulse = Mathf.MoveTowards(pulse, 0f, Time.deltaTime * 4f);
        float s = 1f + pulse * 0.12f;
        transform.localScale = new Vector3(baseScale.x * s, baseScale.y, baseScale.z * s);
        if (mat) mat.SetColor("_EmissionColor", Color.Lerp(baseEmission, hitGlow, pulse));
    }
}
