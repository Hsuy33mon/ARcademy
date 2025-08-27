using System;                          // ✅ needed for Array.IndexOf
using System.Collections.Generic;      // ✅ needed for List<T>
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

public class CareerUiFlow : MonoBehaviour
{
    public VirtualKeyboardController keyboard;
    public HandDwellSelectable searchButton;

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
        if (shirtOverlay) { shirtOverlay.canvas = canvas; shirtOverlay.mirrorX = mirrorX; }
        if (glassesOverlay) { glassesOverlay.canvas = canvas; glassesOverlay.mirrorX = mirrorX; }
        if (helmetOverlay) { helmetOverlay.canvas = canvas; helmetOverlay.mirrorX = mirrorX; }

        clickMode = false;
        SetInputMode(false);
        ShowFacultyList();

        // --- Keyboard wiring ---
        if (keyboard)
        {
            // event-style (optional)
            keyboard.OnSubmit += OnSearchSubmitted;                 // ENTER from keyboard
            keyboard.OnClosed += () => menuBar.gameObject.SetActive(true);

            // dwell/select on the search icon → open (hides the menubar & shows keyboard)
            if (searchButton)
                searchButton.onSelected.AddListener(OpenSearch);    // ✅ call OpenSearch(), not Show()
        }
    }

    void OnDestroy()
    {
        if (keyboard) keyboard.OnSubmit -= OnSearchSubmitted;
    }

    public void OpenSearch()
    {
        if (menuBar) menuBar.gameObject.SetActive(false);
        keyboard.Show(OnSearchSubmitted, () => { if (menuBar) menuBar.gameObject.SetActive(true); });
    }

    void OnSearchSubmitted(string query)
    {
        if (menuBar) menuBar.gameObject.SetActive(true);

        string q = (query ?? "").Trim();
        if (string.IsNullOrEmpty(q)) { ShowFacultyList(); return; }

        // ---- collect faculties by (name/id) OR (career match) ----
        var filtered = new List<FacultyConfig>();
        var seen = new HashSet<FacultyConfig>();

        bool ContainsIC(string hay, string needle)
        {
            if (string.IsNullOrEmpty(needle)) return true;
            if (string.IsNullOrEmpty(hay)) return false;
            return hay.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // preserve the order of the master 'faculties' array
        foreach (var f in faculties)
        {
            bool byName = ContainsIC(f.displayName, q) || ContainsIC(f.facultyId, q);

            bool byCareer = false;
            if (f.careers != null)
            {
                foreach (var c in f.careers)
                {
                    if (ContainsIC(c.displayName, q)) { byCareer = true; break; }
                }
            }

            if (byName || byCareer)
            {
                if (seen.Add(f)) filtered.Add(f);
            }
        }

        // If any faculty matches (by name or via its careers) → show only those faculties
        if (filtered.Count > 0)
        {
            ShowFacultyListFiltered(filtered.ToArray());
            if (!clickMode) PrimeMenuCooldown(0.75f);
            return;
        }

        // Otherwise, as a fallback, show a flat career list (rare for your use case)
        var matchedCareers = new List<CareerConfig>();
        foreach (var f in faculties)
            if (f.careers != null)
                foreach (var c in f.careers)
                    if (!string.IsNullOrEmpty(c.displayName) &&
                        c.displayName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                        matchedCareers.Add(c);

        if (matchedCareers.Count > 0)
        {
            mode = Mode.Career;
            if (backButton)
            {
                backButton.gameObject.SetActive(true);
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(() => ShowFacultyList());
            }

            activeCareers = matchedCareers.ToArray();   // ⬅️ important (see fix #2)
            BuildCareerButtons(activeCareers);
            if (!clickMode) PrimeMenuCooldown(0.75f);
        }
        else
        {
            ShowFacultyList();
        }
    }

    void ShowFacultyListFiltered(FacultyConfig[] filtered)
    {
        mode = Mode.Faculty;
        currentFaculty = -1;
        currentCareer  = -1;

        BuildFacultyButtonsFiltered(filtered);
        if (backButton) backButton.gameObject.SetActive(false);

        if (infoCard)  infoCard.SetActive(false);
        if (salaryCard) salaryCard.SetActive(false);
        SetOverlaysActive(false, false, false);
    }

    void BuildFacultyButtonsFiltered(FacultyConfig[] list)
    {
        ClearMenuBar();
        var content = menuBar.content;

        for (int i = 0; i < list.Length; i++)
        {
            var f = list[i];
            var go = Instantiate(facultyButtonPrefab, content);
            go.name = "Faculty_" + (string.IsNullOrEmpty(f.displayName) ? f.facultyId : f.displayName);

            var iconTr = go.transform.Find("Icon") ?? go.transform.Find("icon");
            if (iconTr)
            {
                var iconImg = iconTr.GetComponent<Image>();
                if (iconImg)
                {
                    iconImg.sprite = f.icon;
                    iconImg.color = Color.white;
                    iconImg.enabled = (iconImg.sprite != null);
                    iconImg.raycastTarget = false;
                }
            }

            int idxLocal = Array.IndexOf(faculties, f); // map back to master index
            go.GetComponent<Button>().onClick.AddListener(() => SelectFaculty(idxLocal));
            ConfigureDwell(go, () => SelectFaculty(idxLocal));
        }

        menuBar.horizontalNormalizedPosition = 0f;
    }

    public void SetInputMode(bool useClick)
    {
        clickMode = useClick;
        if (handScroll) handScroll.enabled = !useClick;
        Cursor.visible = useClick;

        if (menuBar && menuBar.content)
        {
            foreach (var sel in menuBar.content.GetComponentsInChildren<HandDwellSelectable>(true))
                sel.enabled = !clickMode;
        }
    }

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

            var iconTr = go.transform.Find("Icon") ?? go.transform.Find("icon");
            if (iconTr)
            {
                var iconImg = iconTr.GetComponent<Image>();
                if (iconImg)
                {
                    iconImg.color = Color.white;
                    iconImg.sprite = f.icon;
                    iconImg.enabled = (iconImg.sprite != null);
                    iconImg.raycastTarget = false;
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
        // activeCareers = list ?? Array.Empty<CareerConfig>();
        ClearMenuBar();
        var content = menuBar.content;

        for (int i = 0; i < list.Length; i++)
        {
            var cfg = list[i];
            var go  = Instantiate(careerButtonPrefab, content);
            go.name = "Career_" + cfg.displayName;

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

        sel.requireHandDetected = true;
        sel.allowPointerAsPresence = false;
        sel.onlyPalm = true;
        sel.pinchDebounceSec = 0.20f;

        sel.palmDelaySeconds = 3.0f;           // ring shows only after 3s steady palm
        sel.resetProgressOnPinch = true;       // pinch for scrolling won't contribute

        sel.dwellSeconds = 2.0f;               // fill time AFTER the 3s delay
        sel.resetSeconds = 2.0f;

        sel.enterPaddingPx = 60f;
        sel.exitPaddingPx  = 40f;
        sel.graceSeconds   = 0.40f;
        sel.requireEnterFromOutside = true;
        sel.spawnBlockSeconds = 0.75f;

        if (!sel.progressRing)
        {
            var tr = go.transform.Find("ring") ?? go.transform.Find("Ring");
            if (tr) sel.progressRing = tr.GetComponent<Image>();
        }

        sel.onSelected.RemoveAllListeners();
        sel.onSelected.AddListener(onSelect);
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

        sel.enabled = !clickMode;
        sel.body = body ? body : FindAnyObjectByType<BodyDataReceiver>();
        sel.canvas = canvas;
        sel.mirrorX = mirrorX;

        sel.viewport = null; // back button often outside scroll view

        sel.requireHandDetected = true;
        sel.allowPointerAsPresence = false;
        sel.onlyPalm = true;
        sel.pinchDebounceSec = 0.20f;

        sel.palmDelaySeconds = 3.0f;
        sel.resetProgressOnPinch = true;

        sel.dwellSeconds = 2.0f;
        sel.resetSeconds = 2.0f;

        sel.enterPaddingPx = 60f;
        sel.exitPaddingPx = 40f;
        sel.graceSeconds = 0.40f;
        sel.requireEnterFromOutside = true;
        sel.spawnBlockSeconds = 0.75f;

        if (!sel.progressRing)
        {
            var tr = backButton.transform.Find("ring") ?? backButton.transform.Find("Ring");
            if (tr) sel.progressRing = tr.GetComponent<Image>();
        }

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
            // If your CareerConfig doesn't have this next field, remove the line:
            // helmetOverlay.crownDown       = cfg.helmetCrownDown;
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
