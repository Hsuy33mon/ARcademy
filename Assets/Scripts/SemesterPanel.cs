using UnityEngine;
using TMPro;

public class SemesterPanel : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text header;
    public RectTransform contentRoot;   // ScrollView/Viewport/Content
    public SubjectRow rowPrefab;

    const int MinRows = 6;

    public void SetHeader(string text)
    {
        if (header) header.text = text;
    }

    public void Populate(CurriculumConfig.Semester sem)
    {
        // Clear old rows
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        int count = sem != null && sem.subjects != null ? sem.subjects.Count : 0;

        // Add actual subjects
        if (sem != null && sem.subjects != null)
        {
            foreach (var s in sem.subjects)
            {
                var row = Instantiate(rowPrefab, contentRoot);
                row.Bind(s.name, s.credits);
            }
        }

        // Pad to at least 6 rows
        for (int i = count; i < MinRows; i++)
        {
            var row = Instantiate(rowPrefab, contentRoot);
            row.BindPlaceholder();
        }
    }
}
