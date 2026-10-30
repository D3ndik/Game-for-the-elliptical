using UnityEngine;

// Камера следует за шаром сверху-сзади. Висит на Main Camera в сцене Arena, target — объект Player
public class CameraFollow : MonoBehaviour
{
    public Transform target;                         // за кем следить (шар игрока)
    public Vector3 offset = new Vector3(0, 12, -10); // где камера относительно шара: 12 м выше и 10 м позади
    public float fallFollowLimit = -3f;              // камера не уходит вслед за шаром в пропасть

    // После всех перемещений кадра: ставим камеру над шаром и направляем на него
    void LateUpdate()
    {
        if (target == null) return;
        Vector3 focus = target.position;
        focus.y = Mathf.Max(focus.y, fallFollowLimit);

        transform.position = focus + offset;
        transform.LookAt(focus);
    }
}
