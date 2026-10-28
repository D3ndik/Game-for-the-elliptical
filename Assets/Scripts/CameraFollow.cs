using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 12, -10);
    public float fallFollowLimit = -3f; // камера не уходит вслед за шаром в пропасть

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 focus = target.position;
        focus.y = Mathf.Max(focus.y, fallFollowLimit);

        transform.position = focus + offset;
        transform.LookAt(focus);
    }
}
