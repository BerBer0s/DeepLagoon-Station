using System.Numerics;
using System.Linq;
using Content.Client._DeepLagoon.WebUI;
using Content.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Enums;
using Robust.Shared.Maths;

namespace Content.Client._DeepLagoon.CharacterInfo;
public sealed class CharacterCardWindow : FancyWindow
{
    private readonly EntityUid _entity;
    private readonly int _slot;
    private readonly TguiPanel _panel = new();
    private readonly SpriteView _sprite = new() { Scale = new Vector2(3,3), OverrideDirection = Direction.South, MouseFilter = MouseFilterMode.Ignore };
    private readonly CardHost _host;
    private readonly TguiData _data;
    private CharacterCardWindow(EntityUid entity, string name, string flavor, string ooc, byte erp, byte nonCon, byte vore, int slot)
    {
        _entity = entity;
        _slot = slot;
        Title="Профиль персонажа"; SetSize=new Vector2(1000,820); MinSize=new Vector2(700,650);
        _sprite.SetEntity(entity);
        _data=new TguiData().String("name",name).String("flavor",flavor).String("ooc",ooc).Number("erp",erp).Number("noncon",nonCon).Number("vore",vore);
        _host=new CardHost(_panel,_sprite); ContentsContainer.AddChild(_host);
        _panel.OnAction += (action,_)=>{if(action=="close")Close();};
        _panel.SetState("CharacterCard",_data.ToString());
        OnClose+=Dispose;
    }
    public static void Open(EntityUid entity,string name,string flavor,string ooc,byte erp,byte nonCon,byte vore,int slot)
    {
        var existing = IoCManager.Resolve<IUserInterfaceManager>().WindowRoot.Children
            .OfType<CharacterCardWindow>().FirstOrDefault(window => !window.Disposed && window._entity == entity && window._slot == slot);
        if (existing != null)
        {
            existing.MoveToFront();
            return;
        }
        var window=new CharacterCardWindow(entity,name,flavor,ooc,erp,nonCon,vore,slot);window.OpenCentered();
        var entities=IoCManager.Resolve<IEntityManager>();
        entities.System<HeadshotSystem>().Read(slot,slot<0?entities.GetNetEntity(entity):null,(bytes,mime,_,error)=>
        {
            if(window.Disposed)return;
            if(bytes.Length>0 && error.Length==0)
            {
                window._data.String("headshot","data:"+mime+";base64,"+Convert.ToBase64String(bytes));
                window._host.HasHeadshot=true;window._host.InvalidateArrange();
                window._panel.SetState("CharacterCard",window._data.ToString());
            }
        });
    }
    private sealed class CardHost : Container
    {
        private readonly TguiPanel _panel;private readonly Control _sprite;public bool HasHeadshot;
        public CardHost(TguiPanel panel,Control sprite){_panel=panel;_sprite=sprite;HorizontalExpand=VerticalExpand=true;AddChild(panel);AddChild(sprite);}
        protected override Vector2 MeasureOverride(Vector2 available){_panel.Measure(available);_sprite.Measure(new Vector2(240,260));return Vector2.Zero;}
        protected override Vector2 ArrangeOverride(Vector2 final){_panel.Arrange(UIBox2.FromDimensions(Vector2.Zero,final));_sprite.Arrange(UIBox2.FromDimensions(new Vector2(24,HasHeadshot?216:46),new Vector2(240,260)));return final;}
    }
}
