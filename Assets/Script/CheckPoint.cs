using UnityEngine;

public class CheckPoint : MonoBehaviour
{
    public Color defaultColor = Color.white;
    public Color activeColor = Color.green;
    private Renderer _renderer;

    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _renderer.material.color = defaultColor;
    }

    public void SetActive(bool isActive)
    {
        _renderer.material.color = isActive ? activeColor : defaultColor;
    }
}