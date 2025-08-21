using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CareerUiFlow : MonoBehaviour
{
    [Header("Data")]
    public FacultyConfig[] faculties;

    [Header("Canvas")]
    public Canvas canvas;

    [Header("Menu Bar (single ScrollRect)")]
    public ScrollRect menuBar;
    public GameObject facultyButtonPrefab;
    public GameObject careerButtonPrefab;
    public Button backButton;

    [Header("Info Cards")]
    public GameObject infoCard;
    public TMP_Text titleText;
    public TMP_Text descText;
    public GameObject salaryCard;
    public TMP_Text salaryTitle;
    public TMP_Text salaryEntryValue;
    public TMP_Text salaryMidValue;
    public TMP_Text salarySeniorValue;

    [Header("Overlays")]
    public Clothing2DOverlay shirtOverlay;
    public Glasses2DOverlay  glassesOverlay;
    public Helmet2DOverlay   helmetOverlay;
    public bool mirrorX = false;

    [Header("Input Mode (temporary)")]
    public bool clickMode = true;                // set true to use mouse clicks
    public HandPalmDwellClick dwell;
    public HandScrollController handScroll;

    enum Mode { Faculty, Career }
    Mode mode = Mode.Faculty;
    int currentFaculty = -1;
    int currentCareer  = -1;
    CareerConfig[] activeCareers = new CareerConfig[0];

    void Start()
    {
        // overlays know canvas + mirror
        if (shirtOverlay)  { shirtOverlay.canvas = canvas;   shirtOverlay.mirrorX = mirrorX; }
        if (glassesOverlay){ glassesOverlay.canvas = canvas; glassesOverlay.mirrorX = mirrorX; }
        if (helmetOverlay) { helmetOverlay.canvas = canvas;  helmetOverlay.mirrorX = mirrorX; }

        SetInputMode(clickMode);
        ShowFacultyList();
    }

    public void SetInputMode(bool useClick)
    {
        clickMode = useClick;
        if (dwell) dwell.enabled = !useClick;  // disable palm-dwell
        if (handScroll) handScroll.enabled = !useClick;  // disable pinch scroll
    }

    // ---------- UI BUILDERS ----------
    void ClearMenuBar()
    {
        var content = menuBar.content;
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
    }

    void BuildFacultyButtons()
{
    ClearMenuBar();
    var content = menuBar.content;

    for (int i = 0; i < faculties.Length; i++)
    {
        var f = faculties[i];
        var go = Instantiate(facultyButtonPrefab, content);
        go.name = "Faculty_" + (string.IsNullOrEmpty(f.displayName) ? f.facultyId : f.displayName);

        // ----- set the ICON on the child named "Icon" (NOT on the root) -----
        var iconTr = go.transform.Find("Icon") ?? go.transform.Find("icon");
        if (iconTr)
        {
            var iconImg = iconTr.GetComponent<Image>();
            if (iconImg)
            {
                iconImg.color   = Color.white;
                iconImg.sprite  = f.icon;
                iconImg.enabled = (iconImg.sprite != null);
                iconImg.rectTransform.SetAsLastSibling();
            }
        }
        else
        {
            Debug.LogWarning("FacultyButton prefab is missing a child named 'Icon'.");
        }

        // Hook up click
        int idx = i;
        go.GetComponent<Button>().onClick.AddListener(() => SelectFaculty(idx));
    }

    menuBar.horizontalNormalizedPosition = 0f;
}


  void BuildCareerButtons(CareerConfig[] list)
{
    ClearMenuBar();
    var content = menuBar.content;

    Debug.Log($"[CareerUiFlow] Building {list.Length} career buttons for {faculties[currentFaculty].displayName}");

    for (int i = 0; i < list.Length; i++)
    {
        var cfg = list[i];
        var go = Instantiate(careerButtonPrefab, content);
        go.name = "Career_" + cfg.displayName;

        // Label can be TMP_Text or Text
        var labelTr = go.transform.Find("Label");
        var tmp = labelTr ? labelTr.GetComponent<TMPro.TMP_Text>() : null;
        var txt = labelTr ? labelTr.GetComponent<UnityEngine.UI.Text>() : null;
        if (tmp) tmp.text = cfg.displayName;
        else if (txt) txt.text = cfg.displayName;

        // make sure it's visible
        go.SetActive(true);

        int idx = i;
        go.GetComponent<Button>().onClick.AddListener(() => SelectCareer(idx));
    }

    menuBar.horizontalNormalizedPosition = 0f;

    // if you use hand dwell/palm click, rebind the click root:
    var dwell = FindObjectOfType<HandPalmDwellClick>();
    if (dwell) dwell.SetClickRoot(menuBar.content);
}



    // ---------- VIEW SWITCHES ----------
    void ShowFacultyList()
    {
        mode = Mode.Faculty;
        currentFaculty = -1;
        currentCareer  = -1;

        // bar shows faculties
        BuildFacultyButtons();
        if (backButton) backButton.gameObject.SetActive(false);

        // hide cards & overlays
        if (infoCard)  infoCard.SetActive(false);
        if (salaryCard) salaryCard.SetActive(false);
        SetOverlaysActive(false, false, false);

        // for hand gestures
        // FindObjectOfType<HandUIInteractor>()?.RefreshTargets();
    }

    void ShowCareerList()
    {
        mode = Mode.Career;
        if (backButton)
        {
            backButton.gameObject.SetActive(true);
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(() => ShowFacultyList());
        }

        // cards hidden until a career is chosen
        if (infoCard)  infoCard.SetActive(false);
        if (salaryCard) salaryCard.SetActive(false);
        SetOverlaysActive(false, false, false);
    }

    // ---------- SELECTIONS ----------
    public void SelectFaculty(int idx)
    {
        if (idx < 0 || idx >= faculties.Length) return;
        currentFaculty = idx;
        activeCareers = faculties[idx].careers ?? new CareerConfig[0];

        ShowCareerList();
        BuildCareerButtons(activeCareers);

        // Optional: auto-select the first career
        // if (activeCareers.Length > 0) SelectCareer(0);
    }

    public void SelectCareer(int idx)
    {
        if (activeCareers == null || idx < 0 || idx >= activeCareers.Length) return;
        currentCareer = idx;
        ApplyCareer(activeCareers[idx]);
    }

    // ---------- APPLY DATA ----------
    void ApplyCareer(CareerConfig cfg)
    {
        // Info card
        if (infoCard) infoCard.SetActive(true);
        if (titleText) titleText.text = cfg.displayName;
        if (descText)  descText.text  = cfg.description;

        // Salary card
        bool hasAnySalary = !string.IsNullOrWhiteSpace(cfg.salaryEntry)
                         || !string.IsNullOrWhiteSpace(cfg.salaryMid)
                         || !string.IsNullOrWhiteSpace(cfg.salarySenior);

        if (salaryCard) salaryCard.SetActive(hasAnySalary);
        if (hasAnySalary)
        {
            if (salaryTitle)      salaryTitle.text      = $"Average Salary in {cfg.salaryRegion}";
            if (salaryEntryValue) salaryEntryValue.text = cfg.salaryEntry  ?? "";
            if (salaryMidValue)   salaryMidValue.text   = cfg.salaryMid    ?? "";
            if (salarySeniorValue)salarySeniorValue.text= cfg.salarySenior ?? "";
        }

        // Overlays
        if (shirtOverlay && shirtOverlay.clothingRect)
        {
            var img = shirtOverlay.clothingRect.GetComponent<Image>();
            if (img) img.sprite = cfg.shirtSprite;
            shirtOverlay.widthMultiplier     = cfg.shirtWidthMultiplier;
            shirtOverlay.neckUpFromShoulders = cfg.shirtNeckUp;
            shirtOverlay.collarDown          = cfg.shirtCollarDown;
            shirtOverlay.gameObject.SetActive(cfg.shirtSprite != null);
        }
        if (glassesOverlay && glassesOverlay.glassesRect)
        {
            var img = glassesOverlay.glassesRect.GetComponent<Image>();
            if (img) img.sprite = cfg.glassesSprite;
            glassesOverlay.widthMultiplier = cfg.glassesWidthMultiplier;
            glassesOverlay.verticalOffset  = cfg.glassesVerticalOffset;
            glassesOverlay.gameObject.SetActive(cfg.glassesSprite != null);
        }
        if (helmetOverlay && helmetOverlay.helmetRect)
        {
            var img = helmetOverlay.helmetRect.GetComponent<Image>();
            if (img) img.sprite = cfg.helmetSprite;
            helmetOverlay.widthMultiplier = cfg.helmetWidthMultiplier;
            helmetOverlay.upFromEars      = cfg.helmetUpFromEars;
            helmetOverlay.crownDown       = cfg.helmetCrownDown;
            helmetOverlay.gameObject.SetActive(cfg.helmetSprite != null);
        }
    }

    void SetOverlaysActive(bool shirt, bool glasses, bool helmet)
    {
        if (shirtOverlay)   shirtOverlay.gameObject.SetActive(shirt);
        if (glassesOverlay) glassesOverlay.gameObject.SetActive(glasses);
        if (helmetOverlay)  helmetOverlay.gameObject.SetActive(helmet);
    }
}
