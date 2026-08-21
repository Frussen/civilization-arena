using UnityEngine;

public sealed class WonderBuildVisualController : MonoBehaviour
{
    private static readonly int RevealHeightId =
        Shader.PropertyToID("_RevealHeight");

    [SerializeField] private WonderConstruction wonder;
    [SerializeField] private MeshRenderer wonderRenderer;

    private MaterialPropertyBlock propertyBlock;

    private void Start()
    {
        if (wonder == null || wonderRenderer == null)
        {
            Debug.LogError(
                "Wonder build visual references are not fully configured.",
                this);
            enabled = false;
            return;
        }

        SetRendererHierarchyActive();
        propertyBlock = new MaterialPropertyBlock();
        UpdateRevealHeight();
    }

    private void Update()
    {
        UpdateRevealHeight();
    }

    private void SetRendererHierarchyActive()
    {
        Transform current = wonderRenderer.transform;
        while (current != null && current != wonder.transform)
        {
            current.gameObject.SetActive(true);
            current = current.parent;
        }
    }

    private void UpdateRevealHeight()
    {
        float requiredLabor = wonder.LaborHoursRequired;
        float progress = requiredLabor > 0f
            ? Mathf.Clamp01(wonder.LaborHoursCompleted / requiredLabor)
            : wonder.Completed ? 1f : 0f;
        Bounds bounds = wonderRenderer.bounds;
        float revealHeight = Mathf.Lerp(
            bounds.min.y,
            bounds.max.y,
            progress);

        wonderRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(RevealHeightId, revealHeight);
        wonderRenderer.SetPropertyBlock(propertyBlock);
    }
}
