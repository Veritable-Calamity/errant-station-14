using Content.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Systems.Medical.Controls;

public sealed class CommonStatusDisplay : BoxContainer
{

    private readonly Label _targetNameLabel;
    private readonly Label _targetSpeciesLabel;
    private readonly SpriteView _targetSpriteView;
    public CommonStatusDisplay()
    {
        Orientation = LayoutOrientation.Horizontal;
        var column1 = new BoxContainer()
        {
            Orientation = LayoutOrientation.Vertical
        };
        AddChild(column1);
        AddChild( new HSpacer() { });
        var column2 = new BoxContainer()
        {
            Orientation = LayoutOrientation.Vertical
        };
        AddChild(column2);
        _targetNameLabel = new Label() {Text = "Target: Unknown"};
        column1.AddChild(_targetNameLabel);
        _targetSpeciesLabel = new Label() {Text = "Species: Unknown"};
        column1.AddChild(_targetSpeciesLabel);
        column1.AddChild( new VSpacer() { });
        _targetSpriteView = new SpriteView() { };
        column1.AddChild(_targetSpriteView);
    }


}
