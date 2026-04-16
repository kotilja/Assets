using UnityEngine;

[ExecuteAlways]
public class DestinationPointV2 : MonoBehaviour
{
    [SerializeField] private float gizmoRadius = 0.18f;
    [SerializeField] private string destinationId = "Destination";

    public string DestinationId => destinationId;
    public Vector3 Position => transform.position;

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 1f);
        Gizmos.DrawWireSphere(transform.position, gizmoRadius);
        Gizmos.DrawSphere(transform.position, gizmoRadius * 0.25f);
    }
}