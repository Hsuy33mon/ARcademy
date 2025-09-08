using System;                          // ✅ needed for Array.IndexOf
using System.Collections.Generic;      // ✅ needed for List<T>
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

public class ProgramDetailsController : MonoBehaviour
{
    public CareerUiFlow flow;

    [Header("Program Panel")]
    public GameObject programPanel;
    public TMP_Text titleText;
    public TMP_Text bodyText;
    public Image heroImage;             // optional
    public QrFromUrlZxing qrMaker;      // <-- QrDisplay's component (outside ScrollView)

    [Header("Hide while open")]
    public GameObject descriptionPanel;
    public GameObject salaryPanel;

    public HandScrollControllerGeneric programScroll; // vertical scroller on ScrollView (optional)
    public HandScrollController menuBarScroll;        // your horizontal scroller (optional)
    public bool disableMenuScrollWhileOpen = true;

    [Header("Other UI")]
    public GameObject menuBarRoot;


    public void ToggleProgram() { if (programPanel.activeSelf) HideProgram(); else ShowProgram(); }

    public void ShowProgram()
    {
        var f = flow ? flow.CurrentFaculty : null;
        if (!f) return;

        if (titleText) titleText.text = f.displayName;
        if (bodyText) bodyText.text = string.IsNullOrWhiteSpace(f.programDetails) ? "Coming soon." : f.programDetails;
        if (heroImage) { heroImage.sprite = f.programHero; heroImage.enabled = (f.programHero != null); }
        if (qrMaker) qrMaker.SetUrl(f.programWebsiteUrl);

        if (descriptionPanel) descriptionPanel.SetActive(false);
        if (salaryPanel) salaryPanel.SetActive(false);
        if (programScroll) programScroll.enabled = true;
        if (disableMenuScrollWhileOpen && menuBarScroll) menuBarScroll.enabled = false;

        programPanel.SetActive(true);
        if (menuBarRoot) menuBarRoot.SetActive(false);

    }

    public void HideProgram()
    {
        if (programPanel) programPanel.SetActive(false);
        if (descriptionPanel) descriptionPanel.SetActive(true);
        if (salaryPanel) salaryPanel.SetActive(true);
        if (programScroll) programScroll.enabled = false;
        if (disableMenuScrollWhileOpen && menuBarScroll) menuBarScroll.enabled = true;
        if (menuBarRoot) menuBarRoot.SetActive(true);

    }
}
