using System.Windows;
using System.Windows.Controls;
using DesktopPet.Models;

namespace DesktopPet.UI;

/// <summary>
/// 設置視窗（D5，設計檔 §6.3 右鍵選單「設置」）。直接編輯呼叫端傳入的 <see cref="Settings"/>
/// 實例：取消時完全不觸碰它，只有按下「儲存」才寫回並回傳 <see cref="Window.DialogResult"/>
/// = <c>true</c>，呼叫端據此決定要不要套用（<c>MainWindow.ClickThrough</c>/<c>LoadSkin</c>）與存檔
/// （<c>StorageManager.Save</c>）。不在本視窗內直接呼叫這兩者，保持「視窗只管編輯」的單一職責，
/// 呼應 <see cref="MainWindow"/> 其餘選單項「只發事件不做事」的既有分工。
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly Settings _settings;

    public SettingsWindow(Settings settings)
    {
        InitializeComponent();
        _settings = settings;

        ClickThroughCheckBox.IsChecked = _settings.ClickThrough;

        foreach (ComboBoxItem item in ThemeComboBox.Items)
        {
            if ((string)item.Tag == _settings.Theme)
            {
                ThemeComboBox.SelectedItem = item;
                break;
            }
        }
        ThemeComboBox.SelectedItem ??= ThemeComboBox.Items[0]; // 未知主題值（如舊存檔）退回第一套。
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        _settings.ClickThrough = ClickThroughCheckBox.IsChecked ?? false;
        _settings.Theme = (string)((ComboBoxItem)ThemeComboBox.SelectedItem).Tag;

        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
