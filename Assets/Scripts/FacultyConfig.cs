using UnityEngine;

[CreateAssetMenu(menuName = "Careers/Faculty", fileName = "NewFaculty")]
public class FacultyConfig : ScriptableObject
{
    [Header("Identity")]
    public string facultyId;
    public string displayName;

    [Header("Visual ")]
    public Sprite icon;

    [Header("Careers in this faculty")]
    public CareerConfig[] careers;

    [Header("Program Details (per faculty)")]
    [TextArea(4,10)] public string programDetails;
    public Sprite programHero;

    [Header("Tuition Fees (per faculty)")]
    public string tuitionYear1;
    public string tuitionYear2;
    public string tuitionYear3;
    public string tuitionYear4;
}
