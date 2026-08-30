using System.Reflection;
using System.Windows;

namespace DesktopPet.UI;

/// <summary>
/// 關於視窗（D5，設計檔 §6.3 右鍵選單「關於」）。顯示版本／專案資訊，無任何互動邏輯或外部相依。
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        // 版本取自組件本身（.csproj 未手動指定時，SDK 預設為 1.0.0.0），避免另存一份會漂移的字串。
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"版本 {version}";
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
