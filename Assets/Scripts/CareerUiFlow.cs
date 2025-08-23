using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

public class CareerUiFlow : MonoBehaviour
{
    [Header("Data")]
    public FacultyConfig[] faculties;
    public BodyDataReceiver body;              // drag your BodyDataReceiver here

    [Header("Canvas")]
    public Canvas canvas;

    [Header("Menu Bar (single ScrollRect)")]
    public ScrollRect menuBar;
    public GameObject facultyButtonPrefab;     // root has Button+Image; child "Icon" Image; optional child "ring"
    public GameObject careerButtonPrefab;      // root has Button+Image; child "Label" TMP_Text; optional child "ring"
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
    public bool clickMode = true;             
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

        clickMode = false;  
        SetInputMode(false);
        ShowFacultyList();
    }

    public void SetInputMode(bool useClick)
    {
        clickMode = useClick;
        if (handScroll) handScroll.enabled = !useClick;
        Cursor.visible = useClick;

        // If you toggle modes at runtime and buttons already exist,
        // update all HandDwellSelectable components:
        if (menuBar && menuBar.content)
        {
            foreach (var sel in menuBar.content.GetComponentsInChildren<HandDwellSelectable>(true))
                sel.enabled = !clickMode;
        }
    }

    // Prevent instant auto-select when a menu appears
    void PrimeMenuCooldown(float seconds)
    {
        if (!menuBar || !menuBar.content) return;
        foreach (var sel in menuBar.content.GetComponentsInChildren<HandDwellSelectable>(true))
            sel.StartCooldown(seconds);
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
            var f  = faculties[i];
            var go = Instantiate(facultyButtonPrefab, content);
            go.name = "Faculty_" + (string.IsNullOrEmpty(f.displayName) ? f.facultyId : f.displayName);

            // set ICON on child "Icon"
            var iconTr = go.transform.Find("Icon") ?? go.transform.Find("icon");
            if (iconTr)
            {
                var iconImg = iconTr.GetComponent<Image>();
                if (iconImg)
                {
                    iconImg.color = Color.white;
                    iconImg.sprite = f.icon;
                    iconImg.enabled = (iconImg.sprite != null);
                    iconImg.raycastTarget = false; // icon must not block clicks/dwell
                }
            }
            else
            {
                Debug.LogWarning("FacultyButton prefab is missing a child named 'Icon'.");
            }

            int idx = i;
            go.GetComponent<Button>().onClick.AddListener(() => SelectFaculty(idx));
            ConfigureDwell(go, () => SelectFaculty(idx));
        }

        menuBar.horizontalNormalizedPosition = 0f;
        if (!clickMode) PrimeMenuCooldown(0.75f);
    }

    void BuildCareerButtons(CareerConfig[] list)
    {
        ClearMenuBar();
        var content = menuBar.content;

        for (int i = 0; i < list.Length; i++)
        {
            var cfg = list[i];
            var go  = Instantiate(careerButtonPrefab, content);
            go.name = "Career_" + cfg.displayName;

            // label (TMP or legacy Text) on child "Label"
            var labelTr = go.transform.Find("Label");
            var tmp = labelTr ? labelTr.GetComponent<TMP_Text>() : null;
            var txt = labelTr ? labelTr.GetComponent<Text>()     : null;
            if (tmp) tmp.text = cfg.displayName;
            else if (txt) txt.text = cfg.displayName;
            else Debug.LogWarning("Career button prefab needs a child 'Label' with TMP_Text or Text.");

            int idx = i;
            go.GetComponent<Button>().onClick.AddListener(() => SelectCareer(idx));
            ConfigureDwell(go, () => SelectCareer(idx));
        }

        menuBar.horizontalNormalizedPosition = 0f;
        if (!clickMode) PrimeMenuCooldown(0.75f);
    }

    // Wire per-button dwell
    void ConfigureDwell(GameObject go, UnityAction onSelect)
    {
        var sel = go.GetComponent<HandDwellSelectable>();
        if (!sel) return;

        sel.enabled = !clickMode;
        sel.body = body;
        sel.canvas = canvas;
        sel.viewport = menuBar ? menuBar.viewport : null;
        sel.mirrorX = mirrorX;

        // -------- FIX 1: don't require explicit hand flags ----------
        // Your BodyDataReceiver doesn't publish left/right hand booleans,
        // so let pointer presence count as presence.
        sel.requireHandDetected = false;   // <— turn this OFF
        sel.allowPointerAsPresence = true;   // <— leave this ON as fallback

        // Keep palm-only dwell (no pinch) and give it a bit of debounce
        sel.onlyPalm = true;
        sel.pinchDebounceSec = 0.12f;

        // A more forgiving hitbox and grace
        sel.enterPaddingPx = 60f;   // <— bigger = easier to enter
        sel.exitPaddingPx = 40f;   // <— hysteresis so it won’t flicker
        sel.graceSeconds = 0.40f; // <— brief off-screen wiggle won’t reset

        // 2 sec dwell (or whatever you want)
        sel.dwellSeconds = 2.0f;

        // -------- FIX 2: find "ring" OR "Ring" ----------
        if (!sel.progressRing)
        {
            var tr = go.transform.Find("ring") ?? go.transform.Find("Ring");
            if (tr) sel.progressRing = tr.GetComponent<Image>();
        }

        sel.requireHandDetected = true;        // now that BodyDataReceiver exposes presence
        sel.allowPointerAsPresence = false;

        sel.palmDelaySeconds = 2.0f;           // <-- your requirement
        sel.resetProgressOnPinch = true;       // scrolling never contributes

        sel.onlyPalm = true;                    // ignore pinch for dwell
        sel.enterPaddingPx = 60f;
        sel.exitPaddingPx  = 40f;
        sel.graceSeconds   = 0.40f;

        sel.dwellSeconds = 2.0f;               // fill time AFTER the 3s palm delay
        sel.resetSeconds = 2.0f;

        sel.requireEnterFromOutside = true;
        sel.spawnBlockSeconds = 0.75f;
    }


    // ---------- VIEW SWITCHES ----------
    void ShowFacultyList()
    {
        mode = Mode.Faculty;
        currentFaculty = -1;
        currentCareer  = -1;

        BuildFacultyButtons();
        if (backButton) backButton.gameObject.SetActive(false);

        if (infoCard)  infoCard.SetActive(false);
        if (salaryCard) salaryCard.SetActive(false);
        SetOverlaysActive(false, false, false);
    }

    void ShowCareerList()
    {
        mode = Mode.Career;

        if (backButton)
        {
            backButton.gameObject.SetActive(true);
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(() => ShowFacultyList());

            ConfigureBackButtonDwell();
            backButton.GetComponent<HandDwellSelectable>()?.StartCooldown(0.75f);
        }

        if (infoCard) infoCard.SetActive(false);
        if (salaryCard) salaryCard.SetActive(false);
        SetOverlaysActive(false, false, false);
        
        backButton.GetComponent<HandDwellSelectable>()?.StartCooldown(0.75f);
    }

    // ---------- SELECTIONS ----------
    public void SelectFaculty(int idx)
    {
        if (idx < 0 || idx >= faculties.Length) return;

        currentFaculty = idx;
        activeCareers  = faculties[idx].careers ?? new CareerConfig[0];

        ShowCareerList();
        BuildCareerButtons(activeCareers);
    }

    public void SelectCareer(int idx)
    {
        if (activeCareers == null || idx < 0 || idx >= activeCareers.Length) return;
        currentCareer = idx;
        ApplyCareer(activeCareers[idx]);
    }

    void ConfigureBackButtonDwell()
    {
        if (!backButton) return;

        var sel = backButton.GetComponent<HandDwellSelectable>();
        if (!sel) sel = backButton.gameObject.AddComponent<HandDwellSelectable>();

        sel.enabled = !clickMode;                           // dwell only when not in click mode
        sel.body = body ? body : FindAnyObjectByType<BodyDataReceiver>();
        sel.canvas = canvas;
        sel.mirrorX = mirrorX;

        // IMPORTANT: back button typically sits outside the scroll viewport → leave null
        sel.viewport = null;

        // Make it behave like your menu items:
        sel.requireHandDetected = true;   // use BodyDataReceiver.HandPresentWithGrace(...)
        sel.allowPointerAsPresence = false;
        sel.onlyPalm = true;
        sel.pinchDebounceSec = 0.20f;

        // Palm must be steady here BEFORE the ring even appears:
        sel.palmDelaySeconds = 3.0f;   // ⬅️ your requirement
        sel.resetProgressOnPinch = true;

        // After the 3s palm delay, ring fills for:
        sel.dwellSeconds = 2.0f;   // match your items
        sel.resetSeconds = 2.0f;

        // Stability/anti-auto-select (same as items)
        sel.enterPaddingPx = 60f;
        sel.exitPaddingPx = 40f;
        sel.graceSeconds = 0.40f;
        sel.requireEnterFromOutside = true;
        sel.spawnBlockSeconds = 0.75f;

        // Auto-find the ring (works for "ring" OR "Ring")
        if (!sel.progressRing)
        {
            var tr = backButton.transform.Find("ring") ?? backButton.transform.Find("Ring");
            if (tr) sel.progressRing = tr.GetComponent<Image>();
        }

        // What happens on select: call the normal button click (which we wire to ShowFacultyList)
        sel.onSelected.RemoveAllListeners();
        sel.onSelected.AddListener(() => backButton.onClick.Invoke());
    }




    // ---------- APPLY DATA ----------
    void ApplyCareer(CareerConfig cfg)
    {
        if (infoCard) infoCard.SetActive(true);
        if (titleText) titleText.text = cfg.displayName;
        if (descText)  descText.text  = cfg.description;

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
