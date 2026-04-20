using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class BuildingPseudo3DVisualV2 : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer mainVisual;
    [SerializeField] private BuildingZoneV2 buildingZone;

    [Header("2D Setup")]
    [SerializeField] private bool syncZoneSizeToSprite = true;
    [SerializeField] private Vector2 sizePadding = Vector2.zero;
    [SerializeField] private Vector2 minimumZoneSize = new Vector2(0.5f, 0.5f);

    private void Awake()
    {
        AutoAssignReferences();
        SyncZoneFromSprite();
    }

    private void OnEnable()
    {
        AutoAssignReferences();
        SyncZoneFromSprite();
    }

    private void OnValidate()
    {
        sizePadding.x = Mathf.Max(0f, sizePadding.x);
        sizePadding.y = Mathf.Max(0f, sizePadding.y);
        minimumZoneSize.x = Mathf.Max(0.1f, minimumZoneSize.x);
        minimumZoneSize.y = Mathf.Max(0.1f, minimumZoneSize.y);

        AutoAssignReferences();
        SyncZoneFromSprite();
    }

    private void AutoAssignReferences()
    {
        if (mainVisual == null)
            mainVisual = GetComponent<SpriteRenderer>();

        if (mainVisual == null)
        {
            Transform baseChild = transform.Find("Base");
            if (baseChild != null)
                mainVisual = baseChild.GetComponent<SpriteRenderer>();
        }

        if (buildingZone == null)
            buildingZone = GetComponent<BuildingZoneV2>();
    }

    public void SyncZoneFromSprite()
    {
        if (mainVisual == null || mainVisual.sprite == null || buildingZone == null)
            return;

        Vector3 visualCenterOffset = mainVisual.bounds.center - transform.position;
        visualCenterOffset.z = 0f;
        buildingZone.SetCenterOffset(visualCenterOffset);

        Transform entranceChild = transform.Find("Entrance");
        if (entranceChild != null)
            buildingZone.SetEntranceOverride(entranceChild);

        if (!syncZoneSizeToSprite)
            return;

        Vector2 spriteSize = mainVisual.sprite.bounds.size;
        Vector3 scale = mainVisual.transform.lossyScale;

        Vector2 worldSize = new Vector2(
            Mathf.Abs(spriteSize.x * scale.x) + sizePadding.x,
            Mathf.Abs(spriteSize.y * scale.y) + sizePadding.y);

        worldSize.x = Mathf.Max(minimumZoneSize.x, worldSize.x);
        worldSize.y = Mathf.Max(minimumZoneSize.y, worldSize.y);

        buildingZone.SetSize(worldSize);
    }
}
