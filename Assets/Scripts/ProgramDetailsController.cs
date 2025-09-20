using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ProgramDetailsController : MonoBehaviour
{
    public CareerUiFlow flow;

    [Header("Program Panel")]
    public GameObject programPanel;
    public TMP_Text titleText;
    public TMP_Text bodyText;
    public Image heroImage;
    public QrFromUrlZxing qrMaker;

    [Header("Hide while open")]
    public GameObject descriptionPanel;
    public GameObject salaryPanel;

    public HandScrollControllerGeneric programScroll;
    public HandScrollController menuBarScroll;
    public bool disableMenuScrollWhileOpen = true;

    [Header("Other UI")]
    public GameObject menuBarRoot;

    [Header("Program Button Icon Swap")]
    public Image programBtnImage;
    public Sprite programIcon;
    public Sprite closeIcon;

    bool programOpen = false;

    public void ToggleProgram() => SetProgram(!programOpen);

    public void SetProgram(bool open)
    {
        programOpen = open;

        if (open) ShowProgram();
        else      HideProgram();

        // icon swap
        if (programBtnImage && programIcon && closeIcon)
            programBtnImage.sprite = open ? closeIcon : programIcon;

        // menu bar + back button visibility
        if (flow)
        {
            flow.SetMenuBarVisible(!open);
            if (flow.backButton) flow.backButton.gameObject.SetActive(!open);
            flow.SetCurriculumBtnEnabled(!open);
        }
    }

    void ShowProgram()
    {
        var f = flow ? flow.CurrentFaculty : null;
        if (!f) return;

        if (titleText) titleText.text = f.displayName;
        if (bodyText)  bodyText.text  = string.IsNullOrWhiteSpace(f.programDetails) ? "Coming soon." : f.programDetails;
        if (heroImage) { heroImage.sprite = f.programHero; heroImage.enabled = (f.programHero != null); }
        if (qrMaker)   qrMaker.SetUrl(f.programWebsiteUrl);

        if (descriptionPanel) descriptionPanel.SetActive(false);
        if (salaryPanel)      salaryPanel.SetActive(false);
        if (programScroll)    programScroll.enabled = true;
        if (disableMenuScrollWhileOpen && menuBarScroll) menuBarScroll.enabled = false;

        if (menuBarRoot) menuBarRoot.SetActive(false);
        if (flow)
        {
            flow.SetDescriptionVisible(false);
            if (flow.bottomTitleCard) flow.bottomTitleCard.SetActive(false);
            
        }

        programPanel.SetActive(true);
    }

    void HideProgram()
    {
        if (programPanel) programPanel.SetActive(false);

        // don't force description on; let flow decide based on selection
        if (flow)
        {
            flow.RefreshDescriptionVisibility();
            if (flow.bottomTitleCard) flow.bottomTitleCard.SetActive(true);
        } 

        if (salaryPanel)      salaryPanel.SetActive(true);
        if (programScroll)    programScroll.enabled = false;
        if (disableMenuScrollWhileOpen && menuBarScroll) menuBarScroll.enabled = true;
        if (menuBarRoot)      menuBarRoot.SetActive(true);
    }
}
