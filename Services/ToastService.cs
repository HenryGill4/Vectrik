namespace Opcentrix_V3.Services;

public class ToastService
{
    public event Action<ToastMessage>? OnShow;

    public void ShowSuccess(string message, string? title = null)
        => Show(new ToastMessage(ToastLevel.Success, message, title ?? "Success"));

    public void ShowError(string message, string? title = null)
        => Show(new ToastMessage(ToastLevel.Error, message, title ?? "Error"));

    public void ShowWarning(string message, string? title = null)
        => Show(new ToastMessage(ToastLevel.Warning, message, title ?? "Warning"));

    public void ShowInfo(string message, string? title = null)
        => Show(new ToastMessage(ToastLevel.Info, message, title ?? "Info"));

    private void Show(ToastMessage toast) => OnShow?.Invoke(toast);
}

public record ToastMessage(ToastLevel Level, string Message, string Title);

public enum ToastLevel
{
    Success,
    Error,
    Warning,
    Info
}
