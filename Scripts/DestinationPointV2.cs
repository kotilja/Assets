using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class DestinationPointV2 : MonoBehaviour
{
    [SerializeField] private float gizmoRadius = 0.18f;
    [SerializeField] private string destinationId = "Destination";

    private bool delayedGraphRebuildQueued = false;

    public string DestinationId => destinationId;
    public Vector3 Position => transform.position;

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
            return;

        if (delayedGraphRebuildQueued)
            return;

        delayedGraphRebuildQueued = true;
        EditorApplication.delayCall += DelayedRebuildPedestrianGraph;
#endif
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 1f);
        Gizmos.DrawWireSphere(transform.position, gizmoRadius);
        Gizmos.DrawSphere(transform.position, gizmoRadius * 0.25f);
    }

#if UNITY_EDITOR
    private void DelayedRebuildPedestrianGraph()
    {
        delayedGraphRebuildQueued = false;

        if (this == null || Application.isPlaying)
            return;

        PedestrianNetworkV2 pedestrianNetwork = FindFirstObjectByType<PedestrianNetworkV2>();
        if (pedestrianNetwork != null)
            pedestrianNetwork.RebuildGraph();
    }
#endif
}
