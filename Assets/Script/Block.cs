using System.Collections.Generic;
using UnityEngine;

public class Block : MonoBehaviour, IInteractable
{
    [Header("Highlight Settings")]
    [SerializeField] private Color highlightColor = Color.yellow;
    [SerializeField] private float highlightIntensity = 1f;

    private Dictionary<Renderer, Material> originalMaterials = new Dictionary<Renderer, Material>();
    private Renderer[] cachedRenderers;

    private void Awake()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>();
    }

    public void TargetHighlight(bool isLook)
    {
        if (isLook)
        {
            ApplyHighlight();
        }
        else
        {
            RemoveHighlight();
        }
    }

    public void Interact(InteractionDetector player)
    {
        Debug.Log("TTT");
    }

    private void ApplyHighlight()
    {
        if (cachedRenderers == null || cachedRenderers.Length == 0) return;

        foreach (Renderer renderer in cachedRenderers)
        {
            if (renderer == null || !renderer.enabled) continue;

            Material material = renderer.material;

            if (!originalMaterials.ContainsKey(renderer))
            {
                originalMaterials[renderer] = new Material(material);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", highlightColor * highlightIntensity);
            }
        }
    }

    private void RemoveHighlight()
    {
        if (cachedRenderers == null || cachedRenderers.Length == 0) return;

        foreach (Renderer renderer in cachedRenderers)
        {
            if (renderer == null || !renderer.enabled) continue;

            if (originalMaterials.ContainsKey(renderer))
            {
                Material originalMaterial = originalMaterials[renderer];
                if (originalMaterial != null)
                {
                    renderer.material = originalMaterial;
                    renderer.material.DisableKeyword("_EMISSION");
                }
            }
        }
    }
}