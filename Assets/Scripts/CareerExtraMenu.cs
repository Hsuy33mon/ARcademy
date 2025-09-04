using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CareerExtraMenu : MonoBehaviour
{
    [Header("Flow/Data")]
    public CareerUiFlow flow;               // drag your CareerUiFlow here

    [Header("Base Panels (do not touch overlays)")]
    public GameObject descriptionPanel;
    public GameObject salaryPanel;

    [Header("Tuition UI")]
    public GameObject tuitionGroup;         // parent of 4 cards
    public TMP_Text year1Amount;
    public TMP_Text year2Amount;
    public TMP_Text year3Amount;
    public TMP_Text year4Amount;

    [Header("Tuition Button Icon Swap")]
    public Image tuitionBtnImage;           // Image on TuitionBtn
    public Sprite tuitionIcon;              // tuition icon
    public Sprite closeIcon;                // X icon

    bool tuitionOpen = false;

    // Call this from TuitionBtn -> HandDwellSelectable -> OnSelected
    public void ToggleTuition()
    {
        SetTuition(!tuitionOpen);
    }

    void SetTuition(bool open)
    {
        tuitionOpen = open;

        var f = flow ? flow.CurrentFaculty : null;
        if (open && f)
        {
            if (year1Amount) year1Amount.text = string.IsNullOrWhiteSpace(f.tuitionYear1) ? "—" : f.tuitionYear1;
            if (year2Amount) year2Amount.text = string.IsNullOrWhiteSpace(f.tuitionYear2) ? "—" : f.tuitionYear2;
            if (year3Amount) year3Amount.text = string.IsNullOrWhiteSpace(f.tuitionYear3) ? "—" : f.tuitionYear3;
            if (year4Amount) year4Amount.text = string.IsNullOrWhiteSpace(f.tuitionYear4) ? "—" : f.tuitionYear4;
        }

        if (tuitionGroup) tuitionGroup.SetActive(open);
        if (descriptionPanel) descriptionPanel.SetActive(!open);
        if (salaryPanel)      salaryPanel.SetActive(!open);

        if (tuitionBtnImage && tuitionIcon && closeIcon)
            tuitionBtnImage.sprite = open ? closeIcon : tuitionIcon;
    }
}
