using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CareerExtraMenu : MonoBehaviour
{
    [Header("Flow/Data")]
    public CareerUiFlow flow;               // optional: if it exposes currentFaculty
    public FacultyConfig defaultFaculty;    // fallback if flow is null or unset

    [Header("Base Panels")]
    public GameObject descriptionPanel;

    [Header("Curriculum Button Icon Swap")]
    public Image curriculumBtnImage;           
    public Sprite curriculumIcon;              
    public Sprite closeIcon;                   

    [Header("Year Grid")]
    public GameObject curriculumGroup;         // parent of 4 year cards (default OFF)
    public HandDwellSelectable[] yearButtons = new HandDwellSelectable[4];
    public Image[] yearImages = new Image[4];  // set your 1/2/3/4 images here (optional)

    [Header("Semester View")]
    public GameObject semesterGroup;           // parent of TopBar + 2 panels (default OFF)
    public TMP_Text semesterTitle;             // e.g., "Year 1 Curriculum"
    public SemesterPanel semester1Panel;
    public SemesterPanel semester2Panel;
    public HandDwellSelectable backButton;

    bool curriculumOpen = false;
    int currentYearIndex = -1; // 0..3

    // -------- Public hooks --------
    // Call this from CurriculumBtn -> HandDwellSelectable -> OnSelected
    public void ToggleCurriculum() => SetCurriculum(!curriculumOpen);

    // Call these from YearCard#N -> HandDwellSelectable -> OnSelected(int yearIndex1to4)
    public void OpenYear1() => OpenYear(0);
    public void OpenYear2() => OpenYear(1);
    public void OpenYear3() => OpenYear(2);
    public void OpenYear4() => OpenYear(3);

    // Back button
    public void GoBackToYears() => ShowYearGrid(true);

    // -------- Core --------
    void Awake()
    {
        // Optional safety: if you forgot to wire the yearButtons, try to auto-find
        if ((yearButtons == null || yearButtons.Length == 0) && curriculumGroup)
            yearButtons = curriculumGroup.GetComponentsInChildren<HandDwellSelectable>(true);

        // Wire Back if not set via Inspector
        if (backButton != null)
            backButton.onSelected.AddListener(GoBackToYears);

        // Start closed
        SetCurriculum(false);
    }

    void SetCurriculum(bool open)
    {
        curriculumOpen = open;

        if (curriculumGroup) curriculumGroup.SetActive(open);
        if (semesterGroup) semesterGroup.SetActive(false);

        if (curriculumBtnImage && curriculumIcon && closeIcon)
            curriculumBtnImage.sprite = open ? closeIcon : curriculumIcon;

        // keep your menu-bar hide:
        if (flow)
        {
            flow.SetMenuBarVisible(!open);
            if (flow.backButton) flow.backButton.gameObject.SetActive(!open);
            if (open) flow.SetDescriptionVisible(false);
            else flow.RefreshDescriptionVisibility();
            if (flow.bottomTitleCard) flow.bottomTitleCard.SetActive(!open);
            flow.SetProgramBtnEnabled(!open);
        }
        currentYearIndex = -1;
        
    }


    void ShowYearGrid(bool showYears)
    {
        if (curriculumGroup) curriculumGroup.SetActive(showYears);
        if (semesterGroup)  semesterGroup.SetActive(!showYears);
    }

    FacultyConfig GetActiveFaculty()
    {
        if (flow != null)
        {
            // 1) direct property (best)
            try { var cf = flow.CurrentFaculty; if (cf != null) return cf; } catch { }

            // 2) reflection: "CurrentFaculty" (property) or "currentFaculty" (field)
            var t = flow.GetType();
            var prop = t.GetProperty("CurrentFaculty");
            if (prop != null) { var v = prop.GetValue(flow) as FacultyConfig; if (v != null) return v; }
            var field = t.GetField("currentFaculty");
            if (field != null) { var v = field.GetValue(flow) as FacultyConfig; if (v != null) return v; }
        }
        return defaultFaculty;
    }

    public void OpenYear(int yearIndex) // 0..3
    {
        var fac = GetActiveFaculty();
        if (fac == null || fac.curriculum == null)
        {
            Debug.LogWarning("CareerExtraMenu: No Faculty or Curriculum assigned.");
            return;
        }

        if (yearIndex < 0 || yearIndex >= fac.curriculum.years.Length)
        {
            Debug.LogWarning("CareerExtraMenu: Year index out of range.");
            return;
        }

        currentYearIndex = yearIndex;

        // UI state
        ShowYearGrid(false);

        // Title
        if (semesterTitle)
            semesterTitle.text = $"Year {yearIndex + 1} Curriculum";

        // Panels
        var y = fac.curriculum.years[yearIndex];
        if (semester1Panel)
        {
            semester1Panel.SetHeader("Semester 1");
            semester1Panel.Populate(y.semester1);
        }
        if (semester2Panel)
        {
            semester2Panel.SetHeader("Semester 2");
            semester2Panel.Populate(y.semester2);
        }
    }
}
