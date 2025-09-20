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
    public GameObject menuBarRoot;

    [Header("Info Cards")]
    public GameObject infoCard;
    public TMP_Text titleText;
    public TMP_Text descText;
    public TMP_Text bottomTitleText;
    public GameObject bottomTitleCard;
    // public TMP_Text averageSalaryText; 

    [Header("Overlays")]
    public Clothing2DOverlay shirtOverlay;
    public Glasses2DOverlay  glassesOverlay;
    public Helmet2DOverlay   helmetOverlay;
    public bool mirrorX = false;

    [Header("Description → Faculty Badge")]
    public GameObject descFacultyBadgeRoot; // FacultyBadge
    public TMP_Text descFacultyBadgeTitle;  // Title
    public Image descFacultyBadgeLogo;      // Logo
    public TMP_Text descFacultyBadgeName;   // Name

    [Header("Input Mode (temporary)")]
    public bool clickMode = true;             
    public HandScrollController handScroll; 

    enum Mode { Faculty, Career }
    Mode mode = Mode.Faculty;
    int currentFaculty = -1;
    int currentCareer  = -1;
    CareerConfig[] activeCareers = new CareerConfig[0];
    [Header("Page HUD")]
    public GameObject careerTopBar;
    public GameObject logoBtn;

    [Header("Description Panel (career details)")]
    public GameObject descriptionPanel;

    [Header("Top Action Buttons")]
    public GameObject curriculumBtnRoot;     // the Curriculum button root GO
    public GameObject programDetailsBtnRoot; // the Program Details button root GO

    public void SetCurriculumBtnEnabled(bool on)
    {
        SetButtonEnabled(curriculumBtnRoot, on);
    }
    public void SetProgramBtnEnabled(bool on)
    {
        SetButtonEnabled(programDetailsBtnRoot, on);
    }

    void UpdateDescFacultyBadge(FacultyConfig f)
    {
        if (!descFacultyBadgeRoot) return;

        bool show = (f != null);
        descFacultyBadgeRoot.SetActive(show);
        if (!show) return;

        if (descFacultyBadgeTitle)
            descFacultyBadgeTitle.text = "Programs you can join";

        if (descFacultyBadgeLogo)
        {
            descFacultyBadgeLogo.sprite = f.icon;
            descFacultyBadgeLogo.enabled = (f.icon != null);
            // If you want a fallback color/placeholder, set it here.
        }

        if (descFacultyBadgeName)
            descFacultyBadgeName.text = string.IsNullOrEmpty(f.displayName) ? f.facultyId : f.displayName;
    }

    void SetButtonEnabled(GameObject root, bool on)
    {
        if (!root) return;

        // Button click
        var btn = root.GetComponent<Button>();
        if (btn) btn.interactable = on;

        // Hand dwell
        var dwell = root.GetComponent<HandDwellSelectable>();
        if (dwell) dwell.enabled = on;

        // Block raycasts + optionally dim
        var cg = root.GetComponent<CanvasGroup>();
        if (!cg) cg = root.AddComponent<CanvasGroup>(); // safe add once
        cg.interactable = on;
        cg.blocksRaycasts = on;
        cg.alpha = on ? 1f : 0.5f; // dim when disabled (tweak if you want)
    }


    // Is a career currently selected?
    public bool HasSelectedCareer =>
        currentCareer >= 0 &&
        activeCareers != null &&
        currentCareer < activeCareers.Length;   

    public FacultyConfig CurrentFaculty
    {
        get
        {
            if (currentFaculty >= 0 && currentFaculty < faculties.Length)
                return faculties[currentFaculty];
            return null;
        }
    }

    // Set visibility directly
    public void SetDescriptionVisible(bool on)
    {
        if (descriptionPanel) descriptionPanel.SetActive(on);
    }

    // Decide automatically based on whether a career is selected
    public void RefreshDescriptionVisibility()
    {
        SetDescriptionVisible(HasSelectedCareer);
    }


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

    void SetLogoVisible(bool on)
    {
        if (logoBtn) logoBtn.SetActive(on);
    }

    // void OnSearchSubmitted(string query)
    // {
    //     if (menuBar) menuBar.gameObject.SetActive(true);

    //     string q = (query ?? "").Trim();
    //     if (string.IsNullOrEmpty(q)) { ShowFacultyList(); return; }

    //     // ---- collect faculties by (name/id) OR (career match) ----
    //     var filtered = new List<FacultyConfig>();
    //     var seen = new HashSet<FacultyConfig>();

    //     bool ContainsIC(string hay, string needle)
    //     {
    //         if (string.IsNullOrEmpty(needle)) return true;
    //         if (string.IsNullOrEmpty(hay)) return false;
    //         return hay.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    //     }

    //     // preserve the order of the master 'faculties' array
    //     foreach (var f in faculties)
    //     {
    //         bool byName = ContainsIC(f.displayName, q) || ContainsIC(f.facultyId, q);

    //         bool byCareer = false;
    //         if (f.careers != null)
    //         {
    //             foreach (var c in f.careers)
    //             {
    //                 if (ContainsIC(c.displayName, q)) { byCareer = true; break; }
    //             }
    //         }

    //         if (byName || byCareer)
    //         {
    //             if (seen.Add(f)) filtered.Add(f);
    //         }
    //     }

    //     // If any faculty matches (by name or via its careers) → show only those faculties
    //     if (filtered.Count > 0)
    //     {
    //         ShowFacultyListFiltered(filtered.ToArray());
    //         if (!clickMode) PrimeMenuCooldown(0.75f);
    //         return;
    //     }

    //     // Otherwise, as a fallback, show a flat career list (rare for your use case)
    //     var matchedCareers = new List<CareerConfig>();
    //     foreach (var f in faculties)
    //         if (f.careers != null)
    //             foreach (var c in f.careers)
    //                 if (!string.IsNullOrEmpty(c.displayName) &&
    //                     c.displayName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
    //                     matchedCareers.Add(c);

    //     if (matchedCareers.Count > 0)
    //     {
    //         mode = Mode.Career;
    //         if (backButton)
    //         {
    //             backButton.gameObject.SetActive(true);
    //             backButton.onClick.RemoveAllListeners();
    //             backButton.onClick.AddListener(() => ShowFacultyList());
    //         }

    //         activeCareers = matchedCareers.ToArray();   // ⬅️ important (see fix #2)
    //         BuildCareerButtons(activeCareers);
    //         if (!clickMode) PrimeMenuCooldown(0.75f);
    //     }
    //     else
    //     {
    //         ShowFacultyList();
    //     }
    //     if (careerTopBar) careerTopBar.SetActive(false);

    // }

    bool ContainsIC(string hay, string needle)
    {
        if (string.IsNullOrEmpty(needle)) return true;
        if (string.IsNullOrEmpty(hay)) return false;
        return hay.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }


    void OnSearchSubmitted(string query)
    {
        if (menuBar) menuBar.gameObject.SetActive(true);

        string q = (query ?? "").Trim();
        if (string.IsNullOrEmpty(q)) { ShowFacultyList(); return; }

        // case-insensitive "contains"
        bool ContainsIC(string hay, string needle)
        {
            if (string.IsNullOrEmpty(needle)) return true;
            if (string.IsNullOrEmpty(hay)) return false;
            return hay.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ---- ONLY faculties whose careers match the query ----
        var filtered = new List<FacultyConfig>();
        foreach (var f in faculties)
        {
            bool hasCareerMatch = false;
            if (f.careers != null)
            {
                foreach (var c in f.careers)
                {
                    if (!string.IsNullOrEmpty(c?.displayName) &&
                        ContainsIC(c.displayName, q))   // ← case-insensitive
                    {
                        hasCareerMatch = true;
                        break;
                    }
                }
            }
            if (hasCareerMatch) filtered.Add(f);
        }


        if (filtered.Count > 0)
        {
            ShowFacultyListFiltered(filtered.ToArray());
            if (!clickMode) PrimeMenuCooldown(0.75f);
            return;
        }

        // Nothing matched → show full faculty list (or keep previous results)
        ShowFacultyList();
        if (careerTopBar) careerTopBar.SetActive(false);
    }


    void ShowFacultyListFiltered(FacultyConfig[] filtered)
    {
        mode = Mode.Faculty;
        currentFaculty = -1;
        currentCareer = -1;

        BuildFacultyButtonsFiltered(filtered);
        if (backButton) backButton.gameObject.SetActive(false);

        if (infoCard) infoCard.SetActive(false);
        if (bottomTitleCard) bottomTitleCard.SetActive(false);
        SetOverlaysActive(false, false, false);
        if (careerTopBar) careerTopBar.SetActive(false);
        SetLogoVisible(true);
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
        ClearMenuBar();
        var content = menuBar.content;

        for (int i = 0; i < (list?.Length ?? 0); i++)
        {
            var cfg = list[i];
            var root = Instantiate(careerButtonPrefab, content);
            root.name = "Career_" + (cfg ? cfg.displayName : $"#{i}");

            // find label flexibly
            TMP_Text tmp = root.transform.Find("Label")?.GetComponent<TMP_Text>()
                           ?? root.GetComponentInChildren<TMP_Text>(true);
            if (tmp) tmp.text = cfg ? cfg.displayName : "Unnamed";

            // 🔧 the important change: find Button anywhere under the prefab
            var btn = root.GetComponentInChildren<Button>(true);
            if (!btn)
            {
                Debug.LogError($"{root.name} has no Button in children. Put a Button on the clickable GO.");
                continue;
            }

            int idxLocal = i;
            btn.onClick.AddListener(() => SelectCareer(idxLocal));

            // wire dwell on the clickable button object (not necessarily the root)
            ConfigureDwell(btn.gameObject, () => SelectCareer(idxLocal));
        }

        menuBar.horizontalNormalizedPosition = 0f;
        if (!clickMode) PrimeMenuCooldown(0.75f);
    }

    // void BuildCareerButtons(CareerConfig[] list)
    // {
    //     // activeCareers = list ?? Array.Empty<CareerConfig>();
    //     ClearMenuBar();
    //     var content = menuBar.content;

    //     for (int i = 0; i < list.Length; i++)
    //     {
    //         var cfg = list[i];
    //         var go  = Instantiate(careerButtonPrefab, content);
    //         go.name = "Career_" + cfg.displayName;

    //         var labelTr = go.transform.Find("Label");
    //         var tmp = labelTr ? labelTr.GetComponent<TMP_Text>() : null;
    //         var txt = labelTr ? labelTr.GetComponent<Text>()     : null;
    //         if (tmp) tmp.text = cfg.displayName;
    //         else if (txt) txt.text = cfg.displayName;
    //         else Debug.LogWarning("Career button prefab needs a child 'Label' with TMP_Text or Text.");

    //         int idx = i;
    //         go.GetComponent<Button>().onClick.AddListener(() => SelectCareer(idx));
    //         ConfigureDwell(go, () => SelectCareer(idx));
    //     }

    //     menuBar.horizontalNormalizedPosition = 0f;
    //     if (!clickMode) PrimeMenuCooldown(0.75f);
    // }

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
        currentCareer = -1;

        BuildFacultyButtons();
        if (backButton) backButton.gameObject.SetActive(false);

        if (infoCard) infoCard.SetActive(false);
        if (bottomTitleCard) bottomTitleCard.SetActive(false);
        SetOverlaysActive(false, false, false);
        if (careerTopBar) careerTopBar.SetActive(false);
        SetLogoVisible(true);
        SetDescriptionVisible(false);
        UpdateDescFacultyBadge(null);
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
        if (bottomTitleCard) bottomTitleCard.SetActive(false);
        SetOverlaysActive(false, false, false);
        SetLogoVisible(true);
        SetDescriptionVisible(false);
    }

    // ---------- SELECTIONS ----------
    public void SelectFaculty(int idx)
    {
        if (idx < 0 || idx >= faculties.Length) return;

        currentFaculty = idx;
        if (careerTopBar) careerTopBar.SetActive(true); 
        activeCareers  = faculties[idx].careers ?? new CareerConfig[0];
        UpdateDescFacultyBadge(faculties[idx]); 

        ShowCareerList();
        BuildCareerButtons(activeCareers);
    }

    public void SetMenuBarVisible(bool on)
    {
        if (!menuBar) return;
        menuBar.gameObject.SetActive(on);
        menuBarRoot.SetActive(on);

        // also disable dwell on menu buttons while hidden
        if (menuBar.content)
            foreach (var sel in menuBar.content.GetComponentsInChildren<HandDwellSelectable>(true))
                sel.enabled = on ? !clickMode : false;
    }

    public void SelectCareer(int idx)
    {
        if (activeCareers == null || idx < 0 || idx >= activeCareers.Length) return;
        currentCareer = idx;
        ApplyCareer(activeCareers[idx]);
        SetLogoVisible(true);
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
    // void ApplyCareer(CareerConfig cfg)
    // {
    //     if (infoCard) infoCard.SetActive(true);
    //     if (titleText) titleText.text = cfg.displayName;
    //     if (descText)  descText.text  = cfg.description;

    //     if (shirtOverlay && shirtOverlay.clothingRect)
    //     {
    //         var img = shirtOverlay.clothingRect.GetComponent<Image>();
    //         if (img) img.sprite = cfg.shirtSprite;
    //         shirtOverlay.widthMultiplier     = cfg.shirtWidthMultiplier;
    //         shirtOverlay.neckUpFromShoulders = cfg.shirtNeckUp;
    //         shirtOverlay.collarDown          = cfg.shirtCollarDown;
    //         shirtOverlay.gameObject.SetActive(cfg.shirtSprite != null);
    //     }
    //     if (glassesOverlay && glassesOverlay.glassesRect)
    //     {
    //         var img = glassesOverlay.glassesRect.GetComponent<Image>();
    //         if (img) img.sprite = cfg.glassesSprite;
    //         glassesOverlay.widthMultiplier = cfg.glassesWidthMultiplier;
    //         glassesOverlay.verticalOffset  = cfg.glassesVerticalOffset;
    //         glassesOverlay.gameObject.SetActive(cfg.glassesSprite != null);
    //     }
    //     if (helmetOverlay && helmetOverlay.helmetRect)
    //     {
    //         var img = helmetOverlay.helmetRect.GetComponent<Image>();
    //         if (img) img.sprite = cfg.helmetSprite;
    //         helmetOverlay.widthMultiplier = cfg.helmetWidthMultiplier;
    //         helmetOverlay.upFromEars      = cfg.helmetUpFromEars;
    //         // If your CareerConfig doesn't have this next field, remove the line:
    //         // helmetOverlay.crownDown       = cfg.helmetCrownDown;
    //         helmetOverlay.gameObject.SetActive(cfg.helmetSprite != null);
    //     }
    // }

    void ApplyCareer(CareerConfig cfg)
    {
        if (!cfg) return;

        if (infoCard) infoCard.SetActive(true);
        if (bottomTitleCard) bottomTitleCard.SetActive(true);

        // Title
        if (titleText)
            titleText.text = $"{cfg.displayName}";

        if (bottomTitleText)
        {
            bottomTitleText.text = $"I am a {cfg.displayName}";
        }


        // Description: original description + Average salary (if provided)
        string body = cfg.description ?? "";
        if (!string.IsNullOrWhiteSpace(cfg.averageSalary))
        {
            string region = string.IsNullOrWhiteSpace(cfg.salaryRegion) ? "Bangkok" : cfg.salaryRegion;
            body += $"\n\n<b>Average salary ({region})</b>: {cfg.averageSalary}";
        }
        if (descText)
            descText.text = body;


        // ---- overlays (unchanged) ----
        if (shirtOverlay && shirtOverlay.clothingRect)
        {
            var img = shirtOverlay.clothingRect.GetComponent<Image>();
            if (img) img.sprite = cfg.shirtSprite;
            shirtOverlay.widthMultiplier = cfg.shirtWidthMultiplier;
            shirtOverlay.neckUpFromShoulders = cfg.shirtNeckUp;
            shirtOverlay.collarDown = cfg.shirtCollarDown;
            shirtOverlay.gameObject.SetActive(cfg.shirtSprite != null);
        }
        if (glassesOverlay && glassesOverlay.glassesRect)
        {
            var img = glassesOverlay.glassesRect.GetComponent<Image>();
            if (img) img.sprite = cfg.glassesSprite;
            glassesOverlay.widthMultiplier = cfg.glassesWidthMultiplier;
            glassesOverlay.verticalOffset = cfg.glassesVerticalOffset;
            glassesOverlay.gameObject.SetActive(cfg.glassesSprite != null);
        }
        if (helmetOverlay && helmetOverlay.helmetRect)
        {
            var img = helmetOverlay.helmetRect.GetComponent<Image>();
            if (img) img.sprite = cfg.helmetSprite;
            helmetOverlay.widthMultiplier = cfg.helmetWidthMultiplier;
            helmetOverlay.upFromEars = cfg.helmetUpFromEars;
            // helmetOverlay.crownDown     = cfg.helmetCrownDown; // keep only if your overlay has this prop
            helmetOverlay.gameObject.SetActive(cfg.helmetSprite != null);
        }
        SetDescriptionVisible(true);
    }


    void SetOverlaysActive(bool shirt, bool glasses, bool helmet)
    {
        if (shirtOverlay)   shirtOverlay.gameObject.SetActive(shirt);
        if (glassesOverlay) glassesOverlay.gameObject.SetActive(glasses);
        if (helmetOverlay)  helmetOverlay.gameObject.SetActive(helmet);
    }
}
