using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.System;
using WiFiStudio.App.Services;
using WiFiStudio.App.ViewModels;

namespace WiFiStudio.App;

public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        Title = "WiFi Studio Pro";
        ExtendsContentIntoTitleBar = false;
        _viewModel = new MainViewModel(new ProjectFileService(this));
        Root.DataContext = _viewModel;
        PlanCanvas.ViewModel = _viewModel;
        RegisterKeyboardAccelerators();
    }

    private void RegisterKeyboardAccelerators()
    {
        AddAccelerator(VirtualKey.S, VirtualKeyModifiers.Control, (_, args) =>
        {
            _viewModel.SaveProjectCommand.Execute(null);
            args.Handled = true;
        });
        AddAccelerator(VirtualKey.O, VirtualKeyModifiers.Control, (_, args) =>
        {
            _viewModel.OpenProjectCommand.Execute(null);
            args.Handled = true;
        });
        AddAccelerator(VirtualKey.N, VirtualKeyModifiers.Control, (_, args) =>
        {
            _viewModel.NewProjectCommand.Execute(null);
            args.Handled = true;
        });
        AddAccelerator(VirtualKey.R, VirtualKeyModifiers.Control, (_, args) =>
        {
            _viewModel.RunSimulationCommand.Execute(null);
            args.Handled = true;
        });
        AddAccelerator(VirtualKey.Delete, VirtualKeyModifiers.None, (_, args) =>
        {
            _viewModel.DeleteSelectedCommand.Execute(null);
            args.Handled = true;
        });
        AddAccelerator(VirtualKey.Z, VirtualKeyModifiers.Control, (_, args) =>
        {
            _viewModel.UndoCommand.Execute(null);
            args.Handled = true;
        });
        AddAccelerator(VirtualKey.Y, VirtualKeyModifiers.Control, (_, args) =>
        {
            _viewModel.RedoCommand.Execute(null);
            args.Handled = true;
        });
        AddAccelerator(VirtualKey.K, VirtualKeyModifiers.Control, async (_, args) =>
        {
            args.Handled = true;
            await ShowCommandPaletteAsync();
        });
    }

    private void AddAccelerator(VirtualKey key, VirtualKeyModifiers modifiers, TypedEventHandler<KeyboardAccelerator, KeyboardAcceleratorInvokedEventArgs> invoked)
    {
        var accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
        accelerator.Invoked += invoked;
        Root.KeyboardAccelerators.Add(accelerator);
    }

    private async Task ShowCommandPaletteAsync()
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "Command Palette",
            PrimaryButtonText = "Run Simulation",
            SecondaryButtonText = "Recommend AP",
            CloseButtonText = "Close",
            Content = "Ctrl+N New, Ctrl+O Open, Ctrl+S Save, Ctrl+R Run"
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            _viewModel.RunSimulationCommand.Execute(null);
        }
        else if (result == ContentDialogResult.Secondary)
        {
            _viewModel.RecommendApCommand.Execute(null);
        }
    }
}
