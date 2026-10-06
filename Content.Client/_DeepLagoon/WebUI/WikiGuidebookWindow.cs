using System.Linq;
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._DeepLagoon.WebUI;
using Robust.Client.UserInterface.Controls;
using Robust.Client.WebView;
using Robust.Shared.Prototypes;

namespace Content.Client._DeepLagoon.WebUI;

public sealed class WikiGuidebookWindow : FancyWindow
{
    private readonly WebViewControl _web = new() { HorizontalExpand = true, VerticalExpand = true };
    private readonly HashSet<string> _paths = new();
    private readonly List<WikiPagePrototype> _pages;
    private readonly Label _status = new() { Text = Loc.GetString("wiki-loading") };
    private float _loadSeconds;
    public string? LastPage { get; private set; }

    public WikiGuidebookWindow()
    {
        Title = Loc.GetString("wiki-window-title");
        SetSize = new Vector2(1000, 750);
        MinSize = new Vector2(400, 300);
        _pages = IoCManager.Resolve<IPrototypeManager>().EnumeratePrototypes<WikiPagePrototype>()
            .Where(p => WikiNavigationPolicy.IsPath(p.Path)).OrderBy(p => p.ID).ToList();
        foreach (var page in _pages)
            _paths.Add(page.Path);
        // Install both handlers before placing the browser in the control tree.
        _web.AddBeforeBrowseHandler(BeforeBrowse);
        _web.AddResourceRequestHandler(ResourceRequest);
        var body = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        var toolbar = new BoxContainer();
        foreach (var page in _pages)
        {
            var button = new Button { Text = Loc.GetString(page.Name) };
            button.OnPressed += _ => ShowPage(page.ID);
            toolbar.AddChild(button);
        }
        var reload = new Button { Text = Loc.GetString("wiki-reload") };
        reload.OnPressed += _ => { _loadSeconds = 0; _web.Reload(); };
        toolbar.AddChild(reload);
        body.AddChild(toolbar);
        body.AddChild(_status);
        body.AddChild(_web);
        ContentsContainer.AddChild(body);
        ShowPage(_pages.FirstOrDefault(p => p.Default)?.ID ?? _pages.FirstOrDefault()?.ID);
    }

    public void ShowPage(string? id)
    {
        var page = _pages.FirstOrDefault(p => p.ID == id);
        if (page == null)
        {
            _status.Text = Loc.GetString("wiki-page-unmapped");
            _web.Visible = false;
            return;
        }
        _web.Visible = true;
        _loadSeconds = 0;
        _status.Text = Loc.GetString("wiki-loading");
        LastPage = page.ID;
        _web.Url = WikiNavigationPolicy.Origin + page.Path;
    }

    public void ShowGuide(string? guideId)
    {
        if (guideId == null)
            ShowPage(LastPage ?? _pages.FirstOrDefault(p => p.Default)?.ID);
        else
            ShowPage(_pages.FirstOrDefault(p => p.GuideEntries.Contains(guideId))?.ID);
    }

    private void BeforeBrowse(IBeforeBrowseContext context)
    {
        if (context.Method != "GET" || !WikiNavigationPolicy.IsPage(context.Url, _paths))
            context.DoCancel();
    }

    private void ResourceRequest(IRequestHandlerContext context)
    {
        if (context.IsDownload || context.Method != "GET" ||
            (context.IsNavigation ? !WikiNavigationPolicy.IsPage(context.Url, _paths) :
                !WikiNavigationPolicy.IsAsset(context.Url) && !WikiNavigationPolicy.IsPage(context.Url, _paths)))
            context.DoCancel();
    }

    protected override void FrameUpdate(Robust.Shared.Timing.FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_web.Visible)
        {
            _loadSeconds += args.DeltaSeconds;
            _status.Text = Loc.GetString(_web.IsLoading
                ? (_loadSeconds >= 30 ? "wiki-load-timeout" : "wiki-loading")
                : "wiki-load-hint");
        }
    }

    protected override void Dispose(bool disposing)
    {
        _web.RemoveBeforeBrowseHandler(BeforeBrowse);
        _web.RemoveResourceRequestHandler(ResourceRequest);
        base.Dispose(disposing);
    }
}
