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
    private MedicalWindow? _selfMedicalWindow;
    private MedicalWindow? _targetMedicalWindow;
    private bool _targetingSelf = true;
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
        EnsureWindows();
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.OpenMedicalMenu,
                InputCmdHandler.FromDelegate(_ => ToggleWindow()))
            .Register<MedicalMenuUIController>();
    }

    private void ToggleWindow()
    {
        if (_targetingSelf)
        {
            _targetMedicalWindow?.Close();
            if (_selfMedicalWindow == null)
                return;
            _selfMedicalWindow.CommonStatus.UpdateTarget("John Smith", "TimeLord", true);
            if (!_selfMedicalWindow!.IsOpen)
            {
                _selfMedicalWindow.Open();
            }
            else
            {
                _selfMedicalWindow.Close();
            }
        }
        else
        {
            _selfMedicalWindow?.Close();
            if (_targetMedicalWindow == null)
                return;
            _targetMedicalWindow.CommonStatus.UpdateTarget("Martha Jones", "Human", false);
            if (!_targetMedicalWindow!.IsOpen)
            {
                _targetMedicalWindow.Open();
            }
            else
            {
                _targetMedicalWindow.Close();
            }
        }
    }

    private void OnMedicalWindowOpen()
    {
        if (MedicalButton == null)
            return;
        MedicalButton.Pressed = true;
    }

    private void OnMedicalWindowClosed()
    {
        if (MedicalButton == null)
            return;
        MedicalButton.Pressed = false;
    }

    private void EnsureWindows()
    {
        if (_selfMedicalWindow == null)
        {
            _selfMedicalWindow = UIManager.CreateWindow<MedicalWindow>();
            _selfMedicalWindow.OnOpen += OnMedicalWindowOpen;
            _selfMedicalWindow.OnClose += OnMedicalWindowClosed;
        }

        if (_targetMedicalWindow != null)
            return;
        _targetMedicalWindow = UIManager.CreateWindow<MedicalWindow>();
        _targetMedicalWindow.OnOpen += OnMedicalWindowOpen;
        _targetMedicalWindow.OnClose += OnMedicalWindowClosed;
    }

    private void ClearWindows()
    {
        if (_selfMedicalWindow != null)
        {
            _selfMedicalWindow.Close();
            _selfMedicalWindow.OnClose -= OnMedicalWindowClosed;
            _selfMedicalWindow.OnOpen -= OnMedicalWindowOpen;
            _selfMedicalWindow.Dispose();
        }
        if (_targetMedicalWindow != null)
        {
            _targetMedicalWindow.Close();
            _targetMedicalWindow.OnClose -= OnMedicalWindowClosed;
            _targetMedicalWindow.OnOpen -= OnMedicalWindowOpen;
            _targetMedicalWindow.Dispose();
        }
    }

    public void OnStateExited(GameplayState state)
    {
        CommandBinds.Unregister<MedicalMenuUIController>();
        ClearWindows();
    }

    public void OnSystemLoaded(WoundSystem system)
    {
        EnsureWindows();
    }

    public void OnSystemUnloaded(WoundSystem system)
    {
        ClearWindows();
    }
}
