using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Careers/Curriculum", fileName = "NewCurriculum")]
public class CurriculumConfig : ScriptableObject
{
    [System.Serializable]
    public class SubjectEntry
    {
        public string name;
        public int credits = 3;
    }

    [System.Serializable]
    public class Semester
    {
        public List<SubjectEntry> subjects = new List<SubjectEntry>();
    }

    [System.Serializable]
    public class YearCurriculum
    {
        public Semester semester1 = new Semester();
        public Semester semester2 = new Semester();
    }

    [Header("Optional link to faculty")]
    public string facultyId;

    [Header("Years 1..4")]
    public YearCurriculum[] years = new YearCurriculum[4]
    {
        new YearCurriculum(), new YearCurriculum(), new YearCurriculum(), new YearCurriculum()
    };
}
