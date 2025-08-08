using System.ComponentModel;

public class MainViewModel : INotifyPropertyChanged
{
    private bool isButtonEnabled = false;

    public bool IsButtonEnabled
    {
        get => isButtonEnabled;
        set
        {
            isButtonEnabled = value;
            OnPropertyChanged(nameof(IsButtonEnabled));
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

}