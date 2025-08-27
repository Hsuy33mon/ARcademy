using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VirtualKeyboardController : MonoBehaviour
{
    [Header("Layout")]
    public Vector2 keySize = new Vector2(110, 80);
    public Vector2 wideKeySize = new Vector2(220, 80);

    [Header("Wiring")]
    public BodyDataReceiver body;     // drag your BodyDataReceiver
    public Canvas canvas;             // Overlay canvas
    public RectTransform viewport;    // usually KeyboardPanel's RectTransform
    public bool mirrorX = true;

    [Header("UI")]
    public TMP_Text output;           // Header/OutputBox/Output
    public Transform row1, row2, row3, row4;     // empty row containers

    [Header("Prefabs")]
    public GameObject keyPrefab;      // normal key (Button + TMP child "Label")
    public GameObject keyWidePrefab;  // wide key (SPACE/ENTER/etc.)

    // Public events (optional)
    public event Action<string> OnSubmit;
    public event Action OnClosed;

    // Callbacks provided by the caller (CareerUiFlow)
    private Action<string> _onSubmit;
    private Action _onCancel;

    // internal
    readonly List<GameObject> _spawned = new();
    string _current = "";

    void Awake()
    {
        // Keep the panel but don’t show until asked
        gameObject.SetActive(false);
    }

    // ---------- PUBLIC API ----------
    // Keep ONE callback-style Show(...)
    public void Show(Action<string> onSubmit, Action onCancel = null, string seedText = "")
    {
        _onSubmit = onSubmit;
        _onCancel = onCancel;

        _current = seedText ?? "";
        if (output) output.text = _current;

        // bring to front and ensure visible BEFORE layout work
        transform.SetAsLastSibling();
        gameObject.SetActive(true);

        // optional: modal convenience
        if (TryGetComponent<CanvasGroup>(out var cg))
        {
            cg.blocksRaycasts = true;
            cg.interactable   = true;
            cg.alpha          = 1f;
        }

        ClearRows();
        BuildLayout();
        Canvas.ForceUpdateCanvases();
    }

    // Optional convenience overload that just fires the events
    public void Show(string seedText = "")
    {
        Show(
            onSubmit: s => OnSubmit?.Invoke(s),
            onCancel: () => OnClosed?.Invoke(),
            seedText: seedText
        );
    }

    public void Hide()
    {
        // optional: if you want a soft hide via CanvasGroup
        if (TryGetComponent<CanvasGroup>(out var cg))
        {
            cg.blocksRaycasts = false;
            cg.interactable   = false;
            cg.alpha          = 0f;
        }

        gameObject.SetActive(false);
        ClearRows();
        OnClosed?.Invoke();
    }

    // ---------- BUILD KEYS ----------
    void BuildLayout()
    {
        if (!row1 || !row2 || !row3 || !row4)
        {
            Debug.LogError("[VK] Rows not assigned.");
            return;
        }
        if (!keyPrefab)
        {
            Debug.LogError("[VK] keyPrefab not assigned.");
            return;
        }

        // Row 1
        AddRow(row1, "QWERTYUIOP");
        // Row 2
        AddRow(row2, "ASDFGHJKL");
        // Row 3  (letters + BACKSPACE)
        AddRow(row3, "ZXCVBNM");
        AddKey(row3, "delete", KeyType.Backspace, wide:true);

        // Row 4 (SPACE, CLEAR, ENTER)
        AddKey(row4, " ________ ", KeyType.Space, wide:true);
        // AddKey(row4, "CLR",   KeyType.Clear, wide:false);
        AddKey(row4, "return", KeyType.Enter, wide:true);
    }

    void AddRow(Transform row, string letters)
    {
        foreach (char c in letters)
            AddKey(row, c.ToString(), KeyType.Char, false);
    }

    enum KeyType { Char, Backspace, Space, Clear, Enter }

    void AddKey(Transform row, string label, KeyType type, bool wide)
    {
        var prefab = wide && keyWidePrefab ? keyWidePrefab : keyPrefab;
        var go = Instantiate(prefab, row);

        // If prefab was saved inactive, new instances would be inactive — force on.
        go.SetActive(true);

        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
    if (type == KeyType.Space || wide)
    {
        le.minWidth = le.preferredWidth = wideKeySize.x;
        le.minHeight = le.preferredHeight = wideKeySize.y;
        le.flexibleWidth = 0; // keep fixed width; set to 1 only if you want SPACE to stretch
    }
    else
    {
        le.minWidth = le.preferredWidth = keySize.x;
        le.minHeight = le.preferredHeight = keySize.y;
        le.flexibleWidth = 0;
    }

        _spawned.Add(go);
        go.name = "Key_" + label;

        // label text
        var tmp = go.transform.Find("Label")?.GetComponent<TMP_Text>();
        var txt = go.transform.Find("Label")?.GetComponent<Text>();
        if (tmp) tmp.text = label;
        else if (txt) txt.text = label;

        // Prefer your pinch component if present; otherwise fall back to Button
        var pinch = go.GetComponent<HandPinchSelectable>(); // optional in your project
        if (pinch)
        {
            pinch.body = body ? body : FindAnyObjectByType<BodyDataReceiver>();
            pinch.canvas = canvas;
            pinch.viewport = viewport;
            pinch.mirrorX = mirrorX;

            pinch.onPinch.RemoveAllListeners();
            pinch.onPinch.AddListener(() => Press(type, label));
        }
        else
        {
            var btn = go.GetComponent<Button>();
            if (btn)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => Press(type, label));
            }
            else
            {
                Debug.LogWarning($"[VK] Key '{label}' has no HandPinchSelectable or Button; add one.");
            }
        }
    }

    void Press(KeyType type, string label)
    {
        switch (type)
        {
            case KeyType.Char:
                _current += label;
                break;
            case KeyType.Space:
                _current += " ";
                break;
            case KeyType.Backspace:
                if (_current.Length > 0) _current = _current.Substring(0, _current.Length - 1);
                break;
            case KeyType.Clear:
                _current = "";
                break;
            case KeyType.Enter:
                _onSubmit?.Invoke(_current.Trim());
                OnSubmit?.Invoke(_current.Trim());
                Hide();
                return;
        }
        if (output) output.text = _current;
    }

    void ClearRows()
    {
        foreach (var go in _spawned)
            if (go) Destroy(go);
        _spawned.Clear();
    }
}
