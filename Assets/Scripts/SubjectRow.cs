using UnityEngine;
using TMPro;

public class SubjectRow : MonoBehaviour
{
    public TMP_Text subjectText;
    public TMP_Text creditsText;

    public void Bind(string subjectName, int credits)
    {
        if (subjectText) subjectText.text = subjectName;
        if (creditsText) creditsText.text = credits.ToString();
    }

    public void BindPlaceholder()
    {
        if (subjectText) subjectText.text = "—";
        if (creditsText) creditsText.text = "—";
    }
}
