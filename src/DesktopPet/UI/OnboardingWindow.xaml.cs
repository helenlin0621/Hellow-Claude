using System.Windows;

namespace DesktopPet.UI;

/// <summary>
/// 首次啟動引導視窗（E2，設計檔 §6.5.1）：詢問使用者要飼養 1 隻或 2 隻寵物。
/// 由 <c>App</c> 在存檔尚無寵物時以 <see cref="Window.ShowDialog"/> 顯示，讀 <see cref="SelectedCount"/> 決定建立幾隻。
/// </summary>
public partial class OnboardingWindow : Window
{
    /// <summary>
    /// 使用者選擇的飼養數量（1 或 2）。預設為 <c>1</c>——即使使用者直接關閉視窗未按按鈕，
    /// 也以單寵物安全啟動（§6.5.1 之後仍可於設定新增第 2 隻）。
    /// </summary>
    public int SelectedCount { get; private set; } = 1;

    public OnboardingWindow()
    {
        InitializeComponent();
    }

    private void OnChooseOne(object sender, RoutedEventArgs e) => Choose(1);

    private void OnChooseTwo(object sender, RoutedEventArgs e) => Choose(2);

    private void Choose(int count)
    {
        SelectedCount = count;
        DialogResult = true; // 關閉對話框，回到 App 啟動流程。
    }
}
