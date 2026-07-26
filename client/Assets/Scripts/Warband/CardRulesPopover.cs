using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Runtime hover/focus disclosure for a data card. Native UI Toolkit tooltips are editor chrome;
/// this is the in-game equivalent, shared by mouse hover and keyboard focus.
/// </summary>
internal sealed class CardRulesPopover
{
    private readonly VisualElement _host;
    private readonly VisualElement _root;
    private readonly Label _eyebrow;
    private readonly Label _title;
    private readonly Label _weapon;
    private readonly Label _signatureTrigger;
    private readonly Label _signatureName;
    private readonly Label _signatureRule;
    private readonly Label _passiveTrigger;
    private readonly Label _passiveName;
    private readonly Label _passiveRule;
    private readonly VisualElement _keywords;

    public CardRulesPopover(VisualElement host)
    {
        _host = host;
        _root = new VisualElement();
        _root.AddToClassList("rules-popover");
        _root.pickingMode = PickingMode.Ignore;

        _eyebrow = AddLabel("rules-popover__eyebrow");
        _title = AddLabel("rules-popover__title");
        _weapon = AddLabel("rules-popover__weapon");

        var signature = AddRule("rules-popover__rule--signature",
            out var signatureTrigger, out var signatureName, out var signatureRule);
        _signatureTrigger = signatureTrigger;
        _signatureName = signatureName;
        _signatureRule = signatureRule;
        _root.Add(signature);
        var passive = AddRule("rules-popover__rule--passive",
            out var passiveTrigger, out var passiveName, out var passiveRule);
        _passiveTrigger = passiveTrigger;
        _passiveName = passiveName;
        _passiveRule = passiveRule;
        _root.Add(passive);

        _keywords = new VisualElement();
        _keywords.AddToClassList("rules-popover__keywords");
        _root.Add(_keywords);
        _host.Add(_root);
        Hide();
    }

    public void Show(CardModel model, VisualElement anchor)
    {
        if (model == null || anchor == null) return;
        _eyebrow.text = "RULES AT A GLANCE";
        _title.text = model.Title;
        _weapon.text = string.IsNullOrEmpty(model.WeaponSummary)
            ? model.Weapon
            : $"BASIC ATTACK · {model.WeaponSummary}";
        SetDisplayed(_weapon, !string.IsNullOrEmpty(_weapon.text));

        _signatureTrigger.text = string.IsNullOrEmpty(model.AbilityTrigger)
            ? "SIGNATURE"
            : model.AbilityTrigger;
        _signatureName.text = model.AbilityName;
        _signatureRule.text = model.AbilitySummary;
        _passiveTrigger.text = string.IsNullOrEmpty(model.PassiveTrigger)
            ? "PASSIVE"
            : model.PassiveTrigger;
        _passiveName.text = model.PassiveName;
        _passiveRule.text = model.PassiveSummary;

        _keywords.Clear();
        foreach (var note in model.KeywordNotes)
        {
            var label = new Label(note);
            label.AddToClassList("rules-popover__keyword");
            label.pickingMode = PickingMode.Ignore;
            _keywords.Add(label);
        }
        SetDisplayed(_keywords, model.KeywordNotes.Count > 0);

        float hostWidth = _host.resolvedStyle.width;
        Rect a = anchor.worldBound;
        const float width = 430f;
        float left = a.xMax + 14f;
        if (left + width > hostWidth - 18f)
            left = Mathf.Max(18f, a.xMin - width - 14f);
        _root.style.left = left;
        _root.style.top = Mathf.Max(168f, a.yMin + 10f);
        _root.style.display = DisplayStyle.Flex;
    }

    public void Hide() => _root.style.display = DisplayStyle.None;

    private Label AddLabel(string className)
    {
        var label = new Label();
        label.AddToClassList(className);
        label.pickingMode = PickingMode.Ignore;
        _root.Add(label);
        return label;
    }

    private static VisualElement AddRule(string modifier,
        out Label trigger, out Label name, out Label rule)
    {
        var box = new VisualElement();
        box.AddToClassList("rules-popover__rule");
        box.AddToClassList(modifier);
        box.pickingMode = PickingMode.Ignore;
        trigger = new Label();
        trigger.AddToClassList("rules-popover__trigger");
        name = new Label();
        name.AddToClassList("rules-popover__rule-name");
        rule = new Label();
        rule.AddToClassList("rules-popover__rule-copy");
        box.Add(trigger);
        box.Add(name);
        box.Add(rule);
        return box;
    }

    private static void SetDisplayed(VisualElement element, bool shown) =>
        element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
}
