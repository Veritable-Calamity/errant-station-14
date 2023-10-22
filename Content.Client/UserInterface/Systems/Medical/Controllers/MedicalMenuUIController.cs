using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.EscapeMenu;
using Content.Client.UserInterface.Systems.Medical.Windows;
using Content.Shared.Input;
using Content.Shared.Medical.Wounds.Systems;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Medical.Controllers;

public sealed class MedicalMenuUIController: UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>,
    IOnSystemChanged<WoundSystem>
{
    private MedicalWindow? _window;
    private MenuButton? MedicalButton => UIManager.GetActiveUIWidgetOrNull<MenuBar.Widgets.GameTopMenuBar>()?.MedicalButton;
    public void LoadButton()
    {
        if (MedicalButton == null)
        {
            return;
        }

        MedicalButton.OnPressed += OnPressed;
    }
    public void UnloadButton()
    {
        if (MedicalButton == null)
        {
            return;
        }
        MedicalButton.Pressed = false;
        MedicalButton.OnToggled -= OnPressed;
    }

    private void OnPressed(BaseButton.ButtonEventArgs obj)
    {
        ToggleWindow();
    }

    public void OnStateEntered(GameplayState state)
    {
        EnsureWindow();
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.OpenMedicalMenu,
                InputCmdHandler.FromDelegate(_ => ToggleWindow()))
            .Register<MedicalMenuUIController>();
    }

    private void ToggleWindow()
    {
        if (_window == null)
            return;
        if (_window.IsOpen)
        {
            _window.Close();
        }
        else
        {
            _window.Open();
        }
    }

    private void OnWindowOpen()
    {
        if (MedicalButton == null)
            return;
        MedicalButton.Pressed = true;
    }

    private void OnWindowClosed()
    {
        if (MedicalButton == null)
            return;
        MedicalButton.Pressed = false;
    }

    private void EnsureWindow()
    {
        if (_window != null)
            return;
        _window = UIManager.CreateWindow<MedicalWindow>();
        _window.OnOpen += OnWindowOpen;
        _window.OnClose += OnWindowClosed;
    }

    private void ClearWindow()
    {
        if (_window == null)
            return;
        _window.Close();
        _window.OnClose -= OnWindowClosed;
        _window.OnOpen -= OnWindowOpen;
        _window.Dispose();
    }

    public void OnStateExited(GameplayState state)
    {
        CommandBinds.Unregister<MedicalMenuUIController>();
        ClearWindow();
    }

    public void OnSystemLoaded(WoundSystem system)
    {
        EnsureWindow();
    }

    public void OnSystemUnloaded(WoundSystem system)
    {
        ClearWindow();
    }
}
