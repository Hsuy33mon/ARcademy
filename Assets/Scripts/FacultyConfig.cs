using UnityEngine;

[CreateAssetMenu(menuName = "Careers/Faculty", fileName = "NewFaculty")]
public class FacultyConfig : ScriptableObject
{
    [Header("Identity")]
    public string facultyId;
    public string displayName;

    [Header("Visual (optional)")]
    public Sprite icon;

    [Header("Careers in this faculty")]
    public CareerConfig[] careers;
}
