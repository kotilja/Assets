using UnityEngine;

public class PedestrianSpawnerV2 : MonoBehaviour
{
    [SerializeField] private PedestrianNetworkV2 pedestrianNetwork;
    [SerializeField] private PedestrianAgentV2 pedestrianPrefab;
    [SerializeField] private DestinationPointV2 defaultDestination;
    [SerializeField] private ParkingSpotV2 defaultParkingSpot;

    [Header("Spawn")]
    [SerializeField] private bool spawnOnStart = false;
    [SerializeField] private float spawnRadius = 0.1f;
    [SerializeField] private int maxAliveAgents = 10;

    private int aliveAgents = 0;

    public PedestrianNetworkV2 PedestrianNetwork => pedestrianNetwork;
    public DestinationPointV2 DefaultDestination => defaultDestination;
    public ParkingSpotV2 DefaultParkingSpot => defaultParkingSpot;

    private void Start()
    {
        if (spawnOnStart)
            SpawnOne();
    }

    public PedestrianAgentV2 SpawnOne()
    {
        if (pedestrianPrefab == null || pedestrianNetwork == null)
            return null;

        if (aliveAgents >= maxAliveAgents)
            return null;

        Vector3 spawnPosition = transform.position + (Vector3)(Random.insideUnitCircle * spawnRadius);
        spawnPosition.z = 0f;

        PedestrianAgentV2 agent = Instantiate(pedestrianPrefab, spawnPosition, Quaternion.identity);

        if (defaultParkingSpot != null)
            agent.InitializeToParking(pedestrianNetwork, defaultParkingSpot, this);
        else if (defaultDestination != null)
            agent.InitializeToDestination(pedestrianNetwork, defaultDestination, this);
        else
            agent.InitializeFree(pedestrianNetwork, this);

        aliveAgents++;
        return agent;
    }

    public void NotifyAgentDestroyed(PedestrianAgentV2 agent)
    {
        aliveAgents = Mathf.Max(0, aliveAgents - 1);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 1f, 0.3f, 1f);
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
        Gizmos.DrawSphere(transform.position, 0.08f);
    }
#endif
}