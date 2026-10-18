namespace ClipFlow.Core.History;

/// <summary>シークレットモードの ON/OFF。ON の間は <see cref="IsRecordingEnabled"/> が false になる。</summary>
public sealed class SecretModeState : IHistoryGate
{
    private bool _isSecret;

    public event Action<bool>? Changed;

    event Action<bool> IHistoryGate.Changed
    {
        add => Changed += value;
        remove => Changed -= value;
    }

    public bool IsSecret
    {
        get => _isSecret;
        set
        {
            if (_isSecret == value)
            {
                return;
            }

            _isSecret = value;
            Changed?.Invoke(IsRecordingEnabled);
        }
    }

    public bool IsRecordingEnabled => !_isSecret;

    public void Toggle() => IsSecret = !IsSecret;
}
