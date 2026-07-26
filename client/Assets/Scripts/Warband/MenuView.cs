using UnityEngine.UIElements;

/// <summary>
/// Title screen. Calm and centered — the door into a run, not a dashboard. Owns no state: the
/// seed line, the CONTINUE affordance, and the version line are all read off <see cref="MenuModel"/>
/// every Bind, so a run starting or ending elsewhere is reflected without this view knowing why.
/// </summary>
internal sealed class MenuView : IRunScreenView
{
    private readonly RunShellActions _actions;

    private readonly VisualElement _root;
    private readonly Label _title;
    private readonly Label _tagline;
    private readonly Label _seed;
    private readonly Button _continue;
    private readonly Label _notice;
    private readonly Label _version;

    public RunScreen Screen => RunScreen.Menu;

    public VisualElement Root => _root;

    public MenuView(RunShellActions actions)
    {
        _actions = actions;

        _root = new VisualElement();
        _root.AddToClassList("shell-screen");
        _root.AddToClassList("shell-center");

        var column = new VisualElement();
        column.AddToClassList("shell-column");
        column.AddToClassList("panel");
        _root.Add(column);

        _title = new Label();
        _title.AddToClassList("shell-title");
        column.Add(_title);

        _tagline = new Label();
        _tagline.AddToClassList("shell-subtitle");
        column.Add(_tagline);

        _seed = new Label();
        _seed.AddToClassList("public-info-pill");
        column.Add(_seed);

        var newRun = new Button(() => _actions?.NewRun?.Invoke()) { text = "NEW RUN" };
        newRun.AddToClassList("btn");
        newRun.AddToClassList("btn--primary");
        column.Add(newRun);

        _continue = new Button(() => _actions?.ContinueRun?.Invoke()) { text = "CONTINUE" };
        _continue.AddToClassList("btn");
        column.Add(_continue);

        var quit = new Button(() => _actions?.Quit?.Invoke()) { text = "QUIT" };
        quit.AddToClassList("btn");
        quit.AddToClassList("btn--ghost");
        column.Add(quit);

        // A discarded save has to say so here. Failing CONTINUE silently would look like the
        // button is broken, which is exactly the impression a lost run should not also make.
        _notice = new Label();
        _notice.AddToClassList("body-copy");
        _notice.AddToClassList("feedback--error");
        column.Add(_notice);

        _version = new Label();
        _version.AddToClassList("body-copy");
        _root.Add(_version);
    }

    public void Bind(RunShellModel model)
    {
        MenuModel menu = model.Menu;

        _title.text = menu.Title;
        _tagline.text = menu.Tagline;
        _seed.text = menu.SeedLabel;
        _notice.text = menu.Notice;
        _version.text = menu.VersionLine;

        SetDisplayed(_tagline, !string.IsNullOrEmpty(menu.Tagline));
        SetDisplayed(_seed, !string.IsNullOrEmpty(menu.SeedLabel));
        SetDisplayed(_continue, menu.CanContinue);
        SetDisplayed(_notice, !string.IsNullOrEmpty(menu.Notice));
        SetDisplayed(_version, !string.IsNullOrEmpty(menu.VersionLine));
    }

    private static void SetDisplayed(VisualElement element, bool displayed)
    {
        element.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
