using UnityEngine;

[CreateAssetMenu(menuName = "Camping/Tent Definition")]
public class TentDefinition : ScriptableObject
{
    [Header("Identity (type/model)")]
    public string tentId;   // model/type id, same for all instances of this tent kind

    [Header("Interior (choose one)")]
    public string interiorSceneName;          // if you load interiors by scene
    public GameObject interiorPrefab;         // or by prefab (keep null if unused)
    
    [Header("Return")]
    public Vector2 returnOffset = new Vector2(0f, -0.15f);
}
