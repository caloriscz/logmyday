using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using LogMyDay.App.Mobile.Services;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.App.Mobile.ViewModels;

public class TestViewModel : INotifyPropertyChanged
{
    public TestViewModel()
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
