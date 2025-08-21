using UnityEngine;

[CreateAssetMenu(menuName = "Careers/Career", fileName = "NewCareer")]
public class CareerConfig : ScriptableObject
{
    [Header("Display")]
    public string displayName;
    [TextArea(2, 100)] public string description;

    [Header("Salary (optional)")]
    public string salaryRegion = "Bangkok";
    [TextArea] public string salaryEntry;
    [TextArea] public string salaryMid;
    [TextArea] public string salarySenior;


    [Header("Sprites (2D overlays)")]
    public Sprite shirtSprite;
    public Sprite glassesSprite;
    public Sprite helmetSprite;

    [Header("Overlay Tuning (optional)")]
    public float shirtWidthMultiplier = 4.5f;
    public float shirtNeckUp = 0.3f;
    public float shirtCollarDown = 0.05f;

    public float glassesWidthMultiplier = 3.2f;
    public float glassesVerticalOffset = 0.08f;

    public float helmetWidthMultiplier = 3.7f;
    public float helmetUpFromEars = 0.1f;
    public float helmetCrownDown = 0.04f;
}
