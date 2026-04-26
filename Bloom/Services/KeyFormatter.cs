using Avalonia.Input;

namespace Bloom.Services;

internal static class KeyFormatter
{
    internal static string Format(Key key)
    {
        return key switch
        {
            >= Key.A and <= Key.Z => key.ToString(),
            >= Key.D0 and <= Key.D9 => ((int)key - (int)Key.D0).ToString(),
            >= Key.NumPad0 and <= Key.NumPad9 => "Num" + ((int)key - (int)Key.NumPad0),
            >= Key.F1 and <= Key.F24 => key.ToString(),
            Key.Space => "Space",
            Key.Return => "Enter",
            Key.Escape => "Esc",
            Key.Tab => "Tab",
            Key.Back => "Backspace",
            Key.Delete => "Delete",
            Key.Insert => "Insert",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Up => "Up",
            Key.Down => "Down",
            Key.Left => "Left",
            Key.Right => "Right",
            Key.PrintScreen => "PrintScreen",
            Key.OemPeriod => ".",
            Key.OemComma => ",",
            Key.OemPlus => "=",
            Key.OemMinus => "-",
            Key.OemOpenBrackets => "[",
            Key.OemCloseBrackets => "]",
            Key.OemPipe => "\\",
            Key.OemSemicolon => ";",
            Key.OemQuotes => "'",
            Key.OemTilde => "`",
            Key.OemQuestion => "/",
            Key.Multiply => "Num*",
            Key.Add => "Num+",
            Key.Subtract => "Num-",
            Key.Divide => "Num/",
            Key.Decimal => "Num.",
            _ => key.ToString()
        };
    }
}
